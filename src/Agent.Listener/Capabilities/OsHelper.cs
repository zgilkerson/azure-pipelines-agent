using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal static class OsHelper
    {
        internal static bool Is64BitOperatingSystem()
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 || 
                   RuntimeInformation.OSArchitecture ==  Architecture.X64;
        }
    }
}
