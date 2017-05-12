using System;
using System.ServiceProcess;
using System.Diagnostics;
using System.ComponentModel;

namespace AgentService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static int Main(String[] args)
        {
            if (args != null && args.Length == 1)
            {
                if(args[0].Equals("init", StringComparison.InvariantCultureIgnoreCase))
                {
                    return SetupEventSource();
                }

                if(args[0].Equals("runAsProcess", StringComparison.InvariantCultureIgnoreCase))
                {
                    //Not attaching the Ctrl+C event handler on this process
                    LaunchAgentListener();
                    //togo: log
                }
            }

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new AgentService(args.Length > 0 ? args[0] : "VstsAgentService")
            };
            ServiceBase.Run(ServicesToRun);

            return 0;
        }

        public static void LaunchAgentListener()
        {
            AgentListener agentListener = null;
            while(true)
            {
                try
                {
                    agentListener = new AgentListener(ExecutionMode.Process);
                    EventLogger.WriteInfo("Starting VSTS Agent Process");                
                    agentListener.Run();
                }
                catch(Exception ex)
                {
                    EventLogger.WriteException(ex);
                    if(agentListener != null)
                    {
                        agentListener.Stop();
                    }
                }
            }
        }

        public static int SetupEventSource()
        {
            // TODO: LOC all strings.
            if (!EventLog.Exists("Application"))
            {
                Console.WriteLine("[ERROR] Application event log doesn't exist on current machine.");
                return 1;
            }

            EventLog applicationLog = new EventLog("Application");
            if (applicationLog.OverflowAction == OverflowAction.DoNotOverwrite)
            {
                Console.WriteLine("[WARNING] The retention policy for Application event log is set to \"Do not overwrite events\".");
                Console.WriteLine("[WARNING] Make sure manually clear logs as needed, otherwise AgentService will stop writing output to event log.");
            }

            try
            {
                EventLog.WriteEntry(EventLogger.EventSourceName, "create event log trace source for vsts-agent service", EventLogEntryType.Information, 100);
                return 0;
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine("[ERROR] Unable to create '{0}' event source under 'Application' event log.", EventLogger.EventSourceName);
                Console.WriteLine("[ERROR] {0}",ex.Message);
                Console.WriteLine("[ERROR] Error Code: {0}", ex.ErrorCode);
                return 1;
            }
        }
    }
}
