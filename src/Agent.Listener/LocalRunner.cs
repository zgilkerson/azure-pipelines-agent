using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Pipelines;
using YamlDotNet.Serialization;

namespace Microsoft.VisualStudio.Services.Agent.Listener
{
    [ServiceLocator(Default = typeof(LocalRunner))]
    public interface ILocalRunner : IAgentService
    {
        Task<int> RunAsync(CommandSettings command, AgentSettings settings, CancellationToken token);
    }

    public sealed class LocalRunner : AgentService, ILocalRunner
    {
        public async Task<int> RunAsync(CommandSettings command, AgentSettings settings, CancellationToken token)
        {
            Trace.Info(nameof(RunAsync));
            var terminal = HostContext.GetService<ITerminal>();
            string yamlFile = command.GetYaml();
            ArgUtil.File(yamlFile, nameof(yamlFile));
            var pipeline = PipelineParser.LoadAsync(yamlFile);
            ArgUtil.NotNull(pipeline, nameof(pipeline));
            if (command.WhatIf)
            {
                var yamlSerializer = new Serializer();
                terminal.WriteLine(yamlSerializer.Serialize(pipeline));
                return 0;
            }
            
            IJobDispatcher jobDispatcher = null;
            try
            {
                jobDispatcher = HostContext.CreateService<IJobDispatcher>();
                AgentJobRequestMessage newJobMessage = null; // JsonUtility.FromString<AgentJobRequestMessage>(message.Body);
                jobDispatcher.Run(newJobMessage);
                await jobDispatcher.WaitAsync(token);
            }
            finally
            {
                if (jobDispatcher != null)
                {
                    await jobDispatcher.ShutdownAsync();
                }
            }

            return Constants.Agent.ReturnCode.Success;
        }
    }
}