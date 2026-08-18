using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Flow;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 应用流程服务测试：导航、防重入、场景生命周期取消.
    /// </summary>
    public class GameFlowServiceTests
    {
        private FakeSceneLoader _loader;
        private DomainEventBus _eventBus;
        private IGameLogger _logger;
        private GameFlowService _flow;

        [SetUp]
        public void SetUp()
        {
            _loader = new FakeSceneLoader();
            _eventBus = new DomainEventBus(NullLogger.Instance);
            _logger = new NullLogger(collectEntries: true);
            _flow = new GameFlowService(_loader, new FixedClock(), _logger, _eventBus, SceneNames.StartMenu);
        }

        [TearDown]
        public void TearDown()
        {
            _flow.Dispose();
        }

        [Test]
        public async Task EnterStartMenu_Loads_StartMenu()
        {
            await _flow.EnterStartMenuAsync(CancellationToken.None);

            Assert.That(_loader.LoadedSceneNames, Does.Contain(SceneNames.StartMenu));
            Assert.That(_loader.LastLoadRequest, Is.EqualTo(SceneNames.StartMenu));
        }

        [Test]
        public async Task Navigate_To_Another_Scene_Unloads_Old()
        {
            await _flow.EnterStartMenuAsync(CancellationToken.None);
            await _flow.OpenMetaHubAsync(MetaPageId.Map, CancellationToken.None);

            Assert.That(_loader.LoadedSceneNames, Does.Contain(SceneNames.MetaHub));
            Assert.That(_loader.LoadedSceneNames, Does.Not.Contain(SceneNames.StartMenu));
        }

        [Test]
        public async Task Navigate_To_Same_Scene_Is_Idempotent()
        {
            await _flow.EnterStartMenuAsync(CancellationToken.None);
            await _flow.EnterStartMenuAsync(CancellationToken.None);
            await _flow.EnterStartMenuAsync(CancellationToken.None);

            Assert.That(_loader.LoadedSceneNames, Has.Exactly(1).Matches<string>(n => n == SceneNames.StartMenu));
        }

        [Test]
        public async Task Scene_Token_Is_Cancelled_On_Navigation()
        {
            await _flow.EnterStartMenuAsync(CancellationToken.None);
            var firstToken = _flow.ActiveSceneToken;
            Assert.That(firstToken.IsCancellationRequested, Is.False);

            await _flow.OpenMetaHubAsync(MetaPageId.Map, CancellationToken.None);
            var secondToken = _flow.ActiveSceneToken;

            Assert.That(firstToken.IsCancellationRequested, Is.True, "旧场景 token 应被取消");
            Assert.That(secondToken.IsCancellationRequested, Is.False, "新场景 token 应可用");
        }

        [Test]
        public async Task Reset_Event_Is_Published_After_Navigation()
        {
            var activatedScenes = new System.Collections.Generic.List<string>();
            using (_eventBus.Subscribe<SceneActivatedEvent>(e => activatedScenes.Add(e.SceneName)))
            {
                await _flow.EnterStartMenuAsync(CancellationToken.None);
            }

            Assert.That(activatedScenes, Does.Contain(SceneNames.StartMenu));
        }

        [Test]
        public async Task Duplicate_Navigation_During_Load_Is_Blocked()
        {
            _loader.LoadDelayMs = 50;

            var first = _flow.EnterStartMenuAsync(CancellationToken.None);
            var second = _flow.EnterStartMenuAsync(CancellationToken.None);
            await Task.WhenAll(first, second);

            // 防重入：第二次请求被忽略，场景只加载一份
            Assert.That(_loader.LoadedSceneNames, Has.Exactly(1).Matches<string>(n => n == SceneNames.StartMenu));
        }
    }
}
