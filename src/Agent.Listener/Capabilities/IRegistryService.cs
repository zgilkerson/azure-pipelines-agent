namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal interface IRegistryService
    {
        bool TryGetRegistryValue(string hive, string view, string keyName, string valueName, out string registryValue);
    }
}
