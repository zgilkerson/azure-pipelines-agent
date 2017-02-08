using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Pipeline.WebApi.Clients;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;

namespace Agent.Worker.Release
{
    public class ReleasePipelineServer
    {
        private PipelineHttpClient _pipelineHttpClient { get; }

        public ReleasePipelineServer(Uri projectCollection, VssCredentials credentials, Guid projectId)
        {
            ArgUtil.NotNull(projectCollection, nameof(projectCollection));
            ArgUtil.NotNull(credentials, nameof(credentials));

            _pipelineHttpClient = new PipelineHttpClient(projectCollection, credentials, new VssHttpRetryMessageHandler(3));
        }

        public IEnumerable<AgentArtifactDefinition> GetReleaseArtifactsFromService(
            int releaseId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }
    }
}