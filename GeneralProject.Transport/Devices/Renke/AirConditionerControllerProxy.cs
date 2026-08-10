using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeneralProject.Transport.Core;
using GeneralProject.Transport.Parser;
using GeneralProject.Transport.Proxy;

namespace GeneralProject.Transport.Devices.Renke
{
    /// <summary>
    /// 空调控制器状态枚举
    /// </summary>
    public enum AirConditionerStatus
    {
        /// <summary>停止</summary>
        Stopped = 0,
        /// <summary>制冷</summary>
        Cooling = 1,
        /// <summary>制热</summary>
        Heating = 2,
        /// <summary>未知</summary>
        Unknown = 3
    }

    /// <summary>
    /// 山东仁科 RS-KTC-N01 空调控制器设备代理。
    /// 支持读取温湿度、电流、运行状态，以及发送制冷/制热/关机控制指令和自定义指令。
    /// 支持学习功能：学习制冷、制热、关机、自定义1-29。
    /// </summary>
    /// <remarks>
    /// 版本说明：
    /// <list type="bullet">
    /// <item><description>普通款 V16.07 及高采集率款 V17.04 之前：仅支持自定义 1-20</description></item>
    /// <item><description>普通款 V16.07 及高采集率款 V17.04 及以后：支持自定义 1-29</description></item>
    /// </list>
    /// 学习命令超时建议设置为 5000ms（设备学习需要时间）。
    /// </remarks>
    [ProtocolParser(typeof(RenkeParser))]
    public sealed class AirConditionerControllerProxy : DeviceProxyBase<object>
    {
        private readonly byte _address;

        /// <summary>
        /// 初始化空调控制器代理
        /// </summary>
        /// <param name="queue">连接队列器</param>
        /// <param name="deviceId">ModBus 从站地址（1~255）</param>
        /// <exception cref="ArgumentNullException">queue 为 null</exception>
        /// <exception cref="ArgumentException">deviceId 无法解析为有效字节地址</exception>
        public AirConditionerControllerProxy(ConnectionQueue queue, string deviceId)
            : base(queue, deviceId)
        {
            if (!byte.TryParse(deviceId, out byte addr) || addr == 0)
            {
                throw new ArgumentException($"无效的设备地址: {deviceId}，应为 1~255", nameof(deviceId));
            }
            _address = addr;
        }

        #region 读取方法

        /// <summary>
        /// 读取当前温度值（寄存器 0x0001）
        /// </summary>
        public async Task<float> ReadTemperatureAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0001, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseSingleValue(response, 0x03);
        }

        /// <summary>
        /// 读取当前湿度值（寄存器 0x0000）
        /// </summary>
        public async Task<float> ReadHumidityAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0000, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseSingleValue(response, 0x03);
        }

        /// <summary>
        /// 读取第一路电流值（寄存器 0x0071）
        /// </summary>
        public async Task<float> ReadCurrentChannel1Async(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0071, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseSingleValue(response, 0x03, isSigned: false);
        }

        /// <summary>
        /// 读取第二路电流值（寄存器 0x0072）
        /// </summary>
        public async Task<float> ReadCurrentChannel2Async(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0072, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseSingleValue(response, 0x03, isSigned: false);
        }

        /// <summary>
        /// 读取第一路空调运行状态（寄存器 0x00D7）
        /// </summary>
        public async Task<AirConditionerStatus> ReadStatusChannel1Async(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x00D7, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            int value = ParseRawValue(response, 0x03, isSigned: false);
            return (AirConditionerStatus)value;
        }

        /// <summary>
        /// 读取第二路空调运行状态（寄存器 0x00D8）
        /// </summary>
        public async Task<AirConditionerStatus> ReadStatusChannel2Async(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x00D8, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            int value = ParseRawValue(response, 0x03, isSigned: false);
            return (AirConditionerStatus)value;
        }

        #endregion

        #region 发射指令

        /// <summary>
        /// 发送开机制冷指令（寄存器 0x00B9，写入 0x0001）
        /// </summary>
        public async Task SendCoolingOnAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildWriteSingleRequest(0x00B9, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 发送开机制热指令（寄存器 0x00BA，写入 0x0001）
        /// </summary>
        public async Task SendHeatingOnAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildWriteSingleRequest(0x00BA, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 发送关机指令（寄存器 0x00BB，写入 0x0001）
        /// </summary>
        public async Task SendOffAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildWriteSingleRequest(0x00BB, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 发送自定义指令（1-20）
        /// </summary>
        /// <param name="index">自定义指令编号（1-20）</param>
        public async Task SendCustomAsync(int index, int timeoutMs = 3000)
        {
            if (index < 1 || index > 20)
                throw new ArgumentOutOfRangeException(nameof(index), "自定义指令编号范围为 1-20");

            ushort register = (ushort)(0x00BC + (index - 1)); // 0x00BC ~ 0x00CF
            byte[] request = BuildWriteSingleRequest(register, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 发送自定义指令（21-29）（需设备固件支持）
        /// </summary>
        /// <param name="index">自定义指令编号（21-29）</param>
        /// <exception cref="NotSupportedException">设备固件不支持该指令</exception>
        public async Task SendCustomExtendedAsync(int index, int timeoutMs = 3000)
        {
            if (index < 21 || index > 29)
                throw new ArgumentOutOfRangeException(nameof(index), "自定义指令编号范围为 21-29");

            ushort register = (ushort)(0x1100 + (index - 21)); // 0x1100 ~ 0x111C
            byte[] request = BuildWriteSingleRequest(register, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        #endregion

        #region 学习指令（超时 5000ms）

        /// <summary>
        /// 学习制冷指令（寄存器 0x0007，写入 0x0001）
        /// </summary>
        /// <remarks>超时时间默认为 5000ms，设备学习需要时间</remarks>
        public async Task LearnCoolingAsync(int timeoutMs = 5000)
        {
            byte[] request = BuildWriteSingleRequest(0x0007, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 学习制热指令（寄存器 0x0008，写入 0x0001）
        /// </summary>
        /// <remarks>超时时间默认为 5000ms，设备学习需要时间</remarks>
        public async Task LearnHeatingAsync(int timeoutMs = 5000)
        {
            byte[] request = BuildWriteSingleRequest(0x0008, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 学习关机指令（寄存器 0x0009，写入 0x0001）
        /// </summary>
        /// <remarks>超时时间默认为 5000ms，设备学习需要时间</remarks>
        public async Task LearnOffAsync(int timeoutMs = 5000)
        {
            byte[] request = BuildWriteSingleRequest(0x0009, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 学习自定义指令（1-20）
        /// </summary>
        /// <param name="index">自定义指令编号（1-20）</param>
        /// <remarks>超时时间默认为 5000ms，设备学习需要时间</remarks>
        public async Task LearnCustomAsync(int index, int timeoutMs = 5000)
        {
            if (index < 1 || index > 20)
                throw new ArgumentOutOfRangeException(nameof(index), "自定义指令编号范围为 1-20");

            ushort register = (ushort)(0x000A + (index - 1)); // 0x000A ~ 0x001D
            byte[] request = BuildWriteSingleRequest(register, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 学习自定义指令（21-29）（需设备固件支持）
        /// </summary>
        /// <param name="index">自定义指令编号（21-29）</param>
        /// <exception cref="NotSupportedException">设备固件不支持该指令</exception>
        /// <remarks>超时时间默认为 5000ms，设备学习需要时间</remarks>
        public async Task LearnCustomExtendedAsync(int index, int timeoutMs = 5000)
        {
            if (index < 21 || index > 29)
                throw new ArgumentOutOfRangeException(nameof(index), "自定义指令编号范围为 21-29");

            ushort register = (ushort)(0x1000 + (index - 21)); // 0x1000 ~ 0x101C
            byte[] request = BuildWriteSingleRequest(register, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        #endregion

        #region 辅助构建与解析

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

        private float ParseSingleValue(byte[] response, byte expectedFunction, bool isSigned = true)
        {
            int raw = ParseRawValue(response, expectedFunction, isSigned);
            return raw / 10.0f;
        }

        private int ParseRawValue(byte[] response, byte expectedFunction, bool isSigned)
        {
            CheckModbusResponse(response, expectedFunction);
            byte[] data = new byte[2];
            data[0] = response[3];
            data[1] = response[4];
            int value = (data[0] << 8) | data[1];
            if (isSigned && value >= 0x8000)
            {
                value -= 0x10000;
            }
            return value;
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