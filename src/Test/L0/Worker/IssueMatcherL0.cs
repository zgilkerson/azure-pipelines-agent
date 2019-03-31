using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Agent.Worker;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests.Worker
{
    public sealed class IssueMatcherL0
    {
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Validate_Owner_Distinct()
        {
            var config = JSONUtility.Deserialize<IssueMatchersConfig>(@"
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
        public void Validate_Owner_Required()
        {
            var config = JSONUtility.Deserialize<IssueMatchersConfig>(@"
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
        public void Validate_Pattern_Required()
        {
            var config = JSONUtility.Deserialize<IssueMatchersConfig>(@"
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
        public void Validate_Loop_RequiresMultiplePatterns()
        {
            var config = JSONUtility.Deserialize<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": """",
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
                }
                config.Matchers[0].Patterns[0],
            };
            config.Validate();
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void Validate_Loop_OnlyAllowedOnLastPattern()
        {
            var config = JSONUtility.Deserialize<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": """",
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

        // todo: Only the last pattern may set 'message'
        // todo: The last pattern must set 'message'
        // todo: The property '___' is set twice
        // todo: The property '___' is set twice
    }
}
