namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal class RegistryService : IRegistryService
    {
        // TODO: Refactor to TryGetRegistryValue
        internal bool TryGetRegistryValue(string hive, string view, string keyName, string valueName, out string registryValue)
        {
            if (view == "Registry64" && 
                !System.Environment.Is64BitOperatingSystem)
            {
                // TODO: Log... "Skipping."
                return null;
            }

            Win32.RegistryKey baseKey = null;
            Win32.RegistryKey subKey = null;

            try
            {
                baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
                subKey = baseKey.OpenSubKey(keyName);

                var value = subKey.GetValue(valueName);

                if (value != null)
                {
                    string sValue = value as string;

                    if (!string.IsNullOrEmpty(sValue))
                    {
                        // TODO: Write that we found it
                        return sValue;
                    }
                }
                else
                {
                    // TODO: Write that we didn't find it
                    return false;
                }
            }
            finally
            {
                if (baseKey != null) { baseKey.Dispose(); }
                if (subKey != null) { subKey.Dispose(); }
            }

            return false;
        }
    }
}
