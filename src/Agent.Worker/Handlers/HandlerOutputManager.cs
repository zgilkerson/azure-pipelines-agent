using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.Services.WebApi;
using Pipelines = Microsoft.TeamFoundation.DistributedTask.Pipelines;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Handlers
{
    public sealed class HandlerOutputManager
    {
        private readonly IExecutionContext _executionContext;
        private readonly object _matchersLock = new object();
        private IssueMatcher[] _matchers = Array.Empty<IssueMatcher>();
        private bool _hasMatchers;

        public HandlerOutputManager(IExecutionContext executionContext)
        {
            _executionContext = executionContext;

            executionContext.OnMatcherChanged += OnMatcherChanged;
        }

        public void OnDataReceived(object sender, ProcessDataReceivedEventArgs e)
        {
            var line = e.Data;

            // ##vso commands
            if (!String.IsNullOrEmpty(line) && line.IndexOf("##vso") >= 0)
            {
                // This does not need to be inside of a critical section.
                // The logging queues and command handlers are thread-safe.
                CommandManager.TryProcessCommand(ExecutionContext, line);

                return;
            }

            // Problem matchers
            if (_hasMatchers)
            {
                var matchers = _matchers; // copy the reference

                foreach (var matcher in matchers);
                {
                    // todo: add ReDoS mitigation: take the lock and compare reference when ejecting
                    var issue = matcher.Match(line);

                    if (issue != null)
                    {
                        // todo: log the issue

                        // todo: log the line

                        // reset each matcher
                        foreach (var matcher2 in matchers)
                        {
                            matcher2.Reset();
                        }

                        return;
                    }
                }
            }

            // Regular output
            ExecutionContext.Output(line);
        }

        private void OnMatcherChanged(object sender, MatcherChangedEventArgs e)
        {
            lock (_matchersLock)
            {
                var newMatchers = new List<IssueMatcher>()

                if (e.Configuration.Patterns.Count > 0)
                {
                    newMatchers.Add(new IssueMatcher(e.Configuration));
                }

                newMatchers.AddRange(_matchers.Where(x => !string.Equals(x.Name, e.Configuration.Name, OrdinalIgnoreCase)));

                _matchers = newMatchers.ToArray(); // update the reference
                _hasMatchers = true;
            }
        }
    }
}
