using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Clients;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;
using Microsoft.VisualStudio.Services.WebApi;

namespace Agent.Worker.Release
{
    public class ReleaseServer
    {
        private readonly Guid _projectId;

        private readonly ReleaseHttpClient _releaseHttpClient;

        public ReleaseServer(VssConnection connection, Guid projectId)
        {
            ArgUtil.NotNull(connection, nameof(connection));

            _projectId = projectId;
            _releaseHttpClient = connection.GetClient<ReleaseHttpClient>();
        }

        public async Task<IEnumerable<AgentArtifactDefinition>> GetReleaseArtifactsFromService(int releaseId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await _releaseHttpClient.GetAgentArtifactDefinitionsAsync(_projectId, releaseId, cancellationToken: cancellationToken);
        }
    }
}