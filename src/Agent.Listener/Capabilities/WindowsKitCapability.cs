using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class WindowsKitCapability : IPrivateWindowsCapabilityProvider
    {
        private readonly IRegistryService _registryService;

        internal WindowsKitCapability(IRegistryService registryService)
        {
            ArgUtil.NotNull(registryService, nameof(registryService));

            _registryService = registryService;
        }

        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();
            
            string rootsKeyName = "Software\\Microsoft\\Windows Kits\\Installed Roots";
            object[] valueNames = _registryService.GetRegistryValueNames(hive: "LocalMachine", view: "Registry32", keyName: rootsKeyName);

            // $versionInfos = @( )
            object[] versionInfos;
            // foreach ($valueName in $valueNames) {
            //     if (!"$valueName".StartsWith('KitsRoot', 'OrdinalIgnoreCase')) {
            //         continue
            //     }

            //     $installDirectory = Get-RegistryValue -Hive 'LocalMachine' -View 'Registry32' -KeyName $rootsKeyName -ValueName $valueName
            //     $splitInstallDirectory =
            //         "$installDirectory".Split(@( ([System.IO.Path]::DirectorySeparatorChar) ) ) |
            //         ForEach-Object { "$_".Trim() } |
            //         Where-Object { $_ }
            //     $splitInstallDirectory = @( $splitInstallDirectory )
            //     if ($splitInstallDirectory.Length -eq 0) {
            //         continue
            //     }

            //     $version = $null
            //     if (!([System.Version]::TryParse($splitInstallDirectory[-1], [ref]$version))) {
            //         continue
            //     }

            //     Write-Capability -Name "WindowsKit_$($version.Major).$($version.Minor)" -Value $installDirectory
            //     $versionInfos += @{
            //         Version = $version
            //         InstallDirectory = $installDirectory
            //     }
            // }

            if (versionInfos.Length > 0)
            {
                var maxInfo = versionInfos.ToList().OrderBy(v => v).First();

                capabilities.Add(new Capability(name: CapabilityNames.WindowsKit, maxInfo.InstallDirectory));
            }

            return capabilities;
        }
    }
}
