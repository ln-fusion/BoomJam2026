using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>剧情面板预制体必须提供的控件引用。</summary>
    public sealed class StoryUiBindings : MonoBehaviour
    {
        /// <summary>背景图像。</summary>
        public Image Background;

        /// <summary>CG 覆盖层图像。</summary>
        public Image CgLayer;

        /// <summary>屏幕效果覆盖层图像。</summary>
        public Image EffectLayer;

        /// <summary>左侧立绘。</summary>
        public Image PortraitLeft;

        /// <summary>中间立绘。</summary>
        public Image PortraitCenter;

        /// <summary>右侧立绘。</summary>
        public Image PortraitRight;

        /// <summary>说话人文本。</summary>
        public Text Speaker;

        /// <summary>正文文本。</summary>
        public Text Body;

        /// <summary>选项容器。</summary>
        public GameObject ChoicesRoot;

        /// <summary>继续按钮。</summary>
        public Button ContinueButton;

        /// <summary>跳过按钮。</summary>
        public Button SkipButton;

        /// <summary>历史按钮。</summary>
        public Button HistoryButton;

        /// <summary>历史覆盖层。</summary>
        public GameObject HistoryView;

        /// <summary>历史文本。</summary>
        public Text HistoryText;

        /// <summary>检查剧情面板是否包含全部必需控件。</summary>
        public bool IsComplete =>
            Background != null
            && CgLayer != null
            && EffectLayer != null
            && PortraitLeft != null
            && PortraitCenter != null
            && PortraitRight != null
            && Speaker != null
            && Body != null
            && ChoicesRoot != null
            && ContinueButton != null
            && SkipButton != null
            && HistoryButton != null
            && HistoryView != null
            && HistoryText != null;
    }
}
