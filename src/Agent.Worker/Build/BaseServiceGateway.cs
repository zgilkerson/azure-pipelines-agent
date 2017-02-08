using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Pipeline.WebApi.Contracts;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public abstract class BaseServiceGateway : AgentService, IServiceGateway
    {
        public abstract string System { get; }

        public abstract Type ExtensionType { get; }

        public abstract Task AssociateArtifactAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string name,
            string type,
            string data,
            Dictionary<string, string> propertiesDictionary,
            CancellationToken cancellationToken);

        public abstract Task UploadArtifactAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            long containerId,
            string containerPath,
            int buildId,
            string name,
            Dictionary<string, string> propertiesDictionary,
            string source,
            CancellationToken cancellationToken);

        public abstract Task UpdateBuildNumberAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string buildNumber,
            CancellationToken cancellationToken);

        public abstract Task AddBuildTagAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string buildTag,
            CancellationToken cancellationToken);

        public async Task<string> CopyArtifactAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            long containerId,
            string containerPath,
            int buildId,
            string name,
            Dictionary<string, string> propertiesDictionary,
            string source,
            CancellationToken cancellationToken)
        {
            FileContainerServer fileContainerHelper = new FileContainerServer(connection, projectId, containerId, containerPath);
            await fileContainerHelper.CopyToContainerAsync(context, source, cancellationToken);
            string fileContainerFullPath = StringUtil.Format($"#/{containerId}/{containerPath}");
            context.Output(StringUtil.Loc("UploadToFileContainer", source, fileContainerFullPath));

            return fileContainerFullPath;
        }

        public abstract Task<IEnumerable<AgentArtifactDefinition>> GetReleaseArtifactsFromService(
            VssConnection connection,
            Guid projectId,
            int releaseId,
            CancellationToken cancellationToken = default(CancellationToken));

        public abstract Task<List<AgentBuildArtifact>> GetArtifacts(VssConnection connection, int buildId, Guid projectId);
    }
}