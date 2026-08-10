using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralProject.Transport.Channels;
using GeneralProject.Transport.Core.Internal;
using GeneralProject.Transport.Parser;
using GeneralProject.Transport.Proxy;

namespace GeneralProject.Transport.Core
{
    /// <summary>
    /// 连接队列器
    /// </summary>
    /// <remarks>
    /// 这是整个框架的核心调度引擎。一个物理连接对应一个 ConnectionQueue 实例。
    /// 
    /// 核心职责：
    /// <list type="number">
    /// <item><description>双队列调度（高/低优先级命令排队发送）</description></item>
    /// <item><description>请求-响应匹配（通过 matchKey 精准匹配）</description></item>
    /// <item><description>超时管理（超时清理 + 超时扫描器）</description></item>
    /// <item><description>主动上报路由（通过设备地址分发到对应设备代理）</description></item>
    /// <item><description>物理通道收发（通过 ICommChannel）</description></item>
    /// </list>
    /// 
    /// 数据流：
    /// <list type="bullet">
    /// <item><description>发送：业务层 → 入队 → 消费者线程 → 物理通道写入</description></item>
    /// <item><description>接收：物理通道 → FrameDecoder 拆包 → 分流（Response/Report）</description></item>
    /// <item><description>Response：匹配等待字典 → 唤醒对应的 Task 或执行 Callback</description></item>
    /// <item><description>Report：提取设备地址 → 路由到对应的 IDeviceProxy</description></item>
    /// </list>
    /// 
    /// 线程安全：
    /// 所有公共方法都是线程安全的，可被多个设备代理并发调用。
    /// </remarks>
    public sealed class ConnectionQueue : IDisposable
    {
        // ========== 私有字段 ==========

        private readonly ICommChannel _channel;
        private readonly IProtocolParser _parser;
        private readonly FrameDecoder _decoder;
        private readonly string _connectionId;

        // 双队列
        private readonly ConcurrentQueue<QueuedCommand> _highPriorityQueue = new();
        private readonly ConcurrentQueue<QueuedCommand> _normalPriorityQueue = new();
        private readonly SemaphoreSlim _signal = new(0);

        // 等待字典（matchKey → PendingOperation）
        private readonly ConcurrentDictionary<ushort, PendingOperation<byte[]>> _pendingMap = new();

        // 设备路由表（deviceId → IDeviceProxy）
        private readonly ConcurrentDictionary<string, IDeviceProxy> _deviceMap = new();

        // 后台任务
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _consumerTask;
        private readonly Task _timeoutScannerTask;

        // 状态
        private bool _disposed;

        // ========== 公共属性 ==========

        /// <summary>
        /// 连接唯一标识
        /// </summary>
        public string ConnectionId => _connectionId;

        /// <summary>
        /// 物理通道是否已打开
        /// </summary>
        public bool IsOpen => _channel.IsOpen;

        /// <summary>
        /// 通道名称
        /// </summary>
        public string ChannelName => _channel.ChannelName;

        /// <summary>
        /// 挂载的设备数量
        /// </summary>
        public int DeviceCount => _deviceMap.Count;

        /// <summary>
        /// 等待中的请求数量
        /// </summary>
        public int PendingCount => _pendingMap.Count;

        // ========== 事件 ==========

        /// <summary>
        /// 日志事件（用于调试和监控）
        /// </summary>
        public event Action<string, Exception?>? LogEvent;

        // ========== 构造函数 ==========

        /// <summary>
        /// 初始化连接队列器
        /// </summary>
        /// <param name="connectionId">连接唯一标识</param>
        /// <param name="channel">物理通道</param>
        /// <param name="parser">协议解析器</param>
        public ConnectionQueue(string connectionId, ICommChannel channel, IProtocolParser parser)
        {
            _connectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _decoder = new FrameDecoder();

            // 订阅通道事件
            _channel.DataReceived += OnDataReceived;
            _channel.Opened += OnChannelOpened;
            _channel.Closed += OnChannelClosed;
            _channel.ErrorOccurred += OnChannelError;

            // 启动后台任务
            _consumerTask = Task.Run(ConsumerLoop);
            _timeoutScannerTask = Task.Run(TimeoutScannerLoop);

            OnLog($"连接队列器 {_connectionId} 已创建");
        }

        // ========== 设备管理 ==========

        /// <summary>
        /// 注册设备到路由表
        /// </summary>
        /// <param name="deviceProxy">设备代理实例</param>
        public void RegisterDevice(IDeviceProxy deviceProxy)
        {
            if (deviceProxy == null)
                throw new ArgumentNullException(nameof(deviceProxy));

            if (_deviceMap.ContainsKey(deviceProxy.DeviceId))
                throw new InvalidOperationException($"设备 {deviceProxy.DeviceId} 已注册");

            if (!_deviceMap.TryAdd(deviceProxy.DeviceId, deviceProxy))
                throw new InvalidOperationException($"注册设备 {deviceProxy.DeviceId} 失败");

            OnLog($"设备 {deviceProxy.DeviceId} 已注册到连接 {_connectionId}");
        }

        /// <summary>
        /// 注销设备
        /// </summary>
        public bool UnregisterDevice(string deviceId)
        {
            if (_deviceMap.TryRemove(deviceId, out _))
            {
                OnLog($"设备 {deviceId} 已从连接 {_connectionId} 注销");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取设备代理
        /// </summary>
        public IDeviceProxy? GetDevice(string deviceId)
        {
            _deviceMap.TryGetValue(deviceId, out var proxy);
            return proxy;
        }

        /// <summary>
        /// 获取所有设备ID
        /// </summary>
        public string[] GetDeviceIds() => _deviceMap.Keys.ToArray();

        // ========== 发送 API ==========

        /// <summary>
        /// 发送请求并等待响应（异步等待模式）
        /// </summary>
        public async Task<byte[]> SendAsync(
            byte[] request,
            ushort matchKey,
            int timeoutMs = 3000,
            CommandPriority priority = CommandPriority.Normal)
        {

            if (request == null || request.Length == 0)
                throw new ArgumentException("请求数据不能为空", nameof(request));

            if (!IsOpen)
                throw new InvalidOperationException($"连接 {_connectionId} 未打开");

            var operation = new PendingOperation<byte[]>(matchKey, $"Req-{matchKey:X4}", timeoutMs);

            if (!_pendingMap.TryAdd(matchKey, operation))
                throw new InvalidOperationException($"匹配键 {matchKey} 已存在，请检查协议是否正确");

            // 超时回调
            operation.Cts.Token.Register(() =>
            {
                if (_pendingMap.TryRemove(matchKey, out var op) && op.TrySetCompleted())
                {
                    op.IsTimedOut = true;
                    op.Tcs?.TrySetException(new TimeoutException($"请求 {op.CommandName} 超时 ({timeoutMs}ms)"));
                    op.Callback?.Invoke(null!);
                }
            });

            // 入队
            EnqueueCommand(request, priority);

            try
            {
                return await operation.Tcs.Task;
            }
            catch (Exception)
            {
                _pendingMap.TryRemove(matchKey, out _);
                throw;
            }
        }

        /// <summary>
        /// 发送请求，通过回调接收响应（回调模式）
        /// </summary>
        public void SendWithCallback(
            byte[] request,
            ushort matchKey,
            Action<byte[]> callback,
            int timeoutMs = 3000,
            CommandPriority priority = CommandPriority.Normal)
        {
            if (request == null || request.Length == 0)
                throw new ArgumentException("请求数据不能为空", nameof(request));

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (!IsOpen)
                throw new InvalidOperationException($"连接 {_connectionId} 未打开");

            var operation = new PendingOperation<byte[]>(matchKey, $"Cb-{matchKey:X4}", timeoutMs, callback);

            if (!_pendingMap.TryAdd(matchKey, operation))
                throw new InvalidOperationException($"匹配键 {matchKey} 已存在");

            // 超时回调
            operation.Cts.Token.Register(() =>
            {
                if (_pendingMap.TryRemove(matchKey, out var op) && op.TrySetCompleted())
                {
                    op.IsTimedOut = true;
                    op.Callback?.Invoke(null!);
                }
            });

            EnqueueCommand(request, priority);
        }

        /// <summary>
        /// 发送请求，不等待响应（发完即忘）
        /// </summary>
        public void SendOnly(byte[] request, CommandPriority priority = CommandPriority.Normal)
        {
            if (request == null || request.Length == 0)
                throw new ArgumentException("请求数据不能为空", nameof(request));

            if (!IsOpen)
                throw new InvalidOperationException($"连接 {_connectionId} 未打开");

            EnqueueCommand(request, priority);
        }

        // ========== 内部方法 ==========

        /// <summary>
        /// 入队命令
        /// </summary>
        private void EnqueueCommand(byte[] request, CommandPriority priority)
        {
            var cmd = new QueuedCommand(request);

            if (priority == CommandPriority.High)
            {
                _highPriorityQueue.Enqueue(cmd);
            }
            else
            {
                _normalPriorityQueue.Enqueue(cmd);
            }

            _signal.Release();
        }

        /// <summary>
        /// 消费者循环（后台任务）
        /// </summary>
        private async Task ConsumerLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await _signal.WaitAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                while (!_cts.Token.IsCancellationRequested)
                {
                    // 优先消费高优先级队列
                    if (_highPriorityQueue.TryDequeue(out var cmd))
                    {
                        await SendToChannel(cmd.Data);
                        continue;
                    }

                    // 再消费普通队列
                    if (_normalPriorityQueue.TryDequeue(out cmd))
                    {
                        await SendToChannel(cmd.Data);
                        continue;
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// 实际发送到物理通道
        /// </summary>
        private async Task SendToChannel(byte[] data)
        {
            try
            {
                await _channel.WriteAsync(data);
            }
            catch (Exception ex)
            {
                OnLog($"发送数据失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 超时扫描器（后台任务）
        /// </summary>
        private async Task TimeoutScannerLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (_pendingMap.IsEmpty) continue;

                var now = DateTime.UtcNow;
                var timeoutKeys = _pendingMap
                    .Where(kvp => kvp.Value.DeadlineUtc <= now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in timeoutKeys)
                {
                    if (_pendingMap.TryRemove(key, out var op) && op.TrySetCompleted())
                    {
                        op.IsTimedOut = true;
                        op.Tcs?.TrySetException(new TimeoutException($"请求 {op.CommandName} 超时（扫描器触发）"));
                        op.Callback?.Invoke(null!);
                        OnLog($"超时扫描器清理: {op.CommandName}, Key: {key}");
                    }
                }
            }
        }

        // ========== 接收处理 ==========

        /// <summary>
        /// 物理通道数据接收回调
        /// </summary>
        private void OnDataReceived(byte[] data, System.Net.IPEndPoint? remoteEndPoint)
        {
            try
            {

                // 1. 拆包
                var frames = _decoder.Decode(data, _parser);
                if (frames.Count == 0) return;

                foreach (var frame in frames)
                {
                    // 2. 判断帧类型
                    var frameType = _parser.GetFrameType(frame);

                    if (frameType == FrameType.Report)
                    {
                        // ===== 主动上报 =====
                        HandleReport(frame);
                    }
                    else
                    {
                        // ===== 响应帧 =====
                        HandleResponse(frame);
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog($"接收处理异常", ex);
            }
        }

        /// <summary>
        /// 处理主动上报帧
        /// </summary>
        private void HandleReport(byte[] frame)
        {

            string? deviceId = _parser.ExtractDeviceId(frame);
            if (string.IsNullOrEmpty(deviceId))
            {
                OnLog($"主动上报：无法提取设备地址，帧已丢弃");
                return;
            }

            if (_deviceMap.TryGetValue(deviceId, out var proxy))
            {
                try
                {
                    proxy.HandleReport(frame);
                    OnLog($"主动上报：已路由到设备 {deviceId}");
                }
                catch (Exception ex)
                {
                    OnLog($"设备 {deviceId} 处理上报异常", ex);
                }
            }
            else
            {
                OnLog($"主动上报：未找到设备 {deviceId}，帧已丢弃");
            }
        }

        /// <summary>
        /// 处理响应帧
        /// </summary>
        private void HandleResponse(byte[] frame)
        {

            ushort? matchKey = _parser.ExtractMatchKey(frame);

            if (!matchKey.HasValue)
            {
                OnLog($"响应帧：无法提取匹配键，已丢弃");
                return;
            }

            if (_pendingMap.TryRemove(matchKey.Value, out var operation))
            {

                // 防重入：只有赢家能继续
                if (operation.TrySetCompleted())
                {
                    operation.Cts.Cancel();
                    operation.Tcs?.TrySetResult(frame);
                    operation.Callback?.Invoke(frame);
                }
                else
                {
                    OnLog($"响应帧：Key {matchKey} 已完成，忽略重复响应");
                }
            }
            else
            {
                OnLog($"响应帧：未找到匹配的请求，Key: {matchKey}");
            }
        }

        // ========== 日志 ==========

        private void OnLog(string message, Exception? ex = null)
        {
            LogEvent?.Invoke($"[{_connectionId}] {message}", ex);
        }

        // ========== 资源释放 ==========
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            OnLog("正在释放 ConnectionQueue...");

            // 1. 取消后台任务
            try { _cts.Cancel(); } catch { }

            // 2. 等待后台任务结束
            try
            {
                Task.WaitAll(new[] { _consumerTask, _timeoutScannerTask }, 3000);
            }
            catch { }

            // 3. 清理等待字典
            foreach (var key in _pendingMap.Keys.ToList())
            {
                if (_pendingMap.TryRemove(key, out var op))
                {
                    try { op.Cts?.Cancel(); } catch { }
                    try { op.Cts?.Dispose(); } catch { }
                    try { op.Tcs?.TrySetException(new ObjectDisposedException(nameof(ConnectionQueue))); } catch { }
                    try { op.Callback?.Invoke(null!); } catch { }
                }
            }

            // 4. 解绑通道事件
            try { _channel.DataReceived -= OnDataReceived; } catch { }
            try { _channel.Opened -= OnChannelOpened; } catch { }
            try { _channel.Closed -= OnChannelClosed; } catch { }
            try { _channel.ErrorOccurred -= OnChannelError; } catch { }

            // 5. 【关键】关闭并释放物理通道
            try
            {
                if (_channel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch { }

            try { _signal.Dispose(); } catch { }
            try { _decoder.Dispose(); } catch { }
            try { _cts.Dispose(); } catch { }

            OnLog("ConnectionQueue 已释放");
        }

        // 放在构造函数后面，或者其他合适位置

        private void OnChannelOpened(object? sender, EventArgs e)
        {
            OnLog($"连接 {_connectionId} 已打开");
        }

        private void OnChannelClosed(object? sender, EventArgs e)
        {
            OnLog($"连接 {_connectionId} 已关闭");
        }

        private void OnChannelError(Exception ex)
        {
            OnLog($"连接 {_connectionId} 发生错误", ex);
        }

        /// <summary>
        /// 队列命令项
        /// </summary>
        private sealed class QueuedCommand
        {
            public byte[] Data { get; }

            public QueuedCommand(byte[] data)
            {
                Data = data ?? throw new ArgumentNullException(nameof(data));
            }
        }
    }
}