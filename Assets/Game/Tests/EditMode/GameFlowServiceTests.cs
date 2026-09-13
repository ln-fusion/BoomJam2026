using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
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

        /// <summary>创建流转服务测试依赖。</summary>
        [SetUp]
        public void SetUp()
        {
            _loader = new FakeSceneLoader();
            _eventBus = new DomainEventBus(NullLogger.Instance);
            _logger = new NullLogger(collectEntries: true);
            _profile = new ProfileSave();
            _flow = new GameFlowService(
                _loader,
                new FixedClock(),
                _logger,
                _eventBus,
                SceneNames.StartMenu,
                getPrelude: level => new StoryId("pre." + level.Value),
                isStoryCompleted: id => _profile.CompletedStoryIds.Contains(id.Value)
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

        /// <summary>验证关前剧情没有完成事实时，进入关卡会先加载剧情场景。</summary>
        [Test]
        public void EnterLevel_With_Incomplete_Prelude_Loads_Story()
        {
            var storyId = new StoryId("official.story.prelude.test_01_01");
            using var flow = new GameFlowService(_loader, new FixedClock(), _logger, _eventBus,
                SceneNames.StartMenu, _ => storyId, _ => false);

            RunAsync(() => flow.EnterLevelAsync(new LevelId("official.level.test_01_01"),
                CancellationToken.None));

            Assert.That(_loader.LastLoadRequest, Is.EqualTo(SceneNames.Story));
            Assert.That(flow.CurrentStoryId, Is.EqualTo(storyId));
        }

        /// <summary>验证关前剧情完成事实已存在时，进入关卡会直接加载玩法场景。</summary>
        [Test]
        public void EnterLevel_With_Completed_Prelude_Loads_Gameplay()
        {
            var storyId = new StoryId("official.story.prelude.test_01_01");
            using var flow = new GameFlowService(_loader, new FixedClock(), _logger, _eventBus,
                SceneNames.StartMenu, _ => storyId, id => id == storyId);

            RunAsync(() => flow.EnterLevelAsync(new LevelId("official.level.test_01_01"),
                CancellationToken.None));

            Assert.That(_loader.LastLoadRequest, Is.EqualTo(SceneNames.Gameplay));
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

        /// <summary>验证结算只转交已配置的入口一次，不预先伪造剧情完成事实。</summary>
        [Test]
        public void CompleteLevel_DelegatesWithoutCommittingStory()
        {
            RunAsync(async () =>
            {
                var level = new LevelId("official.level.test_01_01");
                int calls = 0;
                using var flow = new GameFlowService(_loader, new FixedClock(), _logger, _eventBus,
                    completeLevel: (id, token) =>
                    {
                        Assert.That(id, Is.EqualTo(level));
                        calls++;
                        return Task.CompletedTask;
                    });
                await flow.CompleteLevelAsync(level, CancellationToken.None);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(_profile.CompletedStoryIds, Is.Empty);
            });
        }

        /// <summary>验证没有结算服务时显式拒绝提交，避免假通关或跳转。</summary>
        [Test]
        public void CompleteLevel_WithoutHandler_RejectsSubmission()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _flow.CompleteLevelAsync(new LevelId("official.level.test_01_01"), CancellationToken.None));
            Assert.That(_profile.CompletedLevelIds, Is.Empty);
        }

        /// <summary>验证第一关剧情完成不会跳过第二关剧情，且无剧情关卡直接进入玩法。</summary>
        [Test]
        public void EnterLevel_UsesPerLevelCompletion()
        {
            RunAsync(async () =>
            {
                var first = new LevelId("official.level.test_01_01");
                var second = new LevelId("official.level.test_01_02");
                _profile.CompletedStoryIds.Add("pre." + first.Value);
                await _flow.EnterLevelAsync(first, CancellationToken.None);
                Assert.That(_loader.LastLoadRequest, Is.EqualTo(SceneNames.Gameplay));
                await _flow.EnterLevelAsync(second, CancellationToken.None);
                Assert.That(_flow.CurrentStoryId.Value, Is.EqualTo("pre." + second.Value));
                using var noStory = new GameFlowService(_loader, new FixedClock(), _logger, _eventBus);
                await noStory.EnterLevelAsync(first, CancellationToken.None);
                Assert.That(_loader.LastLoadRequest, Is.EqualTo(SceneNames.Gameplay));
            });
        }
    }
}
