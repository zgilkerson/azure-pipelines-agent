using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Agent.Worker.Build;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
                    await Task.Delay(1000);
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

            try
            {
                while (true)
                {
                    var gitPath = Path.Combine(context.GetDirectory(WellKnownDirectory.Externals), "git", "cmd", $"git{IOUtil.ExeExtension}");
                    Process runGit = new Process();
                    runGit.StartInfo.FileName = gitPath;
                    runGit.StartInfo.Arguments = "version";
                    runGit.StartInfo.UseShellExecute = false;
                    runGit.StartInfo.RedirectStandardOutput = true;
                    runGit.StartInfo.RedirectStandardError = true;
                    runGit.Start();
                    var stdout = runGit.StandardOutput.ReadToEnd();
                    var stderr = runGit.StandardError.ReadToEnd();
                    runGit.WaitForExit();
                    Console.WriteLine($"Exitcode: {runGit.ExitCode}, STDOUT: {stdout}, STDERR: {stderr}");
                    runGit.Close();
                    if (string.IsNullOrEmpty(stdout))
                    {
                        throw new InvalidOperationException("Git STDOUT is empty!");
                    }
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            try
            {
                while (true)
                {
                    var gitPath = Path.Combine(context.GetDirectory(WellKnownDirectory.Externals), "git", "cmd", $"git{IOUtil.ExeExtension}");
                    string stdout = "";
                    string stderr = "";
                    Process runGit = new Process();
                    runGit.StartInfo.FileName = gitPath;
                    runGit.StartInfo.Arguments = "version";
                    runGit.StartInfo.UseShellExecute = false;
                    runGit.StartInfo.RedirectStandardOutput = true;
                    runGit.StartInfo.RedirectStandardError = true;
                    runGit.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                    {
                        if (!String.IsNullOrEmpty(e.Data))
                        {
                            stdout = stdout + e.Data;
                        }
                        else
                        {
                            trace.Info("Get empty data from STDOUT");
                        }
                    });
                    runGit.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                    {
                        if (!String.IsNullOrEmpty(e.Data))
                        {
                            stderr = stderr + e.Data;
                        }
                        else
                        {
                            trace.Info("Get empty data from STDERR");
                        }
                    });

                    runGit.Start();

                    runGit.BeginOutputReadLine();
                    runGit.BeginErrorReadLine();
                    runGit.WaitForExit();

                    Console.WriteLine($"Exitcode: {runGit.ExitCode}, STDOUT: {stdout}, STDERR: {stderr}");

                    runGit.Close();
                    if (string.IsNullOrEmpty(stdout))
                    {
                        throw new InvalidOperationException("Git STDOUT is empty!");
                    }
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return 1;
        }
    }
}
