using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralProject.Transport.Channels
{
    /// <summary>
    /// 物理通信通道接口
    /// </summary>
    /// <remarks>
    /// 实现类：
    /// - SerialChannel：串口通信
    /// - TcpChannel：TCP 客户端
    /// - UdpChannel：UDP 通信（支持广播/单播）
    /// - ClientSession：TCP 服务端中的客户端会话
    /// </remarks>
    public interface ICommChannel : IDisposable
    {
        /// <summary>
        /// 通道是否已打开
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// 通道名称（如 "COM3@9600" 或 "192.168.1.100:502"）
        /// </summary>
        string ChannelName { get; }

        /// <summary>
        /// 打开通道
        /// </summary>
        Task OpenAsync(CancellationToken ct = default);

        /// <summary>
        /// 关闭通道
        /// </summary>
        Task CloseAsync();

        /// <summary>
        /// 向默认目标发送数据
        /// </summary>
        /// <remarks>
        /// 适用场景：
        /// - 串口：发送到串口线
        /// - TCP 客户端：发送到已连接的服务器
        /// - TCP 服务端会话：发送到该会话对应的客户端
        /// - UDP：建议使用 <see cref="SendToAsync"/> 指定目标，此方法将抛出 <see cref="NotSupportedException"/>
        /// </remarks>
        Task WriteAsync(byte[] data, CancellationToken ct = default);

        /// <summary>
        /// 向指定端点发送数据（UDP 场景专用）
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <param name="remoteEndPoint">目标端点（IP + Port）</param>
        /// <param name="ct">取消令牌</param>
        /// <exception cref="NotSupportedException">当通道类型不支持指定目标发送时抛出</exception>
        Task SendToAsync(byte[] data, IPEndPoint remoteEndPoint, CancellationToken ct = default);

        /// <summary>
        /// 收到原始数据时触发
        /// </summary>
        /// <remarks>
        /// 参数说明：
        /// - byte[]：收到的数据
        /// - IPEndPoint：发送方端点（UDP 场景下不为 null；串口/TCP 场景下为 null）
        /// </remarks>
        event Action<byte[], IPEndPoint?> DataReceived;

        /// <summary>
        /// 通道发生错误时触发
        /// </summary>
        event Action<Exception>? ErrorOccurred;

        /// <summary>
        /// 通道打开时触发
        /// </summary>
        event EventHandler? Opened;

        /// <summary>
        /// 通道关闭时触发
        /// </summary>
        event EventHandler? Closed;
    }
}