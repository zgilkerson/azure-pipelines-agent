using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Handlers
{
    public sealed class MatcherChangedEventArgs : EventArgs
    {
    }

    [DataContract]
    public sealed class IssueMatchers
    {
        [DataMember(Name = "problemMatcher")]
        private List<IssueMatcher> _matchers;

        public List<IssueMatcher> Matchers
        {
            get
            {
                if (_matchers == null)
                {
                    _matchers = new List<IssueMatcher>();
                }

                return _matchers;
            }
        }

        public void Validate()
        {
            // todo
        }
    }

    [DataContract]
    public sealed class IssueMatcher
    {
        [DataMember(Name = "owner")]
        private string _owner;

        [DataMember(Name = "pattern")]
        private IssuePattern[] _patterns;

        private IssueMatch[] _state;

        [JsonConstructor]
        public IssueMatcher()
        {
        }

        public IssueMatcher(IssueMatcher copy, TimeSpan timeout)
        {
            _owner = copy._owner;

            if (copy._patterns?.Length > 0)
            {
                _patterns = copy.Patterns.Select(x => new IssuePattern(x , timeout)).ToArray();
            }
        }

        public string Owner
        {
            get
            {
                if (_owner == null)
                {
                    _owner = String.Empty;
                }

                return _owner;
            }
        }

        public IssuePattern[] Patterns
        {
            get
            {
                if (_patterns == null)
                {
                    _patterns = new IssuePattern[0];
                }

                return _patterns;
            }
        }

        public IssueMatch Match(string line)
        {
            if (_state == null)
            {
                Reset();
            }

            // Each pattern (iterate in reverse)
            for (int i = _patterns.Length - 1; i >= 0; i--)
            {
                var runningMatch = i > 0 ? _state[i - 1] : null;

                // First pattern or a running match
                if (i == 0 || runningMatch != null)
                {
                    var pattern = _patterns[i];
                    var isLast = i == _patterns.Length - 1;
                    var regexMatch = pattern.Regex.Match(line);

                    // Matched
                    if (regexMatch.Success)
                    {
                        // Last pattern
                        if (isLast)
                        {
                            // Multi-line non-loop
                            if (i > 0 && !pattern.Loop)
                            {
                                // Clear the state
                                Reset();
                            }

                            // Return
                            return new IssueMatch(runningMatch, pattern, regexMatch.Groups);
                        }
                        // Not the last pattern
                        else
                        {
                            // Store the match
                            _state[i] = new IssueMatch(runningMatch, pattern, regexMatch.Groups);
                        }
                    }
                    // Not matched
                    else
                    {
                        // Not the last pattern
                        if (isLast)
                        {
                            // Record not matched
                            _state[i] = null;
                        }
                    }
                }
            }

            return null;
        }

        public void Reset()
        {
            _state = new IssueMatch[_patterns.Length - 1];
        }

        public void Validate()
        {
            // todo: only last pattern may contain "loop=true"
            // todo: pattern may not contain "loop=true" when it is the only pattern
            // todo: only the last pattern may define message
            // todo: the same property may not be defined on more than one pattern
            // todo: the last pattern must define message
            // todo: validate at least one pattern
            // todo: validate IssuePattern properties int32 values are >= 0 (or > 0? check vscode)
        }
    }

    [DataContract]
    public sealed class IssuePattern
    {
        private static readonly RegexOptions _options = RegexOptions.CultureInvariant | RegexOptions.ECMAScript | RegexOptions.IgnoreCase;

        [DataMember(Name = "regexp")]
        private string _pattern;

        private Regex _regex;

        private TimeSpan? _timeout;

        [JsonConstructor]
        public IssuePattern()
        {
        }

        public IssuePattern(IssuePattern copy, TimeSpan timeout)
        {
            _pattern = copy._pattern;
            File = copy.File;
            Line = copy.Line;
            Column = copy.Column;
            Severity = copy.Severity;
            Code = copy.Code;
            Message = copy.Message;
            FromPath = copy.FromPath;
            _timeout = timeout;
        }

        [DataMember(Name = "file")]
        public int? File { get; set; }

        [DataMember(Name = "line")]
        public int? Line { get; set; }

        [DataMember(Name = "column")]
        public int? Column { get; set; }

        [DataMember(Name = "severity")]
        public int? Severity { get; set; }

        [DataMember(Name = "code")]
        public int? Code { get; set; }

        [DataMember(Name = "message")]
        public int? Message { get; set; }

        [DataMember(Name = "fromPath")]
        public int? FromPath { get; set; }

        [DataMember(Name = "loop")]
        public bool Loop { get; set; }

        public Regex Regex
        {
            get
            {
                if (_regex == null)
                {
                    _regex = new Regex(_pattern ?? String.Empty, _options, _timeout ?? TimeSpan.FromSeconds(1));
                }

                return _regex;
            }
        }
    }

    public sealed class IssueMatch
    {
        public IssueMatch(IssueMatch runningMatch, IssuePattern pattern, GroupCollection groups)
        {
            File = runningMatch?.File ?? GetValue(groups, pattern.File);
            Line = runningMatch?.Line ?? GetValue(groups, pattern.Line);
            Column = runningMatch?.Column ?? GetValue(groups, pattern.Column);
            Severity = runningMatch?.Severity ?? GetValue(groups, pattern.Severity);
            Code = runningMatch?.Code ?? GetValue(groups, pattern.Code);
            Message = runningMatch?.Message ?? GetValue(groups, pattern.Message);
            FromPath = runningMatch?.FromPath ?? GetValue(groups, pattern.FromPath);
        }

        public string File { get; set; }

        public string Line { get; set; }

        public string Column { get; set; }

        public string Severity { get; set; }

        public string Code { get; set; }

        public string Message { get; set; }

        public string FromPath { get; set; }

        private string GetValue(GroupCollection groups, int? index)
        {
            if (index.HasValue && index.Value < groups.Count)
            {
                var group = groups[index.Value];
                return group.Value;
            }

            return null;
        }
    }
}
