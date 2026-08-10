using System;
using System.Threading.Tasks;
using GeneralProject.Transport.Core;
using GeneralProject.Transport.Parser;
using GeneralProject.Transport.Proxy;

namespace GeneralProject.Transport.Devices.Renke
{
    /// <summary>
    /// 山东仁科 485 型吸顶式微波探测器设备代理。
    /// 支持读取报警状态，设置/读取延时报警时间和报警持续时间。
    /// </summary>
    [ProtocolParser(typeof(RenkeParser))]
    public sealed class MicrowaveDetectorProxy : DeviceProxyBase<object>
    {
        private readonly byte _address;

        /// <summary>
        /// 初始化微波探测器代理
        /// </summary>
        /// <param name="queue">连接队列器</param>
        /// <param name="deviceId">ModBus 从站地址（1~254）</param>
        /// <exception cref="ArgumentNullException">queue 为 null</exception>
        /// <exception cref="ArgumentException">deviceId 无法解析为有效字节地址</exception>
        public MicrowaveDetectorProxy(ConnectionQueue queue, string deviceId)
            : base(queue, deviceId)
        {
            if (!byte.TryParse(deviceId, out byte addr) || addr == 0)
            {
                throw new ArgumentException($"无效的设备地址: {deviceId}，应为 1~254", nameof(deviceId));
            }
            _address = addr;
        }

        #region 读取报警状态

        /// <summary>
        /// 读取当前报警状态（寄存器 0x0003）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>true 表示报警，false 表示正常</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task<bool> ReadAlarmStatusAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0003, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            int raw = ParseRawValue(response, 0x03);
            return raw == 1;
        }

        #endregion

        #region 延时报警时间（寄存器 0x0033，可读写）

        /// <summary>
        /// 读取延时报警时间（寄存器 0x0033）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>延时时间（秒），0 表示无延时</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task<int> ReadDelayAlarmTimeAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0033, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseRawValue(response, 0x03);
        }

        /// <summary>
        /// 设置延时报警时间（寄存器 0x0033）
        /// </summary>
        /// <param name="seconds">延时时间（秒），取值范围 0~65535，0 表示无延时</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task SetDelayAlarmTimeAsync(int seconds, int timeoutMs = 3000)
        {
            if (seconds < 0 || seconds > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "取值范围 0~65535");
            }
            byte[] request = BuildWriteSingleRequest(0x0033, (ushort)seconds);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        #endregion

        #region 报警持续时间（寄存器 0x0043，可读写）

        /// <summary>
        /// 读取报警持续时间（寄存器 0x0043）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>持续时间（秒），0 表示持续输出</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task<int> ReadAlarmDurationAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0043, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseRawValue(response, 0x03);
        }

        /// <summary>
        /// 设置报警持续时间（寄存器 0x0043）
        /// </summary>
        /// <param name="seconds">持续时间（秒），取值范围 0~65535，0 表示持续输出直到取消</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task SetAlarmDurationAsync(int seconds, int timeoutMs = 3000)
        {
            if (seconds < 0 || seconds > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "取值范围 0~65535");
            }
            byte[] request = BuildWriteSingleRequest(0x0043, (ushort)seconds);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        #endregion

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

        private byte[] BuildWriteSingleRequest(ushort registerAddress, ushort value)
        {
            byte[] frame = new byte[8];
            frame[0] = _address;
            frame[1] = 0x06;
            frame[2] = (byte)(registerAddress >> 8);
            frame[3] = (byte)(registerAddress & 0xFF);
            frame[4] = (byte)(value >> 8);
            frame[5] = (byte)(value & 0xFF);
            ushort crc = RenkeParser.CalculateCrc(frame, 0, 6);
            frame[6] = (byte)(crc & 0xFF);
            frame[7] = (byte)(crc >> 8);
            return frame;
        }

        private int ParseRawValue(byte[] response, byte expectedFunction)
        {
            CheckModbusResponse(response, expectedFunction);
            byte[] data = new byte[2];
            data[0] = response[3];
            data[1] = response[4];
            return (data[0] << 8) | data[1];
        }

        private void ValidateWriteSingleResponse(byte[] response)
        {
            CheckModbusResponse(response, 0x06);
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