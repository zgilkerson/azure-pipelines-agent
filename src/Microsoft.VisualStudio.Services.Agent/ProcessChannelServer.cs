using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent
{
    [DataContract]
    public class ProcessChannelMessage
    {
        [DataMember]
        public string MessageType { get; set; }

        [DataMember]
        public string Message { get; set; }
    }

    [ServiceLocator(Default = typeof(ProcessChannelServer))]
    public interface IProcessChannelServer : IAgentService
    {
        IPEndPoint ServerEndPoint { get; }
        Task AcceptTcpClientAsync(CancellationToken cancellationToken);
        Task SendAsync(ProcessChannelMessage message, CancellationToken cancellationToken);
    }

    public sealed class ProcessChannelServer : AgentService, IProcessChannelServer
    {
        public IPEndPoint ServerEndPoint { get; private set; }

        private TcpListener _tcpListener;
        private TcpClient _tcpConnection;

        public override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);

            // OS will use the next available port when set port to 0
            _tcpListener = new TcpListener(new IPEndPoint(IPAddress.Loopback, 0));

            int retryCount = 0;
            while (retryCount++ < 5)
            {
                try
                {
                    Trace.Info($"Start ProcessChannelServer TCP Listener. Attemp {retryCount}");
                    _tcpListener.Start();
                    ServerEndPoint = _tcpListener.Server.LocalEndPoint as IPEndPoint;
                    Trace.Info($"ProcessChannelServer is listen at: '{ServerEndPoint.Address}:{ServerEndPoint.Port}'");
                    break;
                }
                catch (SocketException socketEx)
                {
                    Trace.Error($"Catch Socket exception while start TcpListener. Socket Error: {socketEx.SocketErrorCode}");
                    Trace.Error(socketEx);
                }
                catch (Exception ex)
                {
                    Trace.Error($"Catch exception while start TcpListener.");
                    Trace.Error(ex);
                }
            }
        }

        public async Task AcceptTcpClientAsync(CancellationToken cancellationToken)
        {
            Trace.Entering();
            ArgUtil.NotNull(_tcpListener, nameof(_tcpListener));

            using (cancellationToken.Register(() => { _tcpListener.Stop(); }))
            {
                try
                {
                    _tcpConnection = await _tcpListener.AcceptTcpClientAsync();
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    Trace.Error("Catch exception when attemp to stop TcpListener while it is blocking on AcceptTcpClientAsync.");
                    Trace.Error(ex);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        public async Task SendAsync(ProcessChannelMessage message, CancellationToken cancellationToken)
        {
            ArgUtil.NotNull(_tcpConnection, nameof(_tcpConnection));
            ArgUtil.NotNull(message, nameof(message));

            string data = StringUtil.ConvertToJson(message);
            byte[] buffer = new UnicodeEncoding().GetBytes(data);

            await _tcpConnection.GetStream().WriteAsync(buffer, 0, buffer.Length, cancellationToken);
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
                _tcpConnection?.Dispose();
                _tcpListener?.Stop();
            }
        }
    }
}
