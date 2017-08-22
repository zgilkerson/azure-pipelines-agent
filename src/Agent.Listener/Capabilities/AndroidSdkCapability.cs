using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class AndroidSdkCapability : IPrivateWindowsCapabilityProvider
    {
        private readonly IRegistryService _registryService;

        internal AndroidSdkCapability(IRegistryService registryService)
        {
            ArgUtil.NotNull(registryService, nameof(registryService));

            _registryService = registryService;
        }

        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();
            // Do this when we add any capability
            //Trace.Info($"Adding '{capability.Name}': '{capability.Value}'");

            string androidSdkPath = GetAndroidSdkPath();

            if (!string.IsNullOrEmpty(androidSdkPath))
            {
                // Add the capability
                // TODO: Write to host. We can probably put this in a special collection that writes to host when items are added? Something to reuse code.
                capabilities.Add(new Capability(CapabilityNames.AndroidSdk, androidSdkPath));

                // Check if the platforms directory exists
                string platformsDirectory = Path.Combine(androidSdkPath, "platforms");

                if (Directory.Exists(platformsDirectory))
                {
                    foreach (string platformDir in Directory.GetDirectories(platformsDirectory))
                    {
                        string capabilityName = new DirectoryInfo(platformDir).Name.Replace("android-", CapabilityNames.AndroidSdk + "_");
                        capabilities.Add(new Capability(capabilityName, platformDir));
                    }
                }
            }

            return capabilities;
        }

        private string GetAndroidSdkPath()
        {
            // Attempt to get it from ANDROID_HOME environment variable
            string envVar = Environment.GetEnvironmentVariable("ANDROID_HOME");
            if (!string.IsNullOrEmpty(envVar))
            {
                // Write-Host "Found ANDROID_HOME from machine environment."
                return envVar;
            }

            // Attempt to get from registry info
            var hiveViewPairs = new List<HiveViewPair>
            {
                new HiveViewPair("CurrentUser", "Default"), 
                new HiveViewPair("LocalMachine", "Registry64"), 
                new HiveViewPair("LocalMachine", "Registry32")
            };

            foreach (HiveViewPair pair in hiveViewPairs)
            {
                string registryValue;
                if (_registryService.TryGetRegistryValue(pair.Hive, pair.View, "SOFTWARE\\Android SDK Tools", "Path", out registryValue))
                {
                    return registryValue.Trim();
                }
            }

            return null;
        }
    }
}
