namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    internal class HiveViewPair
    {
        public HiveViewPair(string hive, string view)
        {
            Hive = hive;
            View = view;
        }

        public string Hive { get; }
        public string View { get; }
    }
}
