using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Configuration
{
    public interface IConfigurationProvider : IExtension
    {
        void InitConnection(IAgentServer agentServer);

        void InitConnectionWithCollection(CommandSettings command, string tfsUrl, VssCredentials creds);

        Task<int> GetPoolId(CommandSettings command);

        Task<TaskAgent> UpdateAgentAsync(int poolId, TaskAgent agent);

        Task<TaskAgent> AddAgentAsync(int poolId, TaskAgent agent);
    }

    public abstract class ConfigurationProvider : AgentService
    {
        public Type ExtensionType => typeof(IConfigurationProvider);
        protected ITerminal Term;
        protected IAgentServer AgentServer;

        public override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);
            Term = hostContext.GetService<ITerminal>();
        }

        protected async Task<TaskAgent> UpdateAgent(int poolId, TaskAgent agent)
        {
           return  await AgentServer.UpdateAgentAsync(poolId, agent);
        }

        protected async Task<TaskAgent> AddAgent(int poolId, TaskAgent agent)
        {
            return await AgentServer.AddAgentAsync(poolId, agent);
        }

        protected void InitializeServerConnection(IAgentServer agentServer)
        {
            AgentServer = agentServer;
        }
    }

    public sealed class AutomationAgentConfiguration : ConfigurationProvider, IConfigurationProvider
    {
        public void InitConnection(IAgentServer agentServer)
        {
            InitializeServerConnection(agentServer);
        }
        
        private async Task<int> GetPoolIdAsync(string poolName)
        {
            int id = 0;
            List<TaskAgentPool> pools = await AgentServer.GetAgentPoolsAsync(poolName);
            Trace.Verbose("Returned {0} pools", pools.Count);

            if (pools.Count == 1)
            {
                id = pools[0].Id;
                Trace.Info("Found pool {0} with id {1}", poolName, id);
            }

            return id;
        }

        public async Task<int> GetPoolId(CommandSettings command)
        {
            int poolId = 0;
            string poolName;
            while (true)
            {
                poolName = command.GetPool();
                try
                {
                    poolId = await GetPoolIdAsync(poolName);
                }
                catch (Exception e) when (!command.Unattended)
                {
                    Term.WriteError(e);
                }

                if (poolId > 0)
                {
                    break;
                }

                Term.WriteError(StringUtil.Loc("FailedToFindPool"));
            }
            return poolId;            
        }

        public Task<TaskAgent> UpdateAgentAsync(int poolId, TaskAgent agent)
        {
            return UpdateAgent(poolId, agent);
        }

        public Task<TaskAgent> AddAgentAsync(int poolId, TaskAgent agent)
        {
            return AddAgent(poolId, agent);
        }

        public void InitConnectionWithCollection(CommandSettings command, string tfsUrl, VssCredentials creds)
        {
            // No implementation required
        }
    }

    public sealed class DeploymentAgentConfiguration : ConfigurationProvider, IConfigurationProvider
    {
        private IAgentServer _collectionAgentServer;
        public void InitConnection(IAgentServer agentServer)
        {
            InitializeServerConnection(agentServer);

            // Init it with default agent server, if collection flow is required, InitConnectionWithCollection() will take care!
            _collectionAgentServer = agentServer;
        }

        public async Task<int> GetPoolId(CommandSettings command)
        {
            int poolId = 0;
            while (true)
            {
                string projectName = command.GetProjectName();
                string queueName = command.GetQueueName();
                try
                {
                    poolId =  await GetPoolIdAsync(projectName, queueName);
                }
                catch (Exception e) when (!command.Unattended)
                {
                    Term.WriteError(e);
                }

                if (poolId > 0)
                {
                    break;
                }

                Term.WriteError(StringUtil.Loc("FailedToFindPool"));
            }
            
            return poolId;
        }

        private async Task<int> GetPoolIdAsync(string projectName, string queueName)
        {
            int poolId = 0;
            List<TaskAgentQueue> queue = await _collectionAgentServer.GetAgentQueuesAsync(projectName,queueName);
            Trace.Verbose("Returned {0} queue", queue.Count);

            if (queue.Count == 1)
            {
                int queueId = queue[0].Id;
                Trace.Info("Found queue {0} with id {1}", queueName, queueId);
                poolId = queue[0].Pool.Id;
                Trace.Info("Found poolId {0} with queueName {1}", poolId, queueName);
            }

            return poolId;
        }

        public Task<TaskAgent> UpdateAgentAsync(int poolId, TaskAgent agent)
        {
            return UpdateAgent(poolId, agent);
        }

        public Task<TaskAgent> AddAgentAsync(int poolId, TaskAgent agent)
        {
            return AddAgent(poolId, agent);
        }

        public void InitConnectionWithCollection(CommandSettings command, string tfsUrl, VssCredentials creds)
        {
            string collectionName;
            string tfsUrlWithCollection;

            Trace.Info("Get the Collection name for tfs and validate the connection");
            // No need to loop for cread, as creds are already validated with ConfigManager!
            while (true)
            {
                // Get the Collection Name
                collectionName = command.GetCollectionName("DefaultCollection");
                if (!tfsUrl.EndsWith("/"))
                {
                    tfsUrl += "/";
                }
                tfsUrlWithCollection = new Uri(new Uri(tfsUrl), collectionName).ToString();
                try
                {
                    // Validate can connect.
                    Task connectTask = TestConnectAsync(tfsUrlWithCollection, creds);
                    Task.WaitAll(connectTask);
                    Trace.Info("Connect complete.");
                    break;
                }
                catch (Exception e) when (!command.Unattended)
                {
                    Term.WriteError(e);
                    Term.WriteError(StringUtil.Loc("FailedToConnect"));
                }
            }
        }

        private async Task TestConnectAsync(string url, VssCredentials creds)
        {
            Term.WriteLine(StringUtil.Loc("ConnectingToServer"));
            VssConnection connection = ApiUtil.CreateConnection(new Uri(url), creds);

            _collectionAgentServer = HostContext.CreateService<IAgentServer>();
            await _collectionAgentServer.ConnectAsync(connection);
        }
    }

}
