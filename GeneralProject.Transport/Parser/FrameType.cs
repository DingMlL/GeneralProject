using System;

namespace GeneralProject.Transport.Parser
{
    /// <summary>
    /// 帧类型（用于连接队列器分流）
    /// </summary>
    /// <remarks>
    /// 这是协议解析器 <see cref="IProtocolParser.GetFrameType"/> 方法的返回值。
    /// 连接队列器根据此值决定数据走哪条管道：
    /// 
    /// <list type="bullet">
    /// <item><description><see cref="Response"/>：走请求-响应管道，匹配等待字典</description></item>
    /// <item><description><see cref="Report"/>：走主动上报管道，触发设备代理的 <see cref="IDeviceProxy.HandleReport"/></description></item>
    /// </list>
    /// 
    /// 判断规则（由协议解析器实现）：
    /// <list type="number">
    /// <item><description>正常请求-响应的回复（包含匹配键）→ Response</description></item>
    /// <item><description>设备主动推送的帧（无匹配键）→ Report</description></item>
    /// <item><description>异常/错误响应 → Response（它是对请求的回复）</description></item>
    /// <item><description>无法确定时 → 优先返回 Response（宁丢不错）</description></item>
    /// </list>
    /// </remarks>
    public enum FrameType
    {
        /// <summary>
        /// 对请求的回复（走请求-响应管道，匹配等待字典）
        /// </summary>
        Response = 0,

        /// <summary>
        /// 设备主动上报（走事件管道，不匹配等待字典）
        /// </summary>
        Report = 1
    }
}