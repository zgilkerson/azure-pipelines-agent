#if OS_WINDOWS

using Microsoft.VisualStudio.Services.Agent.Listener.Capabilities;
using Microsoft.VisualStudio.Services.Agent.Listener;
using Moq;
using System;
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
                Assert.NotNull(result);
                Assert.Equal(0, result.Count);
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Agent")]
        public async void TestAntCapabilityFound()
        {
            using (var hc = new TestHostContext(this))
            {
                // Arrange
                string antHomeDirectory = @"C:\Ant";
                var mockEnvironmentService = new Mock<IEnvironmentService>();
                mockEnvironmentService.Setup(service => service.GetEnvironmentVariable("ANT_HOME"))
                                      .Returns(antHomeDirectory);

                var antCapability = new AntCapability(mockEnvironmentService);

                // Act
                List<Capability> result = antCapability.GetCapabilities();

                // Assert
                Assert.NotNull(result);
                Assert.Equal(1, result.Count);

                Capability antCapability = result.First();
                Assert.Equal(CapabilityNames.Ant, antCapability.Name);
                Assert.Equal(antHomeDirectory, antCapability.Value);
            }
        }
    }
}

#endif
