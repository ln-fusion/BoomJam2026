#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Contracts.Progression;
using Game.Foundation;

namespace Game.Flow
{
    /// <summary>
    /// 应用流程服务第一版：驱动功能场景 Additive 加载/卸载/激活，带防重入与取消生命周期。
    /// </summary>
    /// <remarks>
    /// C02 范围：StartMenu/MetaHub/Story/Gameplay 占位场景可往返切换、重复导航不加载两份、
    /// 切换场景时取消旧场景的订阅与异步请求（开发计划 C02 验收）。
    /// 首次/继续、关前/关后剧情分支等路由细节在 C05/C15/C16 落地。
    /// </remarks>
    public sealed class GameFlowService : IGameFlowService, IDisposable
    {
        private readonly ISceneLoader _sceneLoader;
        private readonly IClock _clock;
        private readonly IGameLogger _logger;
        private readonly IDomainEventBus _eventBus;
        private readonly string _startMenuSceneName;
        private readonly IStoryCompletionCoordinator? _storyCompletion;
        private readonly Func<ProfileSave?>? _getProfile;
        private readonly Func<ProfileSave, SaveReason, CancellationToken, Task<SaveResult>>? _saveProfileAsync;

        private CancellationTokenScope? _activeScope;
        private bool _isNavigating;
        private bool _disposed;
        private MetaPageId _lastMetaPage = MetaPageId.Map;
        private StoryReturnTarget? _storyReturnTarget;
        private LevelId? _currentLevel;
        private StoryId? _currentStoryId;
        private const string TestStoryId = "official.story.c06_branch";

        /// <summary>当前场景生命周期的取消令牌（场景激活后有效，切换时被取消）.</summary>
        public CancellationToken ActiveSceneToken => _activeScope?.Token ?? CancellationToken.None;

        /// <summary>最近一次剧情返回目标（PlayStory 时记录，供返回路由使用）.</summary>
        public StoryReturnTarget? LastStoryReturnTarget => _storyReturnTarget;

        /// <summary>最近一次进入的关卡稳定标识（关后流程返回地图前均有值）.</summary>
        public LevelId? CurrentLevelId => _currentLevel;

        /// <summary>最近一次播放的剧情稳定标识；为 null 时表示未播放剧情。</summary>
        public StoryId? CurrentStoryId => _currentStoryId;

        /// <summary>诊断用时钟实例.</summary>
        public IClock Clock => _clock;

        /// <summary>
        /// 构造函数：注入场景加载/时钟/日志/事件总线依赖。
        /// </summary>
        /// <param name="sceneLoader">场景加载器（默认 <see cref="UnitySceneLoader"/>）</param>
        /// <param name="clock">时钟</param>
        /// <param name="logger">日志；为 null 时静默</param>
        /// <param name="eventBus">事件总线</param>
        /// <param name="startMenuSceneName">开始菜单场景名，默认 <see cref="SceneNames.StartMenu"/></param>
        /// <param name="storyCompletion">剧情完成事务协调器；为 null 时关后流程跳过提交。</param>
        /// <param name="storyCompletion">剧情完成事务协调器；为 null 时关后流程跳过提交。</param>
        /// <param name="getProfile">获取当前玩家档案的委托；为 null 时关卡完成事实提交被跳过。</param>
        /// <param name="saveProfileAsync">档案保存委托；与 <paramref name="getProfile"/> 同时提供时才生效。</param>
        public GameFlowService(
            ISceneLoader sceneLoader,
            IClock clock,
            IGameLogger? logger,
            IDomainEventBus eventBus,
            string startMenuSceneName = SceneNames.StartMenu,
            IStoryCompletionCoordinator? storyCompletion = null,
            Func<ProfileSave?>? getProfile = null,
            Func<ProfileSave, SaveReason, CancellationToken, Task<SaveResult>>? saveProfileAsync = null
        )
        {
            _sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? NullLogger.Instance;
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _startMenuSceneName = startMenuSceneName;
            _storyCompletion = storyCompletion;
            _getProfile = getProfile;
            _saveProfileAsync = saveProfileAsync;
        }

        /// <summary>进入开始菜单场景.</summary>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        public Task EnterStartMenuAsync(CancellationToken cancellationToken) =>
            NavigateAsync(_startMenuSceneName, cancellationToken);

        /// <summary>首次开始或继续（C02 占位：直接进入 MetaHub；首次/继续判定在 C05 由 Profile 决定）.</summary>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        public Task StartOrContinueAsync(CancellationToken cancellationToken) =>
            NavigateAsync(SceneNames.MetaHub, cancellationToken);

        /// <summary>打开主界面指定页面（页面切换不换 Scene，记录最后页面供恢复）.</summary>
        /// <param name="page">需要打开并记录的主界面页面。</param>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        public Task OpenMetaHubAsync(MetaPageId page, CancellationToken cancellationToken)
        {
            _lastMetaPage = page;
            return NavigateAsync(SceneNames.MetaHub, cancellationToken);
        }

        /// <summary>
        /// 进入指定关卡：剧情完成事实未提交时先播放关前剧情，已提交时直接进入占位关卡。
        /// </summary>
        /// <param name="levelId">目标关卡稳定标识。</param>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        public Task EnterLevelAsync(LevelId levelId, CancellationToken cancellationToken)
        {
            _currentLevel = levelId;
            bool preludeDone =
                _storyCompletion != null && levelId != null && _storyCompletion.IsCompleted(new StoryId(TestStoryId));
            if (levelId != null && !preludeDone)
                return PlayStoryAsync(new StoryId(TestStoryId), StoryReturnTarget.ToLevel(levelId), cancellationToken);
            return NavigateAsync(SceneNames.Gameplay, cancellationToken);
        }

        /// <summary>
        /// 占位关卡完成后提交剧情完成事实并播放关后剧情，结束后返回地图。
        /// </summary>
        /// <param name="levelId">已完成的关卡稳定标识。</param>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        public async Task CompleteLevelAsync(LevelId levelId, CancellationToken cancellationToken)
        {
            if (_storyCompletion != null)
            {
                var storyId = new StoryId(TestStoryId);
                SaveResult result = await _storyCompletion.CommitCompletedAsync(storyId, cancellationToken);
                if (!result.IsSuccess)
                {
                    _logger.LogError(
                        LogContext.Empty,
                        "[GameFlowService] 关后流程被阻断: 完成事实提交失败 " + result.Message
                    );
                    return;
                }
            }
            if (levelId != null && _getProfile != null && _saveProfileAsync != null)
            {
                SaveResult levelFact = await CommitLevelFactAsync(levelId, cancellationToken);
                if (!levelFact.IsSuccess)
                {
                    _logger.LogError(
                        LogContext.Empty,
                        "[GameFlowService] 关后流程被阻断: 关卡完成事实提交失败 " + levelFact.Message
                    );
                    return;
                }
            }

            // C16 占位：关后剧情复用测试剧情；正式 PostStoryId 在关卡数据契约阶段接入。
            await PlayStoryAsync(
                new StoryId(TestStoryId),
                StoryReturnTarget.ToMetaPage(MetaPageId.Map),
                cancellationToken
            );
        }

        /// <summary>
        /// 提交关卡完成事实：写入 CompletedLevelIds 并以 ProgressCommitted 原因保存。
        /// 写档失败时回滚内存追加，确保重试时仍会再次尝试。
        /// </summary>
        /// <param name="levelId">已完成的关卡稳定标识。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>保存结果；失败时调用方不得继续跳转流程。</returns>
        private async Task<SaveResult> CommitLevelFactAsync(LevelId levelId, CancellationToken cancellationToken)
        {
            ProfileSave? profile = _getProfile?.Invoke();
            if (profile == null)
                return SaveResult.Failure(
                    ErrorCode.SaveFailed,
                    "No active profile; level completion cannot be committed."
                );
            if (levelId == null || string.IsNullOrWhiteSpace(levelId.Value))
            {
                return SaveResult.Failure(ErrorCode.InvalidArgument, "Level ID is required for completion.");
            }

            bool added = !profile.CompletedLevelIds.Contains(levelId.Value);
            if (added)
                profile.CompletedLevelIds.Add(levelId.Value);
            SaveResult result = await _saveProfileAsync!(profile, SaveReason.ProgressCommitted, cancellationToken);
            if (!result.IsSuccess)
            {
                if (added)
                    profile.CompletedLevelIds.Remove(levelId.Value);
                _logger.LogError(LogContext.Empty, "[GameFlowService] 关卡完成事实写入失败: " + levelId.Value);
            }
            return result;
        }

        /// <summary>播放剧情并记录返回目标.</summary>
        /// <param name="storyId">目标剧情稳定标识；C02 占位实现尚未按剧情分流。</param>
        /// <param name="returnTarget">剧情播放结束后使用的返回目标。</param>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        public Task PlayStoryAsync(StoryId storyId, StoryReturnTarget returnTarget, CancellationToken cancellationToken)
        {
            _storyReturnTarget = returnTarget;
            _currentStoryId = storyId;
            return NavigateAsync(SceneNames.Story, cancellationToken);
        }

        /// <summary>返回开始菜单.</summary>
        /// <param name="cancellationToken">取消导航操作的令牌。</param>
        public Task ReturnToStartMenuAsync(CancellationToken cancellationToken) =>
            NavigateAsync(_startMenuSceneName, cancellationToken);

        /// <summary>退出游戏（Editor 中仅记录模拟退出；Player 中调用 Application.Quit）.</summary>
        /// <param name="cancellationToken">在退出前检查的取消令牌。</param>
        public Task QuitGameAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if UNITY_EDITOR
            _logger.LogInfo(LogContext.Empty, "[GameFlowService] 模拟退出(Editor 不终止进程)");
#else
            _logger.LogInfo(LogContext.Empty, "[GameFlowService] 退出游戏");
            UnityEngine.Application.Quit();
#endif
            return Task.CompletedTask;
        }

        /// <summary>释放当前场景生命周期（取消其订阅与异步请求）.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _activeScope?.Dispose();
            _activeScope = null;
        }

        /// <summary>
        /// 执行一次带防重入、旧场景卸载和新场景激活的功能场景导航。
        /// </summary>
        /// <param name="sceneName">目标功能场景名。</param>
        /// <param name="cancellationToken">调用方取消标记。</param>
        private async Task NavigateAsync(string sceneName, CancellationToken cancellationToken)
        {
            // 防重入：切换期间屏蔽重复导航请求
            if (_isNavigating)
            {
                _logger.LogWarning(LogContext.Empty, $"[GameFlowService] 场景切换进行中,忽略请求:{sceneName}");
                return;
            }

            ThrowIfDisposed();

            _isNavigating = true;
            var scope = new CancellationTokenScope();
            try
            {
                // 1. 结束旧场景生命周期：取消其订阅与异步请求
                _activeScope?.Dispose();
                _activeScope = null;

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(scope.Token, cancellationToken);

                // 2. 卸载非目标功能场景（目标已加载时跳过，保证幂等不加载两份）
                // 先快照再卸载：LoadedSceneNames 可能是活集合视图（如测试替身），循环内卸载会改集合
                foreach (var loaded in new List<string>(_sceneLoader.LoadedSceneNames))
                {
                    if (loaded == sceneName)
                    {
                        continue;
                    }

                    if (!await _sceneLoader.UnloadAsync(loaded, linked.Token))
                    {
                        _logger.LogError(LogContext.Empty, $"[GameFlowService] 卸载场景失败:{loaded}");
                    }
                }

                // 3. 加载目标场景并激活（场景加载器负责 SetActive）
                if (!await _sceneLoader.LoadAdditiveAsync(sceneName, linked.Token))
                {
                    _logger.LogError(LogContext.Empty, $"[GameFlowService] 加载场景失败:{sceneName}");
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // 4. 新场景生命周期转交 _activeScope，由下次切换或 Dispose 释放
                _activeScope = scope;
                _eventBus.Publish(new SceneActivatedEvent(sceneName, _lastMetaPage));
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(LogContext.Empty, $"[GameFlowService] 场景切换取消:{sceneName}");
            }
            finally
            {
                // 成功时 scope 已转交 _activeScope；失败/取消时在此释放
                if (!ReferenceEquals(_activeScope, scope))
                {
                    scope.Dispose();
                }

                _isNavigating = false;
            }
        }

        /// <summary>在服务已经释放时抛出对象已释放异常。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GameFlowService));
            }
        }
    }
}
