using System;
using Game.Contracts;

namespace Game.Persistence
{
    /// <summary>
    /// 系统时钟实现：基于 <see cref="DateTimeOffset"/>（真实本地时间）.
    /// </summary>
    /// <remarks>
    /// 系统时间不可信，不参与解锁、成绩或存档排序（技术设计文档 §8.3）.
    /// </remarks>
    public sealed class SystemClock : IClock
    {
        /// <summary>获取当前 UTC 时间。</summary>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        /// <summary>获取当前本地时间。</summary>
        public DateTimeOffset LocalNow => DateTimeOffset.Now;
    }
}
