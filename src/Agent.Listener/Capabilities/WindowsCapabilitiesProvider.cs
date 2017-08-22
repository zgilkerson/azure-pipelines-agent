using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

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

    internal class AzurePowerShellCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();


            return capabilities;
        }
    }
}
