using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>开始菜单预制体必须提供的控件引用。</summary>
    public sealed class StartMenuUiBindings : MonoBehaviour
    {
        /// <summary>界面 Canvas；为空时使用预制体根下的 Canvas。</summary>
        public Canvas Canvas;
        /// <summary>游戏标题文本。</summary>
        public Text Title;
        /// <summary>反馈文本。</summary>
        public Text Feedback;
        /// <summary>开始或继续按钮。</summary>
        public Button StartButton;
        /// <summary>打开设置按钮。</summary>
        public Button SettingsButton;
        /// <summary>退出按钮。</summary>
        public Button QuitButton;
        /// <summary>昵称输入弹窗根节点。</summary>
        public GameObject NicknamePanel;
        /// <summary>昵称提示文本。</summary>
        public Text NicknamePrompt;
        /// <summary>昵称输入框。</summary>
        public InputField NicknameInput;
        /// <summary>昵称校验错误文本。</summary>
        public Text NicknameError;
        /// <summary>昵称确认按钮。</summary>
        public Button NicknameConfirmButton;
        /// <summary>昵称取消按钮。</summary>
        public Button NicknameCancelButton;

        /// <summary>检查开始菜单是否已绑定运行时所需的全部控件。</summary>
        public bool IsComplete => Canvas != null && Title != null && Feedback != null &&
            StartButton != null && SettingsButton != null && QuitButton != null &&
            NicknamePanel != null && NicknamePrompt != null && NicknameInput != null &&
            NicknameError != null && NicknameConfirmButton != null && NicknameCancelButton != null;
    }
}
