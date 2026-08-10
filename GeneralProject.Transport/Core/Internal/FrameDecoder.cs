using System;
using System.Collections.Generic;
using System.IO;
using GeneralProject.Transport.Parser;

namespace GeneralProject.Transport.Core.Internal
{
    /// <summary>
    /// 帧解码器（内部工具）
    /// </summary>
    /// <remarks>
    /// 负责维护接收缓冲区，调用协议解析器的 <see cref="IProtocolParser.TryParseFrame"/> 方法进行拆包。
    /// 
    /// 职责：
    /// <list type="bullet">
    /// <item><description>维护接收缓冲区（处理粘包/半包）</description></item>
    /// <item><description>调用 <see cref="IProtocolParser.TryParseFrame"/> 切出完整帧</description></item>
    /// <item><description>处理脏数据（调用方返回 -1 时跳过 1 字节）</description></item>
    /// <item><description>防止缓冲区溢出（设置最大长度限制）</description></item>
    /// </list>
    /// 
    /// 线程安全：
    /// 使用 <see cref="object"/> 锁保护缓冲区操作。
    /// 
    /// 生命周期：
    /// 一个连接对应一个 FrameDecoder 实例，由 <see cref="ConnectionQueue"/> 持有。
    /// </remarks>
    public sealed class FrameDecoder : IDisposable
    {
        // ========== 核心字段 ==========

        /// <summary>
        /// 接收缓冲区
        /// </summary>
        private readonly MemoryStream _buffer = new();

        /// <summary>
        /// 缓冲区锁
        /// </summary>
        private readonly object _syncLock = new();

        /// <summary>
        /// 最大缓冲区大小（防止恶意数据导致 OOM）
        /// </summary>
        private readonly int _maxBufferSize;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        // ========== 构造函数 ==========

        /// <summary>
        /// 初始化帧解码器
        /// </summary>
        /// <param name="maxBufferSize">最大缓冲区大小（字节），默认 1MB</param>
        public FrameDecoder(int maxBufferSize = 1024 * 1024)
        {
            _maxBufferSize = maxBufferSize;
        }

        // ========== 核心方法 ==========

        /// <summary>
        /// 追加原始数据并切出完整帧
        /// </summary>
        /// <param name="rawData">原始字节数据</param>
        /// <param name="parser">协议解析器</param>
        /// <returns>切出的完整帧列表（可能为空）</returns>
        /// <remarks>
        /// 处理流程：
        /// <list type="number">
        /// <item><description>将原始数据追加到缓冲区</description></item>
        /// <item><description>循环调用 parser.TryParseFrame 尝试切帧</description></item>
        /// <item><description>成功切帧：存入列表，指针前移</description></item>
        /// <item><description>返回 -1（脏数据）：跳过 1 字节，继续尝试</description></item>
        /// <item><description>返回 0（数据不够）：退出循环，等待更多数据</description></item>
        /// <item><description>截断缓冲区，仅保留未处理的数据（半包）</description></item>
        /// </list>
        /// </remarks>
        public IReadOnlyList<byte[]> Decode(byte[] rawData, IProtocolParser parser)
        {
            if (rawData == null || rawData.Length == 0)
                return Array.Empty<byte[]>();

            lock (_syncLock)
            {
                // ===== 1. 防溢出保护 =====
                if (_buffer.Length + rawData.Length > _maxBufferSize)
                {
                    // 缓冲区溢出，清空并重置
                    _buffer.SetLength(0);
                    _buffer.Position = 0;
                    return Array.Empty<byte[]>();
                }

                // ===== 2. 追加新数据 =====
                _buffer.Write(rawData, 0, rawData.Length);

                // ===== 3. 循环拆包 =====
                var completeFrames = new List<byte[]>();
                byte[] fullBuffer = _buffer.ToArray();
                int offset = 0;

                while (offset < fullBuffer.Length)
                {
                    int result = parser.TryParseFrame(
                        fullBuffer,
                        offset,
                        fullBuffer.Length - offset,
                        out byte[]? frame);

                    if (result > 0)
                    {
                        // 成功切出一帧
                        completeFrames.Add(frame!);
                        offset += result;
                    }
                    else if (result == -1)
                    {
                        // 脏数据，跳过 1 字节
                        offset++;
                    }
                    else // result == 0
                    {
                        // 数据不够，等待更多数据
                        break;
                    }
                }

                // ===== 4. 截断缓冲区：保留未消费的数据 =====
                if (offset > 0)
                {
                    int remainLen = fullBuffer.Length - offset;
                    _buffer.SetLength(0);
                    _buffer.Position = 0;

                    if (remainLen > 0)
                    {
                        _buffer.Write(fullBuffer, offset, remainLen);
                    }
                }
                // 如果 offset == 0，说明没有消费任何数据，保留原缓冲区等待更多数据

                return completeFrames;
            }
        }

        /// <summary>
        /// 清空接收缓冲区
        /// </summary>
        /// <remarks>
        /// 在连接断开或重置时调用，清除所有未处理的数据。
        /// </remarks>
        public void Reset()
        {
            lock (_syncLock)
            {
                _buffer.SetLength(0);
                _buffer.Position = 0;
            }
        }

        // ========== 资源释放 ==========

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _buffer?.Dispose();
        }
    }
}