using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.VisualStudio.Services.Agent.Configuration
{
    // TODO: Refactor extension manager to enable using it from the agent process.
    [ServiceLocator(Default = typeof(CredentialManager))]
    public interface ICredentialManager : IAgentService
    {
        ICredentialProvider GetCredentialProvider(string credType);
        VssCredentials LoadCredentials();
    }

    public class CredentialManager : AgentService, ICredentialManager
    {        
        public static readonly Dictionary<string, Type> CredentialTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { Constants.Configuration.PAT, typeof(PersonalAccessToken)},
            { Constants.Configuration.Integrated, typeof(IntegratedCredential)}
        };

        public ICredentialProvider GetCredentialProvider(string credType)
        {
            Trace.Info(nameof(GetCredentialProvider));
            Trace.Info("Creating type {0}", credType);

            if (!CredentialTypes.ContainsKey(credType))
            {
                throw new ArgumentException("Invalid Credential Type");
            }

            Trace.Info("Creating credential type: {0}", credType);
            var creds = Activator.CreateInstance(CredentialTypes[credType]) as ICredentialProvider;
            Trace.Verbose("Created credential type");
            return creds;
        }
        
        public VssCredentials LoadCredentials()
        {
            ILoginStore store = HostContext.GetService<ILoginStore>(); 

            if (!store.HasCredentials())
            {
                throw new InvalidOperationException("Credentials not stored.  Must reconfigure.");
            }
                        
            CredentialData credData = store.GetCredentials();
            ICredentialProvider credProv = GetCredentialProvider(credData.Scheme);
            credProv.CredentialData = credData;
            
            VssCredentials creds = credProv.GetVssCredentials(HostContext);

            return creds;
        }
    }
}