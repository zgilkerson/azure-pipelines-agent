using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AgentService
{
    public enum ExecutionMode
    {
        Process,
        Service
    }

    public class AgentListener
    {
        private ExecutionMode _currentExecutionMode;
        private object ServiceLock { get; set; }
        private Process _listenerProcess;
        private bool Stopping { get; set; }
        private bool _restart = false;
        private const int CTRL_C_EVENT = 0;

        public AgentListener(ExecutionMode mode)
        {
            _currentExecutionMode = mode;
            ServiceLock = new Object();
        }

        public int Run()
        {
            int exitCode = 99;
            try
            {
                bool stopping;
                EventLogger.WriteInfo("Starting VSTS agent listener");
                TimeSpan timeBetweenRetries = TimeSpan.FromSeconds(5);

                lock (ServiceLock)
                {
                    stopping = Stopping;
                }

                while (!stopping)
                {
                    lock (ServiceLock)
                    {
                        CreateAndStartAgentListenerProcess();
                    }
                    _listenerProcess.WaitForExit();
                    exitCode = HandleExitOfListenerProcess(_listenerProcess.ExitCode);
                    if (Stopping)
                    {
                        Stop();
                    }
                    else
                    {
                        // wait for few seconds before restarting the process
                        Thread.Sleep(timeBetweenRetries);
                    }

                    lock (ServiceLock)
                    {
                        DisposeAgentListenerProcess();
                        stopping = Stopping;
                    }
                }
            }
            catch (Exception exception)
            {
                EventLogger.WriteException(exception);
                exitCode = 99;
                Stop();
            }

            return exitCode;
        }

        public void Stop()
        {
            lock (ServiceLock)
            {
                Stopping = true;

                // throw exception during OnStop() will make SCM think the service crash and trigger recovery option.
                // in this way we can self-update the service host.
                if (_currentExecutionMode == ExecutionMode.Service && _restart)
                {
                    throw new Exception(Resource.CrashServiceHost);
                }

                // TODO If agent service is killed make sure AgentListener also is killed
                try
                {
                    if (_listenerProcess != null && !_listenerProcess.HasExited)
                    {
                        // Try to let the agent process know that we are stopping
                        //Attach service process to console of Agent.Listener process. This is needed,
                        //because windows service doesn't use its own console.
                        if (AttachConsole((uint)_listenerProcess.Id))
                        {
                            //Prevent main service process from stopping because of Ctrl + C event with SetConsoleCtrlHandler
                            SetConsoleCtrlHandler(null, true);
                            try
                            {
                                //Generate console event for current console with GenerateConsoleCtrlEvent (processGroupId should be zero)
                                GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);
                                //Wait for the process to finish (give it up to 30 seconds)
                                _listenerProcess.WaitForExit(30000);
                            }
                            finally
                            {
                                //Disconnect from console and restore Ctrl+C handling by main process
                                FreeConsole();
                                SetConsoleCtrlHandler(null, false);
                            }
                        }

                        // if agent is still running, kill it
                        if (!_listenerProcess.HasExited)
                        {
                            _listenerProcess.Kill();
                        }
                    }
                }
                catch (Exception exception)
                {
                    // InvalidOperationException is thrown when there is no process associated to the process object. 
                    // There is no process to kill, Log the exception and shutdown the service. 
                    // If we don't handle this here, the service get into a state where it can neither be stoped nor restarted (Error 1061)
                    EventLogger.WriteException(exception);
                }
            }
        }

        private void CreateAndStartAgentListenerProcess()
        {
            _listenerProcess = CreateAgentListener();
            _listenerProcess.OutputDataReceived += AgentListener_OutputDataReceived;
            _listenerProcess.ErrorDataReceived += AgentListener_ErrorDataReceived;
            _listenerProcess.Start();
            _listenerProcess.BeginOutputReadLine();
            _listenerProcess.BeginErrorReadLine();
        }

        private void DisposeAgentListenerProcess()
        {
            _listenerProcess.OutputDataReceived -= AgentListener_OutputDataReceived;
            _listenerProcess.ErrorDataReceived -= AgentListener_ErrorDataReceived;
            _listenerProcess.Dispose();
            _listenerProcess = null;
        }

        private int HandleExitOfListenerProcess(int exitCode)
        {
            int finalExitCode = exitCode;

            // exit code 0 and 1 need stop service
            // exit code 2 and 3 need restart agent
            switch (exitCode)
            {
                case 0:
                    Stopping = true;
                    EventLogger.WriteInfo(Resource.AgentExitWithoutError);
                    break;
                case 1:
                    Stopping = true;
                    EventLogger.WriteInfo(Resource.AgentExitWithTerminatedError);
                    break;
                case 2:
                    EventLogger.WriteInfo(Resource.AgentExitWithError);
                    break;
                case 3:
                    EventLogger.WriteInfo(Resource.AgentUpdateInProcess);
                    var updateResult = HandleAgentUpdate();
                    if (updateResult == AgentUpdateResult.Succeed)
                    {
                        EventLogger.WriteInfo(Resource.AgentUpdateSucceed);
                    }
                    else if (updateResult == AgentUpdateResult.Failed)
                    {
                        EventLogger.WriteInfo(Resource.AgentUpdateFailed);
                        Stopping = true;
                    }
                    else if (updateResult == AgentUpdateResult.SucceedNeedRestart)
                    {
                        EventLogger.WriteInfo(Resource.AgentUpdateRestartNeeded);
                        _restart = true;                                
                        exitCode = int.MaxValue;
                        Stop();
                    }
                    break;
                default:
                    EventLogger.WriteInfo(Resource.AgentExitWithUndefinedReturnCode);
                    break;
            }

            return finalExitCode;
        }        

        private AgentUpdateResult HandleAgentUpdate()
        {
            // sleep 5 seconds wait for upgrade script to finish
            Thread.Sleep(5000);

            // looking update result record under _diag folder (the log file itself will indicate the result)
            // SelfUpdate-20160711-160300.log.succeed or SelfUpdate-20160711-160300.log.fail
            // Find the latest upgrade log, make sure the log is created less than 15 seconds.
            // When log file named as SelfUpdate-20160711-160300.log.succeedneedrestart, Exit(int.max), during Exit() throw Exception, this will trigger SCM to recovery the service by restart it
            // since SCM cache the ServiceHost in memory, sometime we need update the servicehost as well, in this way we can upgrade the ServiceHost as well.

            DirectoryInfo dirInfo = new DirectoryInfo(GetDiagnosticFolderPath());
            FileInfo[] updateLogs = dirInfo.GetFiles("SelfUpdate-*-*.log.*") ?? new FileInfo[0];
            if (updateLogs.Length == 0)
            {
                // totally wrong, we are not even get a update log.
                return AgentUpdateResult.Failed;
            }
            else
            {
                String latestLogFile = null;
                DateTime latestLogTimestamp = DateTime.MinValue;
                foreach (var logFile in updateLogs)
                {
                    int timestampStartIndex = logFile.Name.IndexOf("-") + 1;
                    int timestampEndIndex = logFile.Name.LastIndexOf(".log") - 1;
                    string timestamp = logFile.Name.Substring(timestampStartIndex, timestampEndIndex - timestampStartIndex + 1);
                    DateTime updateTime;
                    if (DateTime.TryParseExact(timestamp, "yyyyMMdd-HHmmss", null, DateTimeStyles.None, out updateTime) &&
                        updateTime > latestLogTimestamp)
                    {
                        latestLogFile = logFile.Name;
                        latestLogTimestamp = updateTime;
                    }
                }

                if (string.IsNullOrEmpty(latestLogFile) || latestLogTimestamp == DateTime.MinValue)
                {
                    // we can't find update log with expected naming convention.
                    return AgentUpdateResult.Failed;
                }

                if (DateTime.UtcNow - latestLogTimestamp > TimeSpan.FromSeconds(15))
                {
                    // the latest update log we find is more than 15 sec old, the update process is busted.
                    return AgentUpdateResult.Failed;
                }
                else
                {
                    string resultString = Path.GetExtension(latestLogFile).TrimStart('.');
                    AgentUpdateResult result;
                    if (Enum.TryParse<AgentUpdateResult>(resultString, true, out result))
                    {
                        // return the result indicated by the update log.
                        return result;
                    }
                    else
                    {
                        // can't convert the result string, return failed to stop the service.
                        return AgentUpdateResult.Failed;
                    }
                }
            }
        }

        private string GetDiagnosticFolderPath()
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)), "_diag");
        }

        private Process CreateAgentListener()
        {
            var pId = Process.GetCurrentProcess().Id;
            
            string exeLocation = Assembly.GetEntryAssembly().Location;
            string agentExeLocation = Path.Combine(Path.GetDirectoryName(exeLocation), "Agent.Listener.exe");
            Process newProcess = new Process();
            newProcess.StartInfo = new ProcessStartInfo(agentExeLocation, "run");
            newProcess.StartInfo.CreateNoWindow = true;
            newProcess.StartInfo.UseShellExecute = false;
            newProcess.StartInfo.RedirectStandardInput = true;
            newProcess.StartInfo.RedirectStandardOutput = true;
            newProcess.StartInfo.RedirectStandardError = true;
            return newProcess;
        }

        private void AgentListener_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                EventLogger.WriteToEventLog(e.Data, EventLogEntryType.Error);
            }
        }

        private void AgentListener_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                EventLogger.WriteToEventLog(e.Data, EventLogEntryType.Information);
            }
        }

        private enum AgentUpdateResult
        {
            Succeed,
            Failed,
            SucceedNeedRestart,
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

        // Delegate type to be used as the Handler Routine for SetConsoleCtrlHandler
        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);
    }
}