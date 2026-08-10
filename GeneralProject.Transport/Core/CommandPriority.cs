using System;

namespace GeneralProject.Transport.Core
{
    /// <summary>
    /// 命令优先级
    /// </summary>
    /// <remarks>
    /// 用于双队列调度，数值越小优先级越高。
    /// 
    /// 使用场景：
    /// - 配置命令（如写入参数、开关机）应使用 High，优先执行
    /// - 轮询命令（如读取温度）应使用 Normal 或 Low，不影响紧急命令
    /// 
    /// 队列调度规则：
    /// 1. 消费者线程优先消费 High 队列，直到 High 队列为空
    /// 2. 然后消费 Normal 队列
    /// 3. 最后消费 Low 队列
    /// </remarks>
    public enum CommandPriority
    {
        /// <summary>
        /// 高优先级 - 配置/紧急命令，优先发送
        /// </summary>
        High = 0,

        /// <summary>
        /// 普通优先级 - 常规读写命令
        /// </summary>
        Normal = 1,

        /// <summary>
        /// 低优先级 - 后台轮询/日志上传，最后处理
        /// </summary>
        Low = 2
    }
}