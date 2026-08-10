using System;
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
    /// 支持读取温湿度、电流、运行状态，以及发送制冷/制热/关机控制指令。
    /// </summary>
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
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>温度值（摄氏度）</returns>
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
        /// <returns>湿度值（百分比）</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task<float> ReadHumidityAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0000, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseSingleValue(response, 0x03);
        }

        /// <summary>
        /// 读取第一路电流值（寄存器 0x0071）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>电流值（安培）</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task<float> ReadCurrentChannel1Async(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0071, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseSingleValue(response, 0x03, isSigned: false);
        }

        /// <summary>
        /// 读取第二路电流值（寄存器 0x0072）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>电流值（安培）</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task<float> ReadCurrentChannel2Async(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x0072, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            return ParseSingleValue(response, 0x03, isSigned: false);
        }

        /// <summary>
        /// 读取第一路空调运行状态（寄存器 0x00D7）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>空调状态枚举</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
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
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>空调状态枚举</returns>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task<AirConditionerStatus> ReadStatusChannel2Async(int timeoutMs = 3000)
        {
            byte[] request = BuildReadRequest(0x00D8, 1);
            byte[] response = await SendAsync(request, GetMatchKey(0x03), timeoutMs);
            int value = ParseRawValue(response, 0x03, isSigned: false);
            return (AirConditionerStatus)value;
        }

        #endregion

        #region 控制方法（发射指令）

        /// <summary>
        /// 发送“开机制冷”控制指令（寄存器 0x00B9，写入 0x0001）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task SendCoolingOnAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildWriteSingleRequest(0x00B9, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 发送“开机制热”控制指令（寄存器 0x00BA，写入 0x0001）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task SendHeatingOnAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildWriteSingleRequest(0x00BA, 0x0001);
            byte[] response = await SendAsync(request, GetMatchKey(0x06), timeoutMs);
            ValidateWriteSingleResponse(response);
        }

        /// <summary>
        /// 发送“关机”控制指令（寄存器 0x00BB，写入 0x0001）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <exception cref="TimeoutException">超时</exception>
        /// <exception cref="InvalidOperationException">连接未打开</exception>
        /// <exception cref="Exception">ModBus 异常响应或其他错误</exception>
        public async Task SendOffAsync(int timeoutMs = 3000)
        {
            byte[] request = BuildWriteSingleRequest(0x00BB, 0x0001);
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
            frame[1] = 0x03; // 读保持寄存器
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
            // 读响应格式：地址 + 功能码 + 字节数(1) + 数据(2*N) + CRC
            // 这里 N=1，数据占 2 字节
            byte[] data = new byte[2];
            data[0] = response[3]; // 高字节
            data[1] = response[4]; // 低字节
            int value = (data[0] << 8) | data[1];
            if (isSigned && value >= 0x8000)
            {
                value -= 0x10000; // 转换为有符号
            }
            return value;
        }

        private void ValidateWriteSingleResponse(byte[] response)
        {
            CheckModbusResponse(response, 0x06);
            // 写单寄存器响应应与请求一致，我们仅检查异常即可
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
                // 异常响应
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