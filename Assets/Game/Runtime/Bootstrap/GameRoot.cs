#nullable enable
using System;
using System.Threading.Tasks;
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

        /// <summary>创建流程服务并启动开始菜单导航。</summary>
        private void Start()
        {
            var flowService = new GameFlowService(
                new UnitySceneLoader(),
                new SystemClock(),
                UnityDebugLogger.Instance,
                new DomainEventBus(UnityDebugLogger.Instance),
                startMenuSceneName
            );
            _flowService = flowService;

            _ = RunStartupAsync(flowService);
        }

        /// <summary>执行启动导航并记录不可恢复的启动异常。</summary>
        /// <param name="flowService">应用流程服务。</param>
        private static async Task RunStartupAsync(GameFlowService flowService)
        {
            try
            {
                await flowService.EnterStartMenuAsync(System.Threading.CancellationToken.None);
            }
            catch (Exception ex)
            {
                // 启动导航失败不能静默丢弃：黑屏时这是唯一排查线索
                Debug.LogException(ex);
            }
        }

        /// <summary>销毁组合根时释放流程服务及其当前场景生命周期。</summary>
        private void OnDestroy()
        {
            _flowService?.Dispose();
        }
    }
}
