using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Agent.Plugins.TestResults;
using Agent.Sdk;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Moq;
using Xunit;

namespace Test.L0.Plugin
{
    public class AutoTestResultsPublisherPluginL0
    {
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        public async Task AutoTestResultsPublisherPlugin_PublishIfAnyFilesFoundMatchingPattern()
        {
            using (var stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);

                var agentContext = new MockAgentTaskPluginExecutionContext();
                var fakeVariables = new Dictionary<string, VariableValue>
                {
                    {"System.DefaultWorkingDirectory", new VariableValue("/wrk", false)},
                    {"Common.TestResultsDirectory", new VariableValue("/test", false)},
                    {"Common.TestResultsPattern", new VariableValue("*test*.xml", false)}
                };
                var resultFiles = new List<string>
                {
                    "/wrk/test1.xml",
                    "/wrk/test2.xml",
                    "/test/test1.xml",
                    "/test/test2.xml"
                };
                var publisher = new MockAutoTestResultsPublisher
                {
                    MockFiles = resultFiles
                };
                agentContext.Variables = fakeVariables;

                await publisher.RunAsync(agentContext, CancellationToken.None);

                Assert.True(stringWriter.ToString().Contains("Found test result files: 4"));
                Assert.True(stringWriter.ToString().Contains($"##vso[results.publish type=JUnit;publishRunAttachments=true;testRunSystem=AutoPublishTask;mergeResults=true;resultFiles={string.Join(",", resultFiles)}]"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        public async Task AutoTestResultsPublisherPlugin_DontPublishIfPatternIsEmpty()
        {
            using (var stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);

                var agentContext = new MockAgentTaskPluginExecutionContext();
                var fakeVariables = new Dictionary<string, VariableValue>
                {
                    {"System.DefaultWorkingDirectory", new VariableValue("/wrk", false)},
                    {"Common.TestResultsDirectory", new VariableValue("/test", false)},
                };
                var resultFiles = new List<string>
                {
                    "/wrk/test1.xml",
                    "/wrk/test2.xml",
                    "/test/test1.xml",
                    "/test/test2.xml"
                };
                var publisher = new MockAutoTestResultsPublisher
                {
                    MockFiles = resultFiles
                };
                agentContext.Variables = fakeVariables;

                await publisher.RunAsync(agentContext, CancellationToken.None);

                Assert.True(stringWriter.ToString().Contains("Pattern information is not available"));
                Assert.False(stringWriter.ToString().Contains($"##vso[results.publish"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        public async Task AutoTestResultsPublisherPlugin_SearchDefaultWorkingIfNotEmpty()
        {
            using (var stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);

                var agentContext = new MockAgentTaskPluginExecutionContext();
                var fakeVariables = new Dictionary<string, VariableValue>
                {
                    {"System.DefaultWorkingDirectory", new VariableValue("/wrk", false)},
                    {"Common.TestResultsPattern", new VariableValue("*test*.xml", false)}
                };
                var resultFiles = new List<string>
                {
                    "/wrk/test1.xml",
                    "/wrk/test2.xml"
                };
                var publisher = new MockAutoTestResultsPublisher
                {
                    MockFiles = resultFiles
                };
                agentContext.Variables = fakeVariables;

                await publisher.RunAsync(agentContext, CancellationToken.None);

                Assert.True(stringWriter.ToString().Contains("Found test result files: 2"));
                Assert.True(stringWriter.ToString().Contains($"##vso[results.publish"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        public async Task AutoTestResultsPublisherPlugin_SearchTestResultsDirIfNotEmpty()
        {
            using (var stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);

                var agentContext = new MockAgentTaskPluginExecutionContext();
                var fakeVariables = new Dictionary<string, VariableValue>
                {
                    {"Common.TestResultsDirectory", new VariableValue("/test", false)},
                    {"Common.TestResultsPattern", new VariableValue("*test*.xml", false)}
                };
                var resultFiles = new List<string>
                {
                    "/test/test1.xml",
                    "/test/test2.xml"
                };
                var publisher = new MockAutoTestResultsPublisher
                {
                    MockFiles = resultFiles
                };
                agentContext.Variables = fakeVariables;

                await publisher.RunAsync(agentContext, CancellationToken.None);

                Assert.True(stringWriter.ToString().Contains("Found test result files: 2"));
                Assert.True(stringWriter.ToString().Contains($"##vso[results.publish"));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        public async Task AutoTestResultsPublisherPlugin_DontPublishIfNoMatchingFilesFound()
        {
            using (var stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);

                var agentContext = new MockAgentTaskPluginExecutionContext();
                var fakeVariables = new Dictionary<string, VariableValue>
                {
                    {"Common.TestResultsDirectory", new VariableValue("/test", false)},
                    {"Common.TestResultsPattern", new VariableValue("*test*.xml", false)}
                };
                var resultFiles = new List<string>();

                var publisher = new MockAutoTestResultsPublisher
                {
                    MockFiles = resultFiles
                };
                agentContext.Variables = fakeVariables;

                await publisher.RunAsync(agentContext, CancellationToken.None);

                Assert.True(stringWriter.ToString().Contains("Found test result files: 0"));
                Assert.False(stringWriter.ToString().Contains($"##vso[results.publish"));
            }
        }
    }

    public class MockAutoTestResultsPublisher : AutoTestResultsPublisherPlugin
    {
        public List<string> MockFiles { get; set; }

        protected override IEnumerable<string> GetFiles(string path, string[] searchPatterns, SearchOption searchOption = SearchOption.AllDirectories)
        {
            return MockFiles;
        }
    }

    public class MockAgentTaskPluginExecutionContext : AgentTaskPluginExecutionContext
    {

    }
}
