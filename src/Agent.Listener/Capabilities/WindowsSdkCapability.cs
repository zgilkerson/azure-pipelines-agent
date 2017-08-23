using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class WindowsSdkCapability : IPrivateWindowsCapabilityProvider
    {
        private class WindowsSdk
        {

        }

        private readonly IRegistryService _registryService;

        public WindowsSdkCapability(IRegistryService registryService)
        {
            ArgUtil.NotNull(registryService, nameof(registryService));

            _registryService = registryService;
        }

        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            var windowsSdks = new List<WindowsSdk>();

            // # Get the Windows SDK version sub-key names.
            string windowsSdkKeyName = @"Software\Microsoft\Microsoft SDKs\Windows";

            IEnumerable<string> versionSubKeyNames = 
                _registryService.GetRegistrySubKeyNames(hive: Win32.RegistryHive.LocalMachine, view: Win32.RegistryView.Registry32, keyName: windowsSdkKeyName)
                                .ToList()
                                .Where(subKeyName => Regex.IsMatch(subKeyName, "v*A"));

            foreach (string versionSubKeyName in versionSubKeyNames)
            {
                Version version;
                if (!Version.TryParse(versionSubKeyName.Substring(1, versionSubKeyName.Length - 2), out version))
                {
                    continue;
                }

                // Get the installation folder.
                string versionKeyName = $@"{windowsSdkKeyName}\{versionSubKeyName}";

                string installationFolder;
                if (!_registryService.TryGetRegistryValue(Win32.RegistryHive.LocalMachine, Win32.RegistryView.Registry32, versionKeyName, "InstallationFolder", out installationFolder))
                {
                    continue;
                }

                //     # Add the Windows SDK capability.
                //     $installationFolder = $installationFolder.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
                //     $windowsSdkCapabilityName = ("WindowsSdk_{0}.{1}" -f $version.Major, $version.Minor)
                //     Write-Capability -Name $windowsSdkCapabilityName -Value $installationFolder

                //     # Add the Windows SDK info as an object with properties (for sorting).
                //     $windowsSdks += New-Object psobject -Property @{
                //         InstallationFolder = $installationFolder
                //         Version = $version
                //     }

                //     # Get the NetFx sub-key names.
                //     $netFxSubKeyNames =
                //         Get-RegistrySubKeyNames -Hive 'LocalMachine' -View 'Registry32' -KeyName $versionKeyName |
                //         Where-Object { $_ -clike '*NetFx*x86' -or $_ -clike '*NetFx*x64' }
                //     foreach ($netFxSubKeyName in $netFxSubKeyNames) {


                //         # Get the installation folder.
                //         $netFxKeyName = "$versionKeyName\$netFxSubKeyName"
                //         $installationFolder = Get-RegistryValue -Hive 'LocalMachine' -View 'Registry32' -KeyName $netFxKeyName -Value 'InstallationFolder'
                //         if (!$installationFolder) {
                //             continue
                //         }

                //         $installationFolder = $installationFolder.TrimEnd([System.IO.Path]::DirectorySeparatorChar)

                //         # Add the NetFx tool capability.
                //         $toolName = $netFxSubKeyName.Substring($netFxSubKeyName.IndexOf('NetFx')) # Trim before "NetFx".
                //         $toolName = $toolName.Substring(0, $toolName.Length - '-x86'.Length) # Trim the trailing "-x86"/"-x64".
                //         if ($netFxSubKeyName -clike '*x86') {
                //             $netFxCapabilityName = "$($windowsSdkCapabilityName)_$toolName"
                //         } else {
                //             $netFxCapabilityName = "$($windowsSdkCapabilityName)_$($toolName)_x64"
                //         }

                //         Write-Capability -Name $netFxCapabilityName -Value $installationFolder
                //     }

            }

            // # Add a capability for the max.
            // $maxWindowsSdk =
            //     $windowsSdks |
            //     Sort-Object -Property Version -Descending |
            //     Select-Object -First 1

            // if ($maxWindowsSdk) {
            //     Write-Capability -Name 'WindowsSdk' -Value $maxWindowsSdk.InstallationFolder
            // }


            return capabilities;
        }
    }
}
