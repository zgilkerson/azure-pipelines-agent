#if OS_WINDOWS
using System;
using System.Security.Principal;
using Microsoft.Win32;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Configuration
{
    [ServiceLocator(Default = typeof(WindowsRegistryManager))]
    public interface IWindowsRegistryManager : IAgentService
    {
        string GetKeyValue(string path, string keyName);
        void SetKeyValue(string path, string keyName, string keyValue);
        void DeleteKey(string path, string keyName);

        bool RegsitryExists(string securityId);
    }

    public class WindowsRegistryManager : AgentService, IWindowsRegistryManager
    {
        public void DeleteKey(string path, string keyName)
        {
            throw new NotImplementedException();
        }

        public string GetKeyValue(string path, string keyName)
        {
            var regValue = Registry.GetValue(path, keyName, null);
            return regValue != null ? regValue.ToString() : null;
        }

        public void SetKeyValue(string path, string keyName, string keyValue)
        {
            Registry.SetValue(path, keyName, keyValue, RegistryValueKind.String);
        }

        public bool RegsitryExists(string securityId)
        {
            return Registry.Users.OpenSubKey(securityId) != null;
        }
    }

    public class WindowsRegistryHelper
    {
        private IWindowsRegistryManager _registryManager;
        private string _userSecurityId;

        public WindowsRegistryHelper(IWindowsRegistryManager regManager, string sid = null)
        {
            _registryManager = regManager;
            _userSecurityId = sid;
        }

        public bool ValidateIfRegistryExistsForTheUser(string sid)
        {
            return _registryManager.RegsitryExists(sid);
        }

        public void SetRegistry(string targetRegistry, string keyValue, bool takeBackup = false)
        {
            if(takeBackup)
            {
                string origValue = GetRegistry(targetRegistry);
                if(!string.IsNullOrEmpty(origValue))
                {
                    SetRegistryInternal(targetRegistry, RegistryConstants.BackupKeyPrefix + targetRegistry, origValue);
                }
            }

            SetRegistryInternal(targetRegistry, targetRegistry, keyValue);
        }       

        public string GetRegistry(string targetRegistry)
        {
            switch(targetRegistry)
            {
                //user specific registry settings
                case WellKnownRegistries.ScreenSaver :
                    var regPath = string.Format(RegistryConstants.RegPaths.ScreenSaver, GetUserRegistryRootPath(_userSecurityId));
                    return _registryManager.GetKeyValue(regPath, targetRegistry);
                case WellKnownRegistries.ScreenSaverDomainPolicy:
                    regPath = string.Format(RegistryConstants.RegPaths.ScreenSaverDomainPolicy, GetUserRegistryRootPath(_userSecurityId));
                    return _registryManager.GetKeyValue(regPath, targetRegistry);
                case WellKnownRegistries.StartupProcess:
                    regPath = string.Format(RegistryConstants.RegPaths.StartupProcess, GetUserRegistryRootPath(_userSecurityId));
                    return _registryManager.GetKeyValue(regPath, targetRegistry);

                //machine specific registry settings                
                case WellKnownRegistries.AutoLogon :
                case WellKnownRegistries.AutoLogonUserName:
                case WellKnownRegistries.AutoLogonDomainName :
                    return _registryManager.GetKeyValue(RegistryConstants.RegPaths.AutoLogon, targetRegistry);

                case WellKnownRegistries.ShutdownReason :
                case WellKnownRegistries.ShutdownReasonUI :
                    return _registryManager.GetKeyValue(RegistryConstants.RegPaths.ShutdownReasonDomainPolicy, targetRegistry);

                case WellKnownRegistries.LegalNoticeCaption :
                case WellKnownRegistries.LegalNoticeText :
                    return _registryManager.GetKeyValue(RegistryConstants.RegPaths.LegalNotice, targetRegistry);
                default:
                   return null;
            }
        }

        public void DeleteRegistry(string targetRegistry)
        {
            switch(targetRegistry)
            {                
                //machine specific registry settings
                case WellKnownRegistries.AutoLogonCount :
                case WellKnownRegistries.AutoLogonPassword :
                     _registryManager.DeleteKey(RegistryConstants.RegPaths.AutoLogon, RegistryConstants.KeyNames.AutoLogonCount);
                     break;
                default:
                   throw new InvalidOperationException("Delete registry is called for a undocumented registry");
            }
        }

        private void SetRegistryInternal(string targetRegistry, string keyName, string keyValue)
        {
            switch(targetRegistry)
            {
                //user specific registry settings
                case WellKnownRegistries.ScreenSaver :
                    var regPath = string.Format(RegistryConstants.RegPaths.ScreenSaver, GetUserRegistryRootPath(_userSecurityId));
                    _registryManager.SetKeyValue(regPath, keyName, keyValue);                    
                    break;
                case WellKnownRegistries.ScreenSaverDomainPolicy:
                    regPath = string.Format(RegistryConstants.RegPaths.ScreenSaverDomainPolicy, GetUserRegistryRootPath(_userSecurityId));
                    _registryManager.SetKeyValue(regPath, keyName, keyValue);
                    break;
                case WellKnownRegistries.StartupProcess:
                    regPath = string.Format(RegistryConstants.RegPaths.StartupProcess, GetUserRegistryRootPath(_userSecurityId));
                    _registryManager.SetKeyValue(regPath, keyName, keyValue);
                    break;
                
                //machine specific registry settings
                case WellKnownRegistries.AutoLogon :
                case WellKnownRegistries.AutoLogonUserName:
                case WellKnownRegistries.AutoLogonDomainName :
                    _registryManager.SetKeyValue(RegistryConstants.RegPaths.AutoLogon, keyName, keyValue);
                    break;

                case WellKnownRegistries.ShutdownReason :
                case WellKnownRegistries.ShutdownReasonUI :
                    _registryManager.SetKeyValue(RegistryConstants.RegPaths.ShutdownReasonDomainPolicy, keyName, keyValue);
                    break;

                case WellKnownRegistries.LegalNoticeCaption :
                    _registryManager.SetKeyValue(RegistryConstants.RegPaths.LegalNotice, keyName, keyValue);
                    _registryManager.SetKeyValue(RegistryConstants.RegPaths.LegalNotice, keyName, keyValue);
                    break;
            }
        }

        private string GetUserRegistryRootPath(string sid)
        {
            return string.IsNullOrEmpty(sid) ?
                RegistryConstants.CurrentUserRootPath :
                String.Format(RegistryConstants.DifferentUserRootPath, sid);
        }
    }
    
    public class WellKnownRegistries
    {
        public const string ScreenSaver = "ScreenSaveActive";
        public const string ScreenSaverDomainPolicy = "ScreenSaveActive";
        public const string AutoLogon = "AutoAdminLogon";
        public const string AutoLogonUserName = "DefaultUserName";
        public const string AutoLogonDomainName = "DefaultDomainName";
        public const string AutoLogonCount = "AutoLogonCount";
        public const string AutoLogonPassword = "DefaultPassword";
        public const string StartupProcess = "VSTSAgent";
        public const string ShutdownReason = "ShutdownReasonOn";
        public const string ShutdownReasonUI = "ShutdownReasonUI";
        public const string LegalNoticeCaption = "legalnoticecaption";
        public const string LegalNoticeText = "legalnoticetext";
    }

    public struct RegistryConstants
    {
        public const string CurrentUserRootPath = @"HKEY_CURRENT_USER";
        public const string DifferentUserRootPath = @"HKEY_USERS\{0}";
        public const string BackupKeyPrefix = "VSTSAgentBackup_";

        public struct RegPaths
        {
            public const string ScreenSaver = @"{0}\Control Panel\Desktop";
            public const string ScreenSaverDomainPolicy = @"{0}\Software\Policies\Microsoft\Windows\Control Panel\Desktop";
            public const string StartupProcess = @"{0}\SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            public const string AutoLogon = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            public const string ShutdownReasonDomainPolicy = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows NT\Reliability";
            public const string LegalNotice = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        }
    }
}
#endif