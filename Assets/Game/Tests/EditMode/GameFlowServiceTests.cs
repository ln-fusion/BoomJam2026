using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Contracts.Progression;
using Game.Flow;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 应用流程服务测试：导航、防重入、场景生命周期取消。
    /// </summary>
    public class GameFlowServiceTests
    {
        private FakeSceneLoader _loader;
        private DomainEventBus _eventBus;
        private IGameLogger _logger;
        private GameFlowService _flow;
        private ProfileSave _profile;
        private StoryCompletionCoordinator _coordinator;

        /// <summary>创建流转服务测试依赖。</summary>
        [SetUp]
        public void SetUp()
        {
            _loader = new FakeSceneLoader();
            _eventBus = new DomainEventBus(NullLogger.Instance);
            _logger = new NullLogger(collectEntries: true);
            _profile = new ProfileSave();
            _coordinator = new StoryCompletionCoordinator(
                () => _profile,
                (profile, reason, token) => Task.FromResult(SaveResult.Success()),
                _eventBus,
                NullLogger.Instance
            );
            _flow = new GameFlowService(
                _loader,
                new FixedClock(),
                _logger,
                _eventBus,
                SceneNames.StartMenu,
                _coordinator
            );
        }

        /// <summary>释放流转服务。</summary>
        [TearDown]
        public void TearDown()
        {
            _flow.Dispose();
        }

        /// <summary>验证进入开始菜单会加载对应场景。</summary>
        [Test]
        public void EnterStartMenu_Loads_StartMenu()
        {
            RunAsync(() => _flow.EnterStartMenuAsync(CancellationToken.None));

            Assert.That(_loader.LoadedSceneNames, Does.Contain(SceneNames.StartMenu));
            Assert.That(_loader.LastLoadRequest, Is.EqualTo(SceneNames.StartMenu));
        }

        /// <summary>验证切换到其他场景时会卸载旧场景。</summary>
        [Test]
        public void Navigate_To_Another_Scene_Unloads_Old()
        {
            RunAsync(async () =>
            {
                await _flow.EnterStartMenuAsync(CancellationToken.None);
                await _flow.OpenMetaHubAsync(MetaPageId.Map, CancellationToken.None);
            });

            Assert.That(_loader.LoadedSceneNames, Does.Contain(SceneNames.MetaHub));
            Assert.That(_loader.LoadedSceneNames, Does.Not.Contain(SceneNames.StartMenu));
        }

        /// <summary>验证重复导航到同一场景是幂等的。</summary>
        [Test]
        public void Navigate_To_Same_Scene_Is_Idempotent()
        {
            RunAsync(async () =>
            {
                await _flow.EnterStartMenuAsync(CancellationToken.None);
                await _flow.EnterStartMenuAsync(CancellationToken.None);
                await _flow.EnterStartMenuAsync(CancellationToken.None);
            });

            Assert.That(_loader.LoadedSceneNames, Has.Exactly(1).Matches<string>(n => n == SceneNames.StartMenu));
        }

        /// <summary>验证切换场景时旧场景的 token 会被取消。</summary>
        [Test]
        public void Scene_Token_Is_Cancelled_On_Navigation()
        {
            RunAsync(() => _flow.EnterStartMenuAsync(CancellationToken.None));
            var firstToken = _flow.ActiveSceneToken;
            Assert.That(firstToken.IsCancellationRequested, Is.False);

            RunAsync(() => _flow.OpenMetaHubAsync(MetaPageId.Map, CancellationToken.None));
            var secondToken = _flow.ActiveSceneToken;

            Assert.That(firstToken.IsCancellationRequested, Is.True, "旧场景 token 应被取消");
            Assert.That(secondToken.IsCancellationRequested, Is.False, "新场景 token 应可用");
        }

        /// <summary>验证场景激活事件会在导航完成后发布。</summary>
        [Test]
        public void Reset_Event_Is_Published_After_Navigation()
        {
            var activatedScenes = new System.Collections.Generic.List<string>();
            using (_eventBus.Subscribe<SceneActivatedEvent>(e => activatedScenes.Add(e.SceneName)))
            {
                RunAsync(() => _flow.EnterStartMenuAsync(CancellationToken.None));
            }

            Assert.That(activatedScenes, Does.Contain(SceneNames.StartMenu));
        }

        /// <summary>验证加载期间的重复导航请求会被忽略。</summary>
        [Test]
        public void Duplicate_Navigation_During_Load_Is_Blocked()
        {
            _loader.LoadDelayMs = 50;

            RunAsync(async () =>
            {
                var first = _flow.EnterStartMenuAsync(CancellationToken.None);
                var second = _flow.EnterStartMenuAsync(CancellationToken.None);
                await Task.WhenAll(first, second);
            });

            // 防重入：第二次请求被忽略，场景只加载一份
            Assert.That(_loader.LoadedSceneNames, Has.Exactly(1).Matches<string>(n => n == SceneNames.StartMenu));
        }

        /// <summary>在同步测试中执行异步操作并等待结果。</summary>
        /// <param name="operation">要执行的异步操作。</param>
        private static void RunAsync(Func<Task> operation)
        {
            Task.Run(operation).GetAwaiter().GetResult();
        }

        /// <summary>验证首次进入关卡先播放关前剧情再进 Gameplay。</summary>
        [Test]
        public void EnterLevel_FirstTime_PlaysPreludeThenGameplay()
        {
            var level = new LevelId("official.level.test_01_01");
            RunAsync(async () =>
            {
                await _flow.EnterLevelAsync(level, CancellationToken.None);
            });

            Assert.That(_loader.LoadedSceneNames, Does.Contain(SceneNames.Story));
        }

        /// <summary>验证关卡完成提交事实后播放关后剧情并返回地图。</summary>
        [Test]
        public void CompleteLevel_CommitsFact_ThenPlaysPostStory_ReturnsToMap()
        {
            var level = new LevelId("official.level.test_01_01");
            RunAsync(async () =>
            {
                await _flow.EnterLevelAsync(level, CancellationToken.None);
                // 首次进入先播放关前剧情，经路径进入 Gameplay
                await _flow.EnterLevelAsync(level, CancellationToken.None);
                await _flow.CompleteLevelAsync(level, CancellationToken.None);
            });

            // 关后剧情 → 返回地图，元界页面为目标
            Assert.That(
                _coordinator.IsCompleted(new StoryId("official.story.c06_branch")),
                Is.True,
                "完成事实应已提交"
            );
            Assert.That(_flow.LastStoryReturnTarget.HasValue, Is.True);
            Assert.That(_flow.LastStoryReturnTarget.Value.Kind, Is.EqualTo(StoryReturnKind.MetaPage));
            Assert.That(_flow.LastStoryReturnTarget.Value.MetaPage, Is.EqualTo(MetaPageId.Map));
        }

        /// <summary>验证关卡完成时把关卡 ID 写入 CompletedLevelIds 并以 ProgressCommitted 保存。</summary>
        [Test]
        public void CompleteLevel_WritesLevelFact_ToProfile()
        {
            var level = new LevelId("official.level.test_01_01");
            var saves = new System.Collections.Generic.List<SaveReason>();
            var profile = new ProfileSave();
            ProfileSave captured = null;
            var flow = new GameFlowService(
                _loader,
                new FixedClock(),
                _logger,
                _eventBus,
                SceneNames.StartMenu,
                null,
                () => profile,
                (data, reason, token) =>
                {
                    saves.Add(reason);
                    captured = data;
                    return Task.FromResult(SaveResult.Success());
                }
            );
            RunAsync(() => flow.CompleteLevelAsync(level, CancellationToken.None));

            Assert.That(profile.CompletedLevelIds, Does.Contain(level.Value));
            Assert.That(saves, Does.Contain(SaveReason.ProgressCommitted));
            Assert.That(captured, Is.SameAs(profile), "保存委托应收到同一份 Profile 引用");
            flow.Dispose();
        }

        /// <summary>验证保存失败时关卡完成事实回滚, 可重试且不阻断流程判断。</summary>
        [Test]
        public void CompleteLevel_SaveFailure_RollsBackLevelFact()
        {
            var level = new LevelId("official.level.test_01_01");
            var profile = new ProfileSave();
            var flow = new GameFlowService(
                _loader,
                new FixedClock(),
                _logger,
                _eventBus,
                SceneNames.StartMenu,
                null,
                () => profile,
                (data, reason, token) => Task.FromResult(SaveResult.Failure(ErrorCode.SaveFailed, "disk error"))
            );
            RunAsync(() => flow.CompleteLevelAsync(level, CancellationToken.None));

            Assert.That(profile.CompletedLevelIds, Does.Not.Contain(level.Value), "失败时不应残留内存标记");
            flow.Dispose();
        }
    }
}
