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
using Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Pipelines.Yaml;
using Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Pipelines.Yaml.Contracts;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Configuration;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using Yaml = Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Pipelines.Yaml;
using YamlContracts = Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Pipelines.Yaml.Contracts;

namespace Microsoft.VisualStudio.Services.Agent
{
    [ServiceLocator(Default = typeof(YamlRunner))]
    public interface IYamlRunner : IAgentService
    {
        int Lint(CommandSettings command, CancellationToken token);
        Task<int> RunAsync(CommandSettings command, CancellationToken token);
        Task<int> ValidateAsync(CommandSettings command, CancellationToken token);
    }

    public sealed class YamlRunner : AgentService, IYamlRunner
    {
        private string _gitPath;
        //private ITaskStore _taskStore;
        private ITerminal _term;
        private ILoginStore _loginStore;

        public sealed override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);
            hostContext.RunMode = RunMode.Local;
            //_taskStore = HostContext.GetService<ITaskStore>();
            _term = hostContext.GetService<ITerminal>();

            _loginStore = hostContext.GetService<ILoginStore>();            
        }

        public int Lint(CommandSettings command, CancellationToken token)
        {
            try
            {
                string ymlFile = ResolveYamlPath(command);
                _term.WriteLine($"Loading {ymlFile}");

                PipelineParser parser = GetParser();
                parser.DeserializeAndSerialize(
                    defaultRoot: Directory.GetCurrentDirectory(),
                    path: ymlFile,
                    mustacheContext: null,
                    cancellationToken: HostContext.AgentShutdownToken);
                return Constants.Agent.ReturnCode.Success;
            }
            catch (Exception e)
            {
                _term.WriteLine($"Invalid: {e.Message}");    
            }
            return Constants.Agent.ReturnCode.TerminatedError;
        }

        public async Task<int> ValidateAsync(CommandSettings command, CancellationToken token)
        {
            try
            {
                VssCredentials creds = EnsureCredential(command);

                string ymlFile = ResolveYamlPath(command);
                _term.WriteLine($"Loading {ymlFile}");

                PipelineParser parser = GetParser();
                parser.DeserializeAndSerialize(
                    defaultRoot: Directory.GetCurrentDirectory(),
                    path: ymlFile,
                    mustacheContext: null,
                    cancellationToken: HostContext.AgentShutdownToken);
                return Constants.Agent.ReturnCode.Success;
            }
            catch (Exception e)
            {
                _term.WriteLine($"Invalid: {e.Message}");    
            }
            return Constants.Agent.ReturnCode.TerminatedError;
        }

        public async Task<int> RunAsync(CommandSettings command, CancellationToken token)
        {
            VssCredentials creds = EnsureCredential(command);

            string ymlFile = ResolveYamlPath(command);
            _term.WriteLine($"Loading {ymlFile}");

            // Verify the current directory is the root of a git repo.
            string repoDirectory = Directory.GetCurrentDirectory();
            if (!Directory.Exists(Path.Combine(repoDirectory, ".git")))
            {
                throw new Exception("Unable to run the build locally. The command must be executed from the root directory of a local git repository.");
            }

            PipelineParser parser = GetParser();            
            YamlContracts.Process process = parser.LoadInternal(
                defaultRoot: Directory.GetCurrentDirectory(),
                path: ymlFile,
                mustacheContext: null,
                cancellationToken: HostContext.AgentShutdownToken);
            ArgUtil.NotNull(process, nameof(process));  

            // Filter the phases.
            string phaseName = command.GetPhase();
            if (!string.IsNullOrEmpty(phaseName))
            {
                process.Phases = process.Phases
                    .Cast<YamlContracts.Phase>()
                    .Where(x => string.Equals(x.Name, phaseName, StringComparison.OrdinalIgnoreCase))
                    .Cast<YamlContracts.IPhase>()
                    .ToList();
                if (process.Phases.Count == 0)
                {
                    throw new Exception($"Phase '{phaseName}' not found.");
                }
            }

            // Verify a phase was specified if more than one phase was found.
            if (process.Phases.Count > 1)
            {
                throw new Exception($"More than one phase was discovered. Use the --{Constants.Agent.CommandLine.Args.Phase} argument to specify a phase.");
            }

            // Get the matrix.
            var phase = process.Phases[0] as YamlContracts.Phase;
            var queueTarget = phase.Target as QueueTarget;

            // Filter to a specific matrix.
            string matrixName = command.GetMatrix();
            if (!string.IsNullOrEmpty(matrixName))
            {
                if (queueTarget?.Matrix != null)
                {
                    queueTarget.Matrix = queueTarget.Matrix.Keys
                        .Where(x => string.Equals(x, matrixName, StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(keySelector: x => x, elementSelector: x => queueTarget.Matrix[x]);
                }

                if (queueTarget?.Matrix == null || queueTarget.Matrix.Count == 0)
                {
                    throw new Exception($"Job configuration matrix '{matrixName}' not found.");
                }
            }

            // Verify a matrix was specified if more than one matrix was found.
            if (queueTarget?.Matrix != null && queueTarget.Matrix.Count > 1)
            {
                throw new Exception($"More than one job configuration matrix was discovered. Use the --{Constants.Agent.CommandLine.Args.Matrix} argument to specify a matrix.");
            }
                        
            return Constants.Agent.ReturnCode.Success;          
        } 

        private string ResolveYamlPath(CommandSettings command)
        {
            // Resolve the YAML file path.
            string ymlFile = command.GetYml();
            if (string.IsNullOrEmpty(ymlFile))
            {
                string[] ymlFiles =
                    Directory.GetFiles(Directory.GetCurrentDirectory())
                    .Where((string filePath) =>
                    {
                        return filePath.EndsWith(".yml", IOUtil.FilePathStringComparison);
                    })
                    .ToArray();
                if (ymlFiles.Length > 1)
                {
                    throw new Exception($"More than one .yml file exists in the current directory. Specify which file to use via the '{Constants.Agent.CommandLine.Args.Yml}' command line argument.");
                }

                ymlFile = ymlFiles.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(ymlFile))
            {
                throw new Exception($"Unable to find a .yml file in the current directory. Specify which file to use via the '{Constants.Agent.CommandLine.Args.Yml}' command line argument.");
            }

            return ymlFile;            
        }

        private PipelineParser GetParser()
        {
            var parseOptions = new ParseOptions
            {
                MaxFiles = 10,
                MustacheEvaluationMaxResultLength = 512 * 1024, // 512k string length
                MustacheEvaluationTimeout = TimeSpan.FromSeconds(10),
                MustacheMaxDepth = 5,
            };
            return new PipelineParser(new PipelineTraceWriter(), new PipelineFileProvider(), parseOptions);            
        }

        private sealed class JobInfo
        {
            public JobInfo(Phase phase, string jobName, string requestMessage)
            {
                JobName = jobName ?? string.Empty;
                PhaseName = phase.Name ?? string.Empty;
                RequestMessage = JsonUtility.FromString<AgentJobRequestMessage>(requestMessage);
                string timeoutInMinutesString = (phase.Target as QueueTarget)?.TimeoutInMinutes ??
                    (phase.Target as DeploymentTarget)?.TimeoutInMinutes ??
                    "60";
                Timeout = TimeSpan.FromMinutes(int.Parse(timeoutInMinutesString, NumberStyles.None));
            }

            public JobCancelMessage CancelMessage => new JobCancelMessage(RequestMessage.JobId, TimeSpan.FromSeconds(60));

            public string JobName { get; }

            public string PhaseName { get; }

            public AgentJobRequestMessage RequestMessage { get; }

            public TimeSpan Timeout { get; }
        }

        private sealed class PipelineTraceWriter : Yaml.ITraceWriter
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

        private sealed class PipelineFileProvider : Yaml.IFileProvider
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

        private VssCredentials EnsureCredential(CommandSettings command)
        {
            if (command.Offline)
            {
                return null;
            }

            VssCredentials creds;
            // TODO: move to common constants
            string overrideToken = Environment.GetEnvironmentVariable("VSTS_PAT");
            if (overrideToken != null)
            {
                var credentialManager = HostContext.GetService<ICredentialManager>();

                // Create the credential.
                Trace.Info("using override credential for auth: {0}", Constants.Configuration.PAT);
                var credProvider = credentialManager.GetCredentialProvider(Constants.Configuration.PAT);

                VssBasicCredential basicCred = new VssBasicCredential("VstsPi", overrideToken);
                creds = new VssCredentials(null, basicCred, CredentialPromptType.DoNotPrompt);
                return creds;
            }

            if (!_loginStore.IsLoggedIn())
            {
                throw new InvalidOperationException("Must be logged in to run this command");
            }

            ICredentialManager mgr = HostContext.GetService<ICredentialManager>();
            creds = mgr.LoadCredentials();
            Trace.Info("Loaded creds");
            return creds;
        }        
    }   
}