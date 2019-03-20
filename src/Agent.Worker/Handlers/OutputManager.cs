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
    public sealed class OutputManager
    {
        private const int _maxAttempts = 3;
        private readonly IExecutionContext _executionContext;
        private readonly object _matchersLock = new object();
        private IssueMatcher[] _matchers = Array.Empty<IssueMatcher>();
        private bool _hasMatchers;

        public OutputManager(IExecutionContext executionContext)
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
                    Issue issue = null;
                    for (var attempt = 1; attempt <= _maxAttempts; i++)
                    {
                        try
                        {
                            // Max
                            issue = matcher.Match(line);
                        }
                        catch (MatchException ex) // todo: specific exception
                        {
                            if (attempt < _maxAttempts)
                            {
                                // Debug
                                executionContext.Debug($"Error processing issue matcher '{matcher.Name}' against line '{line}'. Exception: {ex.ToString()}");
                            }
                            else
                            {
                                // Warn
                                // todo: loc
                                _executionContext.Warning($"Removing issue matcher '{matcher.Name}'. Matcher failed {_maxAttempts} times. Error: {ex.Message}");

                                // Eject
                                foreach ()
                            }
                        }
                    }

                    if (issue != null)
                    {
                        // Log the issue/line
                        switch (issue.Severity)
                        {
                            case IssueSeverity.Warning:
                                _executionContext.AddIssue(issue, line);
                                context.Write($"{WellKnownTags.Warning}{line}");
                                break;

                            default:
                                context.Write($"{WellKnownTags.Error}{line}");
                                break;
                        }

                        // Reset each matcher
                        foreach (var matcher2 in matchers)
                        {
                            if (!object.ReferenceEquals(matcher, matcher2) || !matcher.Loop)
                            {
                                matcher2.Reset();
                            }
                        }

                        return;
                    }
                }
            }

            // Regular output
            _executionContext.Output(line);
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
