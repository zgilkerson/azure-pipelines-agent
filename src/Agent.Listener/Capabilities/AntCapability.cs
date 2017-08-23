using Microsoft.VisualStudio.Services.Agent.Util;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class AntCapability : IPrivateWindowsCapabilityProvider
    {
        private readonly IEnvironmentService _environmentService;

        public AntCapability(IEnvironmentService environmentService)
        {
            ArgUtil.NotNull(environmentService, nameof(environmentService));

            _environmentService = environmentService;
        }

        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            // TODO: Use CapabilityNames.Ant here?
            var environmentCapability = new EnvironmentVariableCapability(name: CapabilityNames.Ant, variableName: "ANT_HOME");

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
