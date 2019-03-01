using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.Services.Agent.Worker
{
    public sealed class AgentPluginCommandExtension : AgentService, IWorkerCommandExtension
    {
        private bool _enabled = false;

        public Type ExtensionType => typeof(IWorkerCommandExtension);

        public string CommandArea => "agentplugin";

        public bool Enabled
        {
            get
            {
                return _enabled;
            }
            set
            {
                _enabled = value;
            }
        }

        public HostTypes SupportedHostTypes => HostTypes.Build;

        public void ProcessCommand(IExecutionContext context, Command command)
        {
            if (String.Equals(command.Event, WellKnownAgentPluginCommand.UpdateRepository, StringComparison.OrdinalIgnoreCase))
            {
                ProcessAgentPluginUpdateRepositoryCommand(context, command.Properties, command.Data);
            }
            else
            {
                throw new Exception(StringUtil.Loc("AgentPluginCommandNotFound", command.Event));
            }
        }


        private void ProcessAgentPluginUpdateRepositoryCommand(IExecutionContext context, Dictionary<string, string> eventProperties, string data)
        {
            String alias;
            if (!eventProperties.TryGetValue(AgentPluginUpdateRepositoryEventProperties.Alias, out alias) || String.IsNullOrEmpty(alias))
            {
                throw new Exception(StringUtil.Loc("MissingRepositoryAlias"));
            }

            var repository = context.Repositories.FirstOrDefault(x => string.Equals(x.Alias, alias, StringComparison.OrdinalIgnoreCase));
            if (repository == null)
            {
                throw new Exception(StringUtil.Loc("RepositoryNotExist"));
            }

            foreach (var property in eventProperties.Where(x => !string.Equals(x.Key, AgentPluginUpdateRepositoryEventProperties.Alias, StringComparison.OrdinalIgnoreCase)))
            {
                repository.Properties.Set(property.Key, property.Value);
            }
        }
    }

    internal static class WellKnownAgentPluginCommand
    {
        public static readonly String UpdateRepository = "updaterepository";
    }

    internal static class AgentPluginUpdateRepositoryEventProperties
    {
        public static readonly String Alias = "alias";
    }
}