using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>设置预制体必须提供的控件引用。</summary>
    public sealed class SettingsUiBindings : MonoBehaviour
    {
        /// <summary>设置面板根节点。</summary>
        public GameObject Panel;
        /// <summary>标题文本。</summary>
        public Text Title;
        /// <summary>主音量滑条。</summary>
        public Slider MasterVolumeSlider;
        /// <summary>音乐音量滑条。</summary>
        public Slider MusicVolumeSlider;
        /// <summary>音效音量滑条。</summary>
        public Slider SfxVolumeSlider;
        /// <summary>语言下拉框。</summary>
        public Dropdown LanguageDropdown;
        /// <summary>分辨率下拉框。</summary>
        public Dropdown ResolutionDropdown;
        /// <summary>全屏开关。</summary>
        public Toggle FullscreenToggle;
        /// <summary>反馈文本。</summary>
        public Text Feedback;
        /// <summary>恢复默认按钮。</summary>
        public Button RestoreDefaultsButton;
        /// <summary>取消按钮。</summary>
        public Button CancelButton;
        /// <summary>应用按钮。</summary>
        public Button ApplyButton;

        /// <summary>三条音量滑条是否均使用运行时约定的 0～1 范围。</summary>
        public bool VolumeSlidersHaveUnitRange => IsUnitRange(MasterVolumeSlider) &&
            IsUnitRange(MusicVolumeSlider) && IsUnitRange(SfxVolumeSlider);

        /// <summary>检查设置界面是否包含设置服务要求的全部控件。</summary>
        public bool IsComplete => Panel != null && Title != null &&
            MasterVolumeSlider != null && MusicVolumeSlider != null && SfxVolumeSlider != null &&
            LanguageDropdown != null && ResolutionDropdown != null && FullscreenToggle != null &&
            Feedback != null && RestoreDefaultsButton != null && CancelButton != null &&
            ApplyButton != null && VolumeSlidersHaveUnitRange;

        /// <summary>检查滑条是否为 0～1 的规范化范围。</summary>
        /// <param name="slider">待检查滑条。</param>
        /// <returns>范围符合约定时返回 true。</returns>
        private static bool IsUnitRange(Slider slider)
        {
            return slider != null && Mathf.Approximately(slider.minValue, 0f) &&
                Mathf.Approximately(slider.maxValue, 1f);
        }
    }
}
