using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralProject.Transport.Server
{
    /// <summary>
    /// 客户端会话实现
    /// </summary>
    internal class ClientSession : IClientSession
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly string _clientId;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly DateTime _connectedAt;
        private bool _disposed;
        private CancellationTokenSource? _receiveCts;

        public ClientSession(TcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _stream = client.GetStream();
            _remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint!;
            _clientId = _remoteEndPoint.ToString()!;
            _connectedAt = DateTime.Now;
            _receiveCts = new CancellationTokenSource();

            _ = Task.Run(ReceiveLoop);
        }

        public string ClientId => _clientId;
        public IPEndPoint RemoteEndPoint => _remoteEndPoint;
        public DateTime ConnectedAt => _connectedAt;
        public bool IsOpen => _client.Connected;
        public string ChannelName => _clientId;
        public object? Tag { get; set; }

        public event Action<byte[], IPEndPoint?>? DataReceived;
        public event Action<Exception>? ErrorOccurred;
        public event EventHandler? Opened;
        public event EventHandler? Closed;

        public Task OpenAsync(CancellationToken ct = default) => Task.CompletedTask;

        public async Task CloseAsync()
        {
            _receiveCts?.Cancel();
            await DisconnectAsync();
        }

        public async Task DisconnectAsync()
        {
            if (_disposed) return;

            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = null;

            _client.Close();
            _client.Dispose();
            _disposed = true;

            Closed?.Invoke(this, EventArgs.Empty);
        }

        public async Task WriteAsync(byte[] data, CancellationToken ct = default)
        {
            if (!IsOpen || _stream == null)
                throw new InvalidOperationException("连接已断开");

            if (data == null || data.Length == 0)
                throw new ArgumentException("数据不能为空", nameof(data));

            await _stream.WriteAsync(data, 0, data.Length, ct);
            await _stream.FlushAsync(ct);
        }

        /// <summary>
        /// 服务端会话不支持 SendToAsync
        /// </summary>
        public Task SendToAsync(byte[] data, IPEndPoint remoteEndPoint, CancellationToken ct = default)
        {
            throw new NotSupportedException("服务端会话不支持 SendToAsync，请使用 WriteAsync。");
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[4096];

            while (IsOpen && !_receiveCts!.Token.IsCancellationRequested)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _receiveCts.Token);
                    if (bytesRead == 0) break;

                    byte[] data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    DataReceived?.Invoke(data, null);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(ex);
                    break;
                }
            }

            await DisconnectAsync();
        }

        public void Dispose()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
    }
}