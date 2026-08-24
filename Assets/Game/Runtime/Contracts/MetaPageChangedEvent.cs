#nullable enable
using System;
using Game.Contracts;

namespace Game.Contracts
{
    /// <summary>
    /// 主界面页面切换事件：页面路由变化后发布（仅记录，不用于驱动关键流程）.
    /// </summary>
    /// <remarks>
    /// C05：最后页面恢复用；消费方（组合根）负责写 Profile.LastMetaPageId.
    /// </remarks>
    public sealed class MetaPageChangedEvent : IDomainEvent
    {
        /// <summary>切换后的页面</summary>
        public MetaPageId Page { get; }

        public MetaPageChangedEvent(MetaPageId page)
        {
            Page = page;
        }
    }
}
