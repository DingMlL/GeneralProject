using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralProject.Transport.Channels
{
    /// <summary>
    /// UDP 通信通道
    /// </summary>
    public class UdpChannel : ICommChannel
    {
        private UdpClient? _udpClient;
        private readonly int _localPort;
        private bool _isOpen;
        private bool _disposed;
        private CancellationTokenSource? _receiveCts;

        public UdpChannel(int localPort = 0)
        {
            _localPort = localPort;
        }

        public bool IsOpen => _isOpen && _udpClient != null;
        public string ChannelName => $"UDP:{(IsOpen && _udpClient != null ? ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port.ToString() : _localPort.ToString())}";

        public event Action<byte[], IPEndPoint?>? DataReceived;
        public event Action<Exception>? ErrorOccurred;
        public event EventHandler? Opened;
        public event EventHandler? Closed;

        public async Task OpenAsync(CancellationToken ct = default)
        {
            if (IsOpen) return;

            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                _udpClient = new UdpClient(_localPort);
                _isOpen = true;

                _receiveCts = new CancellationTokenSource();

                _ = Task.Run(() => ReceiveLoop(_receiveCts.Token), ct);

                Opened?.Invoke(this, EventArgs.Empty);
            }, ct);
        }

        public async Task CloseAsync()
        {
            if (!IsOpen || _udpClient == null) return;

            await Task.Run(() =>
            {
                _receiveCts?.Cancel();
                _receiveCts?.Dispose();
                _receiveCts = null;

                _udpClient.Close();
                _udpClient.Dispose();
                _udpClient = null;

                _isOpen = false;

                Closed?.Invoke(this, EventArgs.Empty);
            });
        }

        public Task WriteAsync(byte[] data, CancellationToken ct = default)
        {
            throw new NotSupportedException("UDP 通道请使用 SendToAsync 方法指定目标端点发送数据。");
        }

        public async Task SendToAsync(byte[] data, IPEndPoint remoteEndPoint, CancellationToken ct = default)
        {
            if (!IsOpen || _udpClient == null)
                throw new InvalidOperationException("UDP 通道未打开");

            if (data == null || data.Length == 0)
                throw new ArgumentException("数据不能为空", nameof(data));

            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));

            // 使用 Socket 的 SendToAsync 扩展方法，支持 CancellationToken
            await _udpClient.Client.SendToAsync(data, SocketFlags.None, remoteEndPoint, ct);
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _udpClient != null && IsOpen)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(ct);
                    DataReceived?.Invoke(result.Buffer, result.RemoteEndPoint);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(ex);
                    await Task.Delay(100, ct);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CloseAsync().GetAwaiter().GetResult();
        }
    }
}