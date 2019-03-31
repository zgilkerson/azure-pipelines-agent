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
        public void Validate_RequiresDistinctOwner()
        {
            var config = JSONUtility.Deserialize<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": ""myMatcher"",
      ""pattern"": [
        ""regexp"": ""^error: (.+)$"",
        ""message"": 1
      ]
    },
    {
      ""owner"": ""MYmatcher"",
      ""pattern"": [
        ""regexp"": ""^ERR: (.+)$"",
        ""message"": 1
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
        public void Validate_RequiresOwner()
        {
            var config = JSONUtility.Deserialize<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": """",
      ""pattern"": [
        ""regexp"": ""^error: (.+)$"",
        ""message"": 1
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
        public void Validate_RequiresAtLeastOnePattern()
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
        public void Validate_TODO()
        {
            var config = JSONUtility.Deserialize<IssueMatchersConfig>(@"
{
  ""problemMatcher"": [
    {
      ""owner"": """",
      ""pattern"": [
        ""regexp"": ""^error: (.+)$"",
        ""message"": 1
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

        // todo: Only the last pattern in a multiline matcher may set 'loop'
        // todo: Only the last pattern may set 'message'
        // todo: The last pattern must set 'message'
        // todo: The property '___' is set twice
        // todo: The property '___' is set twice
    }
}
