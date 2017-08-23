#if OS_WINDOWS

using Microsoft.VisualStudio.Services.Agent.Listener.Capabilities;
using Microsoft.VisualStudio.Services.Agent.Listener;
// using Microsoft.VisualStudio.Services.Agent.Util;
// using Microsoft.Win32;
using Moq;
using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Threading;
// using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests.Listener
{
    // TODO: Can we make this ApplicationCapabilityTestsL0? Then reuse code for each ApplicationCapability?
    public sealed class AntCapabilityTestsL0
    {
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Agent")]
        public async void TestAntCapabilityNotFound()
        {
            using (var hc = new TestHostContext(this))
            {
                // Arrange
                var mockEnvironmentService = new Mock<IEnvironmentService>();
                mockEnvironmentService.Setup(service => service.GetEnvironmentVariable("ANT_HOME"))
                                      .Returns(null);

                var antCapability = new AntCapability(mockEnvironmentService);

                // Act
                List<Capability> result = antCapability.GetCapabilities();

                // Assert
                Assert.True(result != null);
                Assert.True(result.Count == 0);
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Agent")]
        public async void TestAntCapabilityFound()
        {

        }
    }
}

#endif
