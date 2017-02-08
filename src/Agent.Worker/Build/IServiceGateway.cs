using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Pipeline.WebApi.Contracts;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public interface IServiceGateway : IExtension
    {
        string System { get; }

        Task AssociateArtifactAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string name,
            string type,
            string data,
            Dictionary<string, string> propertiesDictionary,
            CancellationToken cancellationToken);

        Task UploadArtifactAsync(
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

        Task UpdateBuildNumberAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string buildNumber,
            CancellationToken cancellationToken);

        Task AddBuildTagAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string buildTag,
            CancellationToken cancellationToken);

        Task<string> CopyArtifactAsync(
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

        Task<IEnumerable<AgentArtifactDefinition>> GetReleaseArtifactsFromService(
            VssConnection connection,
            Guid projectId,
            int releaseId,
            CancellationToken cancellationToken = default(CancellationToken));

        Task<List<AgentBuildArtifact>> GetArtifacts(
            VssConnection connection,
            int buildId,
            Guid projectId);
    }
}