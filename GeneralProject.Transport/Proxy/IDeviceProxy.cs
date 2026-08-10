using System;

namespace GeneralProject.Transport.Proxy
{
    /// <summary>
    /// 设备代理接口（非泛型，供连接队列器统一调用）
    /// </summary>
    /// <remarks>
    /// 此接口由 <see cref="ConnectionQueue"/> 持有，用于：
    /// <list type="bullet">
    /// <item><description>主动上报路由：通过 <see cref="HandleReport"/> 将上报帧分发给对应设备</description></item>
    /// <item><description>设备标识：通过 <see cref="DeviceId"/> 进行路由匹配</description></item>
    /// </list>
    /// 
    /// 业务层不应直接使用此接口，而应继承 <see cref="DeviceProxyBase{T}"/> 获取强类型的业务方法。
    /// </remarks>
    public interface IDeviceProxy
    {
        /// <summary>
        /// 设备唯一标识（如 Modbus 地址 "1"、"2"）
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// 处理主动上报帧（由连接队列器调用）
        /// </summary>
        /// <param name="frame">完整的协议帧</param>
        /// <remarks>
        /// 实现规则：
        /// 1. 此方法由 <see cref="ConnectionQueue"/> 在识别到 <see cref="FrameType.Report"/> 时调用
        /// 2. 实现类应将帧翻译为业务对象，然后触发 <see cref="DeviceProxyBase{T}.PassiveReport"/> 事件
        /// 3. 不应在此方法中做耗时操作
        /// </remarks>
        void HandleReport(byte[] frame);
    }
}