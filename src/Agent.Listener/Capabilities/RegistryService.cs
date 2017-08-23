namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    [ServiceLocator(Default = typeof(RegistryService))]
    internal interface IRegistryService
    {
        bool TryGetRegistryValue(string hive, string view, string keyName, string valueName, out string registryValue);
        object GetRegistryValueNames(string hive, string view, string keyName);
    }

    internal class RegistryService : IRegistryService
    {
        // TODO: Refactor to TryGetRegistryValue
        internal bool TryGetRegistryValue(string hive, string view, string keyName, string valueName, out string registryValue)
        {
            if (view == "Registry64" && 
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

        internal object GetRegistryValueNames(string hive, string view, string keyName)
        {
            // Write-Host "Checking: hive '$Hive', view '$View', key name '$KeyName', value name '$ValueName'"
            // if ($View -eq 'Registry64' -and !([System.Environment]::Is64BitOperatingSystem)) {
            //     Write-Host "Skipping."
            //     return
            // }

            // $baseKey = $null
            // $subKey = $null
            // try {
            //     # Open the base key.
            //     $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($Hive, $View)

            //     # Open the sub key as read-only.
            //     $subKey = $baseKey.OpenSubKey($KeyName, $false)

            //     # Check if the sub key was found.
            //     if (!$subKey) {
            //         Write-Host "Key not found."
            //         return
            //     }

            //     # Get the value names.
            //     $valueNames = $subKey.GetValueNames()
            //     Write-Host "Value names:"
            //     foreach ($valueName in $valueNames) {
            //         Write-Host "  '$valueName'"
            //     }

            //     return $valueNames
            // } finally {
            //     # Dispose the sub key.
            //     if ($subKey) {
            //         $null = $subKey.Dispose()
            //     }

            //     # Dispose the base key.
            //     if ($baseKey) {
            //         $null = $baseKey.Dispose()
            //     }
            // }
            return null;
        }
    }
}
