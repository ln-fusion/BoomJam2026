using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.Contracts;
using Game.Contracts.Content;
using Game.Contracts.Story;
using Game.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>可由场景直接挂载的 C09 基础对白与选项白盒面板。</summary>
    public sealed class StoryDialoguePanel : MonoBehaviour, IStoryPresentationPort, IPointerClickHandler
    {
        private Text _speaker;
        private Text _body;
        private Image _portrait;
        private Image _background;
        private Image _effect;
        private Button _continue;
        private GameObject _choicesRoot;
        private Action _continueAction;
        private Action<ChoiceId> _choiceAction;
        private Action _skipAction;
        private Button _skipButton;
        private bool _skipPending;
        private bool _inputBlocked;
        private Coroutine _typingCoroutine;
        private Coroutine _waitCoroutine;
        private Action _waitSkipAction;
        private string _fullText = string.Empty;
        private readonly List<StoryHistoryEntry> _history = new List<StoryHistoryEntry>();
        private Text _historyText;
        private GameObject _historyView;
        private ILocalizationService _localization;
        private string _currentCharacterId;

        /// <summary>在场景中创建默认 uGUI 控件。</summary>
        public void BuildPreview()
        {
            if (_speaker != null)
                return;
            Canvas canvas = UiFactory.CreateCanvas("StoryCanvas", transform, 10);
            _background = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _background.transform.SetParent(canvas.transform, false);
            UiFactory.Stretch(_background.rectTransform, Vector2.zero);
            _background.color = new Color(0.06f, 0.08f, 0.12f, 1f);
            _background.raycastTarget = false;
            _effect = new GameObject("Effect", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _effect.transform.SetParent(canvas.transform, false);
            UiFactory.Stretch(_effect.rectTransform, Vector2.zero);
            _effect.color = Color.clear;
            _effect.raycastTarget = false;
            Image panel = UiFactory.CreatePanel("DialoguePanel", canvas.transform, UiTheme.Panel);
            UiFactory.Stretch(panel.rectTransform, new Vector2(0.08f, 0.06f));
            GameObject clickSurface = new GameObject(
                "ClickSurface",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            clickSurface.transform.SetParent(panel.transform, false);
            var clickRect = clickSurface.GetComponent<RectTransform>();
            UiFactory.Stretch(clickRect, Vector2.zero);
            var clickImage = clickSurface.GetComponent<Image>();
            clickImage.color = Color.clear;
            clickSurface.GetComponent<Button>().targetGraphic = clickImage;
            clickSurface.GetComponent<Button>().onClick.AddListener(ContinueClicked);
            clickSurface.transform.SetAsFirstSibling();
            _speaker = UiFactory.CreateText(
                "Speaker",
                panel.transform,
                string.Empty,
                24,
                UiTheme.Accent,
                TextAnchor.UpperLeft
            );
            _speaker.raycastTarget = false;
            _speaker.rectTransform.anchorMin = new Vector2(0.04f, 0.68f);
            _speaker.rectTransform.anchorMax = new Vector2(0.96f, 0.94f);
            _portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _portrait.transform.SetParent(panel.transform, false);
            _portrait.rectTransform.anchorMin = new Vector2(0.04f, 0.22f);
            _portrait.rectTransform.anchorMax = new Vector2(0.26f, 0.68f);
            _portrait.rectTransform.offsetMin = _portrait.rectTransform.offsetMax = Vector2.zero;
            _portrait.preserveAspect = true;
            _portrait.gameObject.SetActive(false);
            _body = UiFactory.CreateText("Body", panel.transform, string.Empty, 26, UiTheme.Text, TextAnchor.UpperLeft);
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
            // 选项区避开 Skip/History 按钮带, 放在正文区域以便垂直堆叠多个选项。
            root.anchorMin = new Vector2(0.28f, 0.24f);
            root.anchorMax = new Vector2(0.96f, 0.62f);
            root.offsetMin = root.offsetMax = Vector2.zero;
            _historyView = UiFactory
                .CreatePanel("HistoryView", panel.transform, new Color(0.02f, 0.03f, 0.06f, 0.98f))
                .gameObject;
            var historyRectView = _historyView.GetComponent<RectTransform>();
            UiFactory.Stretch(historyRectView, new Vector2(0.04f, 0.2f));
            _historyText = UiFactory.CreateText(
                "HistoryText",
                _historyView.transform,
                string.Empty,
                22,
                UiTheme.Text,
                TextAnchor.UpperLeft
            );
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
            if (_inputBlocked)
                return;
            if (_continueAction != null)
                ContinueClicked();
        }

        /// <inheritdoc/>
        public void ShowDialogue(StoryDialogueView dialogue, Action onContinue)
        {
            BuildPreview();
            ClearChoices();
            _speaker.text = dialogue == null ? string.Empty : Localize(dialogue.SpeakerKey);
            _fullText = dialogue == null ? string.Empty : Localize(dialogue.TextKey);
            StartTyping(_fullText);
            _continueAction = onContinue;
            _continue.gameObject.SetActive(true);
        }

        /// <summary>显示当前对白附带指定立绘。</summary>
        /// <param name="dialogue">对白数据。</param>
        /// <param name="portrait">角色立绘；无角色或资源缺失时为 null。</param>
        /// <param name="onContinue">继续回调。</param>
        public void ShowDialogue(StoryDialogueView dialogue, Sprite portrait, Action onContinue)
        {
            ShowDialogue(dialogue, onContinue);
            if (_portrait == null)
                return;
            _portrait.sprite = portrait;
            _portrait.gameObject.SetActive(portrait != null);
            // 立绘占据左侧后正文右移，避免与立绘重叠；无立绘时恢复原位置。
            Vector2 bodyMin = _body.rectTransform.anchorMin;
            _body.rectTransform.anchorMin =
                portrait == null ? new Vector2(0.04f, bodyMin.y) : new Vector2(0.28f, bodyMin.y);
        }

        /// <summary>注入本地化服务用于说话人和正文文本解析。</summary>
        /// <param name="localization">本地化服务。</param>
        public void SetLocalization(ILocalizationService localization) => _localization = localization;

        /// <summary>应用角色立绘状态（ShowCharacter 节点）。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        /// <param name="appearanceId">形象稳定标识；可为空。</param>
        /// <param name="portrait">解析出的立绘精灵。</param>
        public void ShowCharacter(string characterId, string appearanceId, Sprite portrait)
        {
            BuildPreview();
            _currentCharacterId = characterId;
            if (_portrait == null)
                return;
            _portrait.sprite = portrait;
            _portrait.gameObject.SetActive(portrait != null);
            _portrait.name = characterId + "/" + appearanceId;
        }

        /// <summary>显示全屏 CG（ShowCg 节点占位实现：显示纯色覆盖层）。</summary>
        /// <param name="assetId">CG 资源稳定标识。</param>
        public void ShowCg(string assetId)
        {
            BuildPreview();
            // C16/C17 占位：CG 资源解析器尚未接入，用提示文本占位。
            Debug.Log("[StoryDialoguePanel] ShowCg placeholder for asset: " + assetId, this);
        }

        /// <summary>切换背景（SetBackground 节点占位实现：显示纯色背景）。</summary>
        /// <param name="backgroundId">背景资源稳定标识。</param>
        public void SetBackground(string backgroundId)
        {
            BuildPreview();
            // C17 占位：背景资源解析器尚未接入，用提示文本占位。
            Debug.Log("[StoryDialoguePanel] SetBackground placeholder for asset: " + backgroundId, this);
        }

        /// <summary>隐藏指定角色的立绘（HideCharacter 节点）。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        public void HideCharacter(string characterId)
        {
            BuildPreview();
            if (
                _portrait != null
                && !string.IsNullOrEmpty(_currentCharacterId)
                && string.Equals(_currentCharacterId, characterId, StringComparison.Ordinal)
            )
            {
                _portrait.sprite = null;
                _portrait.gameObject.SetActive(false);
                _currentCharacterId = null;
            }
        }

        /// <summary>移动角色立绘到指定位置（MoveCharacter 节点）。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        /// <param name="position">目标位置。</param>
        public void MoveCharacter(string characterId, StoryCharacterPosition position)
        {
            BuildPreview();
            if (_portrait == null || !string.Equals(_currentCharacterId, characterId, StringComparison.Ordinal))
                return;
            ApplyCharacterPosition(position);
        }

        /// <summary>播放屏幕效果（ScreenEffect 节点占位实现：纯色覆盖）。</summary>
        /// <param name="effect">屏幕效果类型。</param>
        public void PlayScreenEffect(StoryScreenEffectType effect)
        {
            BuildPreview();
            if (_effect == null)
                return;
            switch (effect)
            {
                case StoryScreenEffectType.WhiteFlash:
                    _effect.color = new Color(1f, 1f, 1f, 0.85f);
                    break;
                case StoryScreenEffectType.RedFlash:
                    _effect.color = new Color(1f, 0.1f, 0.1f, 0.85f);
                    break;
                case StoryScreenEffectType.Blackout:
                    _effect.color = new Color(0f, 0f, 0f, 0.95f);
                    break;
                case StoryScreenEffectType.Blur:
                    _effect.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    break;
                default:
                    _effect.color = Color.clear;
                    break;
            }
        }

        /// <summary>把立绘移动到指定位置。</summary>
        /// <param name="position">目标位置。</param>
        private void ApplyCharacterPosition(StoryCharacterPosition position)
        {
            Vector2 min = position switch
            {
                StoryCharacterPosition.Center => new Vector2(0.38f, 0.22f),
                StoryCharacterPosition.Right => new Vector2(0.72f, 0.22f),
                _ => new Vector2(0.04f, 0.22f),
            };
            _portrait.rectTransform.anchorMin = min;
            _portrait.rectTransform.anchorMax = min + new Vector2(0.22f, 0.46f);
        }

        /// <summary>显示可跳过的等待（Wait 节点）。</summary>
        /// <param name="seconds">等待秒数。</param>
        /// <param name="onCompleted">等待完成回调。</param>
        public void ShowWait(float seconds, Action onCompleted)
        {
            BuildPreview();
            if (seconds <= 0f)
            {
                onCompleted?.Invoke();
                return;
            }
            _continueAction = null;
            // 等待期间点击跳过按钮即结束等待，不触发整段跳过。
            _waitSkipAction = () => CompleteWait(onCompleted);
            _waitCoroutine = StartCoroutine(WaitCoroutine(seconds, onCompleted));
        }

        /// <summary>等待协程；点击跳过或倒计时结束时推进。</summary>
        /// <param name="seconds">等待秒数。</param>
        /// <param name="onCompleted">等待完成回调。</param>
        private IEnumerator WaitCoroutine(float seconds, Action onCompleted)
        {
            yield return new WaitForSeconds(seconds);
            CompleteWait(onCompleted);
        }

        /// <summary>结束等待并触发完成回调。</summary>
        /// <param name="onCompleted">等待完成回调。</param>
        private void CompleteWait(Action onCompleted)
        {
            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
            }
            _waitSkipAction = null;
            onCompleted?.Invoke();
        }

        /// <summary>把本地化键转换为当前语言文本；无服务时显示键名。</summary>
        /// <param name="key">本地化键。</param>
        /// <returns>文本或键名回退。</returns>
        private string Localize(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            return _localization == null ? key : _localization.Get(new LocalizationKey(key));
        }

        /// <summary>设置跳过按钮回调。</summary>
        /// <param name="onSkip">跳过回调。</param>
        public void SetSkipAction(Action onSkip) => _skipAction = onSkip;

        /// <summary>设置剧情输入是否被设置弹窗阻塞。</summary>
        /// <param name="blocked">为 true 时忽略点击、空格和跳过操作。</param>
        public void SetInputBlocked(bool blocked)
        {
            _inputBlocked = blocked;
            if (_skipButton != null)
                _skipButton.interactable = !blocked;
            if (_continue != null)
                _continue.interactable = !blocked;
        }

        /// <inheritdoc/>
        public void ShowChoices(IReadOnlyList<StoryChoiceView> choices, Action<ChoiceId> onChoice)
        {
            BuildPreview();
            ClearChoices();
            _continueAction = null;
            _skipPending = false;
            _continue.gameObject.SetActive(false);
            StopTyping();
            _body.text = _fullText;
            _choiceAction = onChoice;
            if (choices == null)
                return;
            // 逐项下排布：点锚定的父容器不会自动撑满宽度，必须显式给出全宽矩形,
            // 否则按钮宽度为 0 而完全不可见、不可点击。
            const float itemHeight = 48f;
            const float itemGap = 10f;
            for (int i = 0; i < choices.Count; i++)
            {
                StoryChoiceView choice = choices[i];
                if (choice == null)
                    continue;
                Button button = UiFactory.CreateButton(
                    "Choice_" + choice.ChoiceId.Value,
                    _choicesRoot.transform,
                    Localize(choice.TextKey)
                );
                button.onClick.AddListener(() => _choiceAction?.Invoke(choice.ChoiceId));
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, itemHeight);
                rect.anchoredPosition = new Vector2(0f, -i * (itemHeight + itemGap));
            }
        }

        /// <summary>首次点击显示跳过确认，第二次点击执行跳过。</summary>
        private void RequestSkip()
        {
            if (_inputBlocked)
                return;
            if (!_skipPending)
            {
                _skipPending = true;
                if (_skipButton != null)
                    SetButtonLabel(_skipButton, "Confirm Skip");
                return;
            }
            _skipPending = false;
            if (_skipButton != null)
                SetButtonLabel(_skipButton, "Skip");
            _skipAction?.Invoke();
        }

        /// <summary>更新按钮子文本。</summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="label">新文本。</param>
        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button == null ? null : button.GetComponentInChildren<Text>();
            if (text != null)
                text.text = label;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            if (_speaker != null)
                _speaker.text = string.Empty;
            if (_body != null)
                _body.text = string.Empty;
            if (_portrait != null)
            {
                _portrait.sprite = null;
                _portrait.gameObject.SetActive(false);
            }
            _currentCharacterId = null;
            if (_effect != null)
                _effect.color = Color.clear;
            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
            }
            _continueAction = null;
            StopTyping();
            _history.Clear();
            if (_historyView != null)
                _historyView.SetActive(false);
            ClearChoices();
        }

        /// <summary>追加一条实际经过的剧情历史。</summary>
        /// <param name="entry">历史记录。</param>
        public void AppendHistory(StoryHistoryEntry entry)
        {
            if (entry == null)
                return;
            _history.Add(entry);
            RefreshHistory();
        }

        /// <summary>继续按钮点击处理。</summary>
        private void ContinueClicked()
        {
            if (_waitCoroutine != null)
            {
                // 等待期间点击立即结束等待。
                Action waitCallback = _waitSkipAction;
                _waitSkipAction = null;
                waitCallback?.Invoke();
                return;
            }
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
            if (_typingCoroutine == null)
                return;
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        /// <summary>切换历史记录覆盖层。</summary>
        private void ToggleHistory()
        {
            if (_historyView == null)
                return;
            _historyView.SetActive(!_historyView.activeSelf);
            RefreshHistory();
        }

        /// <summary>刷新历史记录文本。</summary>
        private void RefreshHistory()
        {
            if (_historyText == null)
                return;
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
            if (_choicesRoot == null)
                return;
            for (int i = _choicesRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_choicesRoot.transform.GetChild(i).gameObject);
            _choiceAction = null;
        }
    }
}
