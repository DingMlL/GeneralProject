using GeneralProject.Transport.Channels;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralProject.Transport.Server
{
    /// <summary>
    /// 客户端会话（一个 TCP 连接）
    /// </summary>
    /// <remarks>
    /// 实现 <see cref="ICommChannel"/> 接口，可无缝对接 <see cref="ConnectionQueue"/>。
    /// 
    /// 生命周期：
    /// - 由 <see cref="ITcpServer"/> 在客户端连接时创建
    /// - 客户端断开时自动销毁
    /// - 业务层不应手动创建或销毁
    /// 
    /// 使用示例：
    /// <code>
    /// server.ClientConnected += (s, session) =>
    /// {
    ///     var parser = new MyProtocolParser();
    ///     var queue = new ConnectionQueue(session, parser);
    ///     var device = new MyDevice(queue);
    /// };
    /// </code>
    /// </remarks>
    public interface IClientSession : ICommChannel
    {
        /// <summary>
        /// 会话唯一标识（格式：IP:Port）
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// 客户端远程端点
        /// </summary>
        IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// 连接建立时间
        /// </summary>
        DateTime ConnectedAt { get; }

        /// <summary>
        /// 用户自定义数据
        /// </summary>
        object? Tag { get; set; }

        /// <summary>
        /// 断开连接
        /// </summary>
        Task DisconnectAsync();
    }
}