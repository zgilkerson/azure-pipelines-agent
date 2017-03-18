using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent
{
    [ServiceLocator(Default = typeof(ProcessChannelClient))]
    public interface IProcessChannelClient : IAgentService
    {
        Task ConnectAsync(IPEndPoint serverEndPoint);
        Task<ProcessChannelMessage> ReceiveAsync(TimeSpan timeout, CancellationToken cancellationToken);
    }

    public sealed class ProcessChannelClient : AgentService, IProcessChannelClient
    {
        public IPEndPoint ServerEndPoint { get; private set; }

        private TcpClient _tcpClient = new TcpClient();

        public async Task ConnectAsync(IPEndPoint serverEndPoint)
        {
            await _tcpClient.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);
        }

        public async Task<ProcessChannelMessage> ReceiveAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            ArgUtil.NotNull(_tcpClient, nameof(_tcpClient));
            var networkStream = _tcpClient.GetStream();
            ArgUtil.Equal(true, networkStream.CanRead, nameof(networkStream.CanRead));

            StringBuilder fullMessage = new StringBuilder();

            // Incoming message may be larger than the buffer size.
            do
            {
                byte[] receiveBuffer = new byte[1024];
                int numberOfBytesRead = await networkStream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
                if (numberOfBytesRead > 0)
                {
                    fullMessage.Append(Encoding.Unicode.GetString(receiveBuffer, 0, numberOfBytesRead));
                }
            }
            while (networkStream.DataAvailable);

            return StringUtil.ConvertFromJson<ProcessChannelMessage>(fullMessage.ToString());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tcpClient?.Dispose();
            }
        }
    }
}
