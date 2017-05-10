#if OS_WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.TeamFoundation.DistributedTask.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Configuration
{
    [ServiceLocator(Default = typeof(InteractiveSessionConfigurationManager))]
    public interface IInteractiveSessionConfigurationManager : IAgentService
    {
        void Configure(CommandSettings command);
        void UnConfigure();
        bool RestartNeeded();
        bool IsInteractiveSessionConfigured();
    }

    public class InteractiveSessionConfigurationManager : AgentService, IInteractiveSessionConfigurationManager
    {
        private ITerminal _terminal;
        private List<string> _regSettingsForBackup;

        public override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);
            _terminal = hostContext.GetService<ITerminal>();
            _regSettingsForBackup = new List<string>
            {
                WellKnownRegistries.AutoLogon,
                WellKnownRegistries.AutoLogonUserName,
                WellKnownRegistries.AutoLogonDomainName,
                WellKnownRegistries.AutoLogonPassword,
                WellKnownRegistries.AutoLogonCount,
                WellKnownRegistries.ShutdownReason,
                WellKnownRegistries.ShutdownReasonUI,
                WellKnownRegistries.ScreenSaver,
                WellKnownRegistries.ScreenSaverDomainPolicy,
                WellKnownRegistries.LegalNoticeCaption,
                WellKnownRegistries.LegalNoticeText
            };
        }

        public void Configure(CommandSettings command)
        {
            var logonAccount = command.GetAutoLogonUserName();

            string domainName;
            string userName;

            GetAccountSegments(logonAccount, out domainName, out userName);

            if ((string.IsNullOrEmpty(domainName) || domainName.Equals(".", StringComparison.CurrentCultureIgnoreCase)) && !logonAccount.Contains("@"))
            {
                logonAccount = String.Format("{0}\\{1}", Environment.MachineName, userName);
            }

            Trace.Info("LogonAccount after transforming: {0}, user: {1}, domain: {2}", logonAccount, userName, domainName);

            string logonPassword = string.Empty;
            var windowsServiceHelper = HostContext.GetService<INativeWindowsServiceHelper>();
            while (true)
            {
                logonPassword = command.GetWindowsLogonPassword(logonAccount);
                if (windowsServiceHelper.IsValidCredential(domainName, userName, logonPassword))
                {
                    Trace.Info("Credential validation succeed");
                    break;
                }
                
                if (command.Unattended)
                {
                    throw new SecurityException(StringUtil.Loc("InvalidWindowsCredential"));
                }
                    
                Trace.Info("Invalid credential entered");
                _terminal.WriteLine(StringUtil.Loc("InvalidWindowsCredential"));
            }

            bool isCurrentUserSameAsAutoLogonUser = windowsServiceHelper.IsTheSameUserLoggedIn(domainName, userName);                
            var securityIdForTheUser = windowsServiceHelper.GetSecurityIdForTheUser(userName);
            var regManager = HostContext.GetService<IWindowsRegistryManager>();
            
            WindowsRegistryHelper regHelper = isCurrentUserSameAsAutoLogonUser 
                                                ? new WindowsRegistryHelper(regManager, _regSettingsForBackup) 
                                                : new WindowsRegistryHelper(regManager, _regSettingsForBackup, securityIdForTheUser);
            
            if(!isCurrentUserSameAsAutoLogonUser && !regHelper.ValidateIfRegistryExistsForTheUser(securityIdForTheUser))
            {
                Trace.Error(String.Format($"The autologon user '{logonAccount}' doesnt have a user profile on the machine. Please login once with the expected autologon user and reconfigure the agent again"));
                throw new InvalidOperationException("No user profile exists for the AutoLogon user");
            }

            DisplayUITestingRelatedWarningsIfAny(regHelper);
            UpdateRegistrySettingsforUITesting(regHelper, userName, domainName, logonPassword);
            ConfigurePowerOptions();
        }       

        public bool RestartNeeded()
        {
            return !IsCurrentUserSameAsAutoLogonUser();
        }

        public void UnConfigure()
        {
            WindowsRegistryHelper regHelper = null;
            if(IsCurrentUserSameAsAutoLogonUser())
            {
                regHelper = new WindowsRegistryHelper(HostContext.GetService<IWindowsRegistryManager>(), _regSettingsForBackup);
            }
            else
            {
                FetchAutoLogonUserDetails(out string userName, out string domainName);
                var windowsServiceHelper = HostContext.GetService<INativeWindowsServiceHelper>();
                var securityIdForTheUser = windowsServiceHelper.GetSecurityIdForTheUser(userName);
                Trace.Info(@"Interactive session was configured for the different user. UserName: ${userName}, DomainName: ${domainName}");
                regHelper = new WindowsRegistryHelper(HostContext.GetService<IWindowsRegistryManager>(), _regSettingsForBackup, securityIdForTheUser);
            }
            
            foreach(var registryKey in _regSettingsForBackup)
            {
                regHelper.RevertBackOriginalRegistry(registryKey);
            }
        }

        public bool IsInteractiveSessionConfigured()
        {
            //find out the path for startup process if it is same as current agent location, yes it was configured
            var regHelper = new WindowsRegistryHelper(HostContext.GetService<IWindowsRegistryManager>(), null);
            var startupProcessPath = regHelper.GetRegistryKeyValue(WellKnownRegistries.StartupProcess);

            if(string.IsNullOrEmpty(startupProcessPath))
            {
                return false;
            }

            var expectedStartupProcessPath = Path.Combine(HostContext.GetDirectory(WellKnownDirectory.Bin), "agent.service.exe");
            return startupProcessPath.Equals(expectedStartupProcessPath, StringComparison.CurrentCultureIgnoreCase);
        }

        private bool IsCurrentUserSameAsAutoLogonUser()
        {
            var regHelper = new WindowsRegistryHelper(HostContext.GetService<IWindowsRegistryManager>());
            var userName = regHelper.GetRegistryKeyValue(WellKnownRegistries.AutoLogonUserName);
            var domainName = regHelper.GetRegistryKeyValue(WellKnownRegistries.AutoLogonDomainName);

            if(string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(domainName))
            {
                return false;
            }

            var nativeWindowsHelper = HostContext.GetService<INativeWindowsServiceHelper>();
            return nativeWindowsHelper.IsTheSameUserLoggedIn(domainName, userName);
        }

        private void FetchAutoLogonUserDetails(out string userName, out string domainName)
        {
            var regHelper = new WindowsRegistryHelper(HostContext.GetService<IWindowsRegistryManager>());
            userName = regHelper.GetRegistryKeyValue(WellKnownRegistries.AutoLogonUserName);
            domainName = regHelper.GetRegistryKeyValue(WellKnownRegistries.AutoLogonDomainName);
        }

        private void DisplayUITestingRelatedWarningsIfAny(WindowsRegistryHelper regHelper)
        {
            var warningReasons = new List<string>();

            //screen saver
            var screenSaverValue = regHelper.GetRegistryKeyValue(WellKnownRegistries.ScreenSaverDomainPolicy);
            int.TryParse(screenSaverValue, out int isScreenSaverDomainPolicySet);
            if(isScreenSaverDomainPolicySet == 1)
            {
                warningReasons.Add(StringUtil.Loc("UITestingWarning_ScreenSaver"));
            }

            //shutdown reason
            var shutdownReasonValue = regHelper.GetRegistryKeyValue(WellKnownRegistries.ShutdownReason);
            int.TryParse(shutdownReasonValue, out int shutdownReasonOn);
            if(shutdownReasonOn == 1)
            {
                warningReasons.Add(StringUtil.Loc("UITestingWarning_ShutdownReason"));
            }

            var legalNoticeCaption = regHelper.GetRegistryKeyValue(WellKnownRegistries.LegalNoticeCaption);
            var legalNoticeText =  regHelper.GetRegistryKeyValue(WellKnownRegistries.LegalNoticeText);
            if(!string.IsNullOrEmpty(legalNoticeCaption) || !string.IsNullOrEmpty(legalNoticeText))
            {
                warningReasons.Add(StringUtil.Loc("UITestingWarning_LegalNotice"));
            }

            //auto-logon
            var autoLogonCountValue = regHelper.GetRegistryKeyValue(WellKnownRegistries.AutoLogonCount);            
            if(!string.IsNullOrEmpty(autoLogonCountValue))
            {
                warningReasons.Add(StringUtil.Loc("UITestingWarning_AutoLogonCount"));
            }

            if(warningReasons.Count > 0)
            {
                _terminal.WriteLine(StringUtil.Loc("UITestingWarning"));
                for(int i=0; i < warningReasons.Count; i++)
                {
                    _terminal.WriteLine(String.Format("{0} - {1}", (i+1).ToString(), warningReasons[i]));
                }
            }
        }

        private void UpdateRegistrySettingsforUITesting(WindowsRegistryHelper regHelper, string userName, string domainName, string logonPassword)
        {
            regHelper.SetRegistryKeyValue(WellKnownRegistries.ScreenSaver, "0");
            regHelper.SetRegistryKeyValue(WellKnownRegistries.ScreenSaverDomainPolicy, "0");

            ShowAutoLogonWarningIfAlreadyEnabled(regHelper, userName);

            var windowsHelper = HostContext.GetService<INativeWindowsServiceHelper>();
            windowsHelper.SetAutoLogonPassword(logonPassword);

            regHelper.SetRegistryKeyValue(WellKnownRegistries.AutoLogonUserName, userName);
            regHelper.SetRegistryKeyValue(WellKnownRegistries.AutoLogonDomainName, domainName);
            regHelper.SetRegistryKeyValue(WellKnownRegistries.AutoLogon, "1");

            //this call is to take the backup of the password key if already exists as we delete the key in the next step
            regHelper.SetRegistryKeyValue(WellKnownRegistries.AutoLogonPassword, "");
            regHelper.DeleteRegistry(WellKnownRegistries.AutoLogonPassword);

            //this call is to take the backup of the password key if already exists as we delete the key in the next step
            regHelper.SetRegistryKeyValue(WellKnownRegistries.AutoLogonCount, "");
            regHelper.DeleteRegistry(WellKnownRegistries.AutoLogonCount);

            var startupProcessPath = Path.Combine(HostContext.GetDirectory(WellKnownDirectory.Bin), "agent.service.exe");
            var startupCommand = string.Format($@"{startupProcessPath} runAsProcess");
            regHelper.SetRegistryKeyValue(WellKnownRegistries.StartupProcess, startupCommand);

            regHelper.SetRegistryKeyValue(WellKnownRegistries.ShutdownReason, "0");
            regHelper.SetRegistryKeyValue(WellKnownRegistries.ShutdownReasonUI, "0");

            regHelper.SetRegistryKeyValue(WellKnownRegistries.LegalNoticeCaption, "");
            regHelper.SetRegistryKeyValue(WellKnownRegistries.LegalNoticeText, "");
        }

        private void ConfigurePowerOptions()
        {
            var processInvoker = HostContext.CreateService<IProcessInvoker>();
            processInvoker.ExecuteAsync(
                            workingDirectory: string.Empty,
                            fileName: "powercfg.exe",
                            arguments: "/Change monitor-timeout-ac 0",
                            environment: null,
                            cancellationToken: CancellationToken.None).Wait();

            processInvoker.ExecuteAsync(
                            workingDirectory: string.Empty,
                            fileName: "powercfg.exe",
                            arguments: "/Change monitor-timeout-dc 0",
                            environment: null,
                            cancellationToken: CancellationToken.None).Wait();
        }
        
        private void ShowAutoLogonWarningIfAlreadyEnabled(WindowsRegistryHelper regHelper, string userName)
        {
            var regValue = regHelper.GetRegistryKeyValue(WellKnownRegistries.AutoLogon);
            int.TryParse(regValue, out int autoLogonEnabled);
            if(autoLogonEnabled == 1)
            {
                var autoLogonUser = regHelper.GetRegistryKeyValue(WellKnownRegistries.AutoLogonUserName);
                if(!userName.Equals(autoLogonUser, StringComparison.CurrentCultureIgnoreCase))
                {
                    _terminal.WriteLine(string.Format(StringUtil.Loc("AutoLogonAlreadyEnabledWarning"), userName));
                }
            }
        }        

        //todo: move it to a utility class so that at other places it can be re-used
        private void GetAccountSegments(string account, out string domain, out string user)
        {
            string[] segments = account.Split('\\');
            domain = string.Empty;
            user = account;
            if (segments.Length == 2)
            {
                domain = segments[0];
                user = segments[1];
            }
        }
    }
}
#endif