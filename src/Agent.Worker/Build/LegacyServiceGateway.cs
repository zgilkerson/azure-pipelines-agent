using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Worker.Release;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Pipeline.WebApi.Contracts;
using Microsoft.VisualStudio.Services.WebApi;
using ArtifactResource = Microsoft.TeamFoundation.Build.WebApi.ArtifactResource;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public sealed class LegacyServiceGateway : BaseServiceGateway
    {
        public override string System => "";

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
            BuildServer buildHelper = new BuildServer(connection, projectId);
            var artifact = await buildHelper.AssociateArtifact(buildId, name, type, data, propertiesDictionary, cancellationToken);
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
            var fileContainerFullPath = await base.CopyArtifactAsync(
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
            BuildServer buildServer = new BuildServer(connection, projectId);
            var build = await buildServer.UpdateBuildNumber(buildId, buildNumber, cancellationToken);
            context.Output(StringUtil.Loc("UpdateBuildNumberForBuild", build.BuildNumber, build.Id));
        }

        public override async Task AddBuildTagAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string buildTag,
            CancellationToken cancellationToken)
        {
            BuildServer buildServer = new BuildServer(connection, projectId);
            var tags = await buildServer.AddBuildTag(buildId, buildTag, cancellationToken);

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
            BuildServer buildServer = new BuildServer(connection, projectId);
            var buildArtifacts = await buildServer.GetArtifacts(buildId);
            return ToAgentBuildArtifact(buildArtifacts);
        }

        public override async Task<IEnumerable<AgentArtifactDefinition>> GetReleaseArtifactsFromService(
            VssConnection connection,
            Guid projectId,
            int releaseId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ToAgentArtifactDefinition(await new ReleaseServer(connection, projectId).GetReleaseArtifactsFromService(releaseId, cancellationToken));
        }

        private List<AgentArtifactDefinition> ToAgentArtifactDefinition(
            IEnumerable<ReleaseManagement.WebApi.Contracts.AgentArtifactDefinition> releaseAgentArtifactDefinitions)
        {
            return releaseAgentArtifactDefinitions.Select(
                agentArtifactDefinition => new AgentArtifactDefinition
                {
                    Alias = agentArtifactDefinition.Alias,
                    ArtifactType = ToAgentArtifactType(agentArtifactDefinition.ArtifactType),
                    Details = agentArtifactDefinition.Details,
                    Name = agentArtifactDefinition.Name,
                    Version = agentArtifactDefinition.Version
                }).ToList();
        }

        private AgentArtifactType ToAgentArtifactType(ReleaseManagement.WebApi.Contracts.AgentArtifactType artifactType)
        {
            return (AgentArtifactType) Enum.Parse(typeof (AgentArtifactType), artifactType.ToString(), true);
        }

        private AgentArtifactResource ToAgentArtifactResource(ArtifactResource resource)
        {
            return new AgentArtifactResource
            {
                Data = resource.Data,
                DownloadUrl = resource.DownloadUrl,
                Properties = resource.Properties,
                Type = resource.Type,
                Url = resource.Url
            };
        }

        private List<AgentBuildArtifact> ToAgentBuildArtifact(List<BuildArtifact> artifacts)
        {
            return artifacts.Select(
                artifact => new AgentBuildArtifact
                {
                    Id = artifact.Id,
                    Name = artifact.Name,
                    Resource = ToAgentArtifactResource(artifact.Resource)
                }).ToList();
        }
    }
}