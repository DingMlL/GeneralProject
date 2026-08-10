using System.Collections.Generic;

namespace GeneralProject.Transport.Factory
{
    /// <summary>
    /// 连接配置（统一内部模型）
    /// </summary>
    /// <remarks>
    /// 所有格式的连接配置字符串最终都会被解析为这个统一模型。
    /// </remarks>
    public sealed class ConnectionConfig
    {
        /// <summary>
        /// 连接类型
        /// </summary>
        public ConnectionType Type { get; set; }

        /// <summary>
        /// TCP 目标主机
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// TCP 目标端口
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// 串口名称（如 COM3）
        /// </summary>
        public string? PortName { get; set; }

        /// <summary>
        /// 串口波特率
        /// </summary>
        public int? BaudRate { get; set; }

        /// <summary>
        /// 串口数据位
        /// </summary>
        public int? DataBits { get; set; }

        /// <summary>
        /// 串口校验位（None, Odd, Even, Mark, Space）
        /// </summary>
        public string? Parity { get; set; }

        /// <summary>
        /// 串口停止位（One, Two, OnePointFive）
        /// </summary>
        public string? StopBits { get; set; }

        /// <summary>
        /// UDP 远程目标主机
        /// </summary>
        public string? RemoteHost { get; set; }

        /// <summary>
        /// UDP 远程目标端口
        /// </summary>
        public int? RemotePort { get; set; }

        /// <summary>
        /// UDP 本地端口
        /// </summary>
        public int? LocalPort { get; set; }

        /// <summary>
        /// 额外参数（用于扩展）
        /// </summary>
        public Dictionary<string, string> Extras { get; set; } = new();

        /// <summary>
        /// 获取额外参数的值
        /// </summary>
        public string? GetExtra(string key)
        {
            Extras.TryGetValue(key.ToLowerInvariant(), out var value);
            return value;
        }

        /// <summary>
        /// 获取额外参数的值（带默认值）
        /// </summary>
        public string GetExtraOrDefault(string key, string defaultValue = "")
        {
            return GetExtra(key) ?? defaultValue;
        }

        /// <summary>
        /// 获取额外参数的值（转换为 int）
        /// </summary>
        public int? GetExtraInt(string key)
        {
            if (Extras.TryGetValue(key.ToLowerInvariant(), out var value) && int.TryParse(value, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// 获取额外参数的值（转换为 int，带默认值）
        /// </summary>
        public int GetExtraIntOrDefault(string key, int defaultValue = 0)
        {
            return GetExtraInt(key) ?? defaultValue;
        }

        /// <summary>
        /// 获取额外参数的值（转换为 bool）
        /// </summary>
        public bool? GetExtraBool(string key)
        {
            if (Extras.TryGetValue(key.ToLowerInvariant(), out var value) && bool.TryParse(value, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// 获取额外参数的值（转换为 bool，带默认值）
        /// </summary>
        public bool GetExtraBoolOrDefault(string key, bool defaultValue = false)
        {
            return GetExtraBool(key) ?? defaultValue;
        }

        public override string ToString()
        {
            return Type switch
            {
                ConnectionType.Tcp => $"{Type}://{Host}:{Port}",
                ConnectionType.Serial => $"{Type}://{PortName}:{BaudRate}",
                ConnectionType.Udp => $"{Type}://0.0.0.0:{LocalPort}?remote={RemoteHost}:{RemotePort}",
                _ => base.ToString() ?? "Unknown"
            };
        }
    }

    /// <summary>
    /// 连接类型枚举
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>
        /// TCP 客户端
        /// </summary>
        Tcp,

        /// <summary>
        /// 串口
        /// </summary>
        Serial,

        /// <summary>
        /// UDP
        /// </summary>
        Udp
    }
}