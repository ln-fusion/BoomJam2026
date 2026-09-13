using Game.Contracts.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>C11 关卡资料卡白盒视图，展示状态、成绩和操作占位。</summary>
    public sealed class LevelCardPanel : MonoBehaviour
    {
        private Text _text;
        private Button _start;

        /// <summary>创建资料卡控件。</summary>
        public void BuildPreview()
        {
            if (_text != null) return;
            Image panel = UiFactory.CreatePanel("LevelCard", transform, UiTheme.Panel);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.68f, 0.08f);
            rect.anchorMax = new Vector2(0.98f, 0.92f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _text = UiFactory.CreateText("Details", panel.transform, "Select a level", 20,
                UiTheme.Text, TextAnchor.UpperLeft);
            UiFactory.Stretch(_text.rectTransform, new Vector2(16f, 64f));
            _start = UiFactory.CreateButton("Start", panel.transform, "Start");
            RectTransform startRect = _start.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.1f, 0.05f);
            startRect.anchorMax = new Vector2(0.9f, 0.18f);
            startRect.offsetMin = startRect.offsetMax = Vector2.zero;
            _start.interactable = false;
            CreateReplayButton(panel.transform, "PreludeReplay", "关前剧情复播", 0.36f);
            CreateReplayButton(panel.transform, "PostludeReplay", "关后剧情复播", 0.21f);
            _text.rectTransform.anchorMin = new Vector2(0.08f, 0.52f);
            _text.rectTransform.anchorMax = new Vector2(0.92f, 0.94f);
            _text.rectTransform.offsetMin = _text.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>为代码生成的白盒资料卡创建复播按钮，权限与事件由主界面壳绑定。</summary>
        /// <param name="parent">资料卡父节点。</param>
        /// <param name="name">绑定节点名。</param>
        /// <param name="label">白盒按钮文字。</param>
        /// <param name="bottom">下边缘的归一化位置。</param>
        private static void CreateReplayButton(Transform parent, string name, string label, float bottom)
        {
            var button = UiFactory.CreateButton(name, parent, label);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, bottom);
            rect.anchorMax = new Vector2(0.9f, bottom + 0.12f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            button.interactable = false;
        }

        /// <summary>显示关卡资料卡。</summary>
        /// <param name="card">关卡卡片模型。</param>
        public void Show(LevelCardViewModel card)
        {
            BuildPreview();
            if (card == null)
            {
                _text.text = "Select a level";
                _start.interactable = false;
                return;
            }
            string score = card.BestScore == null ? "-" : card.BestScore.ElapsedTicks + " ticks";
            _text.text = card.Node.DisplayNameKey + "\nState: " + card.Node.State +
                "\nBest: " + score;
            _start.interactable = card.Node.IsInteractable;
        }
    }
}
