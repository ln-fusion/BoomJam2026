using System;
using System.Collections;
using Game.Content;
using Game.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// 跨功能 Scene 的 UI 层：提供全局错误/反馈遮罩和排他的 ModalCanvas。
    /// </summary>
    public sealed class GlobalCanvasLayer : MonoBehaviour
    {
        private const float FeedbackDisplaySeconds = 3f;
        private Canvas _globalCanvas;
        private Canvas _modalCanvas;
        private Text _feedbackText;
        private Coroutine _feedbackHideRoutine;
        private SettingsModalPresenter _settingsPresenter;
        private EventSystem _eventSystem;
        private GameRuntimeServices _runtimeServices;
        private ContentAssetRegistry _contentRegistry;

        /// <summary>全局反馈文字节点。</summary>
        public Text FeedbackText => _feedbackText;

        /// <summary>设置弹窗所在 Canvas。</summary>
        public Canvas ModalCanvas => _modalCanvas;

        /// <summary>初始化并持久化全局 Canvas。</summary>
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            BuildCanvases();
        }

        /// <summary>注入 Bootstrap 创建的运行时服务容器。</summary>
        /// <param name="runtimeServices">本次应用的服务容器。</param>
        /// <param name="contentRegistry">可选官方 UI 预制体 Registry。</param>
        public void Initialize(GameRuntimeServices runtimeServices,
            ContentAssetRegistry contentRegistry = null)
        {
            _runtimeServices = runtimeServices ?? throw new ArgumentNullException(nameof(runtimeServices));
            _contentRegistry = contentRegistry;
        }

        /// <summary>销毁实例时释放当前设置弹窗。</summary>
        private void OnDestroy()
        {
            if (_feedbackHideRoutine != null)
                StopCoroutine(_feedbackHideRoutine);
            if (_settingsPresenter != null)
                Destroy(_settingsPresenter.gameObject);
            _feedbackHideRoutine = null;
            _settingsPresenter = null;
        }

        /// <summary>显示一条不参与鼠标射线检测、并在短暂停留后自动隐藏的全局反馈。</summary>
        /// <param name="message">反馈文本。</param>
        public void ShowFeedback(string message)
        {
            if (_feedbackText == null)
                return;

            if (_feedbackHideRoutine != null)
            {
                StopCoroutine(_feedbackHideRoutine);
                _feedbackHideRoutine = null;
            }

            _feedbackText.text = message ?? string.Empty;
            bool hasMessage = !string.IsNullOrWhiteSpace(message);
            _feedbackText.gameObject.SetActive(hasMessage);
            if (hasMessage)
                _feedbackHideRoutine = StartCoroutine(HideFeedbackAfterDelay());
        }

        /// <summary>使用不受时间缩放影响的等待，在提示停留结束后清空并隐藏文本。</summary>
        /// <returns>等待提示生命周期结束的协程。</returns>
        private IEnumerator HideFeedbackAfterDelay()
        {
            yield return new WaitForSecondsRealtime(FeedbackDisplaySeconds);
            if (_feedbackText != null)
            {
                _feedbackText.text = string.Empty;
                _feedbackText.gameObject.SetActive(false);
            }

            _feedbackHideRoutine = null;
        }

        /// <summary>打开设置弹窗；已有弹窗时只聚焦已有实例。</summary>
        public void OpenSettings()
        {
            if (_settingsPresenter != null)
            {
                _settingsPresenter.Focus();
                return;
            }

            if (_runtimeServices == null)
            {
                ShowFeedback("Settings service is unavailable.");
                return;
            }

            GameObject prefab = _contentRegistry == null ? null :
                new OfficialAssetResolver(_contentRegistry).GetUiPrefab(new UiPrefabId("ui.settings-modal"));
            var gameObject = prefab == null
                ? new GameObject("SettingsModal", typeof(RectTransform))
                : Instantiate(prefab);
            gameObject.transform.SetParent(_modalCanvas.transform, false);
            if (gameObject.transform is RectTransform rootRect)
                UiFactory.Stretch(rootRect, Vector2.zero);
            _settingsPresenter = gameObject.GetComponent<SettingsModalPresenter>() ??
                gameObject.AddComponent<SettingsModalPresenter>();
            _settingsPresenter.Initialize(this, _runtimeServices.Settings,
                _runtimeServices.Localization);
        }

        /// <summary>关闭当前设置弹窗。</summary>
        public void CloseSettings()
        {
            if (_settingsPresenter == null)
                return;

            Destroy(_settingsPresenter.gameObject);
            _settingsPresenter = null;
        }

        /// <summary>创建全局遮罩和模态层。</summary>
        private void BuildCanvases()
        {
            if (_globalCanvas != null && _modalCanvas != null)
                return;

            _globalCanvas = UiFactory.CreateCanvas("GlobalOverlayCanvas", transform, 900);
            var overlay = UiFactory.CreatePanel("Overlay", _globalCanvas.transform, UiTheme.Overlay);
            UiFactory.Stretch(overlay.rectTransform, Vector2.zero);
            overlay.gameObject.SetActive(false);
            _feedbackText = UiFactory.CreateText("Feedback", _globalCanvas.transform, string.Empty,
                24, UiTheme.Text);
            _feedbackText.raycastTarget = false;
            var feedbackRect = _feedbackText.rectTransform;
            feedbackRect.anchorMin = new Vector2(0.2f, 0.02f);
            feedbackRect.anchorMax = new Vector2(0.8f, 0.1f);
            feedbackRect.offsetMin = Vector2.zero;
            feedbackRect.offsetMax = Vector2.zero;
            _feedbackText.gameObject.SetActive(false);

            _modalCanvas = UiFactory.CreateCanvas("ModalCanvas", transform, 1000);
            var blocker = UiFactory.CreatePanel("ModalBlocker", _modalCanvas.transform,
                new Color(0f, 0f, 0f, 0.58f));
            UiFactory.Stretch(blocker.rectTransform, Vector2.zero);
            blocker.gameObject.SetActive(false);
        }

        /// <summary>创建全局 EventSystem，保证动态 uGUI 在空场景中也可交互。</summary>
        private void EnsureEventSystem()
        {
            _eventSystem = FindObjectOfType<EventSystem>();
            if (_eventSystem != null)
                return;

            var eventSystemObject = new GameObject("GlobalEventSystem", typeof(EventSystem),
                typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
            _eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        /// <summary>设置模态层阻挡状态；由设置 Presenter 调用。</summary>
        /// <param name="blocked">是否显示阻挡层。</param>
        internal void SetModalBlocked(bool blocked)
        {
            if (_modalCanvas == null)
                return;

            Transform blocker = _modalCanvas.transform.Find("ModalBlocker");
            if (blocker != null)
                blocker.gameObject.SetActive(blocked);
        }
    }
}
