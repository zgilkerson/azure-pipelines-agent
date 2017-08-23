using System;
using System.Collections.Generic;
using System.IO;
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
            string[] valueNames = _registryService.GetRegistryValueNames(hive: Win32.RegistryHive.LocalMachine, view: Win32.RegistryView.Registry32, keyName: rootsKeyName);

            var versionInfos = new List<VersionInfo>();
            foreach (string valueName in valueNames)
            {
                if (!valueName.StartsWith("KitsRoot", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string installDirectory;
                if(_registryService.TryGetRegistryValue(Win32.RegistryHive.LocalMachine, Win32.RegistryView.Registry32, rootsKeyName, valueName, out installDirectory))
                {
                    List<string> splitInstallDirectory = installDirectory.Split(Path.DirectorySeparatorChar).Select(d => d.Trim()).ToList();

                    if (!splitInstallDirectory.Any())
                    {
                        continue;
                    }

                    //     $version = $null
                    //     if (!([System.Version]::TryParse($splitInstallDirectory[-1], [ref]$version))) {
                    //         continue
                    //     }
                    // TODO: Why is it looking at -1 index?
                    // TODO: Is this a bug?
                    Version version;
                    if(!Version.TryParse(splitInstallDirectory[-1], out version))
                    {
                        continue;
                    }

                    capabilities.Add(new Capability(name: $"{CapabilityNames.WindowsKit}_{version.Major}.{version.Minor}", value: installDirectory));
                    versionInfos.Add(new VersionInfo(version: version, installDirectory: installDirectory));
                }
                else
                {
                    // TODO: Report error? Check PS code again tos ee what it does. The code there seems to assume this will never happen.

                }
            }

            if (versionInfos.Any())
            {
                VersionInfo maxInfo = versionInfos.ToList().OrderBy(v => v).First();

                capabilities.Add(new Capability(name: CapabilityNames.WindowsKit, value: maxInfo.InstallDirectory));
            }

            return capabilities;
        }

        private class VersionInfo
        {
            public VersionInfo(Version version, string installDirectory)
            {
                Version = version;
                InstallDirectory = installDirectory;
            }

            public Version Version { get; }
            public string InstallDirectory { get; }
        }
    }
}
