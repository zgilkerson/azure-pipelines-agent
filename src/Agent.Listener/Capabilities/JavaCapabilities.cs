using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal sealed class JavaCapabilities : IPrivateWindowsCapabilityProvider
    {
        public List<Capability> GetCapabilities()
        {
            var capabilities = new List<Capability>();

            // TODO: Move simple registry loads to base class.
            // For this class, for each of these rows, make them their own class.
            // Then on return check if there are capabilities.

            // # Check for JRE.
            // $latestJre = $null
            // $null = Add-CapabilityFromRegistry -Name 'java_6' -Hive 'LocalMachine' -View 'Registry32' -KeyName $jre6KeyName -ValueName 'JavaHome' -Value ([ref]$latestJre)
            // $null = Add-CapabilityFromRegistry -Name 'java_7' -Hive 'LocalMachine' -View 'Registry32' -KeyName $jre7KeyName -ValueName 'JavaHome' -Value ([ref]$latestJre)
            // $null = Add-CapabilityFromRegistry -Name 'java_8' -Hive 'LocalMachine' -View 'Registry32' -KeyName $jre8KeyName -ValueName 'JavaHome' -Value ([ref]$latestJre)
            // $null = Add-CapabilityFromRegistry -Name 'java_6_x64' -Hive 'LocalMachine' -View 'Registry64' -KeyName $jre6KeyName -ValueName 'JavaHome' -Value ([ref]$latestJre)
            // $null = Add-CapabilityFromRegistry -Name 'java_7_x64' -Hive 'LocalMachine' -View 'Registry64' -KeyName $jre7KeyName -ValueName 'JavaHome' -Value ([ref]$latestJre)
            // $null = Add-CapabilityFromRegistry -Name 'java_8_x64' -Hive 'LocalMachine' -View 'Registry64' -KeyName $jre8KeyName -ValueName 'JavaHome' -Value ([ref]$latestJre)
            
            // NOTE: The order here matters. We want to prefer the latest x64 so we first add non x64 (in order) then add x64 (in order).
            var jreCapabilities = new List<IPrivateWindowsCapabilityProvider>
            {
                new Java6JRECapability(), 
                new Java7JRECapability(), 
                new Java8JRECapability(), 
                new Java6x64JRECapability(), 
                new Java7x64JRECapability(), 
                new Java8x64JRECapability(), 
            };
            Capability latestJre = null;
            foreach (var jreCapability in jreCapabilities)
            {
                var c = jreCapability.GetCapabilities();

                if(c.Any())
                {
                    capabilities.AddRange(c);
                    latestJre = c.First();
                }
            }

            if (latestJre != null)
            {
                // Favor x64.
                capabilities.Add(new Capability(name: CapabilityNames.Java, value: latestJre.Value));
            }

            // # Check for JDK.
            // $latestJdk = $null
            // $null = Add-CapabilityFromRegistry -Name 'jdk_6' -Hive 'LocalMachine' -View 'Registry32' -KeyName $jdk6KeyName -ValueName 'JavaHome' -Value ([ref]$latestJdk)
            // $null = Add-CapabilityFromRegistry -Name 'jdk_7' -Hive 'LocalMachine' -View 'Registry32' -KeyName $jdk7KeyName -ValueName 'JavaHome' -Value ([ref]$latestJdk)
            // $null = Add-CapabilityFromRegistry -Name 'jdk_8' -Hive 'LocalMachine' -View 'Registry32' -KeyName $jdk8KeyName -ValueName 'JavaHome' -Value ([ref]$latestJdk)
            // $null = Add-CapabilityFromRegistry -Name 'jdk_6_x64' -Hive 'LocalMachine' -View 'Registry64' -KeyName $jdk6KeyName -ValueName 'JavaHome' -Value ([ref]$latestJdk)
            // $null = Add-CapabilityFromRegistry -Name 'jdk_7_x64' -Hive 'LocalMachine' -View 'Registry64' -KeyName $jdk7KeyName -ValueName 'JavaHome' -Value ([ref]$latestJdk)
            // $null = Add-CapabilityFromRegistry -Name 'jdk_8_x64' -Hive 'LocalMachine' -View 'Registry64' -KeyName $jdk8KeyName -ValueName 'JavaHome' -Value ([ref]$latestJdk)



            // if ($latestJdk) {
            //     # Favor x64.
            //     Write-Capability -Name 'jdk' -Value $latestJdk
            // }

            return capabilities;
        }

        private static class JavaKeyNames
        {
            public static string Jre6KeyName => "Software\\JavaSoft\\Java Runtime Environment\\1.6";
            public static string Jre7KeyName => "Software\\JavaSoft\\Java Runtime Environment\\1.7";
            public static string Jre8KeyName => "Software\\JavaSoft\\Java Runtime Environment\\1.8";

            public static string Jdk6KeyName => "Software\\JavaSoft\\Java Development Kit\\1.6";
            public static string Jdk7KeyName => "Software\\JavaSoft\\Java Development Kit\\1.7";
            public static string Jdk8KeyName => "Software\\JavaSoft\\Java Development Kit\\1.8";
        }

        // For now, make this private. Move to internal if others need it.
        private abstract class RegistryCapability : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();

                // function Add-CapabilityFromRegistry {
                //     [CmdletBinding()]
                //     param(
                //         [Parameter(Mandatory = $true)]
                //         [string]$Name,

                //         [Parameter(Mandatory = $true)]
                //         [ValidateSet('CurrentUser', 'LocalMachine')]
                //         [string]$Hive,

                //         [Parameter(Mandatory = $true)]
                //         [ValidateSet('Default', 'Registry32', 'Registry64')]
                //         [string]$View,

                //         [Parameter(Mandatory = $true)]
                //         [string]$KeyName,

                //         [Parameter(Mandatory = $true)]
                //         [string]$ValueName,

                //         [ref]$Value)

                //     $val = Get-RegistryValue -Hive $Hive -View $View -KeyName $KeyName -ValueName $ValueName
                //     if ($val -eq $null) {
                //         return $false
                //     }

                //     if ($val -is [string] -and $val -eq '') {
                //         return $false
                //     }

                //     Write-Capability -Name $Name -Value $val
                //     if ($Value) {
                //         $Value.Value = $val
                //     }

                //     return $true
                // }

                return capabilities;
            }
        }

        private sealed class Java6JRECapability : RegistryCapability
        {

        }

        private sealed class Java7JRECapability : RegistryCapability
        {

        }

        private sealed class Java8JRECapability : RegistryCapability
        {

        }

        private sealed class Java6x64JRECapability : RegistryCapability
        {

        }

        private sealed class Java7x64JRECapability : RegistryCapability
        {

        }

        private sealed class Java8x64JRECapability : RegistryCapability
        {

        }
    }
}
