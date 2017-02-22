using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Listener.Capabilities;
using Microsoft.VisualStudio.Services.Agent.Listener.Configuration;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Pipelines;

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
            string yamlFile = command.GetYaml();
            ArgUtil.File(yamlFile, nameof(yamlFile));
            Pipeline pipeline = PipelineParser.LoadAsync(yamlFile);
            if (command.Preview)
            
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