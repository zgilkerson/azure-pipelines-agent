using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Listener;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Castle.Core.Internal;
using Microsoft.VisualStudio.Services.Agent.Listener.Capabilities;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests.Listener.Configuration
{
    using Microsoft.VisualStudio.Services.Agent.Listener.Configuration;
    using WebApi;

    public class ConfigurationProviderTestsL0
    {
        private Mock<IAgentServer> _agentServer;
        private Mock<IPromptManager> _promptManager;
        private string _serverUrl = "https://localhost";
        private string _collectionName = "testCollectionName";
        private int _expectedPoolId = 7;
        private string _projectName = "testProjectName";

        public ConfigurationProviderTestsL0()
        {
            _agentServer = new Mock<IAgentServer>();
            _promptManager = new Mock<IPromptManager>();

            _agentServer.Setup(x => x.ConnectAsync(It.IsAny<VssConnection>())).Returns(Task.FromResult<object>(null));
        }

        private TestHostContext CreateTestContext([CallerMemberName] String testName = "")
        {
            TestHostContext tc = new TestHostContext(this, testName);
            tc.SetSingleton<IAgentServer>(_agentServer.Object);
            tc.EnqueueInstance<IAgentServer>(_agentServer.Object);
            tc.SetSingleton<IPromptManager>(_promptManager.Object);

            return tc;
        }

        /*
         * This test case ensures the flow for Deployment Agent Configuration for on-prem tfs, where collection name is required
        */
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "ConfigurationManagement")]
        public void EnsureDeploymentConfigProviderWorksFineForOnPrem()
        {
            using (TestHostContext tc = CreateTestContext())
            {
                Tracing trace = tc.GetTrace();

                trace.Info("Creating Deployment Config Provide");
                IConfigurationProvider deploymenProvider = new DeploymentAgentConfiguration();
                deploymenProvider.Initialize(tc);


                trace.Info("Preparing command line arguments");
                var command = new CommandSettings(
                    tc,
                    new[]
                    {
                       "configure",
                       "--url", _serverUrl,
                       "--projectname", _projectName,
                       "--collectionname", _collectionName
                    });

                var expectedMachineGroups = new List<DeploymentMachineGroup>() { new DeploymentMachineGroup() { Id = 2, Pool = new TaskAgentPoolReference(new Guid(), _expectedPoolId) } };
                _agentServer.Setup(x => x.GetDeploymentMachineGroupsAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(expectedMachineGroups));

                trace.Info("Init the deployment provider");
                deploymenProvider.InitConnection(_agentServer.Object);
                deploymenProvider.InitConnectionWithCollection(command, _serverUrl, new TestAgentCredential().GetVssCredentials(tc));

                int poolId = deploymenProvider.GetPoolId(command).Result;

                trace.Info("Verifying poolId returned by deployment provider");
                Assert.True(poolId.Equals(_expectedPoolId));
            }
        }
    }
}