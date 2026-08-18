#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;

namespace Game.Contracts
{
    /// <summary>
    /// 应用流程服务：管理场景流转、首次/继续与剧情返回目标.
    /// </summary>
    /// <remarks>
    /// C01/C02 骨架期：接口先行定义，具体实现与完整路由在 C02 落地.
    /// </remarks>
    public interface IGameFlowService
    {
        /// <summary>进入开始菜单场景.</summary>
        Task EnterStartMenuAsync(CancellationToken cancellationToken);

        /// <summary>首次开始或继续已有档案.</summary>
        Task StartOrContinueAsync(CancellationToken cancellationToken);

        /// <summary>打开主界面指定页面.</summary>
        Task OpenMetaHubAsync(MetaPageId page, CancellationToken cancellationToken);

        /// <summary>进入指定关卡（含首次/再次关前剧情分支）.</summary>
        Task EnterLevelAsync(LevelId levelId, CancellationToken cancellationToken);

        /// <summary>播放剧情并定义返回目标.</summary>
        Task PlayStoryAsync(StoryId storyId, StoryReturnTarget returnTarget, CancellationToken cancellationToken);

        /// <summary>返回开始菜单.</summary>
        Task ReturnToStartMenuAsync(CancellationToken cancellationToken);

        /// <summary>退出游戏.</summary>
        Task QuitGameAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// 主界面页面标识：MetaHub 内容页路由.
    /// </summary>
    public enum MetaPageId
    {
        Map = 0,
        Archive,
        Character,
        Lounge,
    }

    /// <summary>
    /// 剧情播放结束后的返回目标.
    /// </summary>
    public readonly struct StoryReturnTarget
    {
        /// <summary>返回目标场景/页面类型.</summary>
        public StoryReturnKind Kind { get; }

        /// <summary>返回目标页（Kind 为 MetaPage 时有效）.</summary>
        public MetaPageId MetaPage { get; }

        /// <summary>返回目标关卡（Kind 为 Level 时有效）.</summary>
        public LevelId? Level { get; }

        private StoryReturnTarget(StoryReturnKind kind, MetaPageId metaPage, LevelId? level)
        {
            Kind = kind;
            MetaPage = metaPage;
            Level = level;
        }

        public static StoryReturnTarget ToMetaPage(MetaPageId page) =>
            new StoryReturnTarget(StoryReturnKind.MetaPage, page, null);

        public static StoryReturnTarget ToLevel(LevelId level) =>
            new StoryReturnTarget(StoryReturnKind.Level, default, level);
    }

    /// <summary>
    /// 剧情返回目标类别.
    /// </summary>
    public enum StoryReturnKind
    {
        /// <summary>返回主界面指定页面</summary>
        MetaPage = 0,

        /// <summary>返回关卡选择（关卡资料卡）</summary>
        Level,
    }
}
