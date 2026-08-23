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
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        Task EnterStartMenuAsync(CancellationToken cancellationToken);

        /// <summary>首次开始或继续已有档案.</summary>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        Task StartOrContinueAsync(CancellationToken cancellationToken);

        /// <summary>打开主界面指定页面.</summary>
        /// <param name="page">需要打开的主界面页面。</param>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        Task OpenMetaHubAsync(MetaPageId page, CancellationToken cancellationToken);

        /// <summary>进入指定关卡（当前骨架实现统一进入 Gameplay 占位场景）.</summary>
        /// <param name="levelId">需要进入的关卡稳定标识。</param>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        Task EnterLevelAsync(LevelId levelId, CancellationToken cancellationToken);

        /// <summary>播放剧情并定义返回目标.</summary>
        /// <param name="storyId">需要播放的剧情稳定标识。</param>
        /// <param name="returnTarget">剧情播放结束后的返回目标。</param>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        Task PlayStoryAsync(StoryId storyId, StoryReturnTarget returnTarget, CancellationToken cancellationToken);

        /// <summary>返回开始菜单.</summary>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        Task ReturnToStartMenuAsync(CancellationToken cancellationToken);

        /// <summary>退出游戏.</summary>
        /// <param name="cancellationToken">在退出前检查的取消令牌。</param>
        Task QuitGameAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// 主界面页面标识：MetaHub 内容页路由.
    /// </summary>
    public enum MetaPageId
    {
        /// <summary>地图页面。</summary>
        Map = 0,
        /// <summary>档案页面。</summary>
        Archive,
        /// <summary>人员页面。</summary>
        Character,
        /// <summary>休息室页面。</summary>
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

        /// <summary>创建具有指定类别和可选页面或关卡数据的剧情返回目标。</summary>
        /// <param name="kind">返回目标类别。</param>
        /// <param name="metaPage"><paramref name="kind"/> 为 <see cref="StoryReturnKind.MetaPage"/> 时的目标页面。</param>
        /// <param name="level"><paramref name="kind"/> 为 <see cref="StoryReturnKind.Level"/> 时的目标关卡。</param>
        private StoryReturnTarget(StoryReturnKind kind, MetaPageId metaPage, LevelId? level)
        {
            Kind = kind;
            MetaPage = metaPage;
            Level = level;
        }

        /// <summary>创建返回主界面指定页面的目标。</summary>
        /// <param name="page">返回的主界面页面。</param>
        /// <returns>主界面页面返回目标。</returns>
        public static StoryReturnTarget ToMetaPage(MetaPageId page) =>
            new StoryReturnTarget(StoryReturnKind.MetaPage, page, null);

        /// <summary>创建返回指定关卡的目标。</summary>
        /// <param name="level">返回的关卡稳定标识。</param>
        /// <returns>关卡返回目标。</returns>
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
