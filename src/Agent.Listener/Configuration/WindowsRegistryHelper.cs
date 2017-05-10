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
        string GetKeyValue(string path, string subKeyName);
        void SetKeyValue(string path, string subKeyName, string subKeyValue);
        void DeleteKey(string path, string subKeyName);
        bool RegsitryExists(string securityId);
    }

    public class WindowsRegistryManager : AgentService, IWindowsRegistryManager
    {
        public void DeleteKey(string path, string subKeyName)
        {
            throw new NotImplementedException();
        }

        public string GetKeyValue(string path, string subKeyName)
        {
            var regValue = Registry.GetValue(path, subKeyName, null);
            return regValue != null ? regValue.ToString() : null;
        }

        public void SetKeyValue(string path, string subKeyName, string subKeyValue)
        {
            Registry.SetValue(path, subKeyName, subKeyValue, RegistryValueKind.String);
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
        private List<WellKnownRegistries> _regSettingsForBackup;

        public WindowsRegistryHelper(IWindowsRegistryManager regManager, List<WellKnownRegistries> registriesForBackup = null, string sid = null)
        {
            _registryManager = regManager;
            _userSecurityId = sid;
            _regSettingsForBackup = registriesForBackup;
        }

        public bool ValidateIfRegistryExistsForTheUser(string sid)
        {
            return _registryManager.RegsitryExists(sid);
        }

        public void SetRegistryKeyValue(WellKnownRegistries targetRegistry, string keyValue)
        {
            if(ShouldTakeBackup(targetRegistry))
            {
                string origValue = GetRegistryKeyValue(targetRegistry);
                if(!string.IsNullOrEmpty(origValue))
                {
                    SetRegistryKeyInternal(targetRegistry, GetBackupKeyName(targetRegistry), origValue);
                }
            }

            SetRegistryKeyInternal(targetRegistry, RegistryConstants.GetActualKeyNameForWellKnownRegistry(targetRegistry), keyValue);
        }       

        public string GetRegistryKeyValue(WellKnownRegistries targetRegistry)
        {
            var regPath = GetRegistryKeyPath(targetRegistry, _userSecurityId);
            
            return string.IsNullOrEmpty(regPath)
                    ? null
                    : _registryManager.GetKeyValue(regPath, RegistryConstants.GetActualKeyNameForWellKnownRegistry(targetRegistry));
        }       

        public void DeleteRegistry(WellKnownRegistries targetRegistry)
        {
            switch(targetRegistryKey)
            {                
                //machine specific registry settings
                case WellKnownRegistries.AutoLogonCount :
                case WellKnownRegistries.AutoLogonPassword :
                     _registryManager.DeleteKey(RegistryConstants.RegPaths.AutoLogon, RegistryConstants.GetActualKeyNameForWellKnownRegistry(targetRegistry));
                     break;
                default:
                   throw new InvalidOperationException("Delete registry is called for a undocumented registry");
            }
        }

        public void RevertBackOriginalRegistry(WellKnownRegistries targetRegistry)
        {
            var regPath = GetRegistryKeyPath(targetRegistryKey, _userSecurityId);
            RevertBackOriginalRegistryInternal(regPath, targetRegistry);
        }

        private string GetRegistryKeyPath(WellKnownRegistries targetRegistry, string userSid = null)
        {
            var userHivePath = GetUserRegistryRootPath(userSid);
            switch(targetRegistry)
            {
                //user specific registry settings
                case WellKnownRegistries.ScreenSaver :
                    return string.Format($@"{userHivePath}\{RegistryConstants.RegPaths.ScreenSaver}");

                case WellKnownRegistries.ScreenSaverDomainPolicy:
                    return string.Format($@"{userHivePath}\{RegistryConstants.RegPaths.ScreenSaverDomainPolicy}");

                case WellKnownRegistries.StartupProcess:
                    return string.Format($@"{userHivePath}\{RegistryConstants.RegPaths.StartupProcess}");

                //machine specific registry settings         
                case WellKnownRegistries.AutoLogon :
                case WellKnownRegistries.AutoLogonUserName:
                case WellKnownRegistries.AutoLogonDomainName :
                case WellKnownRegistries.AutoLogonPassword:
                case WellKnownRegistries.AutoLogonCount:
                    return string.Format($@"{RegistryConstants.LocalMachineRootPath}\{RegistryConstants.RegPaths.AutoLogon}");

                case WellKnownRegistries.ShutdownReason :
                case WellKnownRegistries.ShutdownReasonUI :
                    return string.Format($@"{RegistryConstants.LocalMachineRootPath}\{RegistryConstants.RegPaths.ShutdownReasonDomainPolicy}");

                case WellKnownRegistries.LegalNoticeCaption :
                case WellKnownRegistries.LegalNoticeText :
                    return string.Format($@"{RegistryConstants.LocalMachineRootPath}\{RegistryConstants.RegPaths.LegalNotice}");
                default:
                   return null;
            }
        }

        private void RevertBackOriginalRegistryInternal(string regPath, WellKnownRegistries targetRegistry)
        {
            var backupKeyName = GetBackupKeyName(targetRegistry);
            var originalValue = _registryManager.GetKeyValue(regPath, backupKeyName);
            
            if(string.IsNullOrEmpty(originalValue))
            {
                _registryManager.DeleteKey(regPath, RegistryConstants.GetActualKeyNameForWellKnownRegistry(targetRegistry));
                return;
            }

            //revert back the original value
            _registryManager.SetKeyValue(regPath, RegistryConstants.GetActualKeyNameForWellKnownRegistry(targetRegistry), originalValue);
            //delete the backup key
            _registryManager.DeleteKey(regPath, backupKeyName);
        }

        private string GetBackupKeyName(WellKnownRegistries registry)
        {
            return RegistryConstants.BackupKeyPrefix + RegistryConstants.GetActualKeyNameForWellKnownRegistry(registry);
        }
        
        private void SetRegistryKeyInternal(WellKnownRegistries targetRegistry, string keyName, string keyValue)
        {
            var regPath = GetRegistryKeyPath(targetRegistry, _userSecurityId);
            
            if(string.IsNullOrEmpty(regPath))
            {
                //trace
                return;
            }
            _registryManager.SetKeyValue(regPath, keyName, keyValue);
        }

        private string GetUserRegistryRootPath(string sid)
        {
            return string.IsNullOrEmpty(sid) ?
                RegistryConstants.CurrentUserRootPath :
                String.Format(RegistryConstants.DifferentUserRootPath, sid);
        }

        private bool ShouldTakeBackup(WellKnownRegistries registry)
        {
            return _regSettingsForBackup!= null 
                    && _regSettingsForBackup.Exists(item => item.Equals(registry));
        }
    }
    
    public enum WellKnownRegistries
    {
        ScreenSaver,
        ScreenSaverDomainPolicy,
        AutoLogon,
        AutoLogonUserName,
        AutoLogonDomainName,
        AutoLogonCount,
        AutoLogonPassword,
        StartupProcess,
        ShutdownReason,
        ShutdownReasonUI,
        LegalNoticeCaption,
        LegalNoticeText
    }

    public class RegistryConstants
    {
        public const string CurrentUserRootPath = @"HKEY_CURRENT_USER";
        public const string LocalMachineRootPath = @"HKEY_LOCAL_MACHINE";
        public const string DifferentUserRootPath = @"HKEY_USERS\{0}";
        public const string BackupKeyPrefix = "VSTSAgentBackup_";

        public struct RegPaths
        {
            public const string ScreenSaver = @"Control Panel\Desktop";
            public const string ScreenSaverDomainPolicy = @"Software\Policies\Microsoft\Windows\Control Panel\Desktop";
            public const string StartupProcess = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            public const string AutoLogon = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            public const string ShutdownReasonDomainPolicy = @"SOFTWARE\Policies\Microsoft\Windows NT\Reliability";
            public const string LegalNotice = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        }

        public struct KeyNames
        {
            public const string ScreenSaver = "ScreenSaveActive";
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

        public static string GetActualKeyNameForWellKnownRegistry(WellKnownRegistries registry)
        {
            switch(registry)
            {
                case WellKnownRegistries.ScreenSaverDomainPolicy:
                case WellKnownRegistries.ScreenSaver:
                    return KeyNames.ScreenSaver;
                case WellKnownRegistries.AutoLogon:
                    return KeyNames.AutoLogon;
                case WellKnownRegistries.AutoLogonUserName :
                    return KeyNames.AutoLogonUserName;
                case WellKnownRegistries.AutoLogonDomainName:
                    return KeyNames.AutoLogonDomainName;
                case WellKnownRegistries.AutoLogonCount:
                    return KeyNames.AutoLogonCount;
                case WellKnownRegistries.AutoLogonPassword:
                    return KeyNames.AutoLogonPassword;
                case WellKnownRegistries.StartupProcess:
                    return KeyNames.StartupProcess;
                case WellKnownRegistries.ShutdownReason:
                    return KeyNames.ShutdownReason;
                case WellKnownRegistries.ShutdownReasonUI:
                    return KeyNames.ShutdownReasonUI;
                case WellKnownRegistries.LegalNoticeCaption:
                    return KeyNames.LegalNoticeCaption;
                case WellKnownRegistries.LegalNoticeText:
                    return KeyNames.LegalNoticeText;
                default:
                    return null;
            }                      
        }
    }
}
#endif