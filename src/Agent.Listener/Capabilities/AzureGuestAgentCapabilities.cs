using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
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
}
