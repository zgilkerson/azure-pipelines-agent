using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    // TODO: Rename.
    internal interface IPrivateWindowsCapabilityProvider
    {
        List<Capability> GetCapabilities();
    }

    // TODO: Why is this partial?
    public sealed partial class WindowsCapabilitiesProvider : AgentService, ICapabilitiesProvider
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

    internal sealed class MSBuildCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();


            return capabilities;
        }
    }

    internal sealed class PowerShellCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            //Write-Capability -Name 'PowerShell' -Value $PSVersionTable.PSVersion


            return capabilities;
        }
    }

    internal sealed class VisualStudioCapability : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();


            return capabilities;
        }
    }

    internal sealed class WindowsKitCapability : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();


            return capabilities;
        }
    }

    internal sealed class WindowsSdkCapability : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();


            return capabilities;
        }
    }

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

    internal sealed class SqlPackageCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();


            return capabilities;
        }
    }

    internal sealed class DotNetFrameworkCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();


            return capabilities;
        }
    }

    internal sealed class JavaCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();


            return capabilities;
        }
    }

    internal sealed class MavenCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            // TODO: This is an example of something we could add to a general EnvironmentCapabilities : IPrivateWindowsCapabilityProvider
            // Write-Host "Checking: env:JAVA_HOME"
            // if (!$env:JAVA_HOME) {
            //     Write-Host "Value not found or empty."
            //     return
            // }

            // Add-CapabilityFromEnvironment -Name 'maven' -VariableName 'M2_HOME'

            return capabilities;
        }
    }

    internal interface IEnvironmentService
    {
        // TODO: Write methods and implement. Inject in Capabilities classes.
        string GetEnvironmentVariable(string variable);
    }

    internal class EnvironmentService : IEnvironmentService
    {
        // TODO: Refactory to TryGetEnvironmentVariable
        string IEnvironmentService.GetEnvironmentVariable(string variable)
        {
            throw new NotImplementedException();
        }
    }

    internal static class CapabilityNames
    {
        public static string AndroidSdk = "AndroidSDK";
        public static string AzureGuestAgent = "AzureGuestAgent";
        public static string Chef = "Chef";

        public static string XamarinAndroid = "Xamarin.Android";
    }

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
                string registryValue = _registryService.GetRegistryValue(pair.Hive, pair.View, "SOFTWARE\\Android SDK Tools", "Path");

                if (!string.IsNullOrEmpty(registryValue))
                {
                    return registryValue.Trim();
                }
            }

            return null;
        }
    }

    internal class HiveViewPair
    {
        public HiveViewPair(string hive, string view)
        {
            Hive = hive;
            View = view;
        }

        public string Hive { get; }
        public string View { get; }
    }

    internal class EnvironmentVariableCapability
    {
        public EnvironmentVariableCapability(string name, string variableName)
        {
            Name = name;
            VariableName = variableName;
        }

        public string Name {get;}
        public string VariableName {get;}
    }

    internal class AzureGuestAgentCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            Process runningProcess = Process.GetProcessesByName("WindowsAzureGuestAgent").FirstOrDefault();

            if (runningProcess == null)
            {
                // TODO: Log that we couldnt find WindowsAzureGuestAgent
            }
            else
            {
                // TODO: Log that we found WindowsAzureGuestAgent
                // TODO: Make sure runningProcess.MainModule.FileName is right
                // TODO: Abstract getting the name and file of a running process?
                capabilities.Add(new Capability(CapabilityNames.AzureGuestAgent, runningProcess.MainModule.FileName)); 
            }

            // TODO: Is the best way to get this to look at the list of running processes? Is there something static we can check or does it have to be running?

            return capabilities;
        }
    }

    private class AzurePowerShellCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();


            return capabilities;
        }
    }

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
