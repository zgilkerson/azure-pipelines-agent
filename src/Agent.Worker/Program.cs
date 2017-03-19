using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Net;

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

                    IPAddress agentIP;
                    if (!IPAddress.TryParse(args[0], out agentIP))
                    {
                        throw new ArgumentException(nameof(IPEndPoint.Address));
                    }

                    Int32 agentPort;
                    if (!Int32.TryParse(args[1], out agentPort))
                    {
                        throw new ArgumentException(nameof(IPEndPoint.Port));
                    }
                    var worker = hc.GetService<IWorker>();

                    // Run the worker.
                    return await worker.RunAsync(new IPEndPoint(agentIP, agentPort));
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
