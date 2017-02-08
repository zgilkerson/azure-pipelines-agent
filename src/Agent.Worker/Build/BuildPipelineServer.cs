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

            return await _pipelineHttpClient.AddArtifactAsync(artifact, _projectId, buildId, cancellationToken: cancellationToken);
        }

        public async Task<Pipeline.WebApi.Contracts.Pipeline> UpdateBuildNumber(
            int buildId,
            string buildNumber,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            //TODO(omeshp): Implement the missing api in RM
            return await Task.FromException<Pipeline.WebApi.Contracts.Pipeline>(new NotImplementedException());
        }

        public async Task<IEnumerable<string>> AddBuildTag(
            int buildId,
            string buildTag,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await _pipelineHttpClient.AddPipelineTagAsync(_projectId, buildId, buildTag, cancellationToken: cancellationToken);
        }

        public async Task<List<PipelineArtifact>> GetArtifacts(int buildId)
        {
            return await _pipelineHttpClient.GetPipelineArtifactsAsync(_projectId, buildId);
        }
    }
}
