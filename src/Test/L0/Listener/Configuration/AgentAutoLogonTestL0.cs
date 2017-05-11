#if OS_WINDOWS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.VisualStudio.Services.Agent.Listener.Configuration;
using Microsoft.VisualStudio.Services.Agent.Listener;

namespace Microsoft.VisualStudio.Services.Agent.Tests.Listener
{
    public sealed class AgentAutoLogonTestL0
    {
        private Mock<INativeWindowsServiceHelper> _windowsServiceHelper;
        private Mock<IPromptManager> _promptManager;
        private Mock<IProcessInvoker> _processInvoker;
        private CommandSettings _command;
        private string _sid = "007";
        private string _userName = "ironMan";
        private string _domainName = "avengers";
        private MockRegistryManager _mockRegManager;
        private bool _powerCfgCalledForACOption = false;
        private bool _powerCfgCalledForDCOption = false;    

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Agent")]
        public async void TestAutoLogonConfiguration()
        {
            Debugger.Launch();
            using (var hc = new TestHostContext(this))
            {
                SetupTestEnv(hc);

                var iConfigManager = new InteractiveSessionConfigurationManager();
                iConfigManager.Initialize(hc);
                iConfigManager.Configure(_command);

                VerifyTheRegistryChanges(_userName, _domainName);
                Assert.True(_powerCfgCalledForACOption);
                Assert.True(_powerCfgCalledForDCOption);
                Assert.Equal(false, iConfigManager.RestartNeeded());
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Agent")]
        public async void TestAutoLogonConfigurationForDifferentUser()
        {
            using (var hc = new TestHostContext(this))
            {
                SetupTestEnv(hc);

                //override behavior
                _windowsServiceHelper.Setup(x => x.IsTheSameUserLoggedIn(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

                var iConfigManager = new InteractiveSessionConfigurationManager();
                iConfigManager.Initialize(hc);
                iConfigManager.Configure(_command);
                
                VerifyTheRegistryChanges(_userName, _domainName, _sid);
                Assert.True(_powerCfgCalledForACOption);
                Assert.True(_powerCfgCalledForDCOption);
                Assert.Equal(true, iConfigManager.RestartNeeded());
            }
        }       

        private async void SetupTestEnv(TestHostContext hc)
        {
            _powerCfgCalledForACOption = _powerCfgCalledForDCOption = false;

            _windowsServiceHelper = new Mock<INativeWindowsServiceHelper>();
            hc.SetSingleton<INativeWindowsServiceHelper>(_windowsServiceHelper.Object);

            _promptManager = new Mock<IPromptManager>();
            hc.SetSingleton<IPromptManager>(_promptManager.Object);

            _promptManager
                .Setup(x => x.ReadValue(
                    Constants.Agent.CommandLine.Args.WindowsLogonAccount, // argName
                    It.IsAny<string>(), // description
                    It.IsAny<bool>(), // secret
                    It.IsAny<string>(), // defaultValue
                    Validators.NTAccountValidator, // validator
                    It.IsAny<bool>())) // unattended
                .Returns(string.Format(@"{0}\{1}", _domainName, _userName));

            _windowsServiceHelper.Setup(x => x.IsValidCredential(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            _windowsServiceHelper.Setup(x => x.SetAutoLogonPassword(It.IsAny<string>()));
            _windowsServiceHelper.Setup(x => x.IsTheSameUserLoggedIn(It.IsAny<string>(), It.IsAny<string>())).Returns(true);             
            _windowsServiceHelper.Setup(x => x.GetSecurityIdForTheUser(It.IsAny<string>())).Returns(_sid);
            

            _processInvoker = new Mock<IProcessInvoker>();
            hc.EnqueueInstance<IProcessInvoker>(_processInvoker.Object);
            _processInvoker.Setup(x => x.ExecuteAsync(
                                                It.IsAny<String>(), 
                                                "powercfg.exe", 
                                                "/Change monitor-timeout-ac 0", 
                                                null,
                                                It.IsAny<CancellationToken>())).Returns(Task.FromResult<int>(SetPowerCfgFlags(true)));

            _processInvoker.Setup(x => x.ExecuteAsync(
                                                It.IsAny<String>(), 
                                                "powercfg.exe", 
                                                "/Change monitor-timeout-dc 0", 
                                                null,
                                                It.IsAny<CancellationToken>())).Returns(Task.FromResult<int>(SetPowerCfgFlags(false)));

            _mockRegManager = new MockRegistryManager();
            hc.SetSingleton<IWindowsRegistryManager>(_mockRegManager);

            _command = new CommandSettings(
                hc,
                new[]
                {
                    "--windowslogonaccount", "wont be honored",
                    "--windowslogonpassword", "sssh"
                });
        }

        private int SetPowerCfgFlags(bool isForACOption)
        {
            if(isForACOption)
            {
                _powerCfgCalledForACOption = true;
            }
            else
            {
                _powerCfgCalledForDCOption = true;
            }
            return 0;
        }

        public async void VerifyTheRegistryChanges(string expectedUserName, string expectedDomainName, string userSid = null)
        {
            ValidateRegistryValue(WellKnownRegistries.ScreenSaver, "0", userSid);
            ValidateRegistryValue(WellKnownRegistries.ScreenSaverDomainPolicy, "0", userSid);
            
            //autologon
            ValidateRegistryValue(WellKnownRegistries.AutoLogon, "1", userSid);
            ValidateRegistryValue(WellKnownRegistries.AutoLogonUserName, expectedUserName, userSid);
            ValidateRegistryValue(WellKnownRegistries.AutoLogonDomainName, expectedDomainName, userSid);
            ValidateRegistryValue(WellKnownRegistries.AutoLogonCount, null, userSid);
            ValidateRegistryValue(WellKnownRegistries.AutoLogonPassword, null, userSid);

            //shutdown reason
            ValidateRegistryValue(WellKnownRegistries.ShutdownReason, "0", userSid);
            ValidateRegistryValue(WellKnownRegistries.ShutdownReasonUI, "0", userSid);

            //todo: startup process validation
            // ValidateRegistryValue(WellKnownRegistries.StartupProcess, , usersid);

            // regPath = GetRegistryKeyPath(WellKnownRegistries.StartupProcess, userSid);
            // var processPath = regManager.GetKeyValue(regPath, WellKnownRegistries.StartupProcess);
        }

        public async void ValidateRegistryValue(WellKnownRegistries registry, string expectedValue, string userSid)
        {
            var regPath = GetRegistryKeyPath(registry, userSid);
            var regKey = RegistryConstants.GetActualKeyNameForWellKnownRegistry(registry);
            var actualValue = _mockRegManager.GetKeyValue(regPath, regKey);

            var validationPassed = string.Equals(expectedValue, actualValue, StringComparison.OrdinalIgnoreCase);
            Assert.True(validationPassed, $"{registry.ToString()} validation failed. Expected - {expectedValue} Actual - {actualValue}");
        }

        private string GetUserRegistryRootPath(string sid)
        {
            return string.IsNullOrEmpty(sid) ?
                RegistryConstants.CurrentUserRootPath :
                String.Format(RegistryConstants.DifferentUserRootPath, sid);
        }

        private string GetRegistryKeyPath(WellKnownRegistries targetRegistryKey, string userSid = null)
        {
            var userHivePath = GetUserRegistryRootPath(userSid);
            switch(targetRegistryKey)
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
    }

    public class MockRegistryManager : AgentService, IWindowsRegistryManager
    {
        private Dictionary<string, string> _regStore;

        public MockRegistryManager()
        {
            _regStore = new Dictionary<string, string>();
        }
        
        public void DeleteKey(string path, string keyName)
        {
            var key = string.Concat(path, keyName);
            _regStore.Remove(key);
        }

        public string GetKeyValue(string path, string keyName)
        {
            var key = string.Concat(path, keyName);
            return _regStore.ContainsKey(key) ? _regStore[key] : null;
        }

        public void SetKeyValue(string path, string keyName, string keyValue)
        {
            var key = string.Concat(path, keyName);
            _regStore.Add(key, keyValue);
        }

        public bool RegsitryExists(string securityId)
        {
            return true;
        }
    }
}
#endif