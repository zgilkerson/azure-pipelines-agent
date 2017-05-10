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
            using (var hc = new TestHostContext(this))
            {
                SetupTestEnv(hc);

                var iConfigManager = new InteractiveSessionConfigurationManager();
                iConfigManager.Initialize(hc);
                iConfigManager.Configure(_command);

                VerifyTheRegistryChanges(_mockRegManager, _userName, _domainName);
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
                
                VerifyTheRegistryChanges(_mockRegManager, _userName, _domainName, _sid);
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

        public async void VerifyTheRegistryChanges(IWindowsRegistryManager regManager, string expectedUserName, string expectedDomainName, string userSid = null)
        {
            var regPath = string.Format(RegistryConstants.RegPaths.ScreenSaver, GetUserRegistryRootPath(userSid));
            Assert.Equal("0", regManager.GetKeyValue(regPath, RegistryConstants.KeyNames.ScreenSaver));

            regPath = string.Format(RegistryConstants.RegPaths.ScreenSaverDomainPolicy, GetUserRegistryRootPath(userSid));
            Assert.Equal("0", regManager.GetKeyValue(regPath, RegistryConstants.KeyNames.ScreenSaver));

            regPath = string.Format(RegistryConstants.RegPaths.StartupProcess, GetUserRegistryRootPath(userSid));
            var processPath = regManager.GetKeyValue(regPath, RegistryConstants.KeyNames.StartupProcess);
            //todo: add validation

            Assert.Equal("1", regManager.GetKeyValue(RegistryConstants.RegPaths.AutoLogon, RegistryConstants.KeyNames.AutoLogon));
            Assert.Equal(expectedUserName, regManager.GetKeyValue(RegistryConstants.RegPaths.AutoLogon, RegistryConstants.KeyNames.AutoLogonUser));
            Assert.Equal(expectedDomainName, regManager.GetKeyValue(RegistryConstants.RegPaths.AutoLogon, RegistryConstants.KeyNames.AutoLogonDomain));

            Assert.Equal("0", regManager.GetKeyValue(RegistryConstants.RegPaths.ShutdownReasonDomainPolicy, RegistryConstants.KeyNames.ShutdownReason));
            Assert.Equal("0", regManager.GetKeyValue(RegistryConstants.RegPaths.ShutdownReasonDomainPolicy, RegistryConstants.KeyNames.ShutdownReasonUI));
        }

        private string GetUserRegistryRootPath(string sid)
        {
            return string.IsNullOrEmpty(sid) ?
                RegistryConstants.CurrentUserRootPath :
                String.Format(RegistryConstants.DifferentUserRootPath, sid);
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