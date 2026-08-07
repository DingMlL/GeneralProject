using System;

namespace GeneralProject.Transport.Server
{
    /// <summary>
    /// 客户端数据接收事件参数
    /// </summary>
    public class ClientDataEventArgs : EventArgs
    {
        /// <summary> 发送数据的客户端会话 </summary>
        public IClientSession Session { get; }

        /// <summary> 接收到的数据 </summary>
        public byte[] Data { get; }

        public ClientDataEventArgs(IClientSession session, byte[] data)
        {
            Session = session;
            Data = data;
        }
    }
}