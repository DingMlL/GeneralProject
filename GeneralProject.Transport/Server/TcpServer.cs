using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralProject.Transport.Server
{
    /// <summary>
    /// TCP 服务端实现
    /// </summary>
    public class TcpServer : ITcpServer
    {
        private readonly int _port;
        private TcpListener? _listener;
        private CancellationTokenSource? _acceptCts;
        private readonly ConcurrentDictionary<string, IClientSession> _sessions = new();
        private bool _isRunning;
        private bool _disposed;

        public TcpServer(int port)
        {
            _port = port;
        }

        public bool IsRunning => _isRunning;
        public int Port => _port;
        public int ClientCount => _sessions.Count;

        public event EventHandler<IClientSession>? ClientConnected;
        public event EventHandler<IClientSession>? ClientDisconnected;
        public event Action<Exception>? ErrorOccurred;

        public async Task StartAsync(CancellationToken ct = default)
        {
            if (_isRunning) return;

            await Task.Run(() =>
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                _acceptCts = new CancellationTokenSource();

                _ = Task.Run(() => AcceptLoop(_acceptCts.Token), ct);
            }, ct);
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;

            await Task.Run(() =>
            {
                _acceptCts?.Cancel();
                _acceptCts?.Dispose();
                _acceptCts = null;

                // 断开所有客户端
                foreach (var session in _sessions.Values)
                {
                    try { session.DisconnectAsync().GetAwaiter().GetResult(); } catch { }
                }
                _sessions.Clear();

                _listener?.Stop();
                _listener = null;
                _isRunning = false;
            });
        }

        public async Task BroadcastAsync(byte[] data, CancellationToken ct = default)
        {
            if (!_isRunning)
                throw new InvalidOperationException("服务未启动");

            if (data == null || data.Length == 0)
                throw new ArgumentException("数据不能为空", nameof(data));

            foreach (var session in _sessions.Values)
            {
                try
                {
                    await session.WriteAsync(data, ct);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(ex);
                }
            }
        }

        public async Task SendToClientAsync(string clientId, byte[] data, CancellationToken ct = default)
        {
            if (!_isRunning)
                throw new InvalidOperationException("服务未启动");

            if (string.IsNullOrEmpty(clientId))
                throw new ArgumentException("客户端ID不能为空", nameof(clientId));

            if (data == null || data.Length == 0)
                throw new ArgumentException("数据不能为空", nameof(data));

            if (_sessions.TryGetValue(clientId, out var session))
            {
                await session.WriteAsync(data, ct);
            }
            else
            {
                throw new InvalidOperationException($"客户端 {clientId} 不存在或已断开");
            }
        }

        public async Task DisconnectClientAsync(string clientId)
        {
            if (_sessions.TryRemove(clientId, out var session))
            {
                await session.DisconnectAsync();
            }
        }

        public string[] GetClientIds()
        {
            return _sessions.Keys.ToArray();
        }

        public IClientSession? GetClient(string clientId)
        {
            _sessions.TryGetValue(clientId, out var session);
            return session;
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning && _listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    var session = new ClientSession(client);

                    if (_sessions.TryAdd(session.ClientId, session))
                    {
                        ClientConnected?.Invoke(this, session);
                    }
                }
                catch (OperationCanceledException)
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

        private void OnSessionClosed(object? sender, EventArgs e)
        {
            if (sender is IClientSession session)
            {
                _sessions.TryRemove(session.ClientId, out _);
                ClientDisconnected?.Invoke(this, session);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopAsync().GetAwaiter().GetResult();
        }
    }
}