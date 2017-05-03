using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Principal;

using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Agent.Worker.Release.Artifacts.Definition;
using Microsoft.VisualStudio.Services.Agent.Worker.Release.ContainerFetchEngine;
using Microsoft.VisualStudio.Services.Agent.Worker.Release.ContainerProvider.Helpers;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;

using Newtonsoft.Json;

using ServerBuildArtifact = Microsoft.TeamFoundation.Build.WebApi.BuildArtifact;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Release.Artifacts
{
    // TODO: Write tests for this
    public class BuildArtifact : AgentService, IArtifactExtension
    {
        public Type ExtensionType => typeof(IArtifactExtension);
        public AgentArtifactType ArtifactType => AgentArtifactType.Build;

        private const string AllArtifacts = "*";

        private bool UseRobocopy { get; set; }

        private bool SystemDebug { get; set; }

        private int RobocopyMT { get; set; }



        public async Task DownloadAsync(IExecutionContext executionContext, ArtifactDefinition artifactDefinition, string localFolderPath)
        {
            ArgUtil.NotNull(artifactDefinition, nameof(artifactDefinition));
            ArgUtil.NotNull(executionContext, nameof(executionContext));
            ArgUtil.NotNullOrEmpty(localFolderPath, nameof(localFolderPath));

            int buildId = Convert.ToInt32(artifactDefinition.Version, CultureInfo.InvariantCulture);
            if (buildId <= 0)
            {
                throw new ArgumentException("artifactDefinition.Version");
            }

            var buildArtifactDetails = artifactDefinition.Details as BuildArtifactDetails;
            if (buildArtifactDetails == null)
            {
                throw new ArgumentException("artifactDefinition.Details");
            }

            // Get the list of available artifacts from build. 
            executionContext.Output(StringUtil.Loc("RMPreparingToGetBuildArtifactList"));

            var vssConnection = new VssConnection(buildArtifactDetails.TfsUrl, buildArtifactDetails.Credentials);
            var buildClient = vssConnection.GetClient<BuildHttpClient>();
            var xamlBuildClient = vssConnection.GetClient<XamlBuildHttpClient>();
            List<ServerBuildArtifact> buildArtifacts = null;

            try
            {
                buildArtifacts = await buildClient.GetArtifactsAsync(buildArtifactDetails.Project, buildId);
            }
            catch (BuildNotFoundException)
            {
                buildArtifacts = await xamlBuildClient.GetArtifactsAsync(buildArtifactDetails.Project, buildId);
            }

            // No artifacts found in the build => Fail it. 
            if (buildArtifacts == null || !buildArtifacts.Any())
            {
                throw new ArtifactDownloadException(StringUtil.Loc("RMNoBuildArtifactsFound", buildId));
            }

            // DownloadFromStream each of the artifact sequentially. 
            // TODO: Should we download them parallely?
            foreach (ServerBuildArtifact buildArtifact in buildArtifacts)
            {
                if (Match(buildArtifact, artifactDefinition))
                {
                    executionContext.Output(StringUtil.Loc("RMPreparingToDownload", buildArtifact.Name));
                    await this.DownloadArtifactAsync(executionContext, buildArtifact, artifactDefinition, localFolderPath);
                }
                else
                {
                    executionContext.Warning(StringUtil.Loc("RMArtifactMatchNotFound", buildArtifact.Name));
                }
            }
        }

        public IArtifactDetails GetArtifactDetails(IExecutionContext context, AgentArtifactDefinition agentArtifactDefinition)
        {
            Trace.Entering();

            ServiceEndpoint vssEndpoint = context.Endpoints.FirstOrDefault(e => string.Equals(e.Name, ServiceEndpoints.SystemVssConnection, StringComparison.OrdinalIgnoreCase));
            ArgUtil.NotNull(vssEndpoint, nameof(vssEndpoint));
            ArgUtil.NotNull(vssEndpoint.Url, nameof(vssEndpoint.Url));

            var artifactDetails = JsonConvert.DeserializeObject<Dictionary<string, string>>(agentArtifactDefinition.Details);
            VssCredentials vssCredentials = ApiUtil.GetVssCredential(vssEndpoint);
            var tfsUrl = context.Variables.Get(WellKnownDistributedTaskVariables.TFCollectionUrl);

            Guid projectId = context.Variables.System_TeamProjectId ?? Guid.Empty;
            if(artifactDetails.ContainsKey("Project"))
            {
                Guid.TryParse(artifactDetails["Project"], out projectId);
            }

            ArgUtil.NotEmpty(projectId, nameof(projectId));

            string relativePath;
            string accessToken;
            vssEndpoint.Authorization.Parameters.TryGetValue(EndpointAuthorizationParameters.AccessToken, out accessToken);

            if (artifactDetails.TryGetValue("RelativePath", out relativePath))
            {
                return new BuildArtifactDetails
                {
                    Credentials = vssCredentials,
                    RelativePath = artifactDetails["RelativePath"],
                    AccessToken = accessToken,
                    Project = projectId.ToString(),
                    TfsUrl = new Uri(tfsUrl),
                };
            }
            else
            {
                throw new InvalidOperationException(StringUtil.Loc("RMArtifactDetailsIncomplete"));
            }
        }

        private bool Match(ServerBuildArtifact buildArtifact, ArtifactDefinition artifactDefinition)
        {
            //TODO: If editing artifactDefinitionName is not allowed then we can remove this
            if (string.Equals(artifactDefinition.Name, AllArtifacts, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(artifactDefinition.Name, buildArtifact.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private async Task DownloadArtifactAsync(
            IExecutionContext executionContext,
            ServerBuildArtifact buildArtifact,
            ArtifactDefinition artifactDefinition,
            string localFolderPath)
        {
            var downloadFolderPath = Path.Combine(localFolderPath, buildArtifact.Name);
            var buildArtifactDetails = artifactDefinition.Details as BuildArtifactDetails;
            if ((buildArtifact.Resource.Type == null && buildArtifact.Id == 0) // bug on build API Bug 378900
                || string.Equals(buildArtifact.Resource.Type, WellKnownArtifactResourceTypes.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                executionContext.Output("Artifact Type: FileShare");
                string fileShare;
                if (buildArtifact.Id == 0)
                {
                    fileShare = new Uri(buildArtifact.Resource.DownloadUrl).LocalPath;
                }
                else
                {
                    fileShare = new Uri(Path.Combine(buildArtifact.Resource.DownloadUrl, buildArtifact.Name)).LocalPath;
                    if (!Directory.Exists(fileShare))
                    {
                        // download path does not exist, log and fall back
                        var parenthPath = new Uri(buildArtifact.Resource.DownloadUrl).LocalPath;
                        executionContext.Output(StringUtil.Loc("RMArtifactNameDirectoryNotFound", fileShare, parenthPath));
                        fileShare = parenthPath;
                    }
                }

                if (!Directory.Exists(fileShare))
                {
                    // download path does not exist, raise exception
                    throw new ArtifactDownloadException(StringUtil.Loc("RMArtifactDirectoryNotFoundError", fileShare, WindowsIdentity.GetCurrent().Name));
                }

                executionContext.Output(StringUtil.Loc("RMDownloadingArtifactFromFileShare", fileShare));

                var fileShareArtifact = new FileShareArtifact();

				//Boolean robocopyEnabled = true;

                //System.Threading.Thread.Sleep(20000);

                UseRobocopy = executionContext.Variables.GetBoolean(Constants.Variables.Release.UseRobocopy) ?? false;

                if (UseRobocopy == true)
                {
                    Task<int> workerProcessTask = null;
                    object _outputLock = new object();
                    List<string> workerOutput = new List<string>();

                    ArgUtil.NotNull(artifactDefinition, nameof(artifactDefinition));
                    ArgUtil.NotNull(executionContext, nameof(executionContext));
                    ArgUtil.NotNullOrEmpty(localFolderPath, nameof(localFolderPath));
                    ArgUtil.NotNullOrEmpty(fileShare, nameof(fileShare));

                    RobocopyMT = executionContext.Variables.GetInt(Constants.Variables.Release.RobocopyMT) ?? 8;
                    SystemDebug = executionContext.Variables.GetBoolean(Constants.Variables.System.Debug) ?? false;

                    if(RobocopyMT < 8)
                    {
                        RobocopyMT = 8;
                    }
                    else if (RobocopyMT > 128)
                    {
                        RobocopyMT = 128;
                    }

                    executionContext.Output("Downloading Artifacts using robocopy");
                    using (var processChannel = HostContext.CreateService<IProcessChannel>())
                    {
                        using (var processInvoker = HostContext.CreateService<IProcessInvoker>())
                        {
                            // Start the process channel.
                            // It's OK if StartServer bubbles an execption after the worker process has already started.
                            // The worker will shutdown after 30 seconds if it hasn't received the job message.

                            processChannel.StartServer(
                                // Delegate to start the child process.
                                startProcess: (string pipeHandleOut, string pipeHandleIn) =>
                                {
                                    // Validate args.
                                    ArgUtil.NotNullOrEmpty(pipeHandleOut, nameof(pipeHandleOut));
                                    ArgUtil.NotNullOrEmpty(pipeHandleIn, nameof(pipeHandleIn));

                                    // Save STDOUT from worker, worker will use STDOUT report unhandle exception.
                                    processInvoker.OutputDataReceived += delegate (object sender, ProcessDataReceivedEventArgs stdout)
                                    {
                                        if (!string.IsNullOrEmpty(stdout.Data))
                                        {
                                            lock (_outputLock)
                                            {
                                                executionContext.Output(stdout.Data);
                                            }
                                        }
                                    };

                                    // Save STDERR from worker, worker will use STDERR on crash.
                                    processInvoker.ErrorDataReceived += delegate (object sender, ProcessDataReceivedEventArgs stderr)
                                    {
                                        if (!string.IsNullOrEmpty(stderr.Data))
                                        {
                                            lock (_outputLock)
                                            {
                                                executionContext.Error(stderr.Data);
                                            }
                                        }
                                    };

                                    var trimChars = new[] { '\\', '/' };
                                    var relativePath = artifactDefinition.Details.RelativePath;

                                    // If user has specified a relative folder in the drop, change the drop location itself. 
                                    fileShare = Path.Combine(fileShare.TrimEnd(trimChars), relativePath.Trim(trimChars));

                                    String robocopyArguments;
                                    // Start the child process.
                                    if (SystemDebug == true)
                                    {
                                        robocopyArguments = fileShare + " " + localFolderPath + " /E /Z /MT:" + RobocopyMT;
                                    }
                                    else
                                    {
                                        robocopyArguments = fileShare + " " + localFolderPath + " /E /Z /NDL /NFL /NP /MT:" + RobocopyMT;
                                    }
                                    workerProcessTask = processInvoker.ExecuteAsync(
                                            workingDirectory: "C:\\",
                                            fileName: "robocopy",
                                            arguments: robocopyArguments,
                                            environment: null,
                                            requireExitCodeZero: false,
                                            outputEncoding: null,
                                            killProcessOnCancel: true,
                                            cancellationToken: executionContext.CancellationToken);
                            });

                            try
                            {
                                await workerProcessTask;
                            }
                            catch (OperationCanceledException)
                            {
                                Trace.Info("worker process has been killed.");
                            }

                            int a = workerProcessTask.Result;
                            bool b = workerProcessTask.IsCompleted;
                        }
                    }
                }
                else
                { 
                    await fileShareArtifact.DownloadArtifactAsync(executionContext, HostContext, artifactDefinition, fileShare, downloadFolderPath);
                }
            }
            else if (buildArtifactDetails != null
                     && string.Equals(buildArtifact.Resource.Type, WellKnownArtifactResourceTypes.Container, StringComparison.OrdinalIgnoreCase))
            {
                executionContext.Output("Artifact Type: ServerDrop");
                // Get containerId and rootLocation for the artifact #/922702/drop
                string[] parts = buildArtifact.Resource.Data.Split(new[] { '/' }, 3);

                if (parts.Length < 3)
                {
                    throw new ArtifactDownloadException(StringUtil.Loc("RMArtifactContainerDetailsNotFoundError", buildArtifact.Name));
                }

                int containerId;
                string rootLocation = parts[2];
                if (!int.TryParse(parts[1], out containerId))
                {
                    throw new ArtifactDownloadException(StringUtil.Loc("RMArtifactContainerDetailsInvaidError", buildArtifact.Name));
                }

                IContainerProvider containerProvider =
                    new ContainerProviderFactory(buildArtifactDetails, rootLocation, containerId, executionContext).GetContainerProvider(
                        WellKnownArtifactResourceTypes.Container);

                string rootDestinationDir = Path.Combine(localFolderPath, rootLocation);
                var containerFetchEngineOptions = new ContainerFetchEngineOptions
                {
                    ParallelDownloadLimit = 4,
                };

                using (var engine = new ContainerFetchEngine.ContainerFetchEngine(containerProvider, rootLocation, rootDestinationDir))
                {
                    engine.ContainerFetchEngineOptions = containerFetchEngineOptions;
                    engine.ExecutionLogger = new ExecutionLogger(executionContext);
                    await engine.FetchAsync(executionContext.CancellationToken);
                }
            }
            else
            {
                executionContext.Warning(StringUtil.Loc("RMArtifactTypeNotSupported", buildArtifact.Resource.Type));
            }
        }
    }
}