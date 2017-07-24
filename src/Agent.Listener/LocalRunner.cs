using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Pipelines;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Listener.Configuration;
using Microsoft.VisualStudio.Services.Agent.Util;
using Newtonsoft.Json;
using Pipelines = Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Pipelines;

namespace Microsoft.VisualStudio.Services.Agent.Listener
{
    [ServiceLocator(Default = typeof(LocalRunner))]
    public interface ILocalRunner : IAgentService
    {
        Task<int> CacheTaskAsync(CommandSettings command, CancellationToken token);
        Task<int> ExportTaskAsync(CommandSettings command, CancellationToken token);
        Task<int> ListTaskAsync(CommandSettings command, CancellationToken token);
        Task<int> LocalRunAsync(CommandSettings command, CancellationToken token);
    }

    // todo: rename to LocalManager
    public sealed class LocalRunner : AgentService, ILocalRunner
    {
        private string _gitPath;
        private ITaskStore _taskStore;
        private ITerminal _term;

        public sealed override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);
            _term = hostContext.GetService<ITerminal>();
        }

        public async Task<int> CacheTaskAsync(CommandSettings command, CancellationToken token)
        {
            Trace.Info(nameof(CacheTaskAsync));
            InitializeCommand(command);
            command.GetUrl(); // Validate the URL was supplied.

            List<TaskDefinition> tasks =
                (await _taskStore.GetServerTasksAsync(name: command.GetAgentName(), version: command.GetVersion(), token: token))
                .OrderBy(x => x.Name.ToUpperInvariant())
                .ThenByDescending(x => x.Version.Major)
                .ToList();
            foreach (TaskDefinition task in tasks)
            {
                await _taskStore.EnsureCachedAsync(task, token);
            }

            return Constants.Agent.ReturnCode.Success;
        }

        public Task<int> ExportTaskAsync(CommandSettings command, CancellationToken token)
        {
            Trace.Info(nameof(ExportTaskAsync));
            InitializeCommand(command);
            // todo: validate --directory is a directory or can be created.

            // todo: get server or local tasks matching name/version. export .yml files. clobber.

            throw new System.NotImplementedException();
        }

        public async Task<int> ListTaskAsync(CommandSettings command, CancellationToken token)
        {
            Trace.Info(nameof(ListTaskAsync));
            InitializeCommand(command);

            List<TaskDefinition> tasks;
            if (!string.IsNullOrEmpty(command.GetUrl(optional: true)))
            {
                // Get all server tasks (filtered within major version).
                tasks = await _taskStore.GetServerTasksAsync(name: null, version: null, token: token);
            }
            else
            {
                // Get all cached tasks (filtered within major version).
                tasks = _taskStore.GetLocalTasks(name: null, version: null, token: token);
            }

            // Filter by search value.
            string searchValue = command.GetSearch();
            if (string.IsNullOrEmpty(searchValue))
            {
                // Convert the search value to a regex. * is the only supported wildcard.
                var patternSegments = new List<string>();
                patternSegments.Append("^");
                string[] nameSegments = searchValue.Split('*');
                foreach (string nameSegment in nameSegments)
                {
                    if (string.IsNullOrEmpty(nameSegment))
                    {
                        // Empty indicates the segment was the wildcard "*".
                        if (patternSegments.Count > 0
                            && !string.Equals(patternSegments[patternSegments.Count - 1], ".*", StringComparison.Ordinal))
                        {
                            patternSegments.Append(".*");
                        }
                    }
                    else
                    {
                        // Otherwise not a wildcard. Append the escaped segment.
                        patternSegments.Append(Regex.Escape(nameSegment));
                    }
                }

                patternSegments.Append("$");
                string pattern = string.Join(string.Empty, patternSegments);
                var regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

                // Filter by search value.
                tasks = tasks
                    .Where(x =>
                    {
                        return regex.IsMatch(x.Name) ||
                            regex.IsMatch(x.FriendlyName) ||
                            regex.IsMatch(x.Id.ToString()) ||
                            regex.IsMatch(x.Description);
                    })
                    .ToList();
            }

            // Print each task.
            foreach (TaskDefinition task in tasks)
            {
                _term.WriteLine();
                _term.WriteLine($"Task: {task.Name}@{task.Version.Major}");
                _term.WriteLine($"Friendly name: {task.FriendlyName}");
                _term.WriteLine($"ID: {task.Id}");
                _term.WriteLine($"Description: {task.Description}");
            }

            return Constants.Agent.ReturnCode.Success;
        }

        public async Task<int> LocalRunAsync(CommandSettings command, CancellationToken token)
        {
            Trace.Info(nameof(LocalRunAsync));
            InitializeCommand(command);
            var configStore = HostContext.GetService<IConfigurationStore>();
            AgentSettings settings = configStore.GetSettings();

            // Load the YAML file.
            string yamlFile = command.GetYaml();
            ArgUtil.File(yamlFile, nameof(yamlFile));
            var parseOptions = new ParseOptions
            {
                MaxFiles = 10,
                MustacheEvaluationMaxResultLength = 512 * 1024, // 512k string length
                MustacheEvaluationTimeout = TimeSpan.FromSeconds(10),
                MustacheMaxDepth = 5,
            };
            var pipelineParser = new PipelineParser(new PipelineTraceWriter(), new PipelineFileProvider(), parseOptions);
            Pipelines.Process process = pipelineParser.Load(
                defaultRoot: Directory.GetCurrentDirectory(),
                path: yamlFile,
                mustacheContext: null,
                cancellationToken: HostContext.AgentShutdownToken);
            ArgUtil.NotNull(process, nameof(process));
            if (command.WhatIf)
            {
                return Constants.Agent.ReturnCode.Success;
            }

            // Create job message.
            IJobDispatcher jobDispatcher = null;
            try
            {
                jobDispatcher = HostContext.CreateService<IJobDispatcher>();
                foreach (JobInfo job in await ConvertToJobMessagesAsync(process, token))
                {
                    job.RequestMessage.Environment.Variables[Constants.Variables.Agent.RunMode] = RunMode.Local.ToString();
                    jobDispatcher.Run(job.RequestMessage);
                    Task jobDispatch = jobDispatcher.WaitAsync(token);
                    if (!Task.WaitAll(new[] { jobDispatch }, job.Timeout))
                    {
                        jobDispatcher.Cancel(job.CancelMessage);

                        // Finish waiting on the same job dispatch task. The first call to WaitAsync dequeues
                        // the dispatch task and then proceeds to wait on it. So we need to continue awaiting
                        // the task instance (queue is now empty).
                        await jobDispatch;
                    }
                }
            }
            finally
            {
                if (jobDispatcher != null)
                {
                    await jobDispatcher.ShutdownAsync();
                }
            }

            return Constants.Agent.ReturnCode.Success;
        }

        private void InitializeCommand(CommandSettings command)
        {
            _term.WriteLine("This command is currently in preview. The interface and behavior will change in a future version.");
            if (!command.Unattended)
            {
                _term.WriteLine("Press Enter to continue.");
                _term.ReadLine();
            }

            HostContext.RunMode = RunMode.Local;
            command.SetUnattended();

            // Initialize the task store.
            _taskStore = HostContext.GetService<ITaskStore>();
            string url = command.GetUrl(optional: true);
            if (!string.IsNullOrEmpty(url))
            {
                // Store the HTTP client.
                var credentialManager = HostContext.GetService<ICredentialManager>();
                string authType = command.GetAuth(defaultValue: Constants.Configuration.Integrated);
                ICredentialProvider provider = credentialManager.GetCredentialProvider(authType);
                provider.EnsureCredential(HostContext, command, url);
                _taskStore.HttpClient = new TaskAgentHttpClient(new Uri(url), provider.GetVssCredentials(HostContext));
            }
        }

        private async Task<List<JobInfo>> ConvertToJobMessagesAsync(Pipelines.Process process, CancellationToken token)
        {
            // Verify the current directory is the root of a git repo.
            string repoDirectory = Directory.GetCurrentDirectory();
            if (!Directory.Exists(Path.Combine(repoDirectory, ".git")))
            {
                throw new Exception("Unable to run the build locally. The command must be executed from the root directory of a local git repository.");
            }

            // Collect info about the repo.
            string repoName = Path.GetFileName(repoDirectory);
            string userName = await GitAsync("config --get user.name", token);
            string userEmail = await GitAsync("config --get user.email", token);
            string branch = await GitAsync("symbolic-ref HEAD", token);
            string commit = await GitAsync("rev-parse HEAD", token);
            string commitAuthorName = await GitAsync("show --format=%an --no-patch HEAD", token);
            string commitSubject = await GitAsync("show --format=%s --no-patch HEAD", token);

            var jobs = new List<JobInfo>();
            int requestId = 1;
            foreach (Phase phase in process.Phases ?? new List<IPhase>(0))
            {
                foreach (Job job in phase.Jobs ?? new List<IJob>(0))
                {
                    var builder = new StringBuilder();
                    builder.Append($@"{{
  ""tasks"": [");
                    var steps = new List<ISimpleStep>();
                    foreach (IStep step in job.Steps ?? new List<IStep>(0))
                    {
                        if (step is ISimpleStep)
                        {
                            steps.Add(step as ISimpleStep);
                        }
                        else
                        {
                            var stepsPhase = step as StepsPhase;
                            foreach (ISimpleStep nestedStep in stepsPhase.Steps ?? new List<ISimpleStep>(0))
                            {
                                steps.Add(nestedStep);
                            }
                        }
                    }

                    bool firstStep = true;
                    foreach (ISimpleStep step in steps)
                    {
                        if (!(step is TaskStep))
                        {
                            throw new Exception("Unable to run step type: " + step.GetType().FullName);
                        }

                        var task = step as TaskStep;
                        if (!task.Enabled)
                        {
                            continue;
                        }

                        TaskDefinition definition = await _taskStore.GetTaskAsync(
                            name: task.Reference.Name,
                            version: task.Reference.Version,
                            token: token);
                        await _taskStore.EnsureCachedAsync(definition, token);
                        if (!firstStep)
                        {
                            builder.Append(",");
                        }

                        firstStep = false;
                        builder.Append($@"
    {{
      ""instanceId"": ""{Guid.NewGuid()}"",
      ""displayName"": {JsonConvert.ToString(!string.IsNullOrEmpty(task.Name) ? task.Name : definition.InstanceNameFormat)},
      ""enabled"": true,
      ""continueOnError"": {task.ContinueOnError.ToString().ToLowerInvariant()},
      ""condition"": {JsonConvert.ToString(task.Condition)},
      ""alwaysRun"": false,
      ""timeoutInMinutes"": {task.TimeoutInMinutes.ToString(CultureInfo.InvariantCulture)},
      ""id"": ""{definition.Id}"",
      ""name"": {JsonConvert.ToString(definition.Name)},
      ""version"": {JsonConvert.ToString(GetVersion(definition).ToString())},
      ""inputs"": {{");
                        bool firstInput = true;
                        foreach (KeyValuePair<string, string> input in task.Inputs ?? new Dictionary<string, string>(0))
                        {
                            if (!firstInput)
                            {
                                builder.Append(",");
                            }

                            firstInput = false;
                            builder.Append($@"
        {JsonConvert.ToString(input.Key)}: {JsonConvert.ToString(input.Value)}");
                        }

                        builder.Append($@"
      }},
      ""environment"": {{");
                        bool firstEnv = true;
                        foreach (KeyValuePair<string, string> env in task.Environment ?? new Dictionary<string, string>(0))
                        {
                            if (!firstEnv)
                            {
                                builder.Append(",");
                            }

                            firstEnv = false;
                            builder.Append($@"
        {JsonConvert.ToString(env.Key)}: {JsonConvert.ToString(env.Value)}");
                        }
                        builder.Append($@"
      }}
    }}");
                    }

                    builder.Append($@"
  ],
  ""requestId"": {requestId++},
  ""lockToken"": ""00000000-0000-0000-0000-000000000000"",
  ""lockedUntil"": ""0001-01-01T00:00:00"",
  ""messageType"": ""JobRequest"",
  ""plan"": {{
    ""scopeIdentifier"": ""00000000-0000-0000-0000-000000000000"",
    ""planType"": ""Build"",
    ""version"": 8,
    ""planId"": ""00000000-0000-0000-0000-000000000000"",
    ""artifactUri"": ""vstfs:///Build/Build/1234"",
    ""artifactLocation"": null
  }},
  ""timeline"": {{
    ""id"": ""00000000-0000-0000-0000-000000000000"",
    ""changeId"": 1,
    ""location"": null
  }},
  ""jobId"": ""{Guid.NewGuid()}"",
  ""jobName"": {JsonConvert.ToString(!string.IsNullOrEmpty(job.Name) ? job.Name : "Build")},
  ""environment"": {{
    ""endpoints"": [
      {{
        ""data"": {{
          ""repositoryId"": ""00000000-0000-0000-0000-000000000000"",
          ""localDirectory"": {JsonConvert.ToString(repoDirectory)},
          ""clean"": ""false"",
          ""checkoutSubmodules"": ""False"",
          ""onpremtfsgit"": ""False"",
          ""fetchDepth"": ""0"",
          ""gitLfsSupport"": ""false"",
          ""skipSyncSource"": ""false"",
          ""cleanOptions"": ""0""
        }},
        ""name"": {JsonConvert.ToString(repoName)},
        ""type"": ""LocalRun"",
        ""url"": ""https://127.0.0.1/vsts-agent-local-runner?directory={Uri.EscapeDataString(repoDirectory)}"",
        ""authorization"": {{
          ""parameters"": {{
            ""AccessToken"": ""dummy-access-token""
          }},
          ""scheme"": ""OAuth""
        }},
        ""isReady"": false
      }}
    ],
    ""mask"": [
      {{
        ""type"": ""regex"",
        ""value"": ""dummy-access-token""
      }}
    ],
    ""variables"": {{");
                    builder.Append($@"
      ""system"": ""build"",
      ""system.collectionId"": ""00000000-0000-0000-0000-000000000000"",
      ""system.culture"": ""en-US"",
      ""system.definitionId"": ""55"",
      ""system.isScheduled"": ""False"",
      ""system.hosttype"": ""build"",
      ""system.jobId"": ""00000000-0000-0000-0000-000000000000"",
      ""system.planId"": ""00000000-0000-0000-0000-000000000000"",
      ""system.timelineId"": ""00000000-0000-0000-0000-000000000000"",
      ""system.taskDefinitionsUri"": ""https://127.0.0.1/vsts-agent-local-runner"",
      ""system.teamFoundationCollectionUri"": ""https://127.0.0.1/vsts-agent-local-runner"",
      ""system.teamProject"": {JsonConvert.ToString(repoName)},
      ""system.teamProjectId"": ""00000000-0000-0000-0000-000000000000"",
      ""build.buildId"": ""1863"",
      ""build.buildNumber"": ""1863"",
      ""build.buildUri"": ""vstfs:///Build/Build/1863"",
      ""build.clean"": """",
      ""build.definitionName"": ""My Build Definition Name"",
      ""build.definitionVersion"": ""1"",
      ""build.queuedBy"": {JsonConvert.ToString(userName)},
      ""build.queuedById"": ""00000000-0000-0000-0000-000000000000"",
      ""build.requestedFor"": {JsonConvert.ToString(userName)},
      ""build.requestedForEmail"": {JsonConvert.ToString(userEmail)},
      ""build.requestedForId"": ""00000000-0000-0000-0000-000000000000"",
      ""build.repository.uri"": ""https://127.0.0.1/vsts-agent-local-runner/_git/{Uri.EscapeDataString(repoName)}"",
      ""build.sourceBranch"": {JsonConvert.ToString(branch)},
      ""build.sourceBranchName"": {JsonConvert.ToString(branch.Split('/').Last())},
      ""build.sourceVersion"": {JsonConvert.ToString(commit)},
      ""build.sourceVersionAuthor"": {JsonConvert.ToString(commitAuthorName)},
      ""build.sourceVersionMessage"": {JsonConvert.ToString(commitSubject)},
      ""AZURE_HTTP_USER_AGENT"": ""VSTS_00000000-0000-0000-0000-000000000000_build_55_1863"",
      ""MSDEPLOY_HTTP_USER_AGENT"": ""VSTS_00000000-0000-0000-0000-000000000000_build_55_1863""");
                    foreach (Variable variable in job.Variables ?? new List<IVariable>(0))
                    {
                        builder.Append($@",
      {JsonConvert.ToString(variable.Name ?? string.Empty)}: {JsonConvert.ToString(variable.Value ?? string.Empty)}");
                    }

                    builder.Append($@"
    }},
    ""systemConnection"": {{
      ""data"": {{
        ""ServerId"": ""00000000-0000-0000-0000-000000000000"",
        ""ServerName"": ""127.0.0.1""
      }},
      ""name"": ""SystemVssConnection"",
      ""url"": ""https://127.0.0.1/vsts-agent-local-runner"",
      ""authorization"": {{
        ""parameters"": {{
          ""AccessToken"": ""dummy-access-token""
        }},
        ""scheme"": ""OAuth""
      }},
      ""isReady"": false
    }}
  }}
}}");
                    string message = builder.ToString();
                    try
                    {
                        jobs.Add(new JobInfo(job, message));
                    }
                    catch
                    {
                        Dump("Job message JSON", message);
                        throw;
                    }
                }
            }

            return jobs;
        }

        private async Task<string> GitAsync(string arguments, CancellationToken token)
        {
            // Resolve the location of git.
            if (_gitPath == null)
            {
#if OS_WINDOWS
                _gitPath = Path.Combine(IOUtil.GetExternalsPath(), "git", "cmd", $"git{IOUtil.ExeExtension}");
                ArgUtil.File(_gitPath, nameof(_gitPath));
#else
                var whichUtil = HostContext.GetService<IWhichUtil>();
                _gitPath = whichUtil.Which("git", require: true);
#endif
            }

            // Prepare the environment variables to overlay.
            var overlayEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);
            overlayEnvironment["GIT_TERMINAL_PROMPT"] = "0";
            // Skip any GIT_TRACE variable since GIT_TRACE will affect ouput from every git command.
            // This will fail the parse logic for detect git version, remote url, etc.
            // Ex. 
            //      SET GIT_TRACE=true
            //      git version 
            //      11:39:58.295959 git.c:371               trace: built-in: git 'version'
            //      git version 2.11.1.windows.1
            IDictionary currentEnvironment = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry entry in currentEnvironment)
            {
                string key = entry.Key as string ?? string.Empty;
                if (string.Equals(key, "GIT_TRACE", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("GIT_TRACE_", StringComparison.OrdinalIgnoreCase))
                {
                    overlayEnvironment[key] = string.Empty;
                }
            }

            // Run git and return the output from the streams.
            var output = new StringBuilder();
            var processInvoker = HostContext.CreateService<IProcessInvoker>();
            Console.WriteLine();
            Console.WriteLine($"git {arguments}");
            processInvoker.OutputDataReceived += delegate (object sender, ProcessDataReceivedEventArgs message)
            {
                output.AppendLine(message.Data);
                Console.WriteLine(message.Data);
            };
            processInvoker.ErrorDataReceived += delegate (object sender, ProcessDataReceivedEventArgs message)
            {
                output.AppendLine(message.Data);
                Console.WriteLine(message.Data);
            };
#if OS_WINDOWS
            Encoding encoding = Encoding.UTF8;
#else
            Encoding encoding = null;
#endif
            await processInvoker.ExecuteAsync(
                workingDirectory: Directory.GetCurrentDirectory(),
                fileName: _gitPath,
                arguments: arguments,
                environment: overlayEnvironment,
                requireExitCodeZero: true,
                outputEncoding: encoding,
                cancellationToken: token);

            string result = output.ToString().Trim();
            ArgUtil.NotNullOrEmpty(result, nameof(result));
            return result;
        }

        private static Version GetVersion(TaskDefinition definition)
        {
            return new Version(definition.Version.Major, definition.Version.Minor, definition.Version.Patch);
        }

        private static void Dump(string header, string value)
        {
            Console.WriteLine();
            Console.WriteLine(String.Empty.PadRight(80, '*'));
            Console.WriteLine($"* {header}");
            Console.WriteLine(String.Empty.PadRight(80, '*'));
            Console.WriteLine();
            using (StringReader reader = new StringReader(value))
            {
                int lineNumber = 1;
                string line = reader.ReadLine();
                while (line != null)
                {
                    Console.WriteLine($"{lineNumber.ToString().PadLeft(4)}: {line}");
                    line = reader.ReadLine();
                    lineNumber++;
                }
            }
        }

        [ServiceLocator(Default = typeof(TaskStore))]
        private interface ITaskStore : IAgentService
        {
            TaskAgentHttpClient HttpClient { get; set; }

            Task EnsureCachedAsync(TaskDefinition task, CancellationToken token);
            List<TaskDefinition> GetLocalTasks(string name, string version, CancellationToken token);
            Task<List<TaskDefinition>> GetServerTasksAsync(string name, string version, CancellationToken token);
            Task<TaskDefinition> GetTaskAsync(string name, string version, CancellationToken token);
        }

        private sealed class TaskStore : AgentService
        {
            private List<TaskDefinition> _localTasks;
            private List<TaskDefinition> _serverTasks;
            private ITerminal _term;

            public TaskAgentHttpClient HttpClient { get; set; }

            public sealed override void Initialize(IHostContext hostContext)
            {
                base.Initialize(hostContext);
                _term = hostContext.GetService<ITerminal>();
            }

            public async Task EnsureCachedAsync(TaskDefinition task, CancellationToken token)
            {
                Trace.Entering();
                ArgUtil.NotNull(task, nameof(task));
                ArgUtil.NotNullOrEmpty(task.Version, nameof(task.Version));

                // first check to see if we already have the task
                string destDirectory = GetDirectory(task);
                Trace.Info($"Ensuring task exists: ID '{task.Id}', version '{task.Version}', name '{task.Name}', directory '{destDirectory}'.");
                if (File.Exists(destDirectory + ".completed"))
                {
                    Trace.Info("Task already downloaded.");
                    return;
                }

                // delete existing task folder.
                Trace.Verbose("Deleting task destination folder: {0}", destDirectory);
                IOUtil.DeleteDirectory(destDirectory, CancellationToken.None);

                // Inform the user that a download is taking place. The download could take a while if
                // the task zip is large. It would be nice to print the localized name, but it is not
                // available from the reference included in the job message.
                _term.WriteLine(StringUtil.Loc("DownloadingTask0", task.Name));
                string zipFile;
                var version = new TaskVersion(task.Version);

                //download and extract task in a temp folder and rename it on success
                string tempDirectory = Path.Combine(HostContext.GetDirectory(WellKnownDirectory.Tasks), "_temp_" + Guid.NewGuid());
                try
                {
                    Directory.CreateDirectory(tempDirectory);
                    zipFile = Path.Combine(tempDirectory, string.Format("{0}.zip", Guid.NewGuid()));
                    //open zip stream in async mode
                    using (FileStream fs = new FileStream(zipFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                    {
                        using (Stream result = await HttpClient.GetTaskContentZipAsync(task.Id, version, token))
                        {
                            //81920 is the default used by System.IO.Stream.CopyTo and is under the large object heap threshold (85k). 
                            await result.CopyToAsync(fs, 81920, token);
                            await fs.FlushAsync(token);
                        }
                    }

                    Directory.CreateDirectory(destDirectory);
                    ZipFile.ExtractToDirectory(zipFile, destDirectory);

                    Trace.Verbose("Create watermark file indicate task download succeed.");
                    File.WriteAllText(destDirectory + ".completed", DateTime.UtcNow.ToString());

                    Trace.Info("Finished getting task.");
                }
                finally
                {
                    try
                    {
                        //if the temp folder wasn't moved -> wipe it
                        if (Directory.Exists(tempDirectory))
                        {
                            Trace.Verbose("Deleting task temp folder: {0}", tempDirectory);
                            IOUtil.DeleteDirectory(tempDirectory, CancellationToken.None); // Don't cancel this cleanup and should be pretty fast.
                        }
                    }
                    catch (Exception ex)
                    {
                        //it is not critical if we fail to delete the temp folder
                        Trace.Warning("Failed to delete temp folder '{0}'. Exception: {1}", tempDirectory, ex);
                        Trace.Warning(StringUtil.Loc("FailedDeletingTempDirectory0Message1", tempDirectory, ex.Message));
                    }
                }
            }

            public List<TaskDefinition> GetLocalTasks(string name, string version, CancellationToken token)
            {
                if (_localTasks == null)
                {
                    // Get tasks from the local cache.
                    var tasks = new List<TaskDefinition>();
                    string tasksDirectory = HostContext.GetDirectory(WellKnownDirectory.Tasks);
                    if (Directory.Exists(tasksDirectory))
                    {
                        _term.WriteLine("Getting available tasks from the cache.");
                        foreach (string taskDirectory in Directory.GetDirectories(tasksDirectory))
                        {
                            foreach (string taskSubDirectory in Directory.GetDirectories(taskDirectory))
                            {
                                string taskJsonPath = Path.Combine(taskSubDirectory, "task.json");
                                if (File.Exists(taskJsonPath) && File.Exists(taskSubDirectory + ".completed"))
                                {
                                    token.ThrowIfCancellationRequested();
                                    Trace.Info($"Loading: '{taskJsonPath}'");
                                    TaskDefinition definition = IOUtil.LoadObject<TaskDefinition>(taskJsonPath);
                                    if (definition == null ||
                                        string.IsNullOrEmpty(definition.Name) ||
                                        definition.Version == null)
                                    {
                                        _term.WriteLine($"Task definition is invalid. The name property must not be empty and the version property must not be null. Task definition: {taskJsonPath}");
                                        continue;
                                    }
                                    else if (!string.Equals(taskSubDirectory, GetDirectory(definition), IOUtil.FilePathStringComparison))
                                    {
                                        _term.WriteLine($"Task definition does not match the expected folder structure. Expected: '{GetDirectory(definition)}'; actual: '{taskJsonPath}'");
                                        continue;
                                    }

                                    tasks.Add(definition);
                                }
                            }
                        }
                    }

                    _localTasks = tasks;
                }

                return FilterByReference(_localTasks, name, version);
            }

            public async Task<List<TaskDefinition>> GetServerTasksAsync(string name, string version, CancellationToken token)
            {
                ArgUtil.NotNull(HttpClient, nameof(HttpClient));
                if (_serverTasks == null)
                {
                    _term.WriteLine("Getting available task versions from server.");
                    var tasks = await HttpClient.GetTaskDefinitionsAsync(cancellationToken: token);
                    _term.WriteLine("Successfully retrieved task versions from server.");
                    _serverTasks = FilterWithinMajorVersion(tasks);
                }

                return FilterByReference(_serverTasks, name, version);
            }

            public async Task<TaskDefinition> GetTaskAsync(string name, string version, CancellationToken token)
            {
                ArgUtil.NotNullOrEmpty(name, nameof(name));
                ArgUtil.NotNullOrEmpty(version, nameof(version));
                if (HttpClient != null)
                {
                    return (await GetServerTasksAsync(name, version, token)).Single();
                }

                return GetLocalTasks(name, version, token).Single();
            }

            private List<TaskDefinition> FilterByReference(List<TaskDefinition> tasks, string name, string version)
            {
                // Filter by name.
                if (!string.IsNullOrEmpty(name))
                {
                    Guid id = default(Guid);
                    if (Guid.TryParseExact(name, format: "D", result: out id)) // D = 32 digits separated by hyphens
                    {
                        // Filter by GUID.
                        tasks = tasks.Where(x => x.Id == id).ToList();
                    }
                    else
                    {
                        // Filter by name. Convert the name to a regex. * is the only supported wildcard.
                        var patternSegments = new List<string>();
                        patternSegments.Append("^");
                        string[] nameSegments = name.Split('*');
                        foreach (string nameSegment in nameSegments)
                        {
                            if (string.IsNullOrEmpty(nameSegment))
                            {
                                // Empty indicates the segment was the wildcard "*".
                                if (patternSegments.Count > 0
                                    && !string.Equals(patternSegments[patternSegments.Count - 1], ".*", StringComparison.Ordinal))
                                {
                                    patternSegments.Append(".*");
                                }
                            }
                            else
                            {
                                // Otherwise not a wildcard. Append the escaped segment.
                                patternSegments.Append(Regex.Escape(nameSegment));
                            }
                        }

                        patternSegments.Append("$");
                        string pattern = string.Join(string.Empty, patternSegments);
                        var regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                        tasks = tasks.Where(x => regex.IsMatch(x.Name)).ToList();
                    }

                    // Validate name is not ambiguous.
                    if (tasks.GroupBy(x => x.Id).Count() > 1)
                    {
                        throw new Exception($"Unable to resolve a task for the name '{name}'. The name is ambiguous.");
                    }

                    // Filter by version.
                    if (!string.IsNullOrEmpty(version))
                    {
                        int versionInt = default(int);
                        if (!int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out versionInt))
                        {
                            throw new Exception($"Version must be be a whole number. For example '2'. The following task version is invalid: '{version}'");
                        }

                        tasks = tasks.Where(x => x.Version.Major == versionInt).ToList();
                    }

                    // Validate a task was found.
                    if (tasks.Count == 0)
                    {
                        throw new Exception($"Unable to resolve task by name or ID '{name}'.");
                    }

                    ArgUtil.Equal(1, tasks.Count, nameof(tasks.Count));
                }

                return tasks;
            }

            private List<TaskDefinition> FilterWithinMajorVersion(List<TaskDefinition> tasks)
            {
                return tasks
                    .GroupBy(x => new { Id = x.Id, MajorVersion = x.Version }) // Group by ID and major-version
                    .Select(x => x.OrderByDescending(y => y.Version).First()) // Select the max version
                    .ToList();
            }

            private string GetDirectory(TaskDefinition definition)
            {
                ArgUtil.NotEmpty(definition.Id, nameof(definition.Id));
                ArgUtil.NotNull(definition.Name, nameof(definition.Name));
                ArgUtil.NotNullOrEmpty(definition.Version, nameof(definition.Version));
                return Path.Combine(HostContext.GetDirectory(WellKnownDirectory.Tasks), $"{definition.Name}_{definition.Id}", definition.Version);
            }
        }

        private sealed class JobInfo
        {
            public JobInfo(Job job, string requestMessage)
            {
                RequestMessage = JsonUtility.FromString<AgentJobRequestMessage>(requestMessage);
                Timeout = TimeSpan.FromMinutes(job.TimeoutInMinutes ?? 60);
            }

            public JobCancelMessage CancelMessage => new JobCancelMessage(RequestMessage.JobId, TimeSpan.FromSeconds(60));

            public AgentJobRequestMessage RequestMessage { get; }

            public TimeSpan Timeout { get; }
        }

        private sealed class PipelineTraceWriter : Pipelines.ITraceWriter
        {
            public void Info(String format, params Object[] args)
            {
                Console.WriteLine(format, args);
            }

            public void Verbose(String format, params Object[] args)
            {
                Console.WriteLine(format, args);
            }
        }

        private sealed class PipelineFileProvider : Pipelines.IFileProvider
        {
            public FileData GetFile(String path)
            {
                return new FileData
                {
                    Name = Path.GetFileName(path),
                    Directory = Path.GetDirectoryName(path),
                    Content = File.ReadAllText(path),
                };
            }

            public String ResolvePath(String defaultRoot, String path)
            {
                return Path.Combine(defaultRoot, path);
            }
        }
    }
}