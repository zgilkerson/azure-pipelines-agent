using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class ChefCapabilities : IPrivateWindowsCapabilityProvider
    {
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
                capabilities.Add(new Capability(CapabilityNames.Chef, version));

                // Add-KnifeCapabilities -ChefDirectory $chefDirectory


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
                // return [System.IO.Directory]::GetParent($cdkBin.TrimEnd([System.IO.Path]::DirectorySeparatorChar)).FullName
            }

            return null;
        }
    }
}
