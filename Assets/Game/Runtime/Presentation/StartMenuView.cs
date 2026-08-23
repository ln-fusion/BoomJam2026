using System;
using Game.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// 开始界面 View：只负责 uGUI 控件、按钮事件和昵称输入，不直接调用流程或 Unity 退出 API。
    /// </summary>
    public sealed class StartMenuView : MonoBehaviour, IView<StartMenuViewModel>
    {
        private Canvas _canvas;
        private Text _title;
        private Text _feedback;
        private Button _startButton;
        private Button _settingsButton;
        private Button _quitButton;
        private GameObject _nicknamePanel;
        private InputField _nicknameInput;
        private Text _nicknamePrompt;
        private Text _nicknameError;
        private Button _nicknameConfirmButton;
        private Button _nicknameCancelButton;
        private ILocalizationService _localizationService;
        private bool _hasProfile;

        /// <summary>开始/继续按钮事件。</summary>
        public event Action StartRequested;
        /// <summary>设置按钮事件。</summary>
        public event Action SettingsRequested;
        /// <summary>退出按钮事件。</summary>
        public event Action QuitRequested;
        /// <summary>昵称确认事件。</summary>
        public event Action<string> NicknameSubmitted;
        /// <summary>昵称弹窗取消事件。</summary>
        public event Action NicknameCancelled;

        /// <summary>创建开始界面动态 uGUI。</summary>
        private void Awake()
        {
            BuildView();
        }

        /// <summary>订阅 Locale 变化并刷新文本。</summary>
        private void OnEnable()
        {
            if (_localizationService != null)
                _localizationService.LocaleChanged += OnLocaleChanged;
        }

        /// <summary>取消 Locale 订阅。</summary>
        private void OnDisable()
        {
            if (_localizationService != null)
                _localizationService.LocaleChanged -= OnLocaleChanged;
        }

        /// <summary>注入本地化服务。</summary>
        /// <param name="localizationService">本地化服务。</param>
        public void Initialize(ILocalizationService localizationService)
        {
            if (_localizationService != null)
                _localizationService.LocaleChanged -= OnLocaleChanged;
            _localizationService = localizationService;
            if (_localizationService != null && isActiveAndEnabled)
                _localizationService.LocaleChanged += OnLocaleChanged;
            RefreshTexts();
        }

        /// <summary>在编辑器导出临时预制体时创建默认开始菜单控件。</summary>
        public void BuildPreview()
        {
            if (_canvas == null)
                BuildView();
        }

        /// <summary>渲染当前档案状态对应的按钮文本。</summary>
        /// <param name="viewModel">开始界面模型。</param>
        public void Render(StartMenuViewModel viewModel)
        {
            if (viewModel == null)
                return;

            _hasProfile = viewModel.HasProfile;
            SetButtonText(_startButton, viewModel.HasProfile
                ? Text(UiTextKeys.ContinueGame) : Text(UiTextKeys.StartGame));
            ShowFeedback(viewModel.Feedback);
        }

        /// <summary>显示一条开始界面反馈。</summary>
        /// <param name="message">反馈文本。</param>
        public void ShowFeedback(string message)
        {
            if (_feedback == null)
                return;

            _feedback.text = message ?? string.Empty;
            _feedback.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        /// <summary>显示昵称输入弹窗。</summary>
        public void ShowNicknamePrompt()
        {
            if (_nicknamePanel == null)
                return;

            _nicknamePanel.SetActive(true);
            _nicknameInput.text = string.Empty;
            _nicknameInput.Select();
            _nicknameInput.ActivateInputField();
            _nicknameError.text = string.Empty;
        }

        /// <summary>隐藏昵称输入弹窗。</summary>
        public void HideNicknamePrompt()
        {
            if (_nicknamePanel != null)
                _nicknamePanel.SetActive(false);
        }

        /// <summary>显示昵称校验错误。</summary>
        /// <param name="message">错误消息。</param>
        public void ShowNicknameError(string message)
        {
            if (_nicknameError != null)
                _nicknameError.text = message ?? string.Empty;
        }

        /// <summary>创建开始界面 Canvas、主操作和昵称弹窗。</summary>
        private void BuildView()
        {
            if (TryBindConfiguredView())
                return;

            if (GetComponent<UiPrefabRoot>() != null)
                Debug.LogWarning("StartMenuUI 预制体契约不完整，回退到代码生成界面。", this);

            _canvas = UiFactory.CreateCanvas("SceneCanvas", transform, 0);
            var background = UiFactory.CreatePanel("Background", _canvas.transform, UiTheme.Background);
            UiFactory.Stretch(background.rectTransform, Vector2.zero);

            // 文案统一由 RefreshTexts 从 String Table 注入，避免初始化瞬间保留硬编码语言。
            _title = UiFactory.CreateText("Title", _canvas.transform, string.Empty, 56, UiTheme.Text);
            Place(_title.rectTransform, new Vector2(0.2f, 0.72f), new Vector2(0.8f, 0.86f));
            _feedback = UiFactory.CreateText("Feedback", _canvas.transform, string.Empty, 22, UiTheme.Accent);
            Place(_feedback.rectTransform, new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.2f));

            _startButton = AddMainButton("Start", 0.56f);
            _settingsButton = AddMainButton("Settings", 0.43f);
            _quitButton = AddMainButton("Quit", 0.3f);
            _startButton.onClick.AddListener(() => StartRequested?.Invoke());
            _settingsButton.onClick.AddListener(() => SettingsRequested?.Invoke());
            _quitButton.onClick.AddListener(() => QuitRequested?.Invoke());

            _nicknamePanel = UiFactory.CreatePanel("NicknameModal", _canvas.transform,
                new Color(0.02f, 0.03f, 0.06f, 0.98f)).gameObject;
            var modalRect = _nicknamePanel.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(620f, 330f);
            _nicknamePrompt = UiFactory.CreateText("Prompt", _nicknamePanel.transform, string.Empty, 28,
                UiTheme.Text);
            Place(_nicknamePrompt.rectTransform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.9f));
            _nicknameInput = UiFactory.CreateInputField("Input", _nicknamePanel.transform, string.Empty);
            Place(_nicknameInput.GetComponent<RectTransform>(), new Vector2(0.08f, 0.43f),
                new Vector2(0.92f, 0.62f));
            _nicknameError = UiFactory.CreateText("Error", _nicknamePanel.transform, string.Empty, 18,
                new Color(1f, 0.45f, 0.4f, 1f));
            Place(_nicknameError.rectTransform, new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.4f));
            _nicknameCancelButton = UiFactory.CreateButton("Cancel", _nicknamePanel.transform, string.Empty);
            Place(_nicknameCancelButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.08f),
                new Vector2(0.45f, 0.24f));
            _nicknameConfirmButton = UiFactory.CreateButton("Confirm", _nicknamePanel.transform, string.Empty);
            Place(_nicknameConfirmButton.GetComponent<RectTransform>(), new Vector2(0.55f, 0.08f),
                new Vector2(0.92f, 0.24f));
            _nicknameConfirmButton.onClick.AddListener(() => NicknameSubmitted?.Invoke(_nicknameInput.text));
            _nicknameCancelButton.onClick.AddListener(() =>
            {
                HideNicknamePrompt();
                NicknameCancelled?.Invoke();
            });
            _nicknamePanel.SetActive(false);
        }

        /// <summary>绑定画师预制体中约定名称的控件，不改变其视觉层级。</summary>
        /// <returns>预制体包含完整必需节点时返回 true。</returns>
        private bool TryBindConfiguredView()
        {
            Transform canvas = transform.Find("SceneCanvas");
            if (canvas == null)
                return false;

            _canvas = canvas.GetComponent<Canvas>();
            var configured = GetComponent<StartMenuUiBindings>();
            if (configured != null && configured.IsComplete)
            {
                _title = configured.Title;
                _feedback = configured.Feedback;
                _startButton = configured.StartButton;
                _settingsButton = configured.SettingsButton;
                _quitButton = configured.QuitButton;
                _nicknamePanel = configured.NicknamePanel;
                _nicknamePrompt = configured.NicknamePrompt;
                _nicknameInput = configured.NicknameInput;
                _nicknameError = configured.NicknameError;
                _nicknameConfirmButton = configured.NicknameConfirmButton;
                _nicknameCancelButton = configured.NicknameCancelButton;
                WireConfiguredEvents();
                return true;
            }
            _title = FindText(canvas, "Title");
            _feedback = FindText(canvas, "Feedback");
            _startButton = FindButton(canvas, "Start");
            _settingsButton = FindButton(canvas, "Settings");
            _quitButton = FindButton(canvas, "Quit");
            _nicknamePanel = FindObject(canvas, "NicknameModal");
            _nicknamePrompt = FindText(_nicknamePanel == null ? null : _nicknamePanel.transform, "Prompt");
            _nicknameInput = FindInput(_nicknamePanel == null ? null : _nicknamePanel.transform, "Input");
            _nicknameError = FindText(_nicknamePanel == null ? null : _nicknamePanel.transform, "Error");
            _nicknameCancelButton = FindButton(_nicknamePanel == null ? null : _nicknamePanel.transform, "Cancel");
            _nicknameConfirmButton = FindButton(_nicknamePanel == null ? null : _nicknamePanel.transform, "Confirm");
            if (_canvas == null || _title == null || _feedback == null || _startButton == null ||
                _settingsButton == null || _quitButton == null || _nicknamePanel == null ||
                _nicknamePrompt == null || _nicknameInput == null || _nicknameError == null ||
                _nicknameCancelButton == null || _nicknameConfirmButton == null)
                return false;

            _startButton.onClick.AddListener(() => StartRequested?.Invoke());
            _settingsButton.onClick.AddListener(() => SettingsRequested?.Invoke());
            _quitButton.onClick.AddListener(() => QuitRequested?.Invoke());
            _nicknameConfirmButton.onClick.AddListener(() => NicknameSubmitted?.Invoke(_nicknameInput.text));
            _nicknameCancelButton.onClick.AddListener(() =>
            {
                HideNicknamePrompt();
                NicknameCancelled?.Invoke();
            });
            return true;
        }

        /// <summary>为契约绑定的开始菜单控件连接业务事件。</summary>
        private void WireConfiguredEvents()
        {
            _startButton.onClick.AddListener(() => StartRequested?.Invoke());
            _settingsButton.onClick.AddListener(() => SettingsRequested?.Invoke());
            _quitButton.onClick.AddListener(() => QuitRequested?.Invoke());
            _nicknameConfirmButton.onClick.AddListener(() => NicknameSubmitted?.Invoke(_nicknameInput.text));
            _nicknameCancelButton.onClick.AddListener(() =>
            {
                HideNicknamePrompt();
                NicknameCancelled?.Invoke();
            });
        }

        /// <summary>按名称查找文本控件。</summary>
        /// <param name="parent">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的文本；否则为 null。</returns>
        private static Text FindText(Transform parent, string name)
        {
            return parent == null ? null : FindObject(parent, name)?.GetComponent<Text>();
        }

        /// <summary>按名称查找按钮控件。</summary>
        /// <param name="parent">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的按钮；否则为 null。</returns>
        private static Button FindButton(Transform parent, string name)
        {
            return parent == null ? null : FindObject(parent, name)?.GetComponent<Button>();
        }

        /// <summary>按名称查找输入控件。</summary>
        /// <param name="parent">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的输入框；否则为 null。</returns>
        private static InputField FindInput(Transform parent, string name)
        {
            return parent == null ? null : FindObject(parent, name)?.GetComponent<InputField>();
        }

        /// <summary>按名称递归查找节点。</summary>
        /// <param name="parent">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的节点；否则为 null。</returns>
        private static GameObject FindObject(Transform parent, string name)
        {
            if (parent == null)
                return null;
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child.gameObject;
            return null;
        }

        /// <summary>刷新当前 Locale 下所有界面文字。</summary>
        private void RefreshTexts()
        {
            if (_title == null)
                return;

            _title.text = Text(UiTextKeys.GameTitle);
            SetButtonText(_startButton, _hasProfile
                ? Text(UiTextKeys.ContinueGame) : Text(UiTextKeys.StartGame));
            SetButtonText(_settingsButton, Text(UiTextKeys.Settings));
            SetButtonText(_quitButton, Text(UiTextKeys.Quit));
            _nicknamePrompt.text = Text(UiTextKeys.Nickname);
            SetButtonText(_nicknameConfirmButton, Text(UiTextKeys.Confirm));
            SetButtonText(_nicknameCancelButton, Text(UiTextKeys.Cancel));
            _nicknameInput.placeholder.GetComponent<Text>().text = Text(UiTextKeys.Nickname);
        }

        /// <summary>Locale 变化回调。</summary>
        /// <param name="localeCode">新 Locale。</param>
        private void OnLocaleChanged(string localeCode)
        {
            RefreshTexts();
        }

        /// <summary>创建一个主操作按钮。</summary>
        /// <param name="name">按钮名称。</param>
        /// <param name="centerY">按钮中心高度。</param>
        /// <returns>创建的按钮。</returns>
        private Button AddMainButton(string name, float centerY)
        {
            Button button = UiFactory.CreateButton(name, _canvas.transform, string.Empty);
            Place(button.GetComponent<RectTransform>(), new Vector2(0.3f, centerY - 0.045f),
                new Vector2(0.7f, centerY + 0.045f));
            return button;
        }

        /// <summary>读取当前本地化文本。</summary>
        /// <param name="key">稳定键字符串。</param>
        /// <returns>本地化文本或稳定键。</returns>
        private string Text(string key)
        {
            return _localizationService == null
                ? key
                : _localizationService.Get(new Game.Foundation.LocalizationKey(key));
        }

        /// <summary>设置按钮内部文本。</summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="value">按钮文本。</param>
        private static void SetButtonText(Button button, string value)
        {
            if (button == null)
                return;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.text = value ?? string.Empty;
        }

        /// <summary>设置 RectTransform 的归一化位置。</summary>
        /// <param name="rectTransform">目标矩形。</param>
        /// <param name="min">归一化左下角。</param>
        /// <param name="max">归一化右上角。</param>
        private static void Place(RectTransform rectTransform, Vector2 min, Vector2 max)
        {
            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }

    /// <summary>开始界面的不可变渲染模型。</summary>
    public sealed class StartMenuViewModel
    {
        /// <summary>是否存在有效档案。</summary>
        public bool HasProfile { get; }
        /// <summary>待显示反馈。</summary>
        public string Feedback { get; }

        /// <summary>创建开始界面模型。</summary>
        /// <param name="hasProfile">是否有档案。</param>
        /// <param name="feedback">反馈文本。</param>
        public StartMenuViewModel(bool hasProfile, string feedback)
        {
            HasProfile = hasProfile;
            Feedback = feedback ?? string.Empty;
        }
    }
}
