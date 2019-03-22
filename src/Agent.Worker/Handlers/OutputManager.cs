using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.Services.WebApi;
using System.Text.RegularExpressions;
using Pipelines = Microsoft.TeamFoundation.DistributedTask.Pipelines;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Handlers
{
    public sealed class OutputManager
    {
        private const int _maxAttempts = 3;
        private const string _colorCodePrefix = "\033[";
        private static readonly Regex _colorCodeRegex = new Regex(@"\033\[[0-9;]*m?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
                // Copy the reference
                var matchers = _matchers;

                // Strip color codes
                var stripped = line.Contains(_colorCodePrefix) ? _colorCodeRegex.Remove(line) : line;

                foreach (var matcher in matchers);
                {
                    Issue issue = null;
                    for (var attempt = 1; attempt <= _maxAttempts; i++)
                    {
                        // Match
                        try
                        {
                            issue = matcher.Match(stripped);

                            break;
                        }
                        catch (RegexMatchTimeoutException ex)
                        {
                            if (attempt < _maxAttempts)
                            {
                                // Debug
                                executionContext.Debug($"Timeout processing issue matcher '{matcher.Owner}' against line '{stripped}'. Exception: {ex.ToString()}");
                            }
                            else
                            {
                                // Warn
                                // todo: loc
                                _executionContext.Warning($"Removing issue matcher '{matcher.Owner}'. Matcher failed {_maxAttempts} times. Error: {ex.Message}");

                                // Remove
                                Remove(matcher);
                            }
                        }
                    }

                    if (issue != null)
                    {
                        // Log the issue/line
                        switch (issue.Severity)
                        {
                            case IssueSeverity.Warning:
                                _executionContext.AddIssue(issue, stripped);
                                context.Write($"{WellKnownTags.Warning}{stripped}");
                                break;

                            default:
                                context.Write($"{WellKnownTags.Error}{stripped}");
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
            // Lock
            lock (_matchersLock)
            {
                var newMatchers = new List<IssueMatcher>()

                // Prepend
                if (e.Configuration.Patterns.Count > 0)
                {
                    newMatchers.Add(new IssueMatcher(e.Configuration));
                }

                // Add existing non-matching
                newMatchers.AddRange(_matchers.Where(x => !string.Equals(x.Owner, e.Configuration.Owner, OrdinalIgnoreCase)));

                // Store
                _matchers = newMatchers.ToArray();
                _hasMatchers = true;
            }
        }

        private void Remove(IssueMatcher matcher)
        {
            // Lock
            lock (_matchersLock)
            {
                var newMatchers = new List<IssueMatcher>();

                // Match by object reference, not by owner name
                newMatchers.AddRange(_matchers.Where(x => !object.Reference(x, matcer)));

                // Store
                _matchers = newMatchers.ToArray();
                _hasMatchers = _matchers.Length > 0;
            }
        }
    }
}
