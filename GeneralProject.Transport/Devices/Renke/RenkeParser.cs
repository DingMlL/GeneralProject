using System;
using System.Collections.Generic;
using GeneralProject.Transport.Parser;

namespace GeneralProject.Transport.Devices.Renke
{
    /// <summary>
    /// 山东仁科（Renke）系列设备通用 ModBus RTU 协议解析器。
    /// 所有 Renke 设备（空调控制器、温湿度变送器、微波探测器等）共用此解析器。
    /// </summary>
    /// <remarks>
    /// 解析器为无状态设计，所有状态由 <see cref="ConnectionQueue"/> 管理。
    /// 支持标准 ModBus RTU 帧的拆包、设备地址提取、匹配键提取和帧类型判断。
    /// </remarks>
    public sealed class RenkeParser : IProtocolParser
    {
        // CRC16 查表法所用表
        private static readonly ushort[] CrcTable = new ushort[256];

        static RenkeParser()
        {
            // 预计算 CRC16-Modbus 表
            for (ushort i = 0; i < 256; i++)
            {
                ushort crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
                CrcTable[i] = crc;
            }
        }

        /// <summary>
        /// 计算 ModBus RTU 帧的 CRC16 校验值（低字节在前）。
        /// </summary>
        /// <param name="buffer">待计算的数据缓冲区</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="length">数据长度（不含 CRC 本身）</param>
        /// <returns>16 位 CRC 值</returns>
        public static ushort CalculateCrc(byte[] buffer, int offset, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + length; i++)
            {
                byte index = (byte)(crc ^ buffer[i]);
                crc = (ushort)((crc >> 8) ^ CrcTable[index]);
            }
            return crc;
        }

        /// <summary>
        /// 从接收缓冲区中切出一个完整的 ModBus RTU 帧。
        /// </summary>
        /// <param name="buffer">接收缓冲区</param>
        /// <param name="offset">起始搜索位置</param>
        /// <param name="length">可用数据长度</param>
        /// <param name="frame">切出的完整帧</param>
        /// <returns>
        /// 成功返回帧长度；数据不够返回 0；校验失败或格式错误返回 -1 跳过当前字节。
        /// </returns>
        public int TryParseFrame(byte[] buffer, int offset, int length, out byte[]? frame)
        {
            frame = null;

            // 最少需要 4 字节：地址 + 功能码 + CRC(2)
            if (length < 4)
            {
                return 0;
            }

            int start = offset;
            byte address = buffer[start];
            byte functionCode = buffer[start + 1];

            // 根据功能码计算帧长度（不含 CRC）
            int dataLength = 0;
            bool isException = (functionCode & 0x80) != 0;

            if (isException)
            {
                // 异常响应：地址 + 功能码(0x80) + 异常码，共 3 字节
                dataLength = 3;
            }
            else
            {
                // 功能码 0x01, 0x02, 0x03, 0x04 读操作，响应包含字节数
                if (functionCode == 0x01 || functionCode == 0x02 || functionCode == 0x03 || functionCode == 0x04)
                {
                    // 至少需要 4 字节才能读取字节数
                    if (length < 4)
                    {
                        return 0;
                    }
                    byte byteCount = buffer[start + 2];
                    // 总长度 = 地址(1) + 功能码(1) + 字节数(1) + 数据(byteCount) + CRC(2)
                    dataLength = 1 + 1 + 1 + byteCount;
                }
                // 功能码 0x05, 0x06, 0x0F, 0x10 写操作，响应固定长度 8 字节
                else if (functionCode == 0x05 || functionCode == 0x06 || functionCode == 0x0F || functionCode == 0x10)
                {
                    dataLength = 8;
                }
                else
                {
                    // 未知功能码，跳过当前字节尝试重新同步
                    return -1;
                }
            }

            // 总帧长度 = dataLength + 2(CRC)
            int totalLength = dataLength + 2;

            // 检查缓冲区数据是否足够
            if (length < totalLength)
            {
                return 0;
            }

            // 校验 CRC
            ushort calcCrc = CalculateCrc(buffer, start, dataLength);
            ushort recvCrc = (ushort)((buffer[start + dataLength]) | (buffer[start + dataLength + 1] << 8));
            if (calcCrc != recvCrc)
            {
                // CRC 错误，跳过当前字节重新同步
                return -1;
            }

            // 复制完整帧
            frame = new byte[totalLength];
            Array.Copy(buffer, start, frame, 0, totalLength);
            return totalLength;
        }

        /// <summary>
        /// 从完整帧中提取设备地址（即 ModBus 从站地址）。
        /// </summary>
        /// <param name="frame">完整协议帧</param>
        /// <returns>地址字符串（如 "1"），提取失败返回 null</returns>
        public string? ExtractDeviceId(byte[] frame)
        {
            if (frame == null || frame.Length < 1)
            {
                return null;
            }
            return frame[0].ToString();
        }

        /// <summary>
        /// 从完整帧中提取匹配键，用于请求-响应精准匹配。
        /// ModBus RTU 无事务 ID，使用“设备地址 + 功能码”作为匹配键。
        /// </summary>
        /// <param name="frame">完整协议帧</param>
        /// <returns>匹配键（地址<<8 | 功能码）</returns>
        public ushort? ExtractMatchKey(byte[] frame)
        {
            if (frame == null || frame.Length < 2)
            {
                return null;
            }
            return (ushort)((frame[0] << 8) | frame[1]);
        }

        /// <summary>
        /// 判断帧类型。所有 ModBus RTU 帧均视为对请求的响应（无主动上报）。
        /// </summary>
        /// <param name="frame">完整协议帧</param>
        /// <returns>始终返回 <see cref="FrameType.Response"/></returns>
        public FrameType GetFrameType(byte[] frame)
        {
            return FrameType.Response;
        }
    }
}