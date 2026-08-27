using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Content;
using Game.Contracts.Persistence;
using Game.Contracts.Progression;
using Game.Foundation;
using Game.Story;

namespace Game.Presentation
{
    /// <summary>
    /// Bootstrap 创建并注入给运行时 View/Presenter 的服务容器。
    /// 每个应用根拥有独立实例；它不依赖静态定位，也不暴露具体文件路径或 Unity SDK。
    /// </summary>
    public sealed class GameRuntimeServices
    {
        private readonly Func<ProfileSave, SaveReason, CancellationToken, Task<SaveResult>> _saveProfileAsync;

        /// <summary>当前应用流程服务。</summary>
        public IGameFlowService Flow { get; }

        /// <summary>当前设置服务。</summary>
        public ISettingsService Settings { get; }

        /// <summary>当前本地化服务。</summary>
        public ILocalizationService Localization { get; }

        /// <summary>当前音频服务。</summary>
        public IAudioService Audio { get; }

        /// <summary>当前档案生命周期服务。</summary>
        public IProfileLifecycleService ProfileLifecycle { get; }

        /// <summary>当前只读进度查询。</summary>
        public IProgressQuery ProgressQuery { get; }

        /// <summary>当前系统时钟。</summary>
        public IClock Clock { get; }

        /// <summary>当前已加载的单一玩家档案；首次开始前为空。</summary>
        public ProfileSave CurrentProfile { get; private set; }

        /// <summary>当前角色形象查询与立绘资源注册表。</summary>
        public ICharacterAppearanceQuery Characters { get; }

        /// <summary>当前剧情完成事务协调器。</summary>
        public IStoryCompletionCoordinator StoryCompletion { get; }

        /// <summary>
        /// 创建本次应用的运行时服务容器；只能由 Bootstrap 组合根装配具体实现。
        /// </summary>
        /// <param name="flow">流程服务。</param>
        /// <param name="settings">设置服务。</param>
        /// <param name="localization">本地化服务。</param>
        /// <param name="audio">音频服务。</param>
        /// <param name="profileLifecycle">档案生命周期服务。</param>
        /// <param name="progressQuery">进度查询。</param>
        /// <param name="clock">系统时钟。</param>
        /// <param name="saveProfileAsync">档案保存委托。</param>
        /// <param name="characters">角色形象查询与立绘资源注册表。</param>
        /// <param name="storyCompletion">剧情完成事务协调器。</param>
        public GameRuntimeServices(
            IGameFlowService flow,
            ISettingsService settings,
            ILocalizationService localization,
            IAudioService audio,
            IProfileLifecycleService profileLifecycle,
            IProgressQuery progressQuery,
            IClock clock,
            Func<ProfileSave, SaveReason, CancellationToken, Task<SaveResult>> saveProfileAsync,
            ICharacterAppearanceQuery characters = null,
            IStoryCompletionCoordinator storyCompletion = null
        )
        {
            Flow = flow ?? throw new ArgumentNullException(nameof(flow));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Localization = localization ?? throw new ArgumentNullException(nameof(localization));
            Audio = audio ?? throw new ArgumentNullException(nameof(audio));
            ProfileLifecycle = profileLifecycle ?? throw new ArgumentNullException(nameof(profileLifecycle));
            ProgressQuery = progressQuery ?? throw new ArgumentNullException(nameof(progressQuery));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _saveProfileAsync = saveProfileAsync ?? throw new ArgumentNullException(nameof(saveProfileAsync));
            Characters = characters ?? new DefaultCharacterAssetRegistry(null);
            StoryCompletion = storyCompletion;
        }

        /// <summary>设置当前档案引用，供开始菜单和 MetaHub 读取。</summary>
        /// <param name="profile">已通过生命周期服务校验的档案。</param>
        public void SetCurrentProfile(ProfileSave profile)
        {
            CurrentProfile = profile;
        }

        /// <summary>更新最后页面并通过档案保存用例持久化。</summary>
        /// <param name="page">最后打开的页面。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>保存结果；尚未创建档案时返回成功且不写文件。</returns>
        public async Task<SaveResult> SaveLastMetaPageAsync(MetaPageId page, CancellationToken cancellationToken)
        {
            if (CurrentProfile == null)
                return SaveResult.Success();

            CurrentProfile.LastMetaPageId = ToPersistedPageId(page);
            return await _saveProfileAsync(CurrentProfile, SaveReason.PageChanged, cancellationToken);
        }

        /// <summary>提交剧情完成事实；委托给剧情完成事务协调器。</summary>
        /// <param name="storyId">已完成的剧情稳定标识。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>写入结果；失败时不得继续流程跳转。</returns>
        public Task<SaveResult> SaveStoryCompletedAsync(StoryId storyId, CancellationToken cancellationToken)
        {
            return StoryCompletion == null
                ? Task.FromResult(
                    SaveResult.Failure(ErrorCode.SaveFailed, "Story completion coordinator is not available.")
                )
                : StoryCompletion.CommitCompletedAsync(storyId, cancellationToken);
        }

        /// <summary>把页面枚举转换为存档稳定字符串。</summary>
        /// <param name="page">页面枚举。</param>
        /// <returns>稳定页面字符串。</returns>
        private static string ToPersistedPageId(MetaPageId page)
        {
            return page switch
            {
                MetaPageId.Archive => "archive",
                MetaPageId.Character => "character",
                MetaPageId.Lounge => "lounge",
                _ => "map",
            };
        }
    }

    /// <summary>MetaHub 页面路由端口；页面切换不创建或卸载 Scene。</summary>
    public interface IMetaPageRouter
    {
        /// <summary>当前页面。</summary>
        MetaPageId CurrentPage { get; }

        /// <summary>页面变更事件。</summary>
        event Action<MetaPageId> PageChanged;

        /// <summary>切换到指定页面。</summary>
        /// <param name="page">目标页面。</param>
        void Navigate(MetaPageId page);

        /// <summary>从存档字符串恢复页面；未知或休息室回退地图。</summary>
        /// <param name="persistedPageId">存档页面字符串。</param>
        /// <returns>恢复后的页面。</returns>
        MetaPageId Restore(string persistedPageId);
    }

    /// <summary>MetaHub 页面路由默认实现。</summary>
    public sealed class MetaPageRouter : IMetaPageRouter
    {
        private MetaPageId _currentPage = MetaPageId.Map;

        /// <summary>页面变更事件。</summary>
        public event Action<MetaPageId> PageChanged;

        /// <summary>当前页面。</summary>
        public MetaPageId CurrentPage => _currentPage;

        /// <summary>创建默认位于地图页的路由器。</summary>
        public MetaPageRouter() { }

        /// <summary>切换页面并通知订阅者。</summary>
        /// <param name="page">目标页面。</param>
        public void Navigate(MetaPageId page)
        {
            if (!Enum.IsDefined(typeof(MetaPageId), page))
                page = MetaPageId.Map;
            if (_currentPage == page)
                return;

            _currentPage = page;
            PageChanged?.Invoke(_currentPage);
        }

        /// <summary>解析存档页面；休息室尚未开放时回退地图。</summary>
        /// <param name="persistedPageId">页面字符串。</param>
        /// <returns>恢复后的页面。</returns>
        public MetaPageId Restore(string persistedPageId)
        {
            MetaPageId page = persistedPageId?.Trim().ToLowerInvariant() switch
            {
                "archive" => MetaPageId.Archive,
                "character" => MetaPageId.Character,
                "lounge" => MetaPageId.Map,
                _ => MetaPageId.Map,
            };
            Navigate(page);
            return _currentPage;
        }
    }
}
