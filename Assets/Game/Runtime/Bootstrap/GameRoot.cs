#nullable enable
using Game.Flow;
using Game.Foundation;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// 应用组合根：驻留 00_Bootstrap 场景，组装 Flow 服务并执行启动导航.
    /// </summary>
    /// <remarks>
    /// C02 起：GameRoot 不再直接调 SceneManager，改由 <see cref="IGameFlowService"/> 接管流转
    /// （Additive 加载/卸载/激活、防重入、取消生命周期）.
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRoot : MonoBehaviour
    {
        [Tooltip("启动后加载的功能场景名（Build Settings 场景名）")]
        [SerializeField]
        private string startMenuSceneName = SceneNames.StartMenu;

        private GameFlowService? _flowService;

        private void Start()
        {
            var eventBus = new DomainEventBus(UnityDebugLogger.Instance);
            _flowService = new GameFlowService(
                new UnitySceneLoader(),
                new SystemClock(),
                UnityDebugLogger.Instance,
                eventBus,
                startMenuSceneName
            );

            _flowService.EnterStartMenuAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);
        }

        private void OnDestroy()
        {
            _flowService?.Dispose();
        }
    }
}
