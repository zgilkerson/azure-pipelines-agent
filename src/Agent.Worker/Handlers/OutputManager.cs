using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Handlers
{
    public sealed class OutputManager
    {
        private const string _colorCodePrefix = "\033[";
        private const int _maxAttempts = 3;
        private const string _timeoutKey = "VSTS_ISSUE_MATCHER_TIMEOUT";
        private static readonly Regex _colorCodeRegex = new Regex(@"\033\[[0-9;]*m?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly IWorkerCommandManager _commandManager;
        private readonly IExecutionContext _executionContext;
        private readonly object _matchersLock = new object();
        private IssueMatcher[] _matchers = Array.Empty<IssueMatcher>();

        public OutputManager(IExecutionContext executionContext)
        {
            _executionContext = executionContext;
            _commandManager = hostContext.GetService<IWorkerCommandManager>();

            //executionContext.OnMatcherChanged += OnMatcherChanged;

            // todo: register known problem matcher

            // Determine the timeout
            TimeSpan timeout = null;
            var timeoutStr = _executionContext.Variables.Get(_timeoutKey);
            if (string.IsNullOrEmpty(timeoutStr) ||
                !TimeSpan.TryParse(timeoutStr, CultureInfo.InvariantCulture, out timeout) ||
                timeout <= TimeSpan.Zero)
            {
                timeoutStr = Environment.GetEnvironmentVariable(_timeoutKey);
                if (string.IsNullOrEmpty(timeoutStr) ||
                    !TimeSpan.TryParse(timeoutStr, CultureInfo.InvariantCulture, out timeout) ||
                    timeout <= TimeSpan.Zero)
                {
                    timeout = TimeSpan.FromSeconds(1);
                }
            }

            // Lock
            lock (_matchersLock)
            {
                var matchers = _executionContext.Matchers; // Copy the reference

                _matchers = new IssueMatcher[matchers.Count];

                for (var i = 0; i < matchers.Count; i++)
                {
                    _matchers[i] = new IssueMatcher(matchers[i], timeout); // Copy the matcher
                }

                _matchers = new IssueMatcher[]
                var newMatchers = new List<IssueMatcher>();

                newMatchers.AddRange(_executionContext.Matchers);

                // Store
                _matchers = newMatchers.ToArray();
            }
        }

        public void OnDataReceived(object sender, ProcessDataReceivedEventArgs e)
        {
            var line = e.Data;

            // ##vso commands
            if (!String.IsNullOrEmpty(line) && line.IndexOf("##vso") >= 0)
            {
                // This does not need to be inside of a critical section.
                // The logging queues and command handlers are thread-safe.
                _commandManager.TryProcessCommand(ExecutionContext, line);

                return;
            }

            // Problem matchers
            if (_matchers.Length > 0)
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

                            case IssueSeverity.Error:
                                context.Write($"{WellKnownTags.Error}{stripped}");
                                break;

                            default:
                                // todo
                                throw new NotImplementedException();
                        }

                        // todo: handle if message is null or whitespace

                        // Reset other matchers
                        foreach (var otherMatcher in matchers.Where(x => !object.ReferenceEquals(x, matcher)))
                        {
                            otherMatcher.Reset();
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
                var newMatchers = new List<IssueMatcher>();

                // Prepend
                if (e.Configuration.Patterns.Count > 0)
                {
                    newMatchers.Add(new IssueMatcher(e.Configuration));
                }

                // Add existing non-matching
                newMatchers.AddRange(_matchers.Where(x => !string.Equals(x.Owner, e.Configuration.Owner, OrdinalIgnoreCase)));

                // Store
                _matchers = newMatchers.ToArray();
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
            }
        }
    }
}
