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
    }

    public class InteractiveSessionConfigurationManager : AgentService, IInteractiveSessionConfigurationManager
    {
        private ITerminal _terminal;

        public override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);
            _terminal = hostContext.GetService<ITerminal>();    
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
                    Trace.Info("Credential validation succeeded");
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
                                                ? new WindowsRegistryHelper(regManager) 
                                                : new WindowsRegistryHelper(regManager, securityIdForTheUser);
            
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
            var regHelper = new WindowsRegistryHelper(HostContext.GetService<IWindowsRegistryManager>());
            var userName = regHelper.GetRegistry(WellKnownRegistries.AutoLogonUserName);
            var domainName = regHelper.GetRegistry(WellKnownRegistries.AutoLogonDomainName);

            if(string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(domainName))
            {
                return false;
            }
            var nativeWindowsHelper = HostContext.GetService<INativeWindowsServiceHelper>();
            return !nativeWindowsHelper.IsTheSameUserLoggedIn(domainName, userName);
        }

        public void UnConfigure()
        {
            throw new NotImplementedException();
        }

        private void DisplayUITestingRelatedWarningsIfAny(WindowsRegistryHelper regHelper)
        {
            var warningReasons = new List<string>();

            //screen saver
            var screenSaverValue = regHelper.GetRegistry(WellKnownRegistries.ScreenSaverDomainPolicy);
            int.TryParse(screenSaverValue, out int isScreenSaverDomainPolicySet);
            if(isScreenSaverDomainPolicySet == 1)
            {
                warningReasons.Add(StringUtil.Loc("UITestingWarning_ScreenSaver"));
            }

            //shutdown reason
            var shutdownReasonValue = regHelper.GetRegistry(WellKnownRegistries.ShutdownReason);
            int.TryParse(shutdownReasonValue, out int shutdownReasonOn);
            if(shutdownReasonOn == 1)
            {
                warningReasons.Add(StringUtil.Loc("UITestingWarning_ShutdownReason"));
            }

            var legalNoticeCaption = regHelper.GetRegistry(WellKnownRegistries.LegalNoticeCaption);
            var legalNoticeText =  regHelper.GetRegistry(WellKnownRegistries.LegalNoticeText);
            if(!string.IsNullOrEmpty(legalNoticeCaption) || !string.IsNullOrEmpty(legalNoticeText))
            {
                warningReasons.Add(StringUtil.Loc("UITestingWarning_LegalNotice"));
            }

            //auto-logon
            var autoLogonCountValue = regHelper.GetRegistry(WellKnownRegistries.AutoLogonCount);            
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
            regHelper.SetRegistry(WellKnownRegistries.ScreenSaver, "0");
            regHelper.SetRegistry(WellKnownRegistries.ScreenSaverDomainPolicy, "0");

            ShowAutoLogonWarningIfAlreadyEnabled(regHelper, userName);

            var windowsHelper = HostContext.GetService<INativeWindowsServiceHelper>();
            windowsHelper.SetAutoLogonPassword(logonPassword);

            regHelper.SetRegistry(WellKnownRegistries.AutoLogonUserName, userName);
            regHelper.SetRegistry(WellKnownRegistries.AutoLogonDomainName, domainName);
            regHelper.SetRegistry(WellKnownRegistries.AutoLogon, "1");

            var startupProcessPath = Path.Combine(HostContext.GetDirectory(WellKnownDirectory.Bin), "agent.service.exe");
            var startupCommand = string.Format("{0} {1}", startupProcessPath, "runAsProcess");
            regHelper.SetRegistry(WellKnownRegistries.StartupProcess, startupCommand);

            regHelper.SetRegistry(WellKnownRegistries.ShutdownReason, "0");
            regHelper.SetRegistry(WellKnownRegistries.ShutdownReasonUI, "0");

            regHelper.SetRegistry(WellKnownRegistries.LegalNoticeCaption, "");
            regHelper.SetRegistry(WellKnownRegistries.LegalNoticeText, "");
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
            var regValue = regHelper.GetRegistry(WellKnownRegistries.AutoLogon);
            int.TryParse(regValue, out int autoLogonEnabled);
            if(autoLogonEnabled == 1)
            {
                var autoLogonUser = regHelper.GetRegistry(WellKnownRegistries.AutoLogonUserName);
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