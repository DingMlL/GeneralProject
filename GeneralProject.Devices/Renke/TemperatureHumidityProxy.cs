using System;
using System.Threading.Tasks;
using GeneralProject.Transport.Core;
using GeneralProject.Transport.Parser;
using GeneralProject.Transport.Proxy;

namespace GeneralProject.Transport.Devices.Renke
{
    /// <summary>
    /// 山东仁科 86 壳液晶温湿度变送器（485型）设备代理。
    /// 支持读取温度和湿度值。
    /// </summary>
    [ProtocolParser(typeof(RenkeParser))]
    public sealed class TemperatureHumidityProxy : DeviceProxyBase<object>
    {
        private readonly byte _address;

        /// <summary>
        /// 初始化温湿度变送器代理
        /// </summary>
        /// <param name="queue">连接队列器</param>
        /// <param name="deviceId">ModBus 从站地址（1~254）</param>
        /// <exception cref="ArgumentNullException">queue 为 null</exception>
        /// <exception cref="ArgumentException">deviceId 无法解析为有效字节地址</exception>
        public TemperatureHumidityProxy(ConnectionQueue queue, string deviceId)
            : base(queue, deviceId)
        {
            if (!byte.TryParse(deviceId, out byte addr) || addr == 0)
            {
                throw new ArgumentException($"无效的设备地址: {deviceId}，应为 1~254", nameof(deviceId));
            }
            _address = addr;
        }

        /// <summary>
        /// 读取当前温度值（寄存器 0x0001）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>温度值（摄氏度），实际值 = 返回值 / 10</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task<float> ReadTemperatureAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0001, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseSingleValue(response, 0x03);
        }

        /// <summary>
        /// 读取当前湿度值（寄存器 0x0000）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>湿度值（百分比），实际值 = 返回值 / 10</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task<float> ReadHumidityAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0000, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseSingleValue(response, 0x03);
        }

        #region 辅助方法

        private ushort GetMatchKey(byte functionCode)
        {
            return (ushort)((_address << 8) | functionCode);
        }

        private byte[] BuildReadRequest(ushort registerAddress, ushort quantity)
        {
            byte[] frame = new byte[8];
            frame[0] = _address;
            frame[1] = 0x03;
            frame[2] = (byte)(registerAddress >> 8);
            frame[3] = (byte)(registerAddress & 0xFF);
            frame[4] = (byte)(quantity >> 8);
            frame[5] = (byte)(quantity & 0xFF);
            ushort crc = RenkeParser.CalculateCrc(frame, 0, 6);
            frame[6] = (byte)(crc & 0xFF);
            frame[7] = (byte)(crc >> 8);
            return frame;
        }

        private float ParseSingleValue(byte[] response, byte expectedFunction)
        {
            int raw = ParseRawValue(response, expectedFunction);
            return raw / 10.0f;
        }

        private int ParseRawValue(byte[] response, byte expectedFunction)
        {
            CheckModbusResponse(response, expectedFunction);
            byte[] data = new byte[2];
            data[0] = response[3];
            data[1] = response[4];
            int value = (data[0] << 8) | data[1];
            if (value >= 0x8000)
            {
                value -= 0x10000; // 有符号
            }
            return value;
        }

        private void CheckModbusResponse(byte[] response, byte expectedFunction)
        {
            if (response == null || response.Length < 3)
            {
                throw new InvalidOperationException("响应帧长度不足");
            }
            byte function = response[1];
            if ((function & 0x80) != 0)
            {
                byte exceptionCode = response[2];
                throw new Exception($"ModBus 异常响应，异常码: {exceptionCode}");
            }
            if (function != expectedFunction)
            {
                throw new Exception($"功能码不匹配，期望 {expectedFunction}，实际 {function}");
            }
        }

        #endregion
    }
}