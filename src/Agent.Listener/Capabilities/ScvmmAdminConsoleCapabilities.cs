using System.Collections.Generic;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class ScvmmAdminConsoleCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();
            
            // TODO: Use IRegistryService, find the subcall that Add-CapabilityFromRegistry makes, I think XamarainAndroidCapabilities uses it too.
            // foreach ($view in @('Registry64', 'Registry32')) {
            //     if ((Add-CapabilityFromRegistry -Name 'SCVMMAdminConsole' -Hive 'LocalMachine' -View $view -KeyName 'Software\Microsoft\Microsoft System Center Virtual Machine Manager Administrator Console\Setup' -ValueName 'InstallPath')) {
            //         break
            //     }
            // }

            return capabilities;
        }
    }
}
