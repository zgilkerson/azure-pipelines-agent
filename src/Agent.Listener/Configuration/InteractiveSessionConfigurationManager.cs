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
            
            InteractiveSessionRegHelper regHelper = isCurrentUserSameAsAutoLogonUser 
                                                ? new InteractiveSessionRegHelper(regManager)
                                                : new InteractiveSessionRegHelper(regManager, securityIdForTheUser);
            
            if(!isCurrentUserSameAsAutoLogonUser && !regHelper.ValidateIfRegistryExistsForTheUser(securityIdForTheUser))
            {
                Trace.Error(String.Format($"The autologon user '{logonAccount}' doesnt have a user profile on the machine. Please login once with the expected autologon user and reconfigure the agent again"));
                throw new InvalidOperationException("No user profile exists for the AutoLogon user");
            }

            DisplayWarningsIfAny(regHelper);        
            UpdateRegistriesForInteractiveSession(regHelper, userName, domainName, logonPassword);
            ConfigurePowerOptions();
        }

        public bool RestartNeeded()
        {
            return !IsCurrentUserSameAsAutoLogonUser();
        }

        public void UnConfigure()
        {
            InteractiveSessionRegHelper regHelper = new InteractiveSessionRegHelper(HostContext.GetService<IWindowsRegistryManager>());
            regHelper.RevertBackOriginalRegistrySettings();
        }

        public bool IsInteractiveSessionConfigured()
        {
            //find out the path for startup process if it is same as current agent location, yes it was configured
            var regHelper = new InteractiveSessionRegHelper(HostContext.GetService<IWindowsRegistryManager>(), null);
            var startupCommand = regHelper.GetStartupProcessCommand();

            if(string.IsNullOrEmpty(startupCommand))
            {
                return false;
            }

            var expectedStartupProcessPath = Path.Combine(HostContext.GetDirectory(WellKnownDirectory.Bin), "agent.service.exe");
            return startupCommand.StartsWith(expectedStartupProcessPath, StringComparison.CurrentCultureIgnoreCase);
        }

        private void UpdateRegistriesForInteractiveSession(InteractiveSessionRegHelper regHelper, string userName, string domainName, string logonPassword)
        {
            regHelper.UpdateStandardRegistrySettings();

            //auto logon
            ConfigureAutoLogon(regHelper, userName, domainName, logonPassword);

            //startup process
            var startupProcessPath = Path.Combine(HostContext.GetDirectory(WellKnownDirectory.Bin), "agent.service.exe");
            var startupCommand = string.Format($@"{startupProcessPath} runAsProcess");
            Trace.Verbose($"Setting startup command as {startupCommand}");

            regHelper.SetStartupProcessCommand(startupCommand);
        }

        private void ConfigureAutoLogon(InteractiveSessionRegHelper regHelper, string userName, string domainName, string logonPassword)
        {
            //find out if the autologon was already enabled, show warning in that case
            ShowAutoLogonWarningIfAlreadyEnabled(regHelper, userName);

            var windowsHelper = HostContext.GetService<INativeWindowsServiceHelper>();
            windowsHelper.SetAutoLogonPassword(logonPassword);

            regHelper.UpdateAutoLogonSettings(userName, domainName);
        }

        private void ShowAutoLogonWarningIfAlreadyEnabled(InteractiveSessionRegHelper regHelper, string userName)
        {
            regHelper.FetchAutoLogonUserDetails(out string autoLogonUserName, out string domainName);
            if(autoLogonUserName != null && !userName.Equals(autoLogonUserName, StringComparison.CurrentCultureIgnoreCase))
            {
                _terminal.WriteLine(string.Format(StringUtil.Loc("AutoLogonAlreadyEnabledWarning"), userName));
            }
        }

        private bool IsCurrentUserSameAsAutoLogonUser()
        {
            var regHelper = new InteractiveSessionRegHelper(HostContext.GetService<IWindowsRegistryManager>());            
            regHelper.FetchAutoLogonUserDetails(out string userName, out string domainName);
            if(string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(domainName))
            {
                return false;
            }

            var nativeWindowsHelper = HostContext.GetService<INativeWindowsServiceHelper>();
            return nativeWindowsHelper.IsTheSameUserLoggedIn(domainName, userName);
        }

        private void DisplayWarningsIfAny(InteractiveSessionRegHelper regHelper)
        {
            var warningReasons = regHelper.GetInteractiveSessionRelatedWarningsIfAny();
            if(warningReasons.Count > 0)
            {
                _terminal.WriteLine(StringUtil.Loc("UITestingWarning"));
                for(int i=0; i < warningReasons.Count; i++)
                {
                    _terminal.WriteLine(String.Format("{0} - {1}", (i+1).ToString(), warningReasons[i]));
                }
            }
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