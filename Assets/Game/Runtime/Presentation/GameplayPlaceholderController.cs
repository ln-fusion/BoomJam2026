using System;
using System.Threading;
using Game.Contracts;
using Game.Contracts.Progression;
using Game.Flow;
using Game.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// C16 占位关卡控制器：提供"完成关卡"按钮驱动占位完成到关后剧情的流程。
    /// </summary>
    /// <remarks>
    /// 在正式 Gameplay HUD（C25）落地前由 <see cref="SceneUiInstaller"/> 安装到 04_Gameplay 场景。
    /// </remarks>
    public sealed class GameplayPlaceholderController : MonoBehaviour
    {
        private GameRuntimeServices _runtimeServices;
        private CancellationTokenSource _lifetime;
        private Text _hintText;
        private bool _initialized;

        /// <summary>安装占位控制器。</summary>
        /// <param name="runtimeServices">Bootstrap 创建的运行时服务容器。</param>
        public void Initialize(GameRuntimeServices runtimeServices)
        {
            if (_initialized)
                return;
            _initialized = true;
            _runtimeServices = runtimeServices ?? throw new ArgumentNullException(nameof(runtimeServices));
            _lifetime = new CancellationTokenSource();
            BuildView();
        }

        /// <summary>显示当前关卡提示和完成按钮。</summary>
        private void BuildView()
        {
            Canvas canvas = UiFactory.CreateCanvas("GameplayCanvas", transform, 0);
            var background = UiFactory.CreatePanel("Background", canvas.transform, UiTheme.Background);
            UiFactory.Stretch(background.rectTransform, Vector2.zero);

            _hintText = UiFactory.CreateText(
                "Hint",
                canvas.transform,
                "Gameplay placeholder - press Complete to finish",
                24,
                UiTheme.Text,
                TextAnchor.MiddleCenter
            );
            _hintText.rectTransform.anchorMin = new Vector2(0.2f, 0.6f);
            _hintText.rectTransform.anchorMax = new Vector2(0.8f, 0.8f);
            _hintText.rectTransform.offsetMin = _hintText.rectTransform.offsetMax = Vector2.zero;

            Button complete = UiFactory.CreateButton("Complete", canvas.transform, "Complete Level");
            RectTransform completeRect = complete.GetComponent<RectTransform>();
            completeRect.anchorMin = new Vector2(0.4f, 0.3f);
            completeRect.anchorMax = new Vector2(0.6f, 0.42f);
            completeRect.offsetMin = completeRect.offsetMax = Vector2.zero;
            complete.onClick.AddListener(OnCompleteClicked);
        }

        /// <summary>点击完成按钮时提交完成事实并进入关后剧情流程。</summary>
        private void OnCompleteClicked()
        {
            GameFlowService flow = _runtimeServices.Flow as GameFlowService;
            if (flow == null || flow.CurrentLevelId == null)
            {
                Debug.LogError("[GameplayPlaceholder] 无当前关卡，无法完成。", this);
                return;
            }
            // 完成流程是跨场景导航: 会先卸载本 Gameplay 场景。绑定自身场景生命周期的 token
            // 会在 OnDestroy 时反向取消刚发起的卸载/加载 await, 误报"场景加载失败", 故选 None。
            _ = flow.CompleteLevelAsync(flow.CurrentLevelId, CancellationToken.None);
        }

        /// <summary>销毁时取消挂起的完成流程。</summary>
        private void OnDestroy()
        {
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
        }
    }
}
