using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Agent.Worker;
using Microsoft.VisualStudio.Services.WebApi;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests.Worker
{
    public sealed class IssueMatcherL0
    {
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_Loop_MayNotBeSetOnSinglePattern()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^error: (.+)$"",
          ""message"": 1,
          ""loop"": true
        }
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Patterns = new[]
            {
                new IssuePatternConfig
                {
                    Pattern = "^file: (.+)$",
                    File = 1,
                },
                config.Matchers[0].Patterns[0],
            };
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_Loop_OnlyAllowedOnLastPattern()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^(error)$"",
          ""severity"": 1
        },
        {
          ""regexp"": ""^file: (.+)$"",
          ""file"": 1,
          ""loop"": true
        },
        {
          ""regexp"": ""^error: (.+)$"",
          ""message"": 1
        }
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Patterns[1].Loop = false;
            config.Matchers[0].Patterns[2].Loop = true;
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_Message_OnlyAllowedOnLastPattern()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^file: (.+)$"",
          ""message"": 1
        },
        {
          ""regexp"": ""^error: (.+)$"",
          ""file"": 1
        }
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Patterns[0].File = 1;
            config.Matchers[0].Patterns[0].Message = null;
            config.Matchers[0].Patterns[1].File = null;
            config.Matchers[0].Patterns[1].Message = 1;
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_Message_Required()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^error: (.+)$"",
          ""file"": 1
        }
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Patterns[0].File = null;
            config.Matchers[0].Patterns[0].Message = 1;
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_Owner_Distinct()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^error: (.+)$"",
          ""message"": 1
        }
      ]
    },
    {
      ""owner"": ""MYmatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^ERR: (.+)$"",
          ""message"": 1
        }
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Owner = "asdf";
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_Owner_Required()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": """",
      ""pattern"": [
        {
          ""regexp"": ""^error: (.+)$"",
          ""message"": 1
        }
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Owner = "asdf";
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_Pattern_Required()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Patterns = new[]
            {
                new IssuePatternConfig
                {
                    Pattern = "^error: (.+)$",
                    Message = 1,
                }
            };
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_PropertyMayNotBeSetTwice()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^severity: (.+)$"",
          ""file"": 1
        },
        {
          ""regexp"": ""^file: (.+)$"",
          ""file"": 1
        },
        {
          ""regexp"": ""^(.+)$"",
          ""message"": 1
        },
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Patterns[0].File = null;
            config.Matchers[0].Patterns[0].Severity = 1;
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_PropertyOutOfRange()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^(.+)$"",
          ""message"": 2
        },
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Patterns[0].Message = 1;
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Config_Validate_PropertyOutOfRange_LessThanZero()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^(.+)$"",
          ""message"": -1
        },
      ]
    }
  ]
}
");
            Assert.Throws<ArgumentException>(() => config.Validate());

            // Sanity test
            config.Matchers[0].Patterns[0].Message = 1;
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Matcher_ExtractsProperties_SinglePattern()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^file:(.+) line:(.+) column:(.+) severity:(.+) code:(.+) message:(.+) fromPath:(.+)$"",
          ""file"": 1,
          ""line"": 2,
          ""column"": 3,
          ""severity"": 4,
          ""code"": 5,
          ""message"": 6,
          ""fromPath"": 7
        },
      ]
    }
  ]
}
");
            config.Validate();
            var matcher = new IssueMatcher(config.Matchers[0], TimeSpan.FromSeconds(1));
            var match = matcher.Match("file:my-file.cs line:123 column:45 severity:real-bad code:uh-oh message:not-working fromPath:my-project.proj");
            Assert.Equal("my-file.cs", match.File);
            Assert.Equal("123", match.Line);
            Assert.Equal("45", match.Column);
            Assert.Equal("real-bad", match.Severity);
            Assert.Equal("uh-oh", match.Code);
            Assert.Equal("not-working", match.Message);
            Assert.Equal("my-project.proj", match.FromPath);
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Matcher_SetsOwner()
        {
            var config = JsonUtility.FromString<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        {
          ""regexp"": ""^(.+)$"",
          ""message"": 1
        },
      ]
    }
  ]
}
");
            config.Validate();
            var matcher = new IssueMatcher(config.Matchers[0], TimeSpan.FromSeconds(1));
            Assert.Equal("myMatcher", matcher.Owner);
        }
    }
}
