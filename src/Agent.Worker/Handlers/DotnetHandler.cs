using Microsoft.VisualStudio.Services.Agent.Util;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Services.Agent.Worker.Container;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Handlers
{
    [ServiceLocator(Default = typeof(DotnetHandler))]
    public interface IDotnetHandler : IHandler
    {
        DotnetHandlerData Data { get; set; }
    }

    public sealed class DotnetHandler : Handler, IDotnetHandler
    {
        public DotnetHandlerData Data { get; set; }

        public async Task RunAsync()
        {
            // Validate args.
            Trace.Entering();
            ArgUtil.NotNull(Data, nameof(Data));
            ArgUtil.NotNull(ExecutionContext, nameof(ExecutionContext));
            ArgUtil.NotNull(Inputs, nameof(Inputs));
            ArgUtil.Directory(TaskDirectory, nameof(TaskDirectory));

            // Update the env dictionary.
            AddInputsToEnvironment();
            AddEndpointsToEnvironment();
            AddSecureFilesToEnvironment();
            AddVariablesToEnvironment();
            AddTaskVariablesToEnvironment();
            AddPrependPathToEnvironment();

            // Resolve the task host assembly.
            string taskHost = Path.Combine(TaskDirectory, "VstsTaskHost.dll");
            ArgUtil.File(taskHost, nameof(taskHost));

            // Resolve the target task assembly.
            string target = Data.Target;
            ArgUtil.NotNullOrEmpty(target, nameof(target));
            target = Path.Combine(TaskDirectory, target);
            ArgUtil.File(target, nameof(target));

            // Resolve the working directory.
            string workingDirectory = TaskDirectory;

            // Setup the process invoker.
            using (var processInvoker = HostContext.CreateService<IProcessInvoker>())
            {
                processInvoker.OutputDataReceived += OnDataReceived;
                processInvoker.ErrorDataReceived += OnDataReceived;

                string file;
                string arguments;
                file = Path.Combine(IOUtil.GetExternalsPath(), "dotnet", $"dotnet{IOUtil.ExeExtension}");

                // Format the arguments passed to node.
                // 1) Wrap the script file path in double quotes.
                // 2) Escape double quotes within the script file path. Double-quote is a valid
                // file name character on Linux.
                arguments = StringUtil.Format(@"""{0}"" ""{1}"" ""{2}""", taskHost.Replace(@"""", @"\"""), target, Data.TaskEntryPoint);

                // Execute the process. Exit code 0 should always be returned.
                // A non-zero exit code indicates infrastructural failure.
                // Task failure should be communicated over STDOUT using ## commands.
                await processInvoker.ExecuteAsync(
                    workingDirectory: workingDirectory,
                    fileName: file,
                    arguments: arguments,
                    environment: Environment,
                    requireExitCodeZero: true,
                    cancellationToken: ExecutionContext.CancellationToken);
            }
        }

        private void OnDataReceived(object sender, ProcessDataReceivedEventArgs e)
        {
            // This does not need to be inside of a critical section.
            // The logging queues and command handlers are thread-safe.
            if (!CommandManager.TryProcessCommand(ExecutionContext, e.Data))
            {
                ExecutionContext.Output(e.Data);
            }
        }
    }
}
