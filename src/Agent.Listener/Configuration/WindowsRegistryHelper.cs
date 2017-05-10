#if OS_WINDOWS
using System;
using System.Collections.Generic;
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
        private List<string> _regSettingsForBackup;

        public WindowsRegistryHelper(IWindowsRegistryManager regManager, List<string> registriesForBackup = null, string sid = null)
        {
            _registryManager = regManager;
            _userSecurityId = sid;
            _regSettingsForBackup = registriesForBackup;
        }

        public bool ValidateIfRegistryExistsForTheUser(string sid)
        {
            return _registryManager.RegsitryExists(sid);
        }

        public void SetRegistry(string targetRegistry, string keyValue)
        {
            if(ShouldTakeBackup(targetRegistry))
            {
                string origValue = GetRegistry(targetRegistry);
                if(!string.IsNullOrEmpty(origValue))
                {
                    SetRegistryInternal(targetRegistry, GetBackupKeyName(targetRegistry), origValue);
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
                    return _registryManager.GetKeyValue(regPath, RegistryConstants.ScreenSaverSettingsKeyName);

                case WellKnownRegistries.ScreenSaverDomainPolicy:
                    regPath = string.Format(RegistryConstants.RegPaths.ScreenSaverDomainPolicy, GetUserRegistryRootPath(_userSecurityId));
                    return _registryManager.GetKeyValue(regPath, RegistryConstants.ScreenSaverSettingsKeyName);

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
                     _registryManager.DeleteKey(RegistryConstants.RegPaths.AutoLogon, targetRegistry);
                     break;
                default:
                   throw new InvalidOperationException("Delete registry is called for a undocumented registry");
            }
        }

        public void RevertBackOriginalRegistry(string targetRegistryKey)
        {
            var userRegSettingPath = GetUserRegistryRootPath(_userSecurityId);

            switch(targetRegistryKey)
            {
                //user specific registry settings
                case WellKnownRegistries.ScreenSaver :
                    var regPath = string.Format(RegistryConstants.RegPaths.ScreenSaver, userRegSettingPath);
                    RevertBackOriginalRegistryInternal(regPath, targetRegistryKey);
                    break;

                case WellKnownRegistries.ScreenSaverDomainPolicy:
                    regPath = string.Format(RegistryConstants.RegPaths.ScreenSaverDomainPolicy, userRegSettingPath);
                    RevertBackOriginalRegistryInternal(regPath, targetRegistryKey);
                    break;

                case WellKnownRegistries.StartupProcess:
                    regPath = string.Format(RegistryConstants.RegPaths.StartupProcess, userRegSettingPath);
                    RevertBackOriginalRegistryInternal(regPath, targetRegistryKey);
                    break;
                
                //machine specific registry settings
                case WellKnownRegistries.AutoLogon :
                case WellKnownRegistries.AutoLogonUserName:
                case WellKnownRegistries.AutoLogonDomainName :
                case WellKnownRegistries.AutoLogonPassword:
                case WellKnownRegistries.AutoLogonCount:
                    RevertBackOriginalRegistryInternal(RegistryConstants.RegPaths.AutoLogon, targetRegistryKey);
                    break;

                case WellKnownRegistries.ShutdownReason :
                case WellKnownRegistries.ShutdownReasonUI :
                    RevertBackOriginalRegistryInternal(RegistryConstants.RegPaths.ShutdownReasonDomainPolicy, targetRegistryKey);
                    break;

                case WellKnownRegistries.LegalNoticeCaption :
                case WellKnownRegistries.LegalNoticeText :
                    RevertBackOriginalRegistryInternal(RegistryConstants.RegPaths.LegalNotice, targetRegistryKey);                    
                    break;
            }
        }

        private void RevertBackOriginalRegistryInternal(string regPath, string targetRegistryKey)
        {
            var backupKeyName = GetBackupKeyName(targetRegistryKey);
            var originalValue = _registryManager.GetKeyValue(regPath, backupKeyName);
            
            if(string.IsNullOrEmpty(originalValue))
            {
                _registryManager.DeleteKey(regPath, targetRegistryKey);
                return;
            }

            //revert back the original value
            _registryManager.SetKeyValue(regPath, targetRegistryKey, originalValue);
            //delete the backup key
            _registryManager.DeleteKey(regPath, backupKeyName);
        }

        private string GetBackupKeyName(string targetKey)
        {
            return RegistryConstants.BackupKeyPrefix + targetKey;
        }
        
        private void SetRegistryInternal(string targetRegistry, string keyName, string keyValue)
        {
            var userRegSettingPath = GetUserRegistryRootPath(_userSecurityId);

            switch(targetRegistry)
            {
                //user specific registry settings
                case WellKnownRegistries.ScreenSaver :
                    var regPath = string.Format(RegistryConstants.RegPaths.ScreenSaver, userRegSettingPath);
                    _registryManager.SetKeyValue(regPath, RegistryConstants.ScreenSaverSettingsKeyName, keyValue);                    
                    break;

                case WellKnownRegistries.ScreenSaverDomainPolicy:
                    regPath = string.Format(RegistryConstants.RegPaths.ScreenSaverDomainPolicy, userRegSettingPath);
                    _registryManager.SetKeyValue(regPath, RegistryConstants.ScreenSaverSettingsKeyName, keyValue);
                    break;

                case WellKnownRegistries.StartupProcess:
                    regPath = string.Format(RegistryConstants.RegPaths.StartupProcess, userRegSettingPath);
                    _registryManager.SetKeyValue(regPath, keyName, keyValue);
                    break;
                
                //machine specific registry settings
                case WellKnownRegistries.AutoLogon :
                case WellKnownRegistries.AutoLogonUserName:
                case WellKnownRegistries.AutoLogonDomainName :
                case WellKnownRegistries.AutoLogonPassword:
                case WellKnownRegistries.AutoLogonCount:
                    _registryManager.SetKeyValue(RegistryConstants.RegPaths.AutoLogon, keyName, keyValue);
                    break;

                case WellKnownRegistries.ShutdownReason :
                case WellKnownRegistries.ShutdownReasonUI :
                    _registryManager.SetKeyValue(RegistryConstants.RegPaths.ShutdownReasonDomainPolicy, keyName, keyValue);
                    break;

                case WellKnownRegistries.LegalNoticeCaption :
                case WellKnownRegistries.LegalNoticeText :
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

        private bool ShouldTakeBackup(string registryKey)
        {
            return _regSettingsForBackup!= null 
                    && _regSettingsForBackup.Exists(item => item.Equals(registryKey, StringComparison.OrdinalIgnoreCase));
        }
    }
    
    public class WellKnownRegistries
    {
        public const string ScreenSaver = "ScreenSaveActive";
        public const string ScreenSaverDomainPolicy = "ScreenSaveActiveDomainPolicy";
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
        public const string ScreenSaverSettingsKeyName ="ScreenSaverActive";

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