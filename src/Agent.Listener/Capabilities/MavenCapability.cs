using System.Collections.Generic;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class MavenCapability : IPrivateWindowsCapabilityProvider
    {
        private readonly IEnvironmentService _environmentService;

        public MavenCapability(IEnvironmentService environmentService)
        {
            ArgUtil.NotNull(environmentService, nameof(environmentService));

            _environmentService = environmentService;
        }

        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            // TODO: Put environment variable names in static class with static properties.
            // Write-Host "Checking: env:JAVA_HOME"
            if(!_environmentService.GetEnvironmentVariable("JAVA_HOME"))
            {
                //     Write-Host "Value not found or empty."
                return capabilities;
            }

            var environmentCapability = new EnvironmentVariableCapability(name: "maven", variableName: "M2_HOME");

            // Add-CapabilityFromEnvironment -Name 'ant' -VariableName 'ANT_HOME'
            // TODO: Trace... checking for value ant and variable name ANT_HOME
            // TODO: Potentially move this into either a base class or a service class. Depends how it will be reused.
            // I think a lot of classes get the env var then do a lot with it, so maybe better as a service.
            string value = _environmentService.GetEnvironmentVariable(environmentCapability.VariableName);
            if (!string.IsNullOrEmpty(value))
            {
                // The environment variable exists
                var capability = new Capability(environmentCapability.Name, value);
                //Trace.Info($"Adding '{capability.Name}': '{capability.Value}'");
                capabilities.Add(capability);
            }

            return capabilities;
        }
    }
}
