using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Pipeline.WebApi.Contracts;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public sealed class PipelineServiceGateway : BaseServiceGateway
    {
        public override string System => "pipeline";

        public override Type ExtensionType => typeof(IServiceGateway);

        public override async Task AssociateArtifactAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string name,
            string type,
            string data,
            Dictionary<string, string> propertiesDictionary,
            CancellationToken cancellationToken)
        {
            var buildPipelineServer = new BuildPipelineServer(connection, projectId);
            var artifact = await buildPipelineServer.AssociateArtifact(buildId, name, type, data, propertiesDictionary, cancellationToken);
            context.Output(StringUtil.Loc("AssociateArtifactWithBuild", artifact.Id, buildId));
        }

        public override async Task UploadArtifactAsync(
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
            string fileContainerFullPath = await base.CopyArtifactAsync(
                context,
                connection,
                projectId,
                containerId,
                containerPath,
                buildId,
                name,
                propertiesDictionary,
                source,
                cancellationToken);

            await AssociateArtifactAsync(
                context,
                connection,
                projectId,
                buildId,
                name,
                WellKnownArtifactResourceTypes.Container,
                fileContainerFullPath,
                propertiesDictionary,
                cancellationToken);
        }

        public override async Task UpdateBuildNumberAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string buildNumber,
            CancellationToken cancellationToken)
        {
            var buildPipelineServer = new BuildPipelineServer(connection, projectId);
            var pipeline = await buildPipelineServer.UpdateBuildNumber(buildId, buildNumber, cancellationToken);
            context.Output(StringUtil.Loc("UpdateBuildNumberForBuild", pipeline.Name, pipeline.Id));
        }

        public override async Task AddBuildTagAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string buildTag,
            CancellationToken cancellationToken)
        {
            var buildPipelineServer = new BuildPipelineServer(connection, projectId);
            var tags = await buildPipelineServer.AddBuildTag(buildId, buildTag, cancellationToken);

            if (tags == null || !tags.Contains(buildTag))
            {
                throw new Exception(StringUtil.Loc("BuildTagAddFailed", buildTag));
            }
            else
            {
                context.Output(StringUtil.Loc("BuildTagsForBuild", buildId, String.Join(", ", tags)));
            }
        }

        public override async Task<List<AgentBuildArtifact>> GetArtifacts(VssConnection connection, int buildId, Guid projectId)
        {
            BuildPipelineServer buildServer = new BuildPipelineServer(connection, projectId);
            var buildArtifacts = await buildServer.GetArtifacts(buildId);
            return ToAgentBuildArtifact(buildArtifacts);
        }

        private List<AgentBuildArtifact> ToAgentBuildArtifact(List<PipelineArtifact> artifacts)
        {
            return artifacts.Select(
                artifact => new AgentBuildArtifact
                {
                    Id = artifact.Id,
                    Name = artifact.Name,
                    Resource = ToAgentArtifactResource(artifact.Resource)
                }).ToList();
        }

        private AgentArtifactResource ToAgentArtifactResource(Pipeline.WebApi.Contracts.ArtifactResource resource)
        {
            return new AgentArtifactResource
            {
                Data = resource.Data,
                DownloadUrl = resource.DownloadUri,
                Properties = resource.Properties,
                Type = resource.Type,
                Url = resource.Uri
            };
        }
    }
}