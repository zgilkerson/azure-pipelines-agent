namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
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
}
