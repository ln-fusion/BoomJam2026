using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Content;
using Game.Contracts.Persistence;
using Game.Contracts.Progression;
using Game.Foundation;
using Game.Progression;
using Game.Content;
using Game.Meta;
using Newtonsoft.Json;

namespace Game.Presentation
{
    /// <summary>
    /// Bootstrap 创建并注入给运行时 View/Presenter 的服务容器。
    /// 每个应用根拥有独立实例；它不依赖静态定位，也不暴露具体文件路径或 Unity SDK。
    /// </summary>
    public sealed class GameRuntimeServices
    {
        private readonly Func<ProfileSave, SaveReason, CancellationToken, Task<SaveResult>>
            _saveProfileAsync;
        private readonly SemaphoreSlim _profileWriteGate = new SemaphoreSlim(1, 1);
        private LevelId _whiteboxLevel;
        private string _whiteboxRunId;

        /// <summary>当前选关创建的白盒会话关卡；尚未选关时为空。</summary>
        internal LevelId WhiteboxLevel => _whiteboxLevel;

        /// <summary>记录当前白盒选关并生成独立提交 ID；不保存或授予完成事实。</summary>
        /// <param name="levelId">通过解锁检查的关卡 ID。</param>
        internal void BeginWhiteboxLevel(LevelId levelId)
        {
            _whiteboxLevel = levelId ?? throw new ArgumentNullException(nameof(levelId));
            _whiteboxRunId = Guid.NewGuid().ToString("N");
        }

        /// <summary>结束白盒会话，防止后续未经过选关的场景使用旧关卡提交。</summary>
        internal void EndWhiteboxLevel()
        {
            _whiteboxLevel = null;
            _whiteboxRunId = null;
        }

        /// <summary>模拟当前关卡完成，保存独立档案副本后才发布进度；不生成成绩或剧情完成事实。</summary>
        /// <param name="cancellationToken">取消等待或存档写入；写入成功后仍发布已持久化的副本。</param>
        /// <returns>缺少档案、会话或解锁资格时失败；相同提交已保存时成功且不重复写入。</returns>
        internal async Task<SaveResult> CompleteWhiteboxLevelAsync(CancellationToken cancellationToken)
        {
            LevelId level = _whiteboxLevel;
            string runId = _whiteboxRunId;
            await _profileWriteGate.WaitAsync(cancellationToken);
            try
            {
                if (CurrentProfile == null || level == null || string.IsNullOrEmpty(runId))
                    return SaveResult.Failure(new ErrorCode(ErrorCategory.Validation,
                        "whitebox.session_missing"), "请从地图选择关卡后再模拟通关。");
                if (CurrentProfile.AppliedCompletionRunIds.Contains(runId))
                    return SaveResult.Success();
                var query = new MetaMapQuery(Content, ProgressQuery);
                var card = query.GetLevelCard(level);
                if (card == null || !card.Node.IsInteractable)
                    return SaveResult.Failure(new ErrorCode(ErrorCategory.Validation,
                        "whitebox.level_locked"), "当前关卡未解锁，不能提交通关。");

                ProfileSave candidate = JsonConvert.DeserializeObject<ProfileSave>(
                    JsonConvert.SerializeObject(CurrentProfile));
                if (!candidate.CompletedLevelIds.Contains(level.Value))
                    candidate.CompletedLevelIds.Add(level.Value);
                LevelRecordSave record = candidate.LevelRecords.Find(item =>
                    item != null && item.LevelId == level.Value);
                if (record == null)
                {
                    record = new LevelRecordSave { LevelId = level.Value };
                    candidate.LevelRecords.Add(record);
                }
                record.Completed = true;
                candidate.AppliedCompletionRunIds.Add(runId);
                candidate.LastMetaPageId = "map";
                SaveResult result = await _saveProfileAsync(candidate, SaveReason.ProgressCommitted,
                    cancellationToken);
                if (result.IsSuccess)
                    CurrentProfile = candidate;
                return result;
            }
            finally
            {
                _profileWriteGate.Release();
            }
        }

        /// <summary>把剧情完成事实原子写入档案，保存成功后才发布新的内存进度。</summary>
        /// <param name="storyId">已经完整播放或跳过完成的剧情稳定标识。</param>
        /// <param name="cancellationToken">取消等待或存档写入的令牌。</param>
        /// <returns>保存结果；同一剧情已完成时直接成功且不重复写入。</returns>
        internal async Task<SaveResult> CompleteStoryAsync(StoryId storyId,
            CancellationToken cancellationToken)
        {
            if (storyId == null)
                throw new ArgumentNullException(nameof(storyId));

            await _profileWriteGate.WaitAsync(cancellationToken);
            try
            {
                if (CurrentProfile == null)
                    return SaveResult.Failure(new ErrorCode(ErrorCategory.Validation,
                        "story.profile_missing"), "剧情完成时没有已加载的玩家档案。");
                if (CurrentProfile.CompletedStoryIds.Contains(storyId.Value))
                    return SaveResult.Success();

                ProfileSave candidate = JsonConvert.DeserializeObject<ProfileSave>(
                    JsonConvert.SerializeObject(CurrentProfile));
                candidate.CompletedStoryIds.Add(storyId.Value);
                SaveResult result = await _saveProfileAsync(candidate, SaveReason.StoryCommitted,
                    cancellationToken);
                if (result.IsSuccess)
                    CurrentProfile = candidate;
                return result;
            }
            finally
            {
                _profileWriteGate.Release();
            }
        }

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
        private readonly IProgressQuery _initialProgressQuery;

        /// <summary>从当前档案创建只读进度快照；档案尚未加载时使用注入的默认查询。</summary>
        public IProgressQuery ProgressQuery => CurrentProfile == null
            ? _initialProgressQuery : new ProfileProgressQuery(CurrentProfile);
        /// <summary>当前系统时钟。</summary>
        public IClock Clock { get; }
        /// <summary>当前已加载的单一玩家档案；首次开始前为空。</summary>
        public ProfileSave CurrentProfile { get; private set; }

        /// <summary>当前会话统一使用的地图、关卡和剧情内容源。</summary>
        public IContentService Content { get; }

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
        /// <param name="content">已校验的内容服务；省略时仅使用兼容测试目录。</param>
        public GameRuntimeServices(IGameFlowService flow, ISettingsService settings,
            ILocalizationService localization, IAudioService audio,
            IProfileLifecycleService profileLifecycle, IProgressQuery progressQuery,
            IClock clock,
            Func<ProfileSave, SaveReason, CancellationToken, Task<SaveResult>> saveProfileAsync,
            IContentService content = null)
        {
            Content = content ?? new OfficialContentService(OfficialTestMapCatalog.CreateProvider());
            Flow = flow ?? throw new ArgumentNullException(nameof(flow));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Localization = localization ?? throw new ArgumentNullException(nameof(localization));
            Audio = audio ?? throw new ArgumentNullException(nameof(audio));
            ProfileLifecycle = profileLifecycle ??
                throw new ArgumentNullException(nameof(profileLifecycle));
            _initialProgressQuery = progressQuery ?? throw new ArgumentNullException(nameof(progressQuery));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _saveProfileAsync = saveProfileAsync ??
                throw new ArgumentNullException(nameof(saveProfileAsync));
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
        public async Task<SaveResult> SaveLastMetaPageAsync(MetaPageId page,
            CancellationToken cancellationToken)
        {
            await _profileWriteGate.WaitAsync(cancellationToken);
            try
            {
                if (CurrentProfile == null)
                    return SaveResult.Success();

                CurrentProfile.LastMetaPageId = ToPersistedPageId(page);
                return await _saveProfileAsync(CurrentProfile, SaveReason.PageChanged,
                    cancellationToken);
            }
            finally
            {
                _profileWriteGate.Release();
            }
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
                _ => "map"
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
        public MetaPageRouter()
        {
        }

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
                _ => MetaPageId.Map
            };
            Navigate(page);
            return _currentPage;
        }
    }
}
