using System.Collections.Generic;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class PowerShellCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            //Write-Capability -Name 'PowerShell' -Value $PSVersionTable.PSVersion


            return capabilities;
        }
    }
}
