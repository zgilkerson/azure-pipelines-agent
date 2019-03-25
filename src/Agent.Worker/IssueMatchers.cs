using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Worker
{
    public delegate void OnMatcherChanged(object sender, MatcherChangedEventArgs e);

    public sealed class MatcherChangedEventArgs : EventArgs
    {
        public MatcherChangedEventArgs(IssueMatcherConfig config)
        {
            Config = config;
        }

        public IssueMatcherConfig Config { get; }
    }

    public sealed class IssueMatcher
    {
        private string _owner;
        private IssuePattern[] _patterns;
        private IssueMatch[] _state;

        public IssueMatcher(IssueMatcherConfig config, TimeSpan timeout)
        {
            _owner = config.Owner;
            _patterns = config.Patterns.Select(x => new IssuePattern(x , timeout)).ToArray();
            Reset();
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

        public IssueMatch Match(string line)
        {
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
    }

    public sealed class IssuePattern
    {
        private static readonly RegexOptions _options = RegexOptions.CultureInvariant | RegexOptions.ECMAScript | RegexOptions.IgnoreCase;

        public IssuePattern(IssuePatternConfig config, TimeSpan timeout)
        {
            File = config.File;
            Line = config.Line;
            Column = config.Column;
            Severity = config.Severity;
            Code = config.Code;
            Message = config.Message;
            FromPath = config.FromPath;
            Regex = new Regex(config.Pattern ?? String.Empty, _options, timeout);
        }

        public int? File { get; }

        public int? Line { get; }

        public int? Column { get; }

        public int? Severity { get; }

        public int? Code { get; }

        public int? Message { get; }

        public int? FromPath { get; }

        public bool Loop { get; }

        public Regex Regex { get; }
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

        public string File { get; }

        public string Line { get; }

        public string Column { get; }

        public string Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public string FromPath { get; }

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

    [DataContract]
    public sealed class IssueMatchersConfig
    {
        [DataMember(Name = "problemMatcher")]
        private List<IssueMatcherConfig> _matchers;

        public List<IssueMatcherConfig> Matchers
        {
            get
            {
                if (_matchers == null)
                {
                    _matchers = new List<IssueMatcherConfig>();
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
    public sealed class IssueMatcherConfig
    {
        [DataMember(Name = "owner")]
        private string _owner;

        [DataMember(Name = "pattern")]
        private IssuePatternConfig[] _patterns;

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

            set
            {
                _owner = value;
            }
        }

        public IssuePatternConfig[] Patterns
        {
            get
            {
                if (_patterns == null)
                {
                    _patterns = new IssuePatternConfig[0];
                }

                return _patterns;
            }
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
    public sealed class IssuePatternConfig
    {
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

        [DataMember(Name = "regexp")]
        public string Pattern { get; set; }
    }
}
