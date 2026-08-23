using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// 设置弹窗的 View/Presenter 合体占位实现：UI 只编辑草稿，保存由 <see cref="ISettingsService"/> 完成。
    /// </summary>
    public sealed class SettingsModalPresenter : MonoBehaviour
    {
        private GlobalCanvasLayer _owner;
        private ISettingsService _settingsService;
        private ILocalizationService _localizationService;
        private CancellationTokenSource _lifetime;
        private GameObject _panel;
        private Text _title;
        private Text _masterLabel;
        private Text _musicLabel;
        private Text _sfxLabel;
        private Text _languageLabel;
        private Text _resolutionLabel;
        private Text _feedback;
        private Slider _masterSlider;
        private Slider _musicSlider;
        private Slider _sfxSlider;
        private Dropdown _languageDropdown;
        private Dropdown _resolutionDropdown;
        private Toggle _fullscreenToggle;
        private readonly List<ResolutionOption> _resolutionOptions = new List<ResolutionOption>();

        /// <summary>注入依赖并构建弹窗。</summary>
        /// <param name="owner">全局 Canvas 层。</param>
        /// <param name="settingsService">设置服务。</param>
        /// <param name="localizationService">本地化服务。</param>
        public void Initialize(GlobalCanvasLayer owner, ISettingsService settingsService,
            ILocalizationService localizationService)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _settingsService = settingsService ??
                throw new ArgumentNullException(nameof(settingsService));
            _localizationService = localizationService ??
                throw new ArgumentNullException(nameof(localizationService));
            _lifetime = new CancellationTokenSource();
            BuildView();
            _localizationService.LocaleChanged += OnLocaleChanged;
            Render(_settingsService.Current);
            _owner.SetModalBlocked(true);
        }

        /// <summary>把弹窗带到最前并选中标题节点。</summary>
        public void Focus()
        {
            if (_panel == null)
                return;

            _panel.transform.SetAsLastSibling();
            _panel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        }

        /// <summary>释放生命周期和本地化订阅。</summary>
        private void OnDestroy()
        {
            if (_localizationService != null)
                _localizationService.LocaleChanged -= OnLocaleChanged;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _owner?.SetModalBlocked(false);
        }

        /// <summary>创建设置弹窗的 uGUI 控件。</summary>
        private void BuildView()
        {
            if (TryBindConfiguredView())
            {
                ConfigureDropdownOptions();
                return;
            }

            if (GetComponent<UiPrefabRoot>() != null)
                Debug.LogWarning("SettingsModalUI 预制体契约不完整，回退到代码生成界面。", this);

            _panel = UiFactory.CreatePanel("SettingsPanel", transform, UiTheme.Panel).gameObject;
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 720f);

            _title = UiFactory.CreateText("Title", _panel.transform, string.Empty, 34, UiTheme.Text);
            Place(_title.rectTransform, new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.98f));

            _masterSlider = AddVolumeRow(UiTextKeys.MasterVolume, 0.73f, out _masterLabel);
            _musicSlider = AddVolumeRow(UiTextKeys.MusicVolume, 0.63f, out _musicLabel);
            _sfxSlider = AddVolumeRow(UiTextKeys.SfxVolume, 0.53f, out _sfxLabel);

            _languageLabel = UiFactory.CreateText("LanguageLabel", _panel.transform, string.Empty, 22,
                UiTheme.Text, TextAnchor.MiddleLeft);
            Place(_languageLabel.rectTransform, new Vector2(0.08f, 0.405f), new Vector2(0.36f, 0.475f));
            _languageDropdown = UiFactory.CreateDropdown("Language", _panel.transform,
                new List<string>(), 0);
            Place(_languageDropdown.GetComponent<RectTransform>(), new Vector2(0.42f, 0.405f),
                new Vector2(0.92f, 0.475f));

            _resolutionLabel = UiFactory.CreateText("ResolutionLabel", _panel.transform, string.Empty, 22,
                UiTheme.Text, TextAnchor.MiddleLeft);
            Place(_resolutionLabel.rectTransform, new Vector2(0.08f, 0.31f), new Vector2(0.36f, 0.38f));
            _resolutionDropdown = UiFactory.CreateDropdown("Resolution", _panel.transform,
                new List<string>(), 0);
            Place(_resolutionDropdown.GetComponent<RectTransform>(), new Vector2(0.42f, 0.31f),
                new Vector2(0.92f, 0.38f));

            _fullscreenToggle = UiFactory.CreateToggle("Fullscreen", _panel.transform, string.Empty, true);
            Place(_fullscreenToggle.GetComponent<RectTransform>(), new Vector2(0.08f, 0.215f),
                new Vector2(0.92f, 0.285f));

            _feedback = UiFactory.CreateText("Feedback", _panel.transform, string.Empty, 20, UiTheme.Accent);
            Place(_feedback.rectTransform, new Vector2(0.08f, 0.135f), new Vector2(0.92f, 0.195f));

            Button restoreButton = UiFactory.CreateButton("RestoreDefaults", _panel.transform, string.Empty);
            Place(restoreButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.035f),
                new Vector2(0.32f, 0.115f));
            restoreButton.onClick.AddListener(() => _ = RestoreDefaultsAsync());

            Button cancelButton = UiFactory.CreateButton("Cancel", _panel.transform, string.Empty);
            Place(cancelButton.GetComponent<RectTransform>(), new Vector2(0.37f, 0.035f),
                new Vector2(0.62f, 0.115f));
            cancelButton.onClick.AddListener(Cancel);

            Button applyButton = UiFactory.CreateButton("Apply", _panel.transform, string.Empty);
            Place(applyButton.GetComponent<RectTransform>(), new Vector2(0.67f, 0.035f),
                new Vector2(0.92f, 0.115f));
            applyButton.onClick.AddListener(() => _ = ApplyAsync());

            ConfigureDropdownOptions();

            _masterSlider.onValueChanged.AddListener(value => UpdateVolumeLabel(_masterLabel,
                UiTextKeys.MasterVolume, value));
            _musicSlider.onValueChanged.AddListener(value => UpdateVolumeLabel(_musicLabel,
                UiTextKeys.MusicVolume, value));
            _sfxSlider.onValueChanged.AddListener(value => UpdateVolumeLabel(_sfxLabel,
                UiTextKeys.SfxVolume, value));
        }

        /// <summary>按当前运行环境重建语言与分辨率选项，避免使用导出时的设备数据。</summary>
        private void ConfigureDropdownOptions()
        {
            UiFactory.ConfigureDropdownTemplate(_languageDropdown);
            UiFactory.ConfigureDropdownTemplate(_resolutionDropdown);

            _languageDropdown.ClearOptions();
            _languageDropdown.AddOptions(new List<string> { "zh-CN", "en-US" });
            _languageDropdown.RefreshShownValue();

            _resolutionOptions.Clear();
            List<string> resolutionLabels = BuildResolutionLabels();
            _resolutionDropdown.ClearOptions();
            _resolutionDropdown.AddOptions(resolutionLabels);
            _resolutionDropdown.RefreshShownValue();
        }

        /// <summary>在编辑器导出临时预制体时创建默认控件树。</summary>
        public void BuildPreview()
        {
            if (_panel == null)
                BuildView();
        }

        /// <summary>绑定画师预制体中的设置控件。</summary>
        /// <returns>预制体包含完整必需节点时返回 true。</returns>
        private bool TryBindConfiguredView()
        {
            _panel = transform.Find("SettingsPanel")?.gameObject;
            if (_panel == null)
                return false;

            var configured = GetComponent<SettingsUiBindings>();
            if (configured != null && configured.IsComplete)
            {
                _panel = configured.Panel;
                _title = configured.Title;
                _masterSlider = configured.MasterVolumeSlider;
                _musicSlider = configured.MusicVolumeSlider;
                _sfxSlider = configured.SfxVolumeSlider;
                _languageDropdown = configured.LanguageDropdown;
                _resolutionDropdown = configured.ResolutionDropdown;
                _fullscreenToggle = configured.FullscreenToggle;
                _feedback = configured.Feedback;
                _masterLabel = FindText(_panel.transform, UiTextKeys.MasterVolume + "Label");
                _musicLabel = FindText(_panel.transform, UiTextKeys.MusicVolume + "Label");
                _sfxLabel = FindText(_panel.transform, UiTextKeys.SfxVolume + "Label");
                _languageLabel = FindText(_panel.transform, "LanguageLabel");
                _resolutionLabel = FindText(_panel.transform, "ResolutionLabel");
                if (_masterLabel == null || _musicLabel == null || _sfxLabel == null ||
                    _languageLabel == null || _resolutionLabel == null)
                    return false;

                configured.RestoreDefaultsButton.onClick.AddListener(() => _ = RestoreDefaultsAsync());
                configured.CancelButton.onClick.AddListener(Cancel);
                configured.ApplyButton.onClick.AddListener(() => _ = ApplyAsync());
                WireVolumeEvents();
                return true;
            }

            _title = FindText(_panel.transform, "Title");
            _masterLabel = FindText(_panel.transform, UiTextKeys.MasterVolume + "Label");
            _musicLabel = FindText(_panel.transform, UiTextKeys.MusicVolume + "Label");
            _sfxLabel = FindText(_panel.transform, UiTextKeys.SfxVolume + "Label");
            _languageLabel = FindText(_panel.transform, "LanguageLabel");
            _resolutionLabel = FindText(_panel.transform, "ResolutionLabel");
            _feedback = FindText(_panel.transform, "Feedback");
            _masterSlider = FindSlider(_panel.transform, UiTextKeys.MasterVolume);
            _musicSlider = FindSlider(_panel.transform, UiTextKeys.MusicVolume);
            _sfxSlider = FindSlider(_panel.transform, UiTextKeys.SfxVolume);
            _languageDropdown = FindDropdown(_panel.transform, "Language");
            _resolutionDropdown = FindDropdown(_panel.transform, "Resolution");
            _fullscreenToggle = FindToggle(_panel.transform, "Fullscreen");
            Button restoreButton = FindButton(_panel.transform, "RestoreDefaults");
            Button cancelButton = FindButton(_panel.transform, "Cancel");
            Button applyButton = FindButton(_panel.transform, "Apply");
            if (_title == null || _masterLabel == null || _musicLabel == null || _sfxLabel == null ||
                _languageLabel == null || _resolutionLabel == null || _feedback == null ||
                _masterSlider == null || _musicSlider == null || _sfxSlider == null ||
                _languageDropdown == null || _resolutionDropdown == null || _fullscreenToggle == null ||
                restoreButton == null || cancelButton == null || applyButton == null)
                return false;

            restoreButton.onClick.AddListener(() => _ = RestoreDefaultsAsync());
            cancelButton.onClick.AddListener(Cancel);
            applyButton.onClick.AddListener(() => _ = ApplyAsync());
            WireVolumeEvents();
            return true;
        }

        /// <summary>连接三条音量滑条的草稿显示事件。</summary>
        private void WireVolumeEvents()
        {
            _masterSlider.onValueChanged.AddListener(value => UpdateVolumeLabel(_masterLabel,
                UiTextKeys.MasterVolume, value));
            _musicSlider.onValueChanged.AddListener(value => UpdateVolumeLabel(_musicLabel,
                UiTextKeys.MusicVolume, value));
            _sfxSlider.onValueChanged.AddListener(value => UpdateVolumeLabel(_sfxLabel,
                UiTextKeys.SfxVolume, value));
        }

        /// <summary>按名称查找文本控件。</summary>
        /// <param name="parent">查找根节点。</param><param name="name">节点名称。</param>
        /// <returns>找到的文本；否则为 null。</returns>
        private static Text FindText(Transform parent, string name) =>
            FindObject(parent, name)?.GetComponent<Text>();

        /// <summary>按名称查找滑块控件。</summary>
        /// <param name="parent">查找根节点。</param><param name="name">节点名称。</param>
        /// <returns>找到的滑块；否则为 null。</returns>
        private static Slider FindSlider(Transform parent, string name) =>
            FindObject(parent, name)?.GetComponent<Slider>();

        /// <summary>按名称查找下拉控件。</summary>
        /// <param name="parent">查找根节点。</param><param name="name">节点名称。</param>
        /// <returns>找到的下拉框；否则为 null。</returns>
        private static Dropdown FindDropdown(Transform parent, string name) =>
            FindObject(parent, name)?.GetComponent<Dropdown>();

        /// <summary>按名称查找复选框控件。</summary>
        /// <param name="parent">查找根节点。</param><param name="name">节点名称。</param>
        /// <returns>找到的复选框；否则为 null。</returns>
        private static Toggle FindToggle(Transform parent, string name) =>
            FindObject(parent, name)?.GetComponent<Toggle>();

        /// <summary>按名称查找按钮控件。</summary>
        /// <param name="parent">查找根节点。</param><param name="name">节点名称。</param>
        /// <returns>找到的按钮；否则为 null。</returns>
        private static Button FindButton(Transform parent, string name) =>
            FindObject(parent, name)?.GetComponent<Button>();

        /// <summary>按名称递归查找节点。</summary>
        /// <param name="parent">查找根节点。</param><param name="name">节点名称。</param>
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

        /// <summary>以当前快照刷新控件。</summary>
        /// <param name="snapshot">设置快照。</param>
        private void Render(SettingsSnapshot snapshot)
        {
            _title.text = Text(UiTextKeys.SettingsTitle);
            _masterSlider.value = snapshot.MasterVolume;
            _musicSlider.value = snapshot.MusicVolume;
            _sfxSlider.value = snapshot.SfxVolume;
            _languageDropdown.value = snapshot.LanguageCode.Equals("en-US",
                StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _resolutionDropdown.value = FindResolutionIndex(snapshot.ResolutionWidth,
                snapshot.ResolutionHeight);
            _fullscreenToggle.isOn = snapshot.Fullscreen;
            _languageLabel.text = Text(UiTextKeys.Language);
            _resolutionLabel.text = Text(UiTextKeys.Resolution);
            _fullscreenToggle.GetComponentInChildren<Text>().text = Text(UiTextKeys.Fullscreen);
            SetButtonLabel("RestoreDefaults", Text(UiTextKeys.RestoreDefaults));
            SetButtonLabel("Cancel", Text(UiTextKeys.Cancel));
            SetButtonLabel("Apply", Text(UiTextKeys.Apply));
            UpdateVolumeLabel(_masterLabel, UiTextKeys.MasterVolume, _masterSlider.value);
            UpdateVolumeLabel(_musicLabel, UiTextKeys.MusicVolume, _musicSlider.value);
            UpdateVolumeLabel(_sfxLabel, UiTextKeys.SfxVolume, _sfxSlider.value);
        }

        /// <summary>响应 Locale 变化并立即刷新当前弹窗文字。</summary>
        /// <param name="localeCode">新 Locale。</param>
        private void OnLocaleChanged(string localeCode)
        {
            if (_settingsService != null)
                Render(_settingsService.Current);
        }

        /// <summary>应用草稿并在保存成功后关闭弹窗；弹窗销毁时取消保存请求。</summary>
        private async Task ApplyAsync()
        {
            if (_settingsService == null || _lifetime == null)
                return;

            try
            {
                SettingsDraft draft = BuildDraft();
                Result result = await _settingsService.ApplyAsync(draft, _lifetime.Token);
                if (this == null || _feedback == null)
                    return;

                if (!result.IsSuccess)
                {
                    _feedback.text = result.Message;
                    return;
                }

                _owner.ShowFeedback(Text(UiTextKeys.FeedbackSaved));
                _owner.CloseSettings();
            }
            catch (OperationCanceledException)
            {
                // 弹窗销毁时取消保存属于正常生命周期行为。
            }
            catch (Exception exception)
            {
                if (this != null && _feedback != null)
                    _feedback.text = exception.Message;
            }
        }

        /// <summary>恢复默认设置并刷新控件；弹窗销毁时取消恢复请求。</summary>
        private async Task RestoreDefaultsAsync()
        {
            if (_settingsService == null || _lifetime == null)
                return;

            try
            {
                Result result = await _settingsService.RestoreDefaultsAsync(_lifetime.Token);
                if (this == null || _feedback == null)
                    return;

                if (!result.IsSuccess)
                {
                    _feedback.text = result.Message;
                    return;
                }

                Render(_settingsService.Current);
            }
            catch (OperationCanceledException)
            {
                // 弹窗销毁时取消恢复默认属于正常生命周期行为。
            }
            catch (Exception exception)
            {
                if (this != null && _feedback != null)
                    _feedback.text = exception.Message;
            }
        }

        /// <summary>取消编辑并关闭弹窗。</summary>
        private void Cancel()
        {
            _owner?.CloseSettings();
        }

        /// <summary>构建当前控件值对应的设置草稿。</summary>
        /// <returns>待应用草稿。</returns>
        private SettingsDraft BuildDraft()
        {
            ResolutionOption resolution = _resolutionOptions[_resolutionDropdown.value];
            return new SettingsDraft(_settingsService.Current)
            {
                LanguageCode = _languageDropdown.value == 1 ? "en-US" : "zh-CN",
                MasterVolume = _masterSlider.value,
                MusicVolume = _musicSlider.value,
                SfxVolume = _sfxSlider.value,
                Fullscreen = _fullscreenToggle.isOn,
                ResolutionWidth = resolution.Width,
                ResolutionHeight = resolution.Height
            };
        }

        /// <summary>添加音量行并返回滑块。</summary>
        /// <param name="key">标签键。</param>
        /// <param name="y">行的中心高度。</param>
        /// <param name="label">输出标签。</param>
        /// <returns>创建的滑块。</returns>
        private Slider AddVolumeRow(string key, float y, out Text label)
        {
            label = UiFactory.CreateText(key + "Label", _panel.transform, string.Empty, 22,
                UiTheme.Text, TextAnchor.MiddleLeft);
            Place(label.rectTransform, new Vector2(0.08f, y - 0.035f), new Vector2(0.35f, y + 0.035f));
            var slider = UiFactory.CreateSlider(key, _panel.transform, 1f);
            Place(slider.GetComponent<RectTransform>(), new Vector2(0.4f, y - 0.025f),
                new Vector2(0.92f, y + 0.025f));
            return slider;
        }

        /// <summary>生成可选分辨率列表并确保当前分辨率存在。</summary>
        /// <returns>分辨率显示文本列表。</returns>
        private List<string> BuildResolutionLabels()
        {
            Resolution[] resolutions = Screen.resolutions;
            if (resolutions == null || resolutions.Length == 0)
            {
                _resolutionOptions.Add(new ResolutionOption(Screen.width, Screen.height));
            }
            else
            {
                foreach (Resolution resolution in resolutions)
                {
                    var option = new ResolutionOption(resolution.width, resolution.height);
                    if (!_resolutionOptions.Contains(option))
                        _resolutionOptions.Add(option);
                }
            }

            if (_resolutionOptions.Count == 0)
                _resolutionOptions.Add(new ResolutionOption(1280, 720));

            var current = new ResolutionOption(Screen.width, Screen.height);
            if (!_resolutionOptions.Contains(current))
                _resolutionOptions.Insert(0, current);

            var labels = new List<string>();
            foreach (ResolutionOption option in _resolutionOptions)
                labels.Add(option.Width + " × " + option.Height);
            return labels;
        }

        /// <summary>查找快照对应的分辨率下拉索引。</summary>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <returns>索引；找不到时为 0。</returns>
        private int FindResolutionIndex(int width, int height)
        {
            for (var i = 0; i < _resolutionOptions.Count; i++)
            {
                if (_resolutionOptions[i].Width == width && _resolutionOptions[i].Height == height)
                    return i;
            }

            return 0;
        }

        /// <summary>读取本地化文字。</summary>
        /// <param name="key">稳定键字符串。</param>
        /// <returns>当前 Locale 文本。</returns>
        private string Text(string key)
        {
            return _localizationService.Get(new LocalizationKey(key));
        }

        /// <summary>刷新音量标签。</summary>
        /// <param name="label">标签。</param>
        /// <param name="key">本地化键。</param>
        /// <param name="value">音量值。</param>
        private void UpdateVolumeLabel(Text label, string key, float value)
        {
            if (label != null)
                label.text = Text(key) + "  " + Mathf.RoundToInt(value * 100f) + "%";
        }

        /// <summary>设置按钮内部文本。</summary>
        /// <param name="buttonName">按钮名称。</param>
        /// <param name="label">文本。</param>
        private void SetButtonLabel(string buttonName, string label)
        {
            Transform button = _panel.transform.Find(buttonName);
            Text text = button == null ? null : button.GetComponentInChildren<Text>();
            if (text != null)
                text.text = label;
        }

        /// <summary>设置控件在弹窗中的归一化矩形。</summary>
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

        /// <summary>可显示的分辨率值对象。</summary>
        private readonly struct ResolutionOption : IEquatable<ResolutionOption>
        {
            /// <summary>宽度。</summary>
            public int Width { get; }
            /// <summary>高度。</summary>
            public int Height { get; }

            /// <summary>创建分辨率值。</summary>
            /// <param name="width">宽度。</param>
            /// <param name="height">高度。</param>
            public ResolutionOption(int width, int height)
            {
                Width = width;
                Height = height;
            }

            /// <summary>判断两个分辨率是否相同。</summary>
            /// <param name="other">另一个分辨率。</param>
            /// <returns>相同返回 true。</returns>
            public bool Equals(ResolutionOption other) => Width == other.Width && Height == other.Height;

            /// <summary>判断对象是否为相同分辨率。</summary>
            /// <param name="obj">待比较对象。</param>
            /// <returns>相同返回 true。</returns>
            public override bool Equals(object obj) => obj is ResolutionOption other && Equals(other);

            /// <summary>返回分辨率哈希码。</summary>
            /// <returns>哈希码。</returns>
            public override int GetHashCode() => (Width * 397) ^ Height;
        }
    }
}
