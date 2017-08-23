using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class ChefCapabilities : IPrivateWindowsCapabilityProvider
    {
        private readonly IRegistryService _registryService;

        internal ChefCapabilities(IRegistryService registryService)
        {
            ArgUtil.NotNull(registryService, nameof(registryService));

            _registryService = registryService;
        }

        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            // Attempt to get location from Registry
            string version = GetVersionFromRegistry();

            // Get the chef directory from PATH
            string chefDirectory = GetChefDirectoryFromPath();

            // Add capabilities
            if (!string.IsNullOrEmpty(version) && 
                !string.IsNullOrEmpty(chefDirectory))
            {
                // chef
                // Write-Capability -Name 'Chef' -Value $version // TODO: Would this even work correctly? It's adding the version but not the path
                // Dont we need to mix the two? Install Chef and test this.
                capabilities.Add(new Capability(CapabilityNames.Chef, version));

                // Add Knife Capability if it exists
                capabilities.AddRange(GetKnifeCapabilities(chefDirectory));
            }

            return capabilities;
        }

        private string GetVersionFromRegistry()
        {
            

            return null;
        }

        private string GetChefDirectoryFromPath()
        {
            // TODO: Find out what the path normally looks like
            var pathEnvVar = Environment.GetEnvironmentVariable("PATH");
            string chefPath = pathEnvVar.Split(';').Where(p => p.Contains("chefdk\\bin")).FirstOrDefault();

            if (!string.IsNullOrEmpty(chefPath) && 
                Directory.Exists(chefPath))
            {
                return Directory.GetParent(chefPath.TrimEnd(Path.DirectorySeparatorChar)).FullName;
            }

            return null;
        }

        private List<Capability> GetKnifeCapabilities(string chefDirectory)
        {
            ArgUtil.NotNullOrEmpty(chefDirectory, nameof(chefDirectory));

            var capabilities = new List<Capability>();

            string gemsDirectory = Path.Combine(chefDirectory, @"embedded\lib\ruby\gems");

            if (!Directory.Exists(gemsDirectory))
            {
                return capabilities;
            }

            // # Get the Knife Reporting gem file.
            // Write-Host "Searching for Knife Reporting gem."
            // $file =
            //     Get-ChildItem -LiteralPath $gemsDirectory -Filter "*.gem" -Recurse |
            //     Where-Object { $_ -is [System.IO.FileInfo] } |
            //     ForEach-Object { Write-Host "Candidate: '$($_.FullName)'" } |
            //     Where-Object { $_.FullName -clike '*knife-reporting*' } |
            //     Select-Object -First 1
            // if (!$file) {
            //     Write-Host "Not found."
            //     return
            // }

            // # Get the file name without the extension.
            // $baseName = $file.BaseName

            // # Get the version from the file name.
            // $segments = $baseName.Split('-')
            // $versionString = $segments[-1]
            // $versionObject = $null
            // if ($segments.Length -gt 1 -and ([Systme.Version]::TryParse($versionString, [ref]$versionObject))) {
            //     $versionString = $versionObject.ToString()
            // } else {
            //     $versionString = '0.0'
            // }
            string versionString;

            // # Add the capability.
            // Write-Capability -Name 'KnifeReporting' -Value $versionString
            capabilities.Add(new Capability(name: CapabilityNames.Knife, value: versionString));

            return capabilities;
        }
    }
}
