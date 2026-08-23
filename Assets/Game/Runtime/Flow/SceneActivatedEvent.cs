using Game.Contracts;

namespace Game.Flow
{
    /// <summary>
    /// 功能场景已激活事件：场景加载完成并设为 Active 后发布。
    /// </summary>
    public sealed class SceneActivatedEvent : IDomainEvent
    {
        /// <summary>已激活的场景名</summary>
        public string SceneName { get; }

        /// <summary>进入该场景时携带的主界面页面（非 MetaHub 场景时为默认值）</summary>
        public MetaPageId MetaPage { get; }

        /// <summary>创建功能场景激活事件。</summary>
        /// <param name="sceneName">已激活的场景名。</param>
        /// <param name="metaPage">进入场景时携带的主界面页面。</param>
        public SceneActivatedEvent(string sceneName, MetaPageId metaPage)
        {
            SceneName = sceneName;
            MetaPage = metaPage;
        }
    }
}
