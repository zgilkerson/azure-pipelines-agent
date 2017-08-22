using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    public sealed partial class WindowsCapabilitiesProvider
    {
        internal abstract class ApplicationCapability : IPrivateWindowsCapabilityProvider
        {
            protected abstract string Name { get; }

            protected abstract string ApplicationName { get; }

            public List<Capability> GetCapabilities()
            {
                ArgUtil.NotNullOrEmpty(Name, nameof(Name));
                ArgUtil.NotNullOrEmpty(ApplicationName, nameof(ApplicationName));

                // TODO: Get the capability for the application
                // Add-CapabilityFromApplication -Name 'npm' -ApplicationName 'npm'
                // which then calls:
                //Get-Command -Name $ApplicationName -CommandType Application -ErrorAction Ignore
                // Then get the Path

                throw new NotImplementedException();
            }
        }

        internal sealed class NpmCapability : ApplicationCapability
        {
            protected override string Name => "npm";
            protected override string ApplicationName => "npm";
        }

        internal sealed class GulpCapability : ApplicationCapability
        {
            protected override string Name => "gulp";
            protected override string ApplicationName => "gulp";
        }

        internal sealed class NodeJsCapability : ApplicationCapability
        {
            protected override string Name => "node.js";
            protected override string ApplicationName => "node";
        }

        internal sealed class BowerCapability : ApplicationCapability
        {
            protected override string Name => "bower";
            protected override string ApplicationName => "bower";
        }

        internal sealed class GruntCapability : ApplicationCapability
        {
            protected override string Name => "grunt";
            protected override string ApplicationName => "grunt";
        }

        internal sealed class SvnCapability : ApplicationCapability
        {
            protected override string Name => "svn";
            protected override string ApplicationName => "svn";
        }
    }
}
