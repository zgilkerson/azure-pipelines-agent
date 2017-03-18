using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Worker
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }

        public static async Task<int> MainAsync(
            string[] args)
        {
            //ITerminal registers a CTRL-C handler, which keeps the Agent.Worker process running
            //and lets the Agent.Listener handle gracefully the exit.
            using (var hc = new HostContext("Worker"))
            using (var term = hc.GetService<ITerminal>())
            {
                Tracing trace = hc.GetTrace(nameof(Program));
                try
                {
                    trace.Info($"Version: {Constants.Agent.Version}");
                    trace.Info($"Commit: {BuildConstants.Source.CommitHash}");
                    trace.Info($"Culture: {CultureInfo.CurrentCulture.Name}");
                    trace.Info($"UI Culture: {CultureInfo.CurrentUICulture.Name}");

                    // Validate args.
                    ArgUtil.NotNull(args, nameof(args));
                    ArgUtil.Equal(2, args.Length, nameof(args.Length));
                    ArgUtil.NotNullOrEmpty(args[0], $"{nameof(args)}[0]");
                    ArgUtil.NotNullOrEmpty(args[1], $"{nameof(args)}[1]");

                    var worker = hc.GetService<IWorker>();

                    // Run the worker.
                    return await worker.RunAsync(
                        pipeIn: args[0],
                        pipeOut: args[1]);
                }
                catch (Exception ex)
                {
                    trace.Error(ex);
                }
                finally
                {
                    hc.Dispose();
                }

                return 1;
            }
        }
    }
}
