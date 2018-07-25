using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Agent.Worker.Build;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Worker
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            using (HostContext context = new HostContext("Worker"))
            {
                return MainAsync(context, args).GetAwaiter().GetResult();
            }
        }

        public static async Task<int> MainAsync(IHostContext context, string[] args)
        {
            //ITerminal registers a CTRL-C handler, which keeps the Agent.Worker process running
            //and lets the Agent.Listener handle gracefully the exit.
            var term = context.GetService<ITerminal>();
            Tracing trace = context.GetTrace(nameof(Program));
            try
            {
                trace.Info($"Version: {Constants.Agent.Version}");
                trace.Info($"Commit: {BuildConstants.Source.CommitHash}");
                trace.Info($"Culture: {CultureInfo.CurrentCulture.Name}");
                trace.Info($"UI Culture: {CultureInfo.CurrentUICulture.Name}");

                var git = context.GetService<IGitCommandManager>();

                while (true)
                {
                    await git.LoadGitExecutionInfo(term, true);
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                // Populate any exception that cause worker failure back to agent.
                Console.WriteLine(ex.ToString());
                try
                {
                    trace.Error(ex);
                }
                catch (Exception e)
                {
                    // make sure we don't crash the app on trace error.
                    // since IOException will throw when we run out of disk space.
                    Console.WriteLine(e.ToString());
                }
            }

            return 1;
        }
    }
}
