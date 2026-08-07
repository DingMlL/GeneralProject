using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralProject.Transport.Channels
{
    /// <summary>
    /// TCP 客户端通信通道
    /// </summary>
    public class TcpChannel : ICommChannel
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _disposed;
        private CancellationTokenSource? _receiveCts;

        public TcpChannel(string host, int port)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
        }

        public bool IsOpen => _client?.Connected ?? false;
        public string ChannelName => $"{_host}:{_port}";

        public event Action<byte[], IPEndPoint?>? DataReceived;
        public event Action<Exception>? ErrorOccurred;
        public event EventHandler? Opened;
        public event EventHandler? Closed;

        public async Task OpenAsync(CancellationToken ct = default)
        {
            if (IsOpen) return;

            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port);
            _stream = _client.GetStream();

            _receiveCts = new CancellationTokenSource();

            _ = Task.Run(() => ReceiveLoop(_receiveCts.Token), ct);

            Opened?.Invoke(this, EventArgs.Empty);
        }

        public async Task CloseAsync()
        {
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = null;

            if (_stream != null)
            {
                await _stream.DisposeAsync();
                _stream = null;
            }

            if (_client != null)
            {
                _client.Close();
                _client = null;
            }

            Closed?.Invoke(this, EventArgs.Empty);
        }

        public async Task WriteAsync(byte[] data, CancellationToken ct = default)
        {
            if (!IsOpen || _stream == null)
                throw new InvalidOperationException("TCP 未连接");
            if (data == null || data.Length == 0)
                throw new ArgumentException("数据不能为空", nameof(data));

            await _stream.WriteAsync(data, 0, data.Length, ct);
            await _stream.FlushAsync(ct);
        }

        /// <summary>
        /// TCP 客户端不支持指定目标发送
        /// </summary>
        public Task SendToAsync(byte[] data, IPEndPoint remoteEndPoint, CancellationToken ct = default)
        {
            throw new NotSupportedException("TCP 客户端通道不支持 SendToAsync，请使用 WriteAsync。");
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            byte[] buffer = new byte[4096];

            while (IsOpen && !ct.IsCancellationRequested && _stream != null)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0) break;

                    byte[] data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    // TCP 场景：IPEndPoint 传 null
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

            await CloseAsync();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CloseAsync().GetAwaiter().GetResult();
        }
    }
}