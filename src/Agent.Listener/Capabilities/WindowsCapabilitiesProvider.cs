using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    // TODO: Rename.
    [ServiceLocator(Default = typeof(WindowsCapabilitiesProvider))]
    internal interface IPrivateWindowsCapabilityProvider
    {
        List<Capability> GetCapabilities();
    }

    public sealed class WindowsCapabilitiesProvider : AgentService, ICapabilitiesProvider
    {
        public Type ExtensionType => typeof(ICapabilitiesProvider);

        // Only runs on Windows.
        public int Order => 2;

        public async Task<List<Capability>> GetCapabilitiesAsync(AgentSettings settings, CancellationToken cancellationToken)
        {
            Trace.Entering();
            var capabilities = new List<Capability>();

            // TODO: Get this from the HostContext. Will have to add mapping there.
            //HostContext.GetService<IEnvironmentService>()
            IEnvironmentService environmentService = new EnvironmentService();
            IRegistryService registryService = new RegistryService();

            var capabilityProviders = new List<IPrivateWindowsCapabilityProvider>
            {
                // new AndroidSdkCapability(), 
                new AntCapability(environmentService), 
                // new AzureGuestAgentCapabilities(), 
                // new AzurePowerShellCapabilities(), 
                // new ChefCapabilities(), 
                // new DotNetFrameworkCapabilities(), 
                // new JavaCapabilities(), 
                // new MavenCapabilities(), 
                // new MSBuildCapabilities(), 

                // TODO: Add npm, gulp, etc. All of the classes that extend ApplicationCapability

                // new PowerShellCapabilities(), 
                // new ScvmmAdminConsoleCapabilities(), 
                // new SqlPackageCapabilities(), 
                // new VisualStudioCapabilities(), 
                // new WindowsKitCapabilities(), 
                // new WindowsSdkCapabilities(), 
                // new XamarinAndroidCapabilities()
                new XamarinAndroidCapability(registryService)
            };

            foreach (IPrivateWindowsCapabilityProvider provider in capabilityProviders)
            {
                capabilities.AddRange(provider.GetCapabilities());
            }

            return capabilities;
        }
    }
}
