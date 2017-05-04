#if OS_WINDOWS
using Moq;
using Xunit;
using Microsoft.VisualStudio.Services.Agent.Listener.Configuration;

namespace Microsoft.VisualStudio.Services.Agent.Tests.Listener
{
    public sealed class AgentAutoLogonTestL0
    {
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Agent")]
        public async void TestAutoLogonConfiguration()
        {
            // using (var hc = new TestHostContext(this))
            // {
            //     Mock<INativeWindowsServiceHelper> windowsServiceHelper = new Mock<INativeWindowsServiceHelper>();
            //     hc.SetSingleton<INativeWindowsServiceHelper>(windowsServiceHelper.Object);

            //     windowsServiceHelper.Setup(x => x.IsValidCredential(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            //     windowsServiceHelper.Setup(x => x.IsTheSameUserLoggedIn(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

                
            // }
        }
    }
}
#endif