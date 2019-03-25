using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.WebApi;
using DTWebApi = Microsoft.TeamFoundation.DistributedTask.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Handlers
{
    public sealed class OutputManager : IDisposable
    {
        private const string _colorCodePrefix = "\033[";
        private const int _maxAttempts = 3;
        private const string _timeoutKey = "VSTS_ISSUE_MATCHER_TIMEOUT";
        private static readonly Regex _colorCodeRegex = new Regex(@"\033\[[0-9;]*m?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly IWorkerCommandManager _commandManager;
        private readonly IExecutionContext _executionContext;
        private readonly object _matchersLock = new object();
        private readonly TimeSpan _timeout;
        private IssueMatcher[] _matchers = Array.Empty<IssueMatcher>();

        public OutputManager(IExecutionContext executionContext, IWorkerCommandManager commandManager)
        {
            _executionContext = executionContext;
            _commandManager = commandManager;

            // Determine the timeout
            var timeoutStr = _executionContext.Variables.Get(_timeoutKey);
            if (string.IsNullOrEmpty(timeoutStr) ||
                !TimeSpan.TryParse(timeoutStr, CultureInfo.InvariantCulture, out _timeout) ||
                _timeout <= TimeSpan.Zero)
            {
                timeoutStr = Environment.GetEnvironmentVariable(_timeoutKey);
                if (string.IsNullOrEmpty(timeoutStr) ||
                    !TimeSpan.TryParse(timeoutStr, CultureInfo.InvariantCulture, out _timeout) ||
                    _timeout <= TimeSpan.Zero)
                {
                    _timeout = TimeSpan.FromSeconds(1);
                }
            }

            // Lock
            lock (_matchersLock)
            {
                _executionContext.Add(OnMatcherChanged);
                _matchers = _executionContext.GetMatchers().Select(x => new IssueMatcher(x, _timeout)).ToArray();
            }
        }

        public void Dispose()
        {
            try
            {
                _executionContext.Remove(OnMatcherChanged);
            }
            catch
            {
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
                _commandManager.TryProcessCommand(_executionContext, line);

                return;
            }

            // Problem matchers
            if (_matchers.Length > 0)
            {
                // Copy the reference
                var matchers = _matchers;

                // Strip color codes
                var stripped = line.Contains(_colorCodePrefix) ? _colorCodeRegex.Replace(line, string.Empty) : line;

                foreach (var matcher in matchers)
                {
                    IssueMatch match = null;
                    for (var attempt = 1; attempt <= _maxAttempts; attempt++)
                    {
                        // Match
                        try
                        {
                            match = matcher.Match(stripped);

                            break;
                        }
                        catch (RegexMatchTimeoutException ex)
                        {
                            if (attempt < _maxAttempts)
                            {
                                // Debug
                                _executionContext.Debug($"Timeout processing issue matcher '{matcher.Owner}' against line '{stripped}'. Exception: {ex.ToString()}");
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

                    if (match != null)
                    {
                        // Reset other matchers
                        foreach (var otherMatcher in matchers.Where(x => !object.ReferenceEquals(x, matcher)))
                        {
                            otherMatcher.Reset();
                        }

                        // Convert to issue
                        var issue = ConvertToIssue(match);

                        if (issue != null)
                        {
                            // Log issue
                            _executionContext.AddIssue(issue, stripped);

                            return;
                        }
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
                if (e.Config.Patterns.Length > 0)
                {
                    newMatchers.Add(new IssueMatcher(e.Config, _timeout));
                }

                // Add existing non-matching
                newMatchers.AddRange(_matchers.Where(x => !string.Equals(x.Owner, e.Config.Owner, StringComparison.OrdinalIgnoreCase)));

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
                newMatchers.AddRange(_matchers.Where(x => !object.ReferenceEquals(x, matcher)));

                // Store
                _matchers = newMatchers.ToArray();
            }
        }

        private DTWebApi.Issue ConvertToIssue(IssueMatch match)
        {
            // todo: handle if message is null or whitespace
            // todo: validate severity
            throw new NotImplementedException();
        }
    }
}
