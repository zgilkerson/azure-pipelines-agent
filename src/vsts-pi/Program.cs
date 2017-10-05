using Microsoft.VisualStudio.Services.Agent;
using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Pipeline
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // we need to ensure enough structure is in place to even log.
            try
            {
                PipelineContext.EnsureDataPath();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return 1;
            }
            

            using (PipelineContext context = new PipelineContext("Pipeline"))
            {
                return MainAsync(context, args).GetAwaiter().GetResult();
            }
        }

        public static async Task<int> MainAsync(PipelineContext context, string[] args)
        {
            //ITerminal registers a CTRL-C handler, which keeps the Agent.Worker process running
            //and lets the Agent.Listener handle gracefully the exit.
            var terminal = context.GetService<ITerminal>();
            Tracing trace = context.GetTrace("vsts-pl");
            try
            {
                trace.Info($"Version: {Constants.Agent.Version}");
                trace.Info($"Commit: {BuildConstants.Source.CommitHash}");
                trace.Info($"Culture: {CultureInfo.CurrentCulture.Name}");
                trace.Info($"UI Culture: {CultureInfo.CurrentUICulture.Name}");

// TODO: Loc
                string bannerFormat =
@"

Visual Studio Team Services (R) {0} version {1}
Copyright (C) Microsoft Corporation. All rights reserved.

Caution: Tool is in early preview and subject to change.

";
                terminal.WriteLine(StringUtil.Format(bannerFormat, "Pipeline", Constants.Agent.Version));

                // Validate args.
                // ArgUtil.NotNull(args, nameof(args));
                // ArgUtil.Equal(3, args.Length, nameof(args.Length));
                // ArgUtil.NotNullOrEmpty(args[0], $"{nameof(args)}[0]");
                // ArgUtil.Equal("spawnclient", args[0].ToLowerInvariant(), $"{nameof(args)}[0]");
                // ArgUtil.NotNullOrEmpty(args[1], $"{nameof(args)}[1]");
                // ArgUtil.NotNullOrEmpty(args[2], $"{nameof(args)}[2]");

                // Parse the command line args.
                var command = new CommandSettings(context, args);
                trace.Info("Arguments parsed");

                // Up front validation, warn for unrecognized commandline args.
                var unknownCommandlines = command.ValidateCommands();
                if (unknownCommandlines.Count > 0)
                {
                    terminal.WriteError(StringUtil.Loc("UnrecognizedCmdArgs", string.Join(", ", unknownCommandlines)));
                }                

                // Defer to the pipeline class to execute the command.
                IPipelineCommander commander = context.GetService<IPipelineCommander>();
                try
                {
                    return await commander.RunAsync(command);
                }
                catch (OperationCanceledException) when (context.AgentShutdownToken.IsCancellationRequested)
                {
                    trace.Info("Pipeline execution been cancelled.");
                    return Constants.Agent.ReturnCode.Success;
                }
                catch (NonRetryableException e)
                {
                    terminal.WriteError(StringUtil.Loc("ErrorOccurred", e.Message));
                    trace.Error(e);
                    return Constants.Agent.ReturnCode.TerminatedError;
                }
            }
            catch (Exception e)
            {
                terminal.WriteError(StringUtil.Loc("ErrorOccurred", e.Message));
                trace.Error(e);
                return Constants.Agent.ReturnCode.RetryableError;
            }
        }
    }
}
