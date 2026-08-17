using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap
{
    /// <summary>
    /// 应用组合根：驻留 00_Bootstrap 场景，负责启动阶段的场景流转.
    /// </summary>
    /// <remarks>
    /// C01 骨架期：仅完成最小链路（进入空 StartMenu），C02 将由 IGameFlowService 接管正式流转.
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRoot : MonoBehaviour
    {
        [Tooltip("启动后加载的功能场景名称（Build Settings 顺序即可，Additive 加载）")]
        [SerializeField]
        private string startMenuSceneName = "01_StartMenu";

        [Tooltip("加载功能场景前卸载 Bootstrap 场景内不需要保留的对象层级")]
        [SerializeField]
        private bool unloadBootstrapScene = false;

        private void Start()
        {
            LoadStartMenu();
        }

        /// <summary>
        /// 用 Additive 方式加载 StartMenu 并设为 Active 场景.
        /// </summary>
        private void LoadStartMenu()
        {
            if (string.IsNullOrEmpty(startMenuSceneName))
            {
                Debug.LogError($"[{nameof(GameRoot)}] startMenuSceneName 为空,F1 无法继续");
                return;
            }

            var op = SceneManager.LoadSceneAsync(startMenuSceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[{nameof(GameRoot)}] 场景 {startMenuSceneName} 不存在,Build Settings 未包含");
                return;
            }

            op.completed += OnStartMenuLoaded;
        }

        /// <summary>
        /// StartMenu 加载完成回调：设为 Active 场景，卸载 Bootstrap 场景（保护性双重校验）.
        /// </summary>
        private void OnStartMenuLoaded(AsyncOperation op)
        {
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(startMenuSceneName));

            if (unloadBootstrapScene)
            {
                SceneManager.UnloadSceneAsync(gameObject.scene);
            }
        }
    }
}
