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
