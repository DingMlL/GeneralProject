using GeneralProject.Transport.Channels;
using GeneralProject.Transport.Core;
using GeneralProject.Transport.Extensions;
using GeneralProject.Transport.Factory;
using GeneralProject.Transport.Parser;
using GeneralProject.Transport.Proxy;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace GeneralProject.Transport.Manager
{
    /// <summary>
    /// 全局设备管理器（单例）
    /// </summary>
    /// <remarks>
    /// 负责管理所有连接队列器（ConnectionQueue）和设备代理实例。
    /// 
    /// 核心职责：
    /// <list type="number">
    /// <item><description>管理连接队列器池（connectionId → ConnectionQueue）</description></item>
    /// <item><description>管理设备代理实例（connectionId:deviceId → IDeviceProxy）</description></item>
    /// <item><description>自动通过 <see cref="ProtocolParserAttribute"/> 创建协议解析器</description></item>
    /// <item><description>按需创建连接和设备（懒加载）</description></item>
    /// </list>
    /// 
    /// 使用方式：
    /// <code>
    /// // 1. 手动注册连接和设备
    /// var channel = new TcpChannel("192.168.1.100", 502);
    /// var parser = new ModbusRtuParser();
    /// var queue = DeviceManager.Instance.GetOrCreateConnection("PLC_1", channel, parser);
    /// var device = new TemperatureProxy(queue, "1");
    /// DeviceManager.Instance.RegisterDevice("PLC_1", "1", device);
    /// 
    /// // 2. 自动创建设备（推荐）
    /// var channel = new TcpChannel("192.168.1.100", 502);
    /// var device = DeviceManager.Instance.GetOrCreateDevice&lt;TemperatureProxy&gt;("PLC_1", "1", channel);
    /// </code>
    /// </remarks>
    public sealed class DeviceManager : IDisposable
    {
        // ========== 单例实现 ==========

        private static readonly Lazy<DeviceManager> _instance = new(() => new DeviceManager());
        public static DeviceManager Instance => _instance.Value;

        private DeviceManager()
        {
            OnLog("DeviceManager 已创建");
        }

        // ========== 私有字段 ==========

        private readonly ConcurrentDictionary<string, ConnectionQueue> _connections = new();
        private readonly ConcurrentDictionary<string, IDeviceProxy> _devices = new();
        private bool _disposed;

        // ========== 事件 ==========

        public event Action<string, Exception?>? LogEvent;

        // ========== 连接管理 ==========

        /// <summary>
        /// 获取或创建连接队列器
        /// </summary>
        /// <param name="connectionId">连接唯一标识</param>
        /// <param name="channel">物理通道</param>
        /// <param name="parser">协议解析器</param>
        /// <returns>连接队列器实例</returns>
        public ConnectionQueue GetOrCreateConnection(
            string connectionId,
            ICommChannel channel,
            IProtocolParser parser)
        {
            if (string.IsNullOrEmpty(connectionId))
                throw new ArgumentException("连接ID不能为空", nameof(connectionId));

            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            if (_connections.TryGetValue(connectionId, out var existing))
                return existing;

            var queue = new ConnectionQueue(connectionId, channel, parser);
            queue.LogEvent += (msg, ex) => OnLog($"[{connectionId}] {msg}", ex);

            if (!_connections.TryAdd(connectionId, queue))
            {
                queue.Dispose();
                throw new InvalidOperationException($"添加连接 {connectionId} 失败");
            }

            OnLog($"连接 {connectionId} 已创建并注册");
            return queue;
        }

        /// <summary>
        /// 获取已存在的连接队列器
        /// </summary>
        public ConnectionQueue? GetConnection(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var queue);
            return queue;
        }

        /// <summary>
        /// 移除连接并释放资源
        /// </summary>
        public bool RemoveConnection(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var queue))
            {
                queue.LogEvent -= (msg, ex) => { };
                queue.Dispose();
                OnLog($"连接 {connectionId} 已移除");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有连接ID
        /// </summary>
        public string[] GetConnectionIds() => _connections.Keys.ToArray();

        // ========== 设备管理 ==========

        /// <summary>
        /// 注册设备到指定连接
        /// </summary>
        public void RegisterDevice<T>(string connectionId, string deviceId, T deviceProxy) where T : IDeviceProxy
        {
            if (string.IsNullOrEmpty(connectionId))
                throw new ArgumentException("连接ID不能为空", nameof(connectionId));

            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (deviceProxy == null)
                throw new ArgumentNullException(nameof(deviceProxy));

            var key = $"{connectionId}:{deviceId}";
            if (_devices.ContainsKey(key))
                throw new InvalidOperationException($"设备 {key} 已注册");

            var connection = GetOrCreateConnectionForDevice(connectionId, deviceProxy);
            connection.RegisterDevice(deviceProxy);

            if (!_devices.TryAdd(key, deviceProxy))
                throw new InvalidOperationException($"注册设备 {key} 失败");

            OnLog($"设备 {deviceId} 已注册到连接 {connectionId}");
        }

        /// <summary>
        /// 获取或创建设备代理（自动创建连接和解析器）
        /// </summary>
        /// <typeparam name="TDevice">设备代理类型（必须继承 DeviceProxyBase 并标注 ProtocolParserAttribute）</typeparam>
        /// <param name="connectionId">连接唯一标识</param>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="channel">物理通道（如果连接不存在则使用此通道创建）</param>
        /// <returns>设备代理实例</returns>
        public TDevice GetOrCreateDevice<TDevice>(
            string connectionId,
            string deviceId,
            ICommChannel channel) where TDevice : class, IDeviceProxy
        {
            if (string.IsNullOrEmpty(connectionId))
                throw new ArgumentException("连接ID不能为空", nameof(connectionId));

            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            var key = $"{connectionId}:{deviceId}";

            // 1. 尝试获取已注册的设备
            if (_devices.TryGetValue(key, out var existing) && existing is TDevice typedExisting)
                return typedExisting;

            // 2. 获取或创建连接
            var parser = CreateParserFromAttribute<TDevice>();
            var queue = GetOrCreateConnection(connectionId, channel, parser);

            // 3. 创建设备实例
            var device = CreateDeviceInstance<TDevice>(queue, deviceId);

            // 4. 注册到连接和字典
            queue.RegisterDevice(device);
            if (!_devices.TryAdd(key, device))
            {
                throw new InvalidOperationException($"注册设备 {key} 失败");
            }

            OnLog($"设备 {deviceId}（类型 {typeof(TDevice).Name}）已创建并注册到连接 {connectionId}");
            return device;
        }

        // ========== 新增：配置驱动方法 ==========

        /// <summary>
        /// 获取或创建设备（配置驱动）
        /// </summary>
        /// <typeparam name="TDevice">设备代理类型</typeparam>
        /// <param name="connectionId">连接唯一标识</param>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="connectionConfig">连接配置字符串</param>
        /// <returns>设备代理实例</returns>
        /// <remarks>
        /// 支持多种配置格式：
        /// <list type="bullet">
        /// <item><description>键值对：type=tcp;host=192.168.1.100;port=502</description></item>
        /// <item><description>URI：tcp://192.168.1.100:502</description></item>
        /// <item><description>串口URI：serial://COM3:9600</description></item>
        /// <item><description>推断格式：192.168.1.100:502 或 COM3:9600</description></item>
        /// </list>
        /// </remarks>
        public TDevice GetOrCreateDevice<TDevice>(
            string connectionId,
            string deviceId,
            string connectionConfig) where TDevice : class, IDeviceProxy
        {
            if (string.IsNullOrEmpty(connectionId))
                throw new ArgumentException("连接ID不能为空", nameof(connectionId));

            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (string.IsNullOrEmpty(connectionConfig))
                throw new ArgumentException("连接配置不能为空", nameof(connectionConfig));


            var key = $"{connectionId}:{deviceId}";

            // 1. 检查设备池
            if (_devices.TryGetValue(key, out var existing) && existing is TDevice typedExisting)
                return typedExisting;

            // 2. 解析配置
            var config = ConnectionConfigParser.Parse(connectionConfig);

            // 3. 创建通道
            var channel = CreateChannelFromConfig(config);

            // 4. 创建解析器
            var parser = CreateParserFromAttribute<TDevice>();

            // 5. 打开通道
            try
            {
                channel.OpenAsync().GetAwaiter().GetResult();
                OnLog($"通道已打开: {config}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"打开通道失败: {connectionConfig}", ex);
            }

            // 6. 获取或创建连接队列器
            var queue = GetOrCreateConnection(connectionId, channel, parser);

            // 7. 创建设备实例
            var device = CreateDeviceInstance<TDevice>(queue, deviceId);

            // 8. 注册设备
            queue.RegisterDevice(device);
            if (!_devices.TryAdd(key, device))
            {
                (device as IDisposable)?.Dispose();
                throw new InvalidOperationException($"注册设备 {key} 失败");
            }

            OnLog($"设备 {deviceId}（类型 {typeof(TDevice).Name}）已创建，配置: {connectionConfig}");
            return device;
        }

        /// <summary>
        /// 根据配置创建通道
        /// </summary>
        private ICommChannel CreateChannelFromConfig(ConnectionConfig config)
        {
            return config.Type switch
            {
                ConnectionType.Tcp => new TcpChannel(config.Host ?? "127.0.0.1", config.Port ?? 502),
                ConnectionType.Serial => new SerialChannel(config.PortName ?? "COM1", config.BaudRate ?? 9600),
                ConnectionType.Udp => new UdpChannel(config.LocalPort ?? 0),
                _ => throw new NotSupportedException($"不支持的连接类型: {config.Type}")
            };
        }

        /// <summary>
        /// 获取已注册的设备
        /// </summary>
        public TDevice? GetDevice<TDevice>(string connectionId, string deviceId) where TDevice : class, IDeviceProxy
        {
            var key = $"{connectionId}:{deviceId}";
            if (_devices.TryGetValue(key, out var device) && device is TDevice typed)
                return typed;
            return null;
        }

        /// <summary>
        /// 移除设备
        /// </summary>
        public bool RemoveDevice(string connectionId, string deviceId)
        {
            var key = $"{connectionId}:{deviceId}";
            if (_devices.TryRemove(key, out _))
            {
                OnLog($"设备 {deviceId} 已从连接 {connectionId} 移除");
                return true;
            }
            return false;
        }

        // ========== 内部辅助方法 ==========

        /// <summary>
        /// 通过特性创建协议解析器
        /// </summary>
        private IProtocolParser CreateParserFromAttribute<TDevice>() where TDevice : IDeviceProxy
        {
            var attr = typeof(TDevice).GetCustomAttribute<ProtocolParserAttribute>();
            if (attr == null)
                throw new InvalidOperationException(
                    $"设备代理类型 {typeof(TDevice).Name} 缺少 {nameof(ProtocolParserAttribute)} 特性标注");

            var parserType = attr.ParserType;
            if (!typeof(IProtocolParser).IsAssignableFrom(parserType))
                throw new InvalidOperationException(
                    $"解析器类型 {parserType.Name} 未实现 {nameof(IProtocolParser)} 接口");

            return (IProtocolParser)Activator.CreateInstance(parserType)!;
        }

        /// <summary>
        /// 创建设备实例
        /// </summary>
        private TDevice CreateDeviceInstance<TDevice>(ConnectionQueue queue, string deviceId) where TDevice : IDeviceProxy
        {
            // 尝试通过构造函数 (ConnectionQueue, string) 创建
            var ctor = typeof(TDevice).GetConstructor(new[] { typeof(ConnectionQueue), typeof(string) });
            if (ctor != null)
                return (TDevice)ctor.Invoke(new object[] { queue, deviceId });

            // 尝试通过无参构造函数创建（然后通过属性注入）
            var ctorParamless = typeof(TDevice).GetConstructor(Type.EmptyTypes);
            if (ctorParamless != null)
            {
                var device = (TDevice)ctorParamless.Invoke(Array.Empty<object>());
                // 假设有 Queue 和 DeviceId 属性
                var queueProp = typeof(TDevice).GetProperty("Queue");
                var deviceIdProp = typeof(TDevice).GetProperty("DeviceId");
                queueProp?.SetValue(device, queue);
                deviceIdProp?.SetValue(device, deviceId);
                return device;
            }

            throw new InvalidOperationException(
                $"无法创建 {typeof(TDevice).Name} 实例。请确保有构造函数 (ConnectionQueue, string) 或 (无参) + 属性注入");
        }

        /// <summary>
        /// 获取连接，若不存在则根据设备代理创建（自动查找已注册设备对应的连接）
        /// </summary>
        private ConnectionQueue GetOrCreateConnectionForDevice<T>(string connectionId, T deviceProxy) where T : IDeviceProxy
        {
            if (_connections.TryGetValue(connectionId, out var queue))
                return queue;

            // 如果连接不存在，尝试通过设备代理的特性创建解析器
            var parser = CreateParserFromAttribute<T>();
            throw new InvalidOperationException(
                $"连接 {connectionId} 不存在。请先使用 GetOrCreateConnection 或 GetOrCreateDevice 创建连接。");
        }

        // ========== 日志 ==========

        private void OnLog(string message, Exception? ex = null)
        {
            LogEvent?.Invoke($"[DeviceManager] {message}", ex);
        }

        // ========== 资源释放 ==========

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            OnLog("正在释放 DeviceManager...");

            foreach (var key in _connections.Keys)
            {
                if (_connections.TryRemove(key, out var queue))
                {
                    queue.LogEvent -= (msg, ex) => { };
                    queue.Dispose();
                }
            }
            _devices.Clear();

            OnLog("DeviceManager 已释放");
        }
    }
}