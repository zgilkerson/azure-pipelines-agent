using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

namespace Microsoft.VisualStudio.Services.Agent
{
    public sealed class PipelineContext : HostContext
    {
        public PipelineContext(string hostType, string logFile = null): base("pipeline", GetLogPath()) {
            this.RunMode = RunMode.Local;

            // create root dir $HOME .vsts-pi. Here?
            // how to pass in logFile since it needs HomeDir
        }

        public static string EnsureDataPath()
        {
            string home = IOUtil.GetHomePath();
            if (!Directory.Exists(home))
            {
                // TODO: loc, right exception type.
                throw new InvalidOperationException("HOME directory cannot be found");
            }

            string path = Path.Combine(IOUtil.GetHomePath(), ".vsts-pi");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        private static string GetLogPath()
        {
            string fileName = StringUtil.Format("pipeline_{0:yyyyMMdd-HHmmss}-utc.log", DateTime.UtcNow);           
            string path = Path.Combine(EnsureDataPath(), WellKnownDirectory.Diag.ToString(), fileName);
            return path;
        }

        public override string GetDirectory(WellKnownDirectory directory)
        {
            Tracing trace = GetTrace("pipeline");
            string path;
            switch (directory)
            {
                case WellKnownDirectory.Bin:
                    path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    break;

                case WellKnownDirectory.Diag:
                    path = Path.Combine(
                        GetDirectory(WellKnownDirectory.Root),
                        Constants.Path.DiagDirectory);
                    break;

                case WellKnownDirectory.Externals:
                    path = Path.Combine(
                        GetDirectory(WellKnownDirectory.Root),
                        Constants.Path.ExternalsDirectory);
                    break;

                case WellKnownDirectory.LegacyPSHost:
                    throw new NotSupportedException(WellKnownDirectory.LegacyPSHost.ToString());

                case WellKnownDirectory.Root:
                    path = Path.Combine(IOUtil.GetHomePath(), ".vsts-pi");
                    break;

                case WellKnownDirectory.ServerOM:
                    throw new NotSupportedException(WellKnownDirectory.ServerOM.ToString());

                case WellKnownDirectory.Tee:
                    throw new NotSupportedException(WellKnownDirectory.Tee.ToString());

                case WellKnownDirectory.Tasks:
                    path = Path.Combine(
                        GetDirectory(WellKnownDirectory.Work),
                        Constants.Path.TasksDirectory);
                    break;

                case WellKnownDirectory.Update:
                    throw new NotSupportedException(WellKnownDirectory.Update.ToString());

                case WellKnownDirectory.Work:
                    path = Path.Combine(
                        GetDirectory(WellKnownDirectory.Root),
                        Constants.Path.WorkDirectory);
                    break;

                default:
                    throw new NotSupportedException($"Unexpected well known directory: '{directory}'");
            }

            trace.Info($"Well known directory '{directory}': '{path}'");
            return path;
        }        
    }
}