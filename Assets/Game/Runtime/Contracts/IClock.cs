#nullable enable
using System;

namespace Game.Contracts
{
    /// <summary>
    /// 时钟抽象，提供当前 UTC 和本地时间，供 UI 与存档使用。
    /// </summary>
    /// <remarks>
    /// 参见技术设计文档 §8.3：系统时间不可信，不参与解锁、成绩或存档排序；
    /// 云冲突只把 UTC 时间作为辅助证据，以修订号和内容比较为主。
    /// </remarks>
    public interface IClock
    {
        /// <summary>当前 UTC 时间</summary>
        DateTimeOffset UtcNow { get; }

        /// <summary>当前本地时间（玩家电脑时区）</summary>
        DateTimeOffset LocalNow { get; }
    }
}
