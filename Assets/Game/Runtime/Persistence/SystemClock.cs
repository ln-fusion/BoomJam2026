using System;
using Game.Contracts;

namespace Game.Persistence
{
    /// <summary>
    /// Persistence 模块内部的系统时钟实现。
    /// </summary>
    internal sealed class SystemClock : IClock
    {
        /// <summary>获取当前 UTC 时间。</summary>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        /// <summary>获取当前本地时间。</summary>
        public DateTimeOffset LocalNow => DateTimeOffset.Now;
    }
}
