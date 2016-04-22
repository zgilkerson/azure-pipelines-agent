using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Worker.TestResults
{
    [ServiceLocator(Default = typeof(TestRunPublisher))]
    public interface ITestRunPublisher : IAgentService
    {
        void StartTestRun(TestRunData testRunData);
        void AddResults(TestCaseResultData[] testResults);
        TestRunData ReadResultsFromFile(string filePath);
        string EndTestRun(bool publishAttachmentsAsArchive = false);
        void InitializePublisher(IExecutionContext executionContext, VssConnection connection, string projectName, TestRunContext runContext, IResultReader resultReader);
        void InitializePublisher(IExecutionContext executionContext, ITestManagementHttpClient httpClient, string projectName, TestRunContext runContext, IResultReader resultReader);
        TestRunData ReadResultsFromFile(string filePath, string runName);
    }

    public class TestRunPublisher : AgentService, ITestRunPublisher
    {
        #region Private
        const int BATCH_SIZE = 1000;
        const int PUBLISH_TIMEOUT = 300;
        const int TCM_MAX_FILESIZE = 104857600;
        private IExecutionContext _executionContext;
        private string _projectName;
        private ITestManagementHttpClient _httpClient;
        private TestRun _testRun;
        private TestRunData _testRunData;
        private IResultReader _resultReader;
        private TestRunContext _runContext;
        #endregion

        #region Public API
        public void InitializePublisher(IExecutionContext executionContext, VssConnection connection, string projectName, TestRunContext runContext, IResultReader resultReader)
        {
            _executionContext = executionContext;
            _projectName = projectName;
            _runContext = runContext;
            _resultReader = resultReader;
            connection.InnerHandler.Settings.SendTimeout = TimeSpan.FromSeconds(PUBLISH_TIMEOUT);
            _httpClient = connection.GetClient<TestManagementHttpClient>();
        }

        public void InitializePublisher(IExecutionContext executionContext, ITestManagementHttpClient httpClient, string projectName, TestRunContext runContext, IResultReader resultReader)
        {
            _executionContext = executionContext;
            _projectName = projectName;
            _runContext = runContext;
            _resultReader = resultReader;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Publishes the given results to the test run.
        /// </summary>
        /// <param name="testResults">Results to be published.</param>
        public void AddResults(TestCaseResultData[] testResults)
        {
            int noOfResultsToBePublished = BATCH_SIZE;

            for (int i = 0; i < testResults.Length; i += BATCH_SIZE)
            {
                if (i + BATCH_SIZE >= testResults.Length)
                {
                    noOfResultsToBePublished = testResults.Length - i;
                }
                _executionContext.Debug($"Test results remaining: {(testResults.Length - i)}");

                var currentBatch = new TestCaseResultData[noOfResultsToBePublished];
                Array.Copy(testResults, i, currentBatch, 0, noOfResultsToBePublished);

                Task<List<TestCaseResult>> testresults = _httpClient.AddTestResultsToTestRunAsync(currentBatch, _projectName, _testRun.Id);

                testresults.Wait();

                for (int j = 0; j < noOfResultsToBePublished; j++)
                {
                    // Do not upload duplicate entries 
                    string[] attachments = testResults[i + j].Attachments;
                    if (attachments != null)
                    {
                        Hashtable attachedFiles = new Hashtable(StringComparer.CurrentCultureIgnoreCase);
                        foreach (string attachment in attachments)
                        {
                            if (!attachedFiles.ContainsKey(attachment))
                            {
                                TestAttachmentRequestModel reqModel = GetAttachmentRequestModel(attachment);
                                if (reqModel != null)
                                {
                                    Task<TestAttachmentReference> trTask = _httpClient.CreateTestResultAttachmentAsync(reqModel, _projectName, _testRun.Id, testresults.Result[j].Id);
                                    trTask.Wait();
                                }
                                attachedFiles.Add(attachment, null);
                            }
                        }
                    }
                    // Upload console log as attachment
                    string consoleLog = testResults[i + j].ConsoleLog;
                    TestAttachmentRequestModel attachmentRequestModel = GetConsoleLogAttachmentRequestModel(consoleLog);
                    if (attachmentRequestModel != null)
                    {
                        Task<TestAttachmentReference> trTask = _httpClient.CreateTestResultAttachmentAsync(attachmentRequestModel, _projectName, _testRun.Id, testresults.Result[j].Id);
                        trTask.Wait();
                    }
                }
            }
        }

        /// <summary>
        /// Start a test run  
        /// </summary>
        public void StartTestRun(TestRunData testRun)
        {
            _testRunData = testRun;

            Task<TestRun> sendAsycn = _httpClient.CreateTestRunAsync(_projectName, _testRunData);
            sendAsycn.Wait();
            _testRun = sendAsycn.Result;
        }

        /// <summary>
        /// Mark the test run as completed 
        /// </summary>
        public string EndTestRun(bool publishAttachmentsAsArchive = false)
        {
            RunUpdateModel updateModel = new RunUpdateModel(
                completedDate: _testRunData.CompleteDate,
                state: TestRunState.Completed.ToString()
                );
            Task<TestRun> sendAsycn = _httpClient.UpdateTestRunAsync(_projectName, _testRun.Id, updateModel);
            sendAsycn.Wait();

            // Uploading run level attachments, only after run is marked completed;
            // so as to make sure that any server jobs that acts on the uploaded data (like CoverAn job does for Coverage files)  
            // have a fully published test run results, in case it wants to iterate over results 
            if (publishAttachmentsAsArchive)
            {
                UploadTestRunAttachmentsAsArchive();
            }
            else
            {
                UploadTestRunAttachmentsIndividual();
            }

            _testRun = sendAsycn.Result;
            return _testRun.WebAccessUrl;
        }

        /// <summary>
        /// Converts the given results file to TestRunData object
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>TestRunData</returns>
        public TestRunData ReadResultsFromFile(string filePath)
        {
            return _resultReader.ReadResults(_executionContext, filePath, _runContext);
        }

        /// <summary>
        /// Converts the given results file to TestRunData object
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <param name="runName">Run Name</param>
        /// <returns>TestRunData</returns>
        public TestRunData ReadResultsFromFile(string filePath, string runName)
        {
            _runContext.RunName = runName;
            return _resultReader.ReadResults(_executionContext, filePath, _runContext);
        }
        #endregion

        private void UploadTestRunAttachmentsAsArchive()
        {
            // Do not upload duplicate entries 
            var attachedFiles = UniqueTestRunFiles;
            try
            {
                var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDirectory);
                var zipFile = Path.Combine(tempDirectory, "TestResults_" + _testRun.Id + ".zip");

                File.Delete(zipFile); //if there's already file. remove silently without exception
                CreateZipFile(zipFile, attachedFiles);
                CreateTestRunAttachment(zipFile);
            }
            catch (Exception ex)
            {
                _executionContext.Warning(StringUtil.Loc("UnableToArchiveResults", ex));
                UploadTestRunAttachmentsIndividual();
            }
        }

        private void CreateZipFile(string zipfileName, IEnumerable<string> files)
        {
            // Create and open a new ZIP file
            using (var zip = ZipFile.Open(zipfileName, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    // Add the entry for each file
                    zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
                }
            }
        }

        private void UploadTestRunAttachmentsIndividual()
        {
            _executionContext.Debug("Uploading test run attachements individually");
            // Do not upload duplicate entries 
            var attachedFiles = UniqueTestRunFiles;
            foreach (var file in attachedFiles)
            {
                CreateTestRunAttachment(file);
            }
        }

        private void CreateTestRunAttachment(string zipFile)
        {
            TestAttachmentRequestModel reqModel = GetAttachmentRequestModel(zipFile);
            if (reqModel != null)
            {
                Task<TestAttachmentReference> trTask = _httpClient.CreateTestRunAttachmentAsync(reqModel, _projectName,
                    _testRun.Id);
                trTask.Wait();
            }
        }

        private String GetAttachmentType(String file)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);

            if (String.Compare(Path.GetExtension(file), ".coverage", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return AttachmentType.CodeCoverage.ToString();
            }
            else if (String.Compare(Path.GetExtension(file), ".trx", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return AttachmentType.TmiTestRunSummary.ToString();
            }
            else if (String.Compare(fileName, "testimpact", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return AttachmentType.TestImpactDetails.ToString();
            }
            else if (String.Compare(fileName, "SystemInformation", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return AttachmentType.IntermediateCollectorData.ToString();
            }
            else
            {
                return AttachmentType.GeneralAttachment.ToString();
            }
        }

        private TestAttachmentRequestModel GetAttachmentRequestModel(string attachment)
        {
            if (File.Exists(attachment) && new FileInfo(attachment).Length <= TCM_MAX_FILESIZE)
            {
                byte[] bytes = File.ReadAllBytes(attachment);
                string encodedData = Convert.ToBase64String(bytes);
                if (encodedData.Length <= TCM_MAX_FILESIZE)
                {
                    return new TestAttachmentRequestModel(encodedData, Path.GetFileName(attachment), "", GetAttachmentType(attachment));
                }
                else
                {
                    _executionContext.Warning(StringUtil.Loc("AttachmentExceededMaximum", attachment));
                }
            }
            else
            {
                _executionContext.Warning(StringUtil.Loc("NoSpaceOnDisk", attachment));
            }

            return null;
        }

        private TestAttachmentRequestModel GetConsoleLogAttachmentRequestModel(string consoleLog)
        {
            if (!string.IsNullOrWhiteSpace(consoleLog))
            {
                string consoleLogFileName = "Standard Console Output.log";

                if (consoleLog.Length <= TCM_MAX_FILESIZE)
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(consoleLog);
                    string encodedData = Convert.ToBase64String(bytes);
                    return new TestAttachmentRequestModel(encodedData, consoleLogFileName, "",
                        AttachmentType.ConsoleLog.ToString());
                }
                else
                {
                    _executionContext.Warning(StringUtil.Loc("AttachmentExceededMaximum", consoleLogFileName));
                }
            }

            return null;
        }

        private HashSet<string> UniqueTestRunFiles
        {
            get
            {
                var attachedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_testRunData.Attachments != null)
                {
                    foreach (string attachment in _testRunData.Attachments)
                    {
                        attachedFiles.Add(attachment);
                    }
                }
                return attachedFiles;
            }
        }
    }
}