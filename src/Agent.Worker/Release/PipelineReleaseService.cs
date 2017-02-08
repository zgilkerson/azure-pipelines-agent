using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Pipeline.WebApi.Clients;
using Microsoft.VisualStudio.Services.Pipeline.WebApi.Contracts;
using Microsoft.VisualStudio.Services.WebApi;

namespace Agent.Worker.Release
{
    public class ReleasePipelineServer
    {
        private readonly Guid _projectId;

        private PipelineHttpClient _pipelineHttpClient { get; }

        public ReleasePipelineServer(VssConnection connection, Guid projectId)
        {
            ArgUtil.NotNull(connection, nameof(connection));

            _projectId = projectId;
            _pipelineHttpClient = connection.GetClient<PipelineHttpClient>();
        }

        public async Task<IEnumerable<AgentArtifactDefinition>> GetReleaseArtifactsFromService(
            int releaseId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await _pipelineHttpClient.GetAgentArtifactDefinitionsAsync(_projectId, releaseId, cancellationToken: cancellationToken);
        }
    }
}