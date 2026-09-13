using System;
using Game.Foundation;

namespace Game.Contracts.Progression
{
    /// <summary>
    /// 剧情完成事实提交事件：剧情完成记录首次成功写入玩家档案后发布。
    /// </summary>
    /// <remarks>
    /// 事件只承载已提交的不可变事实，供地图刷新、解锁和重播查询订阅（技术设计文档 §13）。
    /// </remarks>
    public sealed class StoryCompletedCommittedEvent : IDomainEvent
    {
        /// <summary>已提交的剧情稳定标识。</summary>
        public StoryId StoryId { get; }

        /// <summary>创建剧情完成事件。</summary>
        /// <param name="storyId">已提交的剧情稳定标识。</param>
        public StoryCompletedCommittedEvent(StoryId storyId)
        {
            StoryId = storyId ?? throw new ArgumentNullException(nameof(storyId));
        }
    }

}
