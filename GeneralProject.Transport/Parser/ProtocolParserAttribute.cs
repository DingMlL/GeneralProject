using System;

namespace GeneralProject.Transport.Parser
{
    /// <summary>
    /// 协议解析器特性
    /// </summary>
    /// <remarks>
    /// 用于在设备代理类上标注该设备使用的协议解析器类型。
    /// 
    /// 使用方式：
    /// <code>
    /// [ProtocolParser(typeof(ModbusRtuParser))]
    /// public class TemperatureProxy : DeviceProxyBase
    /// {
    ///     // ...
    /// }
    /// </code>
    /// 
    /// 工作原理：
    /// 1. 业务层调用 <see cref="DeviceManager.GetOrCreate{T}(string, string)"/>
    /// 2. DeviceManager 通过反射读取 T 上的 ProtocolParserAttribute
    /// 3. 提取 ParserType，创建对应的 IProtocolParser 实例
    /// 4. 绑定到连接队列器
    /// 
    /// 约束：
    /// <list type="bullet">
    /// <item><description>ParserType 必须实现 <see cref="IProtocolParser"/> 接口</description></item>
    /// <item><description>ParserType 必须有无参构造函数（框架自动创建实例）</description></item>
    /// <item><description>一个设备代理只能标注一个解析器特性</description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProtocolParserAttribute : Attribute
    {
        /// <summary>
        /// 协议解析器类型
        /// </summary>
        public Type ParserType { get; }

        /// <summary>
        /// 初始化 ProtocolParserAttribute
        /// </summary>
        /// <param name="parserType">实现 <see cref="IProtocolParser"/> 的类型</param>
        /// <exception cref="ArgumentNullException">当 parserType 为 null 时抛出</exception>
        /// <exception cref="ArgumentException">当 parserType 未实现 <see cref="IProtocolParser"/> 时抛出</exception>
        public ProtocolParserAttribute(Type parserType)
        {
            if (parserType == null)
                throw new ArgumentNullException(nameof(parserType));

            if (!typeof(IProtocolParser).IsAssignableFrom(parserType))
            {
                throw new ArgumentException(
                    $"类型 {parserType.Name} 必须实现 {nameof(IProtocolParser)} 接口",
                    nameof(parserType));
            }

            ParserType = parserType;
        }
    }
}