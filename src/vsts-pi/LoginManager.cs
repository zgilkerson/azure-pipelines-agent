using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Agent.Configuration;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent
{
    [ServiceLocator(Default = typeof(LoginManager))]
    public interface ILoginManager : IAgentService
    {
        Task<int> Login(CommandSettings command);
    }

    public class LoginManager: AgentService, ILoginManager
    {
        private ILoginStore _loginStore;
        private ITerminal _term;

        public sealed override void Initialize(IHostContext context)
        {
            base.Initialize(context);
            _loginStore = context.GetService<ILoginStore>();
            _term = context.GetService<ITerminal>();
        }

        // TODO: move to login manager
        public async Task<int> Login(CommandSettings command)
        {
            Trace.Info(nameof(Login));
            try
            {
                if (_loginStore.IsLoggedIn()) {
                    // TODO: loc
                    throw new InvalidOperationException("Already logged in.  Log out first.");
                }

                LoginSettings settings = new LoginSettings();

                // Loop getting url and creds until you can connect
                ICredentialProvider credProvider = null;
                VssCredentials creds = null;
                string authType;
                while (true)
                {
                    // Get the URL
                    settings.ServerUrl = command.GetUrl();

                    // Get the credentials
                    // Get the auth type. On premise defaults to negotiate (Kerberos with fallback to NTLM).
                    // Hosted defaults to PAT authentication.
                    string defaultAuth = UrlUtil.IsHosted(settings.ServerUrl) ? Constants.Configuration.PAT :
                        (Constants.Agent.Platform == Constants.OSPlatform.Windows ? Constants.Configuration.Integrated : Constants.Configuration.Negotiate);
                    authType = command.GetAuth(defaultValue: defaultAuth);

                    credProvider = GetCredentialProvider(command, authType, settings.ServerUrl);
                    creds = credProvider.GatherCredential(HostContext, command, settings.ServerUrl);

                    //creds = credProvider.LoadVssCredentials(HostContext);
                    Trace.Info("cred gathered");
                    try
                    {
                        // Validate can connect.
                        await TestConnectionAsync(settings, creds);
                        Trace.Info("Test Connection complete.");
                        break;
                    }
                    catch (Exception e) when (!command.Unattended)
                    {
                        _term.WriteError(e);
                        _term.WriteError(StringUtil.Loc("FailedToConnect"));
                    }
                }
                
                var credentialData = new CredentialData
                {
                    Scheme = authType
                };
                _loginStore.SaveCredentialData(credentialData);
                credProvider.SaveCredential(HostContext);
                _loginStore.SaveSettings(settings);       

                return Constants.Agent.ReturnCode.Success;
            }
            catch (Exception e)
            {
                _term.WriteError(e);
                // TODO: loc
                _term.WriteError("Failed to Login");
                return Constants.Agent.ReturnCode.TerminatedError;
            }
        }

        private ICredentialProvider GetCredentialProvider(CommandSettings command, string authType, string serverUrl)
        {
            Trace.Info(nameof(GetCredentialProvider));

            var credentialManager = HostContext.GetService<ICredentialManager>();

            // Create the credential.
            Trace.Info("Creating credential for auth: {0}", authType);
            var provider = credentialManager.GetCredentialProvider(authType);
            return provider;
        }

        public async Task TestConnectionAsync(LoginSettings settings, VssCredentials creds)
        {
            Trace.Info("Testing connection");
            IAgentServer server = HostContext.GetService<IAgentServer>();
            _term.WriteLine(StringUtil.Loc("ConnectingToServer"));
            VssConnection connection = ApiUtil.CreateConnection(new Uri(settings.ServerUrl), creds);
            await server.ConnectAsync(connection);
            Trace.Info("connected");
        }          
    }   
}