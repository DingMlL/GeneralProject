using System;

namespace GeneralProject.Transport.Parser
{
    /// <summary>
    /// 协议解析器接口
    /// </summary>
    /// <remarks>
    /// 这是整个框架中 **AI 生成代码的核心入口**。
    /// 每个连接（物理通道）绑定一个协议解析器实例，负责该连接上所有设备的：
    /// <list type="number">
    /// <item><description>帧边界识别（拆包/粘包/半包）</description></item>
    /// <item><description>设备地址提取（用于路由）</description></item>
    /// <item><description>匹配键提取（用于请求-响应匹配）</description></item>
    /// <item><description>帧类型判断（响应 vs 主动上报）</description></item>
    /// </list>
    /// 
    /// **重要约束：**
    /// <list type="bullet">
    /// <item><description>实现类必须**无状态**——不保存任何业务数据，所有状态由 <see cref="ConnectionQueue"/> 管理</description></item>
    /// <item><description>实现类应该是**线程安全**的——可能被多个设备并发调用</description></item>
    /// <item><description>不要修改传入的 <c>buffer</c> 内容，只读取不写入</description></item>
    /// </list>
    /// 
    /// **AI 生成指引：**
    /// 当新设备协议到来时，AI 需要生成此接口的实现类，包含：
    /// 1. 根据协议文档实现 <see cref="TryParseFrame"/> 的拆包逻辑
    /// 2. 根据协议文档实现 <see cref="ExtractDeviceId"/> 的地址提取
    /// 3. 根据协议文档实现 <see cref="ExtractMatchKey"/> 的匹配键提取
    /// 4. 根据协议文档实现 <see cref="GetFrameType"/> 的帧类型判断
    /// </remarks>
    public interface IProtocolParser
    {
        /// <summary>
        /// 从接收缓冲区中切出一个完整的协议帧
        /// </summary>
        /// <param name="buffer">接收缓冲区（原始字节流）</param>
        /// <param name="offset">起始搜索位置</param>
        /// <param name="length">可用数据长度</param>
        /// <param name="frame">切出的完整帧（成功时返回）</param>
        /// <returns>
        /// <list type="bullet">
        /// <item><description>&gt; 0：成功，返回帧长度（字节数），指针前移该长度</description></item>
        /// <item><description>0：数据不够（半包），等待更多数据</description></item>
        /// <item><description>-1：脏数据/错位，跳过当前 1 个字节重新同步</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// 实现规则：
        /// 1. 不要修改 buffer 的内容
        /// 2. 不要保留对 buffer 的引用（无状态设计）
        /// 3. 识别帧头、读取长度、校验 CRC，缺一不可
        /// 4. 如果协议有帧头，找不到帧头时返回 -1
        /// 5. 如果数据不够完整，返回 0 等待下一包
        /// 
        /// 示例（Modbus RTU）：
        /// - 检查地址码和功能码
        /// - 根据功能码计算帧长度
        /// - 校验 CRC16
        /// </remarks>
        int TryParseFrame(byte[] buffer, int offset, int length, out byte[]? frame);

        /// <summary>
        /// 从完整帧中提取设备地址（用于路由分发）
        /// </summary>
        /// <param name="frame">完整的协议帧</param>
        /// <returns>设备地址的字符串形式（如 "1"、"A001"），提取失败时返回 null</returns>
        /// <remarks>
        /// 实现规则：
        /// 1. 只从帧中读取，不修改帧数据
        /// 2. 如果协议无设备地址（点对点场景），返回 "0"
        /// 3. 如果协议有设备地址但提取失败，返回 null（连接层会丢弃该帧）
        /// 4. 返回的字符串应具有唯一性，用于匹配 <see cref="DeviceManager"/> 中的设备注册
        /// 
        /// 示例：
        /// - Modbus RTU：帧的第 0 字节是地址 → 返回 frame[0].ToString()
        /// - 自定义协议：帧的第 3 字节是地址 → 返回 frame[3].ToString()
        /// </remarks>
        string? ExtractDeviceId(byte[] frame);

        /// <summary>
        /// 从完整帧中提取匹配键（用于请求-响应精准匹配）
        /// </summary>
        /// <param name="frame">完整的协议帧</param>
        /// <returns>匹配键（如事务 ID、序列号），无匹配键时返回 null</returns>
        /// <remarks>
        /// 实现规则（重要）：
        /// 1. 响应帧必须返回与请求帧相同的匹配键，否则无法匹配
        /// 2. 主动上报帧通常返回 null（走 Report 管道）
        /// 3. 匹配键需要在短时间内唯一
        /// 
        /// 示例：
        /// - Modbus TCP：帧的第 0-1 字节是事务 ID → 返回 (frame[0] &lt;&lt; 8) | frame[1]
        /// - 自定义协议：帧头后的 2 字节是序列号 → 返回 (frame[2] &lt;&lt; 8) | frame[3]
        /// </remarks>
        ushort? ExtractMatchKey(byte[] frame);

        /// <summary>
        /// 判断帧类型：响应或主动上报
        /// </summary>
        /// <param name="frame">完整的协议帧</param>
        /// <returns>
        /// <list type="bullet">
        /// <item><description><see cref="FrameType.Response"/>：对请求的回复（走请求-响应管道）</description></item>
        /// <item><description><see cref="FrameType.Report"/>：设备主动上报（走事件管道）</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// 实现规则（重要！）：
        /// <list type="number">
        /// <item><description>正常请求-响应的回复（包含匹配键）→ <see cref="FrameType.Response"/></description></item>
        /// <item><description>设备主动推送的帧（无匹配键）→ <see cref="FrameType.Report"/></description></item>
        /// <item><description>异常/错误响应 → <see cref="FrameType.Response"/>（它是对请求的回复）</description></item>
        /// <item><description>无法确定时 → 优先返回 <see cref="FrameType.Response"/>（宁丢不错）</description></item>
        /// </list>
        /// 
        /// 判断依据示例：
        /// - Modbus：功能码在 0x01-0x7F 为 Response，0x80 以上为异常 Response
        /// - RFID 自定义协议：协议控制字中特定标志位为 0 为 Response，为 1 为 Report
        /// - 如果协议无法区分，可结合 <see cref="ExtractMatchKey"/> 判断：有匹配键则为 Response
        /// </remarks>
        FrameType GetFrameType(byte[] frame);
    }
}