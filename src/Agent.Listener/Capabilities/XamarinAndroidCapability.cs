using System.Collections.Generic;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class XamarinAndroidCapability : IPrivateWindowsCapabilityProvider
    {
        private readonly IRegistryService _registryService;

        internal XamarinAndroidCapability(IRegistryService registryService)
        {
            ArgUtil.NotNull(registryService, nameof(registryService));

            _registryService = registryService;
        }

        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            // TODO: This can at least be combined with ScvmmAdminConsoleCapabilities since they both use the registry
            // $null = Add-CapabilityFromRegistry -Name 'Xamarin.Android' -Hive 'LocalMachine' -View 'Registry32' -KeyName 'Software\Novell\Mono for Android' -ValueName 'InstalledVersion'
            // which then calls
            // Get-RegistryValue -Hive $Hive -View $View -KeyName $KeyName -ValueName $ValueName
            // if this is not null or empty, add the new capability
            // TODO: create static class for Hive options, same for View
            string registryValue;
            if (_registryService.TryGetRegistryValue(hive: "LocalMachine", view: "Registry32", keyName: "Software\\Novell\\Mono for Android", valueName: "InstalledVersion", registryValue: out registryValue))
            {
                capabilities.Add(new Capability(CapabilityNames.XamarinAndroid, registryValue));
            }

            return capabilities;
        }
    }
}
