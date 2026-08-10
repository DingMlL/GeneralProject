using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace GeneralProject.Transport.Factory
{
    /// <summary>
    /// 连接配置解析器
    /// </summary>
    /// <remarks>
    /// 支持多种格式的配置字符串，自动识别并转换为统一的 <see cref="ConnectionConfig"/> 模型。
    /// 
    /// 支持的格式：
    /// <list type="bullet">
    /// <item><description>键值对：type=tcp;host=192.168.1.100;port=502</description></item>
    /// <item><description>URI 格式：tcp://192.168.1.100:502</description></item>
    /// <item><description>串口 URI：serial://COM3:9600</description></item>
    /// <item><description>UDP URI：udp://0.0.0.0:9999?remote=255.255.255.255:8888</description></item>
    /// <item><description>老式简写：com=COM3;baud=9600</description></item>
    /// <item><description>推断格式：192.168.1.100:502 或 COM3:9600</description></item>
    /// </list>
    /// </remarks>
    public static class ConnectionConfigParser
    {
        /// <summary>
        /// 解析连接配置字符串
        /// </summary>
        /// <param name="input">配置字符串</param>
        /// <returns>统一配置模型</returns>
        /// <exception cref="ArgumentException">当配置字符串无效时抛出</exception>
        /// <exception cref="NotSupportedException">当配置格式不支持时抛出</exception>
        public static ConnectionConfig Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("连接配置不能为空", nameof(input));

            input = input.Trim();

            // ===== 格式1：URI 格式（包含 ://） =====
            // 示例：tcp://192.168.1.100:502
            //       serial://COM3:9600
            //       udp://0.0.0.0:9999?remote=255.255.255.255:8888
            if (input.Contains("://"))
                return ParseUri(input);

            // ===== 格式2：键值对格式（包含 type=） =====
            // 示例：type=tcp;host=192.168.1.100;port=502
            if (input.Contains("type=", StringComparison.OrdinalIgnoreCase) ||
                input.Contains(";", StringComparison.OrdinalIgnoreCase))
                return ParseKeyValue(input);

            // ===== 格式3：老式简写 =====
            // 示例：com=COM3;baud=9600
            if (input.StartsWith("com=", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("serial=", StringComparison.OrdinalIgnoreCase))
                return ParseLegacy(input);

            // ===== 格式4：单一段落（推断格式） =====
            // 示例：192.168.1.100:502
            //       COM3:9600
            return ParseInferred(input);
        }

        // ========== 格式1：URI 格式解析 ==========

        private static ConnectionConfig ParseUri(string input)
        {
            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
                throw new ArgumentException($"无效的 URI 格式: {input}");

            var config = new ConnectionConfig();

            config.Type = uri.Scheme.ToLowerInvariant() switch
            {
                "tcp" or "tcpclient" => ConnectionType.Tcp,
                "serial" or "com" => ConnectionType.Serial,
                "udp" => ConnectionType.Udp,
                _ => throw new NotSupportedException($"不支持的 URI 协议: {uri.Scheme}")
            };

            switch (config.Type)
            {
                case ConnectionType.Tcp:
                    config.Host = uri.Host;
                    config.Port = uri.Port > 0 ? uri.Port : 502;
                    break;

                case ConnectionType.Serial:
                    config.PortName = uri.Host; // serial://COM3 → Host = "COM3"
                    if (uri.Port > 0)
                        config.BaudRate = uri.Port;
                    else
                        config.BaudRate = 9600;
                    break;

                case ConnectionType.Udp:
                    config.LocalPort = uri.Port > 0 ? uri.Port : 0;
                    config.Host = uri.Host;

                    // 解析 Query 参数：?remote=255.255.255.255:8888
                    if (!string.IsNullOrEmpty(uri.Query))
                    {
                        var query = ParseQueryString(uri.Query);
                        if (query.TryGetValue("remote", out var remote))
                        {
                            var parts = remote.Split(':');
                            config.RemoteHost = parts[0];
                            if (parts.Length > 1 && int.TryParse(parts[1], out var rp))
                                config.RemotePort = rp;
                        }
                        if (query.TryGetValue("localport", out var lp) && int.TryParse(lp, out var lPort))
                            config.LocalPort = lPort;
                    }
                    break;
            }

            return config;
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query) || query.Length < 2)
                return dict;

            var parts = query.Substring(1).Split('&');
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length == 2)
                    dict[kv[0]] = Uri.UnescapeDataString(kv[1]);
            }
            return dict;
        }

        // ========== 格式2：键值对格式解析 ==========

        private static ConnectionConfig ParseKeyValue(string input)
        {
            var dict = ParseKeyValuePairs(input);

            if (!dict.TryGetValue("type", out var type))
                throw new ArgumentException("键值对配置中缺少 type 字段");

            var config = new ConnectionConfig();

            config.Type = type.ToLowerInvariant() switch
            {
                "tcp" or "tcpclient" => ConnectionType.Tcp,
                "serial" or "com" => ConnectionType.Serial,
                "udp" => ConnectionType.Udp,
                _ => throw new NotSupportedException($"不支持的通道类型: {type}")
            };

            switch (config.Type)
            {
                case ConnectionType.Tcp:
                    config.Host = dict.GetValueOrDefault("host") ?? dict.GetValueOrDefault("ip");
                    if (dict.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var port))
                        config.Port = port;
                    else
                        config.Port = 502;
                    break;

                case ConnectionType.Serial:
                    config.PortName = dict.GetValueOrDefault("port") ?? dict.GetValueOrDefault("portname");
                    if (dict.TryGetValue("baudrate", out var baudStr) && int.TryParse(baudStr, out var baud))
                        config.BaudRate = baud;
                    else
                        config.BaudRate = 9600;

                    if (dict.TryGetValue("databits", out var dbStr) && int.TryParse(dbStr, out var db))
                        config.DataBits = db;
                    if (dict.TryGetValue("parity", out var parity))
                        config.Parity = parity;
                    if (dict.TryGetValue("stopbits", out var stopBits))
                        config.StopBits = stopBits;
                    break;

                case ConnectionType.Udp:
                    if (dict.TryGetValue("localport", out var lpStr) && int.TryParse(lpStr, out var lp))
                        config.LocalPort = lp;
                    else
                        config.LocalPort = 0;

                    config.Host = dict.GetValueOrDefault("host");
                    config.RemoteHost = dict.GetValueOrDefault("remotehost");
                    if (dict.TryGetValue("remoteport", out var rpStr) && int.TryParse(rpStr, out var rp))
                        config.RemotePort = rp;
                    break;
            }

            // 保存额外参数
            foreach (var kv in dict)
            {
                if (!config.Extras.ContainsKey(kv.Key))
                    config.Extras[kv.Key] = kv.Value;
            }

            return config;
        }

        private static Dictionary<string, string> ParseKeyValuePairs(string input)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 用分号分割，但也支持 & 符号
            var separator = input.Contains(';') ? ';' : '&';
            var parts = input.Split(separator);

            foreach (var part in parts)
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length == 2)
                {
                    var key = kv[0].Trim();
                    var value = kv[1].Trim();
                    if (!string.IsNullOrEmpty(key))
                        dict[key] = value;
                }
            }

            return dict;
        }

        // ========== 格式3：老式简写解析 ==========

        private static ConnectionConfig ParseLegacy(string input)
        {
            var dict = ParseKeyValuePairs(input);
            var config = new ConnectionConfig();

            if (dict.TryGetValue("com", out var comPort) || dict.TryGetValue("serial", out comPort))
            {
                config.Type = ConnectionType.Serial;
                config.PortName = comPort;
                config.BaudRate = dict.TryGetValue("baud", out var baudStr) && int.TryParse(baudStr, out var baud)
                    ? baud
                    : 9600;
            }
            else if (dict.TryGetValue("host", out var host) || dict.TryGetValue("ip", out host))
            {
                config.Type = ConnectionType.Tcp;
                config.Host = host;
                config.Port = dict.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var port)
                    ? port
                    : 502;
            }
            else
            {
                throw new ArgumentException($"无法识别的简写配置: {input}");
            }

            return config;
        }

        // ========== 格式4：推断格式解析 ==========

        private static ConnectionConfig ParseInferred(string input)
        {
            var config = new ConnectionConfig();

            // 判断是否是串口格式（以 COM 开头）
            var comMatch = Regex.Match(input, @"^(COM\d+):?(\d*)$", RegexOptions.IgnoreCase);
            if (comMatch.Success)
            {
                config.Type = ConnectionType.Serial;
                config.PortName = comMatch.Groups[1].Value;
                config.BaudRate = !string.IsNullOrEmpty(comMatch.Groups[2].Value)
                    ? int.Parse(comMatch.Groups[2].Value)
                    : 9600;
                return config;
            }

            // 判断是否是 IP:Port 格式
            var ipMatch = Regex.Match(input, @"^(\d+\.\d+\.\d+\.\d+):(\d+)$");
            if (ipMatch.Success)
            {
                config.Type = ConnectionType.Tcp;
                config.Host = ipMatch.Groups[1].Value;
                config.Port = int.Parse(ipMatch.Groups[2].Value);
                return config;
            }

            // 判断是否是域名:Port 格式
            var hostMatch = Regex.Match(input, @"^([a-zA-Z0-9\-\.]+):(\d+)$");
            if (hostMatch.Success)
            {
                config.Type = ConnectionType.Tcp;
                config.Host = hostMatch.Groups[1].Value;
                config.Port = int.Parse(hostMatch.Groups[2].Value);
                return config;
            }

            throw new ArgumentException($"无法推断连接格式: {input}");
        }
    }
}