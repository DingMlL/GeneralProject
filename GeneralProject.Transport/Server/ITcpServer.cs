using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralProject.Transport.Server
{
    /// <summary>
    /// TCP 服务端
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 监听端口，接受客户端连接
    /// - 管理所有客户端会话（ClientSession）
    /// - 提供广播、单播、连接管理等能力
    /// 
    /// 使用场景：
    /// 1. 数据采集服务器：多个设备主动连接上报数据
    /// 2. Modbus TCP 从站：多个上位机同时连接读写数据
    /// 3. 网关服务：接收设备数据并转发
    /// 
    /// 每个客户端连接对应一个 <see cref="IClientSession"/> 实例，
    /// 业务层通过 <see cref="ClientConnected"/> 事件获取会话，
    /// 然后为该会话创建独立的 <see cref="ConnectionQueue"/>。
    /// </remarks>
    public interface ITcpServer : IDisposable
    {
        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 监听端口
        /// </summary>
        int Port { get; }

        /// <summary>
        /// 当前连接的客户端数量
        /// </summary>
        int ClientCount { get; }

        /// <summary>
        /// 启动服务
        /// </summary>
        Task StartAsync(CancellationToken ct = default);

        /// <summary>
        /// 停止服务（断开所有客户端连接）
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 向所有客户端广播数据
        /// </summary>
        Task BroadcastAsync(byte[] data, CancellationToken ct = default);

        /// <summary>
        /// 向指定客户端发送数据
        /// </summary>
        Task SendToClientAsync(string clientId, byte[] data, CancellationToken ct = default);

        /// <summary>
        /// 断开指定客户端
        /// </summary>
        Task DisconnectClientAsync(string clientId);

        /// <summary>
        /// 获取所有在线客户端 ID
        /// </summary>
        string[] GetClientIds();

        /// <summary>
        /// 获取指定客户端会话
        /// </summary>
        IClientSession? GetClient(string clientId);

        /// <summary>
        /// 客户端连接时触发
        /// </summary>
        event EventHandler<IClientSession>? ClientConnected;

        /// <summary>
        /// 客户端断开时触发
        /// </summary>
        event EventHandler<IClientSession>? ClientDisconnected;

        /// <summary>
        /// 服务发生错误时触发
        /// </summary>
        event Action<Exception>? ErrorOccurred;
    }
}