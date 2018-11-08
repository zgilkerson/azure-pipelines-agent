using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Agent.Plugins.TestResults
{
    public class TestResultsPublisherPlugin : IAgentTaskPlugin
    {
        public Guid Id => new Guid("8d637240-a8b0-42c6-9063-ecf00106c98f");
        public string Version => "1.0.0";

        public string Stage => "post";

        public async Task RunAsync(AgentTaskPluginExecutionContext executionContext, CancellationToken token)
        {
            await PublishTestResultsAsync(executionContext, token);
        }

        protected async Task PublishTestResultsAsync(AgentTaskPluginExecutionContext executionContext, CancellationToken token)
        {
            executionContext.Output($"Searching for test results to publish");

            Task.Delay(20000).Wait();

            executionContext.Variables.TryGetValue("System.DefaultWorkingDirectory", out var defaultWorkingDir);
            executionContext.Variables.TryGetValue("Common.TestResultsDirectory", out var commonTestResultsDir);
            executionContext.Variables.TryGetValue("Common.TestResultsPattern", out var commonTestResultsPattern);

            executionContext.Debug($"Looking for test results in following folders: {defaultWorkingDir?.Value} {commonTestResultsDir?.Value}");

            var pattern = string.IsNullOrWhiteSpace(commonTestResultsPattern?.Value) ? "*Junit*.xml" : commonTestResultsPattern.Value;
            executionContext.Debug($"Test results pattern lookup: {pattern}");

            var testResultFiles = Enumerable.Empty<string>();
            if (!string.IsNullOrWhiteSpace(defaultWorkingDir?.Value))
            {
                testResultFiles = testResultFiles.Union(Directory.EnumerateFiles(defaultWorkingDir.Value, pattern, SearchOption.AllDirectories));
            }
            if (!string.IsNullOrWhiteSpace(commonTestResultsDir?.Value))
            {
                testResultFiles = testResultFiles.Union(Directory.EnumerateFiles(commonTestResultsDir.Value, pattern, SearchOption.AllDirectories));
            }

            executionContext.Debug($"Number of test results found: {testResultFiles.Count()}");
            if (testResultFiles.Any())
            {
                executionContext.Output($"##vso[results.publish type=JUnit;publishRunAttachments=true;testRunSystem=VSTSTask;mergeResults=true;resultFiles={string.Join(",", testResultFiles)}]");
            }
        }
    }
}
