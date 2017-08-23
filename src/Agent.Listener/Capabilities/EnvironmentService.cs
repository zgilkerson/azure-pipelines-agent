using System;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    [ServiceLocator(Default = typeof(EnvironmentService))]
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
}
