using System;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralProject.Transport.Core.Internal
{
    /// <summary>
    /// 待处理操作（请求-响应状态机）
    /// </summary>
    /// <typeparam name="TResult">响应结果的类型（byte[] 或业务对象）</typeparam>
    /// <remarks>
    /// 这是整个框架防重入机制的核心。管理单个请求的等待状态，
    /// 保证超时回调和数据回调只触发一次。
    /// 
    /// 防重入原理：
    /// <list type="number">
    /// <item><description>使用 <see cref="Interlocked.Exchange"/> 实现原子状态转换</description></item>
    /// <item><description>只有第一个成功标记为"已完成"的线程能执行业务逻辑</description></item>
    /// <item><description>后续线程（包括超时回调）会被拦截，确保业务逻辑只执行一次</description></item>
    /// </list>
    /// 
    /// 使用场景：
    /// <list type="bullet">
    /// <item><description>异步等待模式：通过 <see cref="TaskCompletionSource{TResult}"/> 唤醒等待者</description></item>
    /// <item><description>回调模式：通过 <see cref="Action{TResult}"/> 通知调用方</description></item>
    /// </list>
    /// 
    /// 生命周期：
    /// <list type="number">
    /// <item><description>创建实例 → 存入等待字典</description></item>
    /// <item><description>等待响应或超时</description></item>
    /// <item><description>触发 <see cref="TrySetCompleted"/>（原子操作，只有第一个线程成功）</description></item>
    /// <item><description>成功者执行业务逻辑（唤醒 Task 或执行 Callback）</description></item>
    /// <item><description>失败者被拦截，不做任何操作</description></item>
    /// </list>
    /// </remarks>
    internal class PendingOperation<TResult>
    {
        // ========== 核心字段 ==========

        /// <summary>
        /// 匹配键（用于从等待字典中查找）
        /// </summary>
        public ushort MatchKey { get; }

        /// <summary>
        /// 命令名称（用于日志/调试）
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// 超时取消令牌源
        /// </summary>
        public CancellationTokenSource Cts { get; }

        /// <summary>
        /// 异步等待模式专用：TaskCompletionSource
        /// </summary>
        public TaskCompletionSource<TResult>? Tcs { get; }

        /// <summary>
        /// 回调模式专用：回调委托
        /// </summary>
        public Action<TResult>? Callback { get; }

        /// <summary>
        /// 绝对截止时间（UTC），用于超时扫描器
        /// </summary>
        public DateTime DeadlineUtc { get; }

        /// <summary>
        /// 是否已超时（用于外部判断）
        /// </summary>
        public bool IsTimedOut { get; set; }

        // ========== 防重入门卫 ==========

        // 0 = 未完成，1 = 已完成（已裁决）
        private int _isCompleted;

        /// <summary>
        /// 尝试原子性地标记此操作为"已完成"
        /// </summary>
        /// <returns>
        /// true：我是第一个成功标记的（赢家），可以继续执行业务逻辑
        /// false：已有其他线程标记完成（输家），应放弃执行
        /// </returns>
        /// <remarks>
        /// 使用 <see cref="Interlocked.Exchange"/> 保证原子性，
        /// 这是整个防重入机制的核心。
        /// 
        /// 典型用法：
        /// <code>
        /// if (operation.TrySetCompleted())
        /// {
        ///     // 我是赢家，执行业务逻辑
        ///     operation.Tcs?.TrySetResult(result);
        ///     operation.Callback?.Invoke(result);
        /// }
        /// else
        /// {
        ///     // 我是输家，什么也不做
        /// }
        /// </code>
        /// </remarks>
        public bool TrySetCompleted()
        {
            return Interlocked.Exchange(ref _isCompleted, 1) == 0;
        }

        // ========== 构造函数 ==========

        /// <summary>
        /// 异步等待模式构造函数
        /// </summary>
        /// <param name="matchKey">匹配键</param>
        /// <param name="commandName">命令名称（用于日志）</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        public PendingOperation(ushort matchKey, string commandName, int timeoutMs)
            : this(matchKey, commandName, timeoutMs, null, null)
        {
            Tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>
        /// 回调模式构造函数
        /// </summary>
        /// <param name="matchKey">匹配键</param>
        /// <param name="commandName">命令名称（用于日志）</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <param name="callback">回调委托</param>
        public PendingOperation(ushort matchKey, string commandName, int timeoutMs, Action<TResult> callback)
            : this(matchKey, commandName, timeoutMs, callback, null)
        {
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        // 私有基础构造函数
        private PendingOperation(
            ushort matchKey,
            string commandName,
            int timeoutMs,
            Action<TResult>? callback,
            TaskCompletionSource<TResult>? tcs)
        {
            MatchKey = matchKey;
            CommandName = commandName ?? "Unknown";
            Cts = new CancellationTokenSource(timeoutMs);
            DeadlineUtc = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            Callback = callback;
            Tcs = tcs;
            IsTimedOut = false;
            _isCompleted = 0;
        }

        // ========== 资源释放 ==========

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try { Cts.Cancel(); } catch { }
            try { Cts.Dispose(); } catch { }
        }
    }
}