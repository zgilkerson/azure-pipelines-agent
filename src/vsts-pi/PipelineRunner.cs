using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.VisualStudio.Services.Agent
{
    [ServiceLocator(Default = typeof(PipelineRunner))]
    public interface IPipelineRunner : IAgentService
    {
        Task<int> RunAsync(CommandSettings command);
    }

    public sealed class PipelineRunner : AgentService, IPipelineRunner
    {
        private ITerminal _term;
        private ManualResetEvent _completedCommand = new ManualResetEvent(false);

        public sealed override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);
            _term = hostContext.GetService<ITerminal>();
        }

        public async Task<int> RunAsync(CommandSettings command)
        {
            try
            {
                Trace.Info(nameof(RunAsync));
                var agentWebProxy = HostContext.GetService<IVstsAgentWebProxy>();
                VssHttpMessageHandler.DefaultWebProxy = agentWebProxy;

                _completedCommand.Reset();
                _term.CancelKeyPress += CtrlCHandler;

                //register a SIGTERM handler
                HostContext.Unloading += Agent_Unloading;
                
                var yamlRunner = HostContext.GetService<IYamlRunner>();

                if (command.Help)
                {
                    PrintUsage(command);
                    return Constants.Agent.ReturnCode.Success;
                }

                if (command.Version)
                {
                    _term.WriteLine(Constants.Agent.Version);
                    return Constants.Agent.ReturnCode.Success;
                }

                if (command.Commit)
                {
                    _term.WriteLine(BuildConstants.Source.CommitHash);
                    return Constants.Agent.ReturnCode.Success;
                }

                if (command.Login)
                {
                    _term.WriteLine("TODO: Login");
                    return Constants.Agent.ReturnCode.Success;                    
                }

                if (command.Logout)
                {
                    _term.WriteLine("TODO: Logout");
                    return Constants.Agent.ReturnCode.Success;                    
                }

                if (command.Validate)
                {
                    await yamlRunner.ValidateAsync(command, HostContext.AgentShutdownToken);
                    return Constants.Agent.ReturnCode.Success;
                }

                if (command.Run)
                {
                    await yamlRunner.RunAsync(command, HostContext.AgentShutdownToken);
                    return Constants.Agent.ReturnCode.Success;
                }

                // if no command, print usage and return 1
                PrintUsage(command);
                return Constants.Agent.ReturnCode.TerminatedError;

            }
            catch (Exception ex)
            {
                Trace.Error(ex);
                _term.WriteError(ex.Message);
                return Constants.Agent.ReturnCode.TerminatedError;
            }            
            finally
            {
                _term.CancelKeyPress -= CtrlCHandler;
                HostContext.Unloading -= Agent_Unloading;
                _completedCommand.Set();
            }                
        }

        private void Agent_Unloading(object sender, EventArgs e)
        {
            HostContext.ShutdownAgent(ShutdownReason.UserCancelled);
            _completedCommand.WaitOne(Constants.Agent.ExitOnUnloadTimeout);
        }

        private void CtrlCHandler(object sender, EventArgs e)
        {
            _term.WriteLine("Exiting...");
            HostContext.Dispose();
            Environment.Exit(Constants.Agent.ReturnCode.TerminatedError);
        }

        private void PrintUsage(CommandSettings command)
        {
            string separator;
#if OS_WINDOWS
            separator = "\\";
#else
            separator = "/";
#endif

            string commonHelp = StringUtil.Loc("CommandLineHelp_Common");
            _term.WriteLine(StringUtil.Loc("CommandLineHelp_PL", separator, commonHelp));

            // TODO: per command help and examples
            // if (command.Run)
            // {
            //     _term.WriteLine(StringUtil.Loc("CommandLineHelp_PL_Run"));
            // }
            // else if (command.Validate)
            // {
            //     _term.WriteLine(StringUtil.Loc("CommandLineHelp_PL_Validate"));
            // }
            // else if (command.Login)
            // {
            //     _term.WriteLine(StringUtil.Loc("CommandLineHelp_PL_Login"));
            // }
            // else if (command.Logout)
            // {
            //     _term.WriteLine(StringUtil.Loc("CommandLineHelp_PL_Logout"));
            // }             
            // else
            // {
            //     _term.WriteLine(StringUtil.Loc("CommandLineHelp_PL", separator, commonHelp));
            // }
        }        
    }
}
