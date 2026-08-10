using System;
using System.Threading.Tasks;
using GeneralProject.Transport.Core;
using GeneralProject.Transport.Parser;

namespace GeneralProject.Transport.Proxy
{
    /// <summary>
    /// 设备代理基类（泛型）
    /// </summary>
    /// <typeparam name="TResult">业务结果类型（如 float、bool、EPCData[]）</typeparam>
    /// <remarks>
    /// 业务层通过继承此类实现具体的设备操作。
    /// 
    /// 使用示例：
    /// <code>
    /// [ProtocolParser(typeof(ModbusRtuParser))]
    /// public class TemperatureProxy : DeviceProxyBase&lt;float&gt;
    /// {
    ///     public TemperatureProxy(ConnectionQueue queue, string deviceId) 
    ///         : base(queue, deviceId) 
    ///     { }
    ///     
    ///     public async Task&lt;float&gt; ReadTemperatureAsync()
    ///     {
    ///         var request = BuildReadRequest();
    ///         var response = await SendAsync(request, 0x0001, 3000);
    ///         return ParseTemperature(response);
    ///     }
    /// }
    /// </code>
    /// 
    /// 主动上报处理：
    /// 子类重写 <see cref="OnReportReceived"/> 方法，将帧翻译为业务对象并触发 <see cref="PassiveReport"/> 事件。
    /// </remarks>
    public abstract class DeviceProxyBase<TResult> : IDeviceProxy
    {
        // ========== 核心字段 ==========

        /// <summary>
        /// 连接队列器（提供发送能力）
        /// </summary>
        protected ConnectionQueue Queue { get; }

        /// <summary>
        /// 设备唯一标识
        /// </summary>
        public string DeviceId { get; }

        // ========== 事件 ==========

        /// <summary>
        /// 主动上报事件（业务层订阅）
        /// </summary>
        /// <remarks>
        /// 当设备主动推送数据时触发，携带翻译后的强类型业务对象。
        /// </remarks>
        public event Action<TResult>? PassiveReport;

        // ========== 构造函数 ==========

        /// <summary>
        /// 初始化设备代理
        /// </summary>
        /// <param name="queue">连接队列器</param>
        /// <param name="deviceId">设备唯一标识</param>
        protected DeviceProxyBase(ConnectionQueue queue, string deviceId)
        {
            Queue = queue ?? throw new ArgumentNullException(nameof(queue));
            DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        }

        // ========== 发送方法（供子类调用） ==========

        /// <summary>
        /// 发送请求并等待响应（异步等待模式）
        /// </summary>
        /// <param name="request">请求字节数组</param>
        /// <param name="matchKey">匹配键（用于响应匹配）</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <param name="priority">命令优先级</param>
        /// <returns>响应字节数组</returns>
        /// <exception cref="TimeoutException">超时时抛出</exception>
        /// <exception cref="InvalidOperationException">连接未打开时抛出</exception>
        protected async Task<byte[]> SendAsync(
            byte[] request,
            ushort matchKey,
            int timeoutMs = 3000,
            CommandPriority priority = CommandPriority.Normal)
        {
            return await Queue.SendAsync(request, matchKey, timeoutMs, priority);
        }

        /// <summary>
        /// 发送请求，通过回调接收响应（回调模式）
        /// </summary>
        /// <param name="request">请求字节数组</param>
        /// <param name="matchKey">匹配键（用于响应匹配）</param>
        /// <param name="callback">响应回调</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <param name="priority">命令优先级</param>
        /// <remarks>
        /// 此方法立即返回，不阻塞调用线程。
        /// 收到响应或超时时，回调方法在后台线程中执行。
        /// </remarks>
        protected void SendWithCallback(
            byte[] request,
            ushort matchKey,
            Action<byte[]> callback,
            int timeoutMs = 3000,
            CommandPriority priority = CommandPriority.Normal)
        {
            Queue.SendWithCallback(request, matchKey, callback, timeoutMs, priority);
        }

        /// <summary>
        /// 发送请求，不等待响应（发完即忘）
        /// </summary>
        /// <param name="request">请求字节数组</param>
        /// <param name="priority">命令优先级</param>
        /// <remarks>
        /// 适用于不需要设备回复的场景（如复位命令、日志上传等）。
        /// </remarks>
        protected void SendOnly(byte[] request, CommandPriority priority = CommandPriority.Normal)
        {
            Queue.SendOnly(request, priority);
        }

        // ========== IDeviceProxy 实现 ==========

        /// <summary>
        /// 处理主动上报帧（由连接队列器调用）
        /// </summary>
        void IDeviceProxy.HandleReport(byte[] frame)
        {
            OnReportReceived(frame);
        }

        /// <summary>
        /// 子类重写：处理主动上报帧
        /// </summary>
        /// <param name="frame">完整的协议帧</param>
        /// <remarks>
        /// 子类应在此方法中：
        /// 1. 调用协议解析器翻译帧为业务对象
        /// 2. 触发 <see cref="PassiveReport"/> 事件
        /// 
        /// 示例：
        /// <code>
        /// protected override void OnReportReceived(byte[] frame)
        /// {
        ///     var data = _parser.ParseReport(frame);
        ///     PassiveReport?.Invoke(data);
        /// }
        /// </code>
        /// </remarks>
        protected virtual void OnReportReceived(byte[] frame)
        {
            // 默认不做任何处理，子类可重写
        }

        // ========== 资源释放 ==========

        /// <summary>
        /// 释放资源
        /// </summary>
        public virtual void Dispose()
        {
            // 基类无特殊资源需要释放，子类可重写
        }
    }
}