using Microsoft.Win32;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    [ServiceLocator(Default = typeof(RegistryService))]
    internal interface IRegistryService
    {
        bool TryGetRegistryValue(Win32.RegistryHive hive, Win32.RegistryView view, string keyName, string valueName, out string registryValue);
        string[] GetRegistryValueNames(Win32.RegistryHive hive, Win32.RegistryView view, string keyName);
    }

    internal class RegistryService : IRegistryService
    {
        // TODO: Refactor to TryGetRegistryValue
        public bool TryGetRegistryValue(Win32.RegistryHive hive, Win32.RegistryView view, string keyName, string valueName, out string registryValue)
        {
            if (view == Win32.RegistryView.Registry64 && 
                !OsHelper.Is64BitOperatingSystem())
            {
                // TODO: Log... "Skipping."
                registryValue = string.Empty;
                return false;
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
                        registryValue = sValue;
                        return true;
                    }
                }
                else
                {
                    // TODO: Write that we didn't find it
                    registryValue = string.Empty;
                    return false;
                }
            }
            finally
            {
                if (baseKey != null) { baseKey.Dispose(); }
                if (subKey != null) { subKey.Dispose(); }
            }

            registryValue = string.Empty;
            return false;
        }

        public string[] GetRegistryValueNames(Win32.RegistryHive hive, Win32.RegistryView view, string keyName)
        {
            if (view == Win32.RegistryView.Registry64 && 
                !OsHelper.Is64BitOperatingSystem())
            {
                // TODO: Log... "Skipping."
                return null; // TODO: Return correct thing.
            }

            Win32.RegistryKey baseKey = null;
            Win32.RegistryKey subKey = null;

            try
            {
                baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
                subKey = baseKey.OpenSubKey(keyName);

                if (subKey == null)
                {
                    // Write-Host "Key not found."
                    // return
                }

                string[] valueNames = subKey.GetValueNames();

                //     Write-Host "Value names:"
                foreach (string valueName in valueNames)
                {
                    //         Write-Host "  '$valueName'"
                }

                return valueNames;
            }
            finally
            {
                if (baseKey != null) { baseKey.Dispose(); }
                if (subKey != null) { subKey.Dispose(); }
            }
        }
    }
}
