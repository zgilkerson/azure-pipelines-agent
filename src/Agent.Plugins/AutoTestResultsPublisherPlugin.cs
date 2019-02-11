using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Sdk;

namespace Agent.Plugins.TestResults
{
    public class AutoTestResultsPublisherPlugin : IAgentTaskPlugin
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
            await Task.Run(() =>
            {
                executionContext.Output($"Searching for test result files to publish");

                executionContext.Variables.TryGetValue("System.DefaultWorkingDirectory", out var defaultWorkingDir);
                executionContext.Variables.TryGetValue("Common.TestResultsDirectory", out var commonTestResultsDir);
                executionContext.Variables.TryGetValue("Common.TestResultsPattern", out var commonTestResultsPattern);

                if (string.IsNullOrWhiteSpace(commonTestResultsPattern?.Value))
                {
                    executionContext.Debug($"Pattern information is not available");
                    return;
                }

                executionContext.Debug($"Looking for test results in following folders: {defaultWorkingDir?.Value} {commonTestResultsDir?.Value}");
                executionContext.Debug($"Test results pattern lookup: {commonTestResultsPattern.Value}");

                var pattern = commonTestResultsPattern.Value.Split(",");

                var testResultFiles = Enumerable.Empty<string>();
                if (!string.IsNullOrWhiteSpace(defaultWorkingDir?.Value))
                {
                    testResultFiles = testResultFiles.Union(GetFiles(defaultWorkingDir.Value, pattern));
                }
                if (!string.IsNullOrWhiteSpace(commonTestResultsDir?.Value))
                {
                    testResultFiles = testResultFiles.Union(GetFiles(commonTestResultsDir.Value, pattern));
                }

                var resultFiles = testResultFiles.ToList();
                executionContext.Debug($"Found test result files: {resultFiles.Count}");

                if (resultFiles.Any())
                {
                    executionContext.Output($"##vso[results.publish type=JUnit;publishRunAttachments=true;testRunSystem=AutoPublishTask;mergeResults=true;resultFiles={string.Join(",", resultFiles)}]");
                }
            }, token);
        }

        // Takes same patterns, and executes in parallel
        protected virtual IEnumerable<string> GetFiles(string path, string[] searchPatterns, SearchOption searchOption = SearchOption.AllDirectories)
        {
            return searchPatterns.AsParallel()
                   .SelectMany(searchPattern =>
                          Directory.EnumerateFiles(path, searchPattern, searchOption));
        }
    }
}
