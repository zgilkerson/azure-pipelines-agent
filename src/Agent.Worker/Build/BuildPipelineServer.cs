using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Pipeline.WebApi;
using Microsoft.VisualStudio.Services.Pipeline.WebApi.Clients;
using Microsoft.VisualStudio.Services.Pipeline.WebApi.Contracts;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public class BuildPipelineServer
    {
        private readonly PipelineHttpClient _pipelineHttpClient;
        private Guid _projectId;
        private IExecutionContext context;

        public BuildPipelineServer(VssConnection connection, Guid projectId)
        {
            ArgUtil.NotNull(connection, nameof(connection));
            ArgUtil.NotEmpty(projectId, nameof(projectId));

            _projectId = projectId;
            _pipelineHttpClient = connection.GetClient<PipelineHttpClient>();
        }

        public async Task<PipelineArtifact> AssociateArtifact(
            int buildId,
            string name,
            string type,
            string data,
            Dictionary<string, string> propertiesDictionary,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var artifact = new PipelineArtifact
            {
                Name = name,
                Resource = new ArtifactResource
                {
                    Data = data,
                    Type = type,
                    Properties = propertiesDictionary
                }
            };

            //TODO(omeshp): pipeline api should not need environmentid and attemptId in case of build
            return await _pipelineHttpClient.AddArtifactAsync(artifact, _projectId, buildId, pipelineEnvironmentId: 1, attempt: 1, cancellationToken: cancellationToken);
        }

        public Task<Pipeline.WebApi.Contracts.Pipeline> UpdateBuildNumber(
            int buildId,
            string buildNumber,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromException<Pipeline.WebApi.Contracts.Pipeline>(new NotImplementedException());
        }

        public Task<IEnumerable<string>> AddBuildTag(
            int buildId,
            string buildTag,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromException<IEnumerable<string>>(new NotImplementedException());
        }

        public async Task<List<PipelineArtifact>> GetArtifacts(int buildId)
        {
            return await _pipelineHttpClient.GetPipelineArtifactsAsync(_projectId, buildId);
        }
    }
}
