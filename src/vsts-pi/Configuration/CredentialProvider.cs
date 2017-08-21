using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using System.Net;

namespace Microsoft.VisualStudio.Services.Agent.Configuration
{
    public interface ICredentialProvider
    {
        CredentialData CredentialData { get; set; }
        VssCredentials LoadVssCredentials(IHostContext context);
        VssCredentials GatherCredential(IHostContext context, CommandSettings command, string serverUrl);
        void SaveCredential(IHostContext context);
    }

    public abstract class CredentialProvider : ICredentialProvider
    {
        public CredentialProvider(string scheme)
        {
            CredentialData = new CredentialData();
            CredentialData.Scheme = scheme;
        }

        public CredentialData CredentialData { get; set; }

        public abstract VssCredentials LoadVssCredentials(IHostContext context);
        public abstract VssCredentials GatherCredential(IHostContext context, CommandSettings command, string serverUrl);
        public abstract void SaveCredential(IHostContext context);
    }

    public sealed class PersonalAccessToken : CredentialProvider
    {
        string _tokenInput;
        public PersonalAccessToken() : base(Constants.Configuration.PAT) { }

        public override VssCredentials LoadVssCredentials(IHostContext context)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(PersonalAccessToken));
            trace.Info(nameof(LoadVssCredentials));
            ArgUtil.NotNull(CredentialData, nameof(CredentialData));

            string token = null;
            var credStore = context.GetService<IAgentCredentialStore>();
            NetworkCredential cred = credStore.Read($"VSTS_PI");
            if (cred != null)
            {
                token = cred.Password;
            }

            ArgUtil.NotNullOrEmpty(token, nameof(token));
            trace.Info("token retrieved: {0} chars", token.Length);

            VssCredentials creds = CredentialFromToken(token);
            trace.Verbose("cred created");

            return creds;
        }

        public override VssCredentials GatherCredential(IHostContext context, CommandSettings command, string serverUrl)
        {
            ArgUtil.NotNull(context, nameof(context));
            Tracing trace = context.GetTrace(nameof(PersonalAccessToken));
            trace.Info(nameof(GatherCredential));
            ArgUtil.NotNull(command, nameof(command));
            _tokenInput = command.GetToken();
            return CredentialFromToken(_tokenInput);
        }

        public override void SaveCredential(IHostContext context)
        {
            Tracing trace = context.GetTrace(nameof(PersonalAccessToken));
            trace.Info("Saving credential");
            var credStore = context.GetService<IAgentCredentialStore>();
            credStore.Write($"VSTS_PI", "VstsPi", _tokenInput);
            trace.Info("written");
        }

        private VssCredentials CredentialFromToken(string token)
        {
            VssBasicCredential basicCred = new VssBasicCredential("VstsPi", token);
            VssCredentials creds = new VssCredentials(null, basicCred, CredentialPromptType.DoNotPrompt);
            return creds;
        }
    }
}