using System;
using System.Collections.Generic;
using Game.Contracts.Story;
using Game.Foundation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;

namespace Game.Presentation
{
    /// <summary>可由场景直接挂载的 C09 基础对白与选项白盒面板。</summary>
    public sealed class StoryDialoguePanel : MonoBehaviour, IStoryPresentationPort, IPointerClickHandler
    {
        private Text _speaker;
        private Text _body;
        private Button _continue;
        private GameObject _choicesRoot;
        private Action _continueAction;
        private Action<ChoiceId> _choiceAction;
        private Action _skipAction;
        private Button _skipButton;
        private bool _skipPending;
        private bool _inputBlocked;
        private Coroutine _typingCoroutine;
        private string _fullText = string.Empty;
        private readonly List<StoryHistoryEntry> _history = new List<StoryHistoryEntry>();
        private Text _historyText;
        private GameObject _historyView;

        /// <summary>在场景中创建默认 uGUI 控件。</summary>
        public void BuildPreview()
        {
            if (_speaker != null)
                return;
            Canvas canvas = UiFactory.CreateCanvas("StoryCanvas", transform, 10);
            Image panel = UiFactory.CreatePanel("DialoguePanel", canvas.transform, UiTheme.Panel);
            UiFactory.Stretch(panel.rectTransform, new Vector2(0.08f, 0.06f));
            GameObject clickSurface = new GameObject("ClickSurface", typeof(RectTransform),
                typeof(Image), typeof(Button));
            clickSurface.transform.SetParent(panel.transform, false);
            var clickRect = clickSurface.GetComponent<RectTransform>();
            UiFactory.Stretch(clickRect, Vector2.zero);
            var clickImage = clickSurface.GetComponent<Image>();
            clickImage.color = Color.clear;
            clickSurface.GetComponent<Button>().targetGraphic = clickImage;
            clickSurface.GetComponent<Button>().onClick.AddListener(ContinueClicked);
            clickSurface.transform.SetAsFirstSibling();
            _speaker = UiFactory.CreateText("Speaker", panel.transform, string.Empty, 24, UiTheme.Accent,
                TextAnchor.UpperLeft);
            _speaker.raycastTarget = false;
            _speaker.rectTransform.anchorMin = new Vector2(0.04f, 0.68f);
            _speaker.rectTransform.anchorMax = new Vector2(0.96f, 0.94f);
            _body = UiFactory.CreateText("Body", panel.transform, string.Empty, 26, UiTheme.Text,
                TextAnchor.UpperLeft);
            _body.raycastTarget = false;
            _body.rectTransform.anchorMin = new Vector2(0.04f, 0.22f);
            _body.rectTransform.anchorMax = new Vector2(0.96f, 0.68f);
            _continue = UiFactory.CreateButton("Continue", panel.transform, "Continue");
            RectTransform continueRect = _continue.GetComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(0.72f, 0.04f);
            continueRect.anchorMax = new Vector2(0.96f, 0.18f);
            _continue.onClick.AddListener(ContinueClicked);
            _skipButton = UiFactory.CreateButton("Skip", panel.transform, "Skip");
            RectTransform skipRect = _skipButton.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.48f, 0.04f);
            skipRect.anchorMax = new Vector2(0.68f, 0.18f);
            skipRect.offsetMin = skipRect.offsetMax = Vector2.zero;
            _skipButton.onClick.AddListener(RequestSkip);
            Button historyButton = UiFactory.CreateButton("History", panel.transform, "History");
            RectTransform historyRect = historyButton.GetComponent<RectTransform>();
            historyRect.anchorMin = new Vector2(0.04f, 0.04f);
            historyRect.anchorMax = new Vector2(0.2f, 0.18f);
            historyButton.onClick.AddListener(ToggleHistory);
            _choicesRoot = new GameObject("Choices", typeof(RectTransform));
            _choicesRoot.transform.SetParent(panel.transform, false);
            var root = (RectTransform)_choicesRoot.transform;
            root.anchorMin = new Vector2(0.04f, 0.04f);
            root.anchorMax = new Vector2(0.68f, 0.2f);
            root.offsetMin = root.offsetMax = Vector2.zero;
            _historyView = UiFactory.CreatePanel("HistoryView", panel.transform,
                new Color(0.02f, 0.03f, 0.06f, 0.98f)).gameObject;
            var historyRectView = _historyView.GetComponent<RectTransform>();
            UiFactory.Stretch(historyRectView, new Vector2(0.04f, 0.2f));
            _historyText = UiFactory.CreateText("HistoryText", _historyView.transform,
                string.Empty, 22, UiTheme.Text, TextAnchor.UpperLeft);
            UiFactory.Stretch(_historyText.rectTransform, new Vector2(16f, 16f));
            _historyView.SetActive(false);
        }

        /// <summary>空格键触发当前对白继续。</summary>
        private void Update()
        {
            if (_inputBlocked)
                return;
            if (_continueAction != null && Input.GetKeyDown(KeyCode.Space))
                ContinueClicked();
        }

        /// <summary>点击对白区域时推进当前对白。</summary>
        /// <param name="eventData">Unity UI 指针事件。</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_inputBlocked) return;
            if (_continueAction != null)
                ContinueClicked();
        }

        /// <inheritdoc/>
        public void ShowDialogue(StoryDialogueView dialogue, Action onContinue)
        {
            BuildPreview();
            ClearChoices();
            _speaker.text = dialogue == null ? string.Empty : dialogue.SpeakerKey;
            _fullText = dialogue == null ? string.Empty : dialogue.TextKey;
            StartTyping(_fullText);
            _continueAction = onContinue;
            _continue.gameObject.SetActive(true);
        }

        /// <summary>设置跳过按钮回调。</summary>
        /// <param name="onSkip">跳过回调。</param>
        public void SetSkipAction(Action onSkip) => _skipAction = onSkip;

        /// <summary>设置剧情输入是否被设置弹窗阻塞。</summary>
        /// <param name="blocked">为 true 时忽略点击、空格和跳过操作。</param>
        public void SetInputBlocked(bool blocked)
        {
            _inputBlocked = blocked;
            if (_skipButton != null) _skipButton.interactable = !blocked;
            if (_continue != null) _continue.interactable = !blocked;
        }

        /// <inheritdoc/>
        public void ShowChoices(IReadOnlyList<StoryChoiceView> choices, Action<ChoiceId> onChoice)
        {
            BuildPreview();
            ClearChoices();
            _continueAction = null;
            _skipAction = null;
            _skipPending = false;
            _continue.gameObject.SetActive(false);
            _choiceAction = onChoice;
            if (choices == null)
                return;
            foreach (StoryChoiceView choice in choices)
            {
                if (choice == null) continue;
                Button button = UiFactory.CreateButton("Choice_" + choice.ChoiceId.Value,
                    _choicesRoot.transform, choice.TextKey);
                button.onClick.AddListener(() => _choiceAction?.Invoke(choice.ChoiceId));
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 42f);
            }
        }

        /// <summary>首次点击显示跳过确认，第二次点击执行跳过。</summary>
        private void RequestSkip()
        {
            if (_inputBlocked) return;
            if (!_skipPending)
            {
                _skipPending = true;
                if (_skipButton != null) SetButtonLabel(_skipButton, "Confirm Skip");
                return;
            }
            _skipPending = false;
            if (_skipButton != null) SetButtonLabel(_skipButton, "Skip");
            _skipAction?.Invoke();
        }

        /// <summary>更新按钮子文本。</summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="label">新文本。</param>
        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button == null ? null : button.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            if (_speaker != null) _speaker.text = string.Empty;
            if (_body != null) _body.text = string.Empty;
            _continueAction = null;
            StopTyping();
            _history.Clear();
            if (_historyView != null) _historyView.SetActive(false);
            ClearChoices();
        }

        /// <summary>追加一条实际经过的剧情历史。</summary>
        /// <param name="entry">历史记录。</param>
        public void AppendHistory(StoryHistoryEntry entry)
        {
            if (entry == null) return;
            _history.Add(entry);
            RefreshHistory();
        }

        /// <summary>继续按钮点击处理。</summary>
        private void ContinueClicked()
        {
            if (_typingCoroutine != null)
            {
                StopTyping();
                _body.text = _fullText;
                return;
            }
            Action callback = _continueAction;
            _continueAction = null;
            callback?.Invoke();
        }

        /// <summary>以固定字符间隔显示正文，首次点击只补全文本。</summary>
        /// <param name="text">待显示正文。</param>
        private void StartTyping(string text)
        {
            StopTyping();
            _body.text = string.Empty;
            _typingCoroutine = StartCoroutine(TypeText(text ?? string.Empty));
        }

        /// <summary>执行基础打字机协程。</summary>
        /// <param name="text">待显示正文。</param>
        private System.Collections.IEnumerator TypeText(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                _body.text = text.Substring(0, i + 1);
                yield return new WaitForSeconds(0.025f);
            }
            _typingCoroutine = null;
        }

        /// <summary>停止当前打字机协程。</summary>
        private void StopTyping()
        {
            if (_typingCoroutine == null) return;
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        /// <summary>切换历史记录覆盖层。</summary>
        private void ToggleHistory()
        {
            if (_historyView == null) return;
            _historyView.SetActive(!_historyView.activeSelf);
            RefreshHistory();
        }

        /// <summary>刷新历史记录文本。</summary>
        private void RefreshHistory()
        {
            if (_historyText == null) return;
            var builder = new StringBuilder();
            foreach (StoryHistoryEntry entry in _history)
            {
                builder.Append(entry.SpeakerKey).Append(' ').Append(entry.TextKey);
                if (!string.IsNullOrEmpty(entry.ChoiceTextKey))
                    builder.Append(" [").Append(entry.ChoiceTextKey).Append(']');
                builder.AppendLine();
            }
            _historyText.text = builder.ToString();
        }

        /// <summary>移除当前选项按钮。</summary>
        private void ClearChoices()
        {
            if (_choicesRoot == null) return;
            for (int i = _choicesRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_choicesRoot.transform.GetChild(i).gameObject);
            _choiceAction = null;
        }
    }
}
