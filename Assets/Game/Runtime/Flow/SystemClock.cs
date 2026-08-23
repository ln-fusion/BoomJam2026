using System;
using Game.Contracts;

namespace Game.Flow
{
    /// <summary>
    /// 系统时钟实现：基于 <see cref="DateTimeOffset"/>（真实本地时间）。
    /// </summary>
    /// <remarks>
    /// 提供系统 UTC 与本地时间；调用方不得用它决定解锁、成绩或存档排序。
    /// </remarks>
    public sealed class SystemClock : IClock
    {
        /// <summary>当前 UTC 时间</summary>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        /// <summary>当前本地时间</summary>
        public DateTimeOffset LocalNow => DateTimeOffset.Now;
    }
}
