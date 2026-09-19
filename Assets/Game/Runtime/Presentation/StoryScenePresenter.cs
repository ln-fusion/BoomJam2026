using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Content;
using Game.Contracts;
using Game.Contracts.Content;
using Game.Contracts.Persistence;
using Game.Contracts.Story;
using Game.Foundation;
using Game.Story;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>驱动 C09 测试剧情与基础对白面板的场景表现器。</summary>
    public sealed class StoryScenePresenter : MonoBehaviour
    {
        private const string TestStoryId = "official.story.c06_branch";
        private StoryDialoguePanel _panel;
        private StoryRunner _runner;
        private GlobalCanvasLayer _globalCanvas;
        private GameRuntimeServices _runtimeServices;
        private ILocalizationService _localization;
        private IContentService _content;
        private StoryId _storyId;
        private bool _isReturning;
        private string _lastHistoryDialogueNodeId;
        private CancellationTokenSource _lifetime = new CancellationTokenSource();

        /// <summary>销毁表现器时禁止未完成的返回任务继续操作旧界面。</summary>
        private void OnDestroy()
        {
            _isReturning = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        /// <summary>创建面板、启动当前流程指定的剧情并显示首个节点。</summary>
        /// <param name="runtimeServices">运行时服务容器；为空时只播放兼容测试剧情且不执行返回流程。</param>
        public void Initialize(GameRuntimeServices runtimeServices = null)
        {
            if (_runner != null)
                return;
            _runtimeServices = runtimeServices;
            _content = runtimeServices?.Content ??
                new OfficialContentService(OfficialTestMapCatalog.CreateProvider());
            _runner = new StoryRunner(id => _content.GetStory(id));
            GameObject panelObject = new GameObject("StoryDialoguePanel");
            panelObject.transform.SetParent(transform, false);
            _panel = panelObject.AddComponent<StoryDialoguePanel>();
            _panel.SetSkipAction(Skip);
            _globalCanvas = FindObjectOfType<GlobalCanvasLayer>();
            _panel.SetSettingsAction(() => _globalCanvas?.OpenSettings());
            _runtimeServices = runtimeServices;
            _localization = runtimeServices == null ? null : runtimeServices.Localization;
            if (_localization != null)
                _panel.SetLocalization(_localization);
            TryBindStoryPrefab();
            if (runtimeServices != null && runtimeServices.Assets != null)
            {
                var stageSource = new StoryAssetSource(runtimeServices.Assets);
                _panel.SetStageAssetSource(stageSource);
            }
            StoryId storyId = ResolveStoryId();
            _storyId = storyId;
            _runner.Start(storyId);
            RenderCurrentNode();
        }

        /// <summary>解析本次播放的剧情 ID: 优先 Flow 记录, 否则回退测试剧情。</summary>
        /// <returns>剧情稳定标识。</returns>
        private StoryId ResolveStoryId()
        {
            Game.Flow.GameFlowService flow = _runtimeServices?.Flow as Game.Flow.GameFlowService;
            return flow?.CurrentStoryId ?? new StoryId(TestStoryId);
        }

        /// <summary>尝试绑定剧情面板预制体；缺失时回退代码生成。</summary>
        private void TryBindStoryPrefab()
        {
            GameObject prefab = _runtimeServices?.StoryPrefab;
            if (prefab == null)
                return;
            GameObject instance = Instantiate(prefab, _panel.transform, false);
            StoryUiBindings bindings = instance.GetComponent<StoryUiBindings>();
            if (bindings == null || !bindings.IsComplete)
            {
                Debug.LogWarning(
                    "[StoryScenePresenter] ui.story-panel 预制体绑定不完整，回退代码生成界面。",
                    this
                );
                Destroy(instance);
                return;
            }
            _panel.BindFromPrefab(bindings);
        }

        /// <summary>每帧同步设置弹窗对剧情输入的阻塞状态。</summary>
        private void Update()
        {
            if (_globalCanvas == null || _panel == null)
                return;
            _panel.SetInputBlocked(
                _globalCanvas.ModalCanvas != null
                    && _globalCanvas.ModalCanvas.transform.Find("ModalBlocker")?.gameObject.activeSelf == true
            );
        }

        /// <summary>显示当前节点，自动跳过 Goto 并在 End 时清理面板。</summary>
        private void RenderCurrentNode()
        {
            StorySnapshot snapshot = _runner.GetSnapshot();
            if (snapshot == null || snapshot.IsCompleted)
            {
                ReturnAfterStory();
                return;
            }

            StoryNodeDefinition node = snapshot.CurrentNode;
            if (node.Type == StoryNodeType.Dialogue)
            {
                string speaker = SelectStoryText(node.SpeakerTextZhCn, node.SpeakerTextEnUs, node, false);
                string text = SelectStoryText(node.TextZhCn, node.TextEnUs, node);
                if (!string.Equals(_lastHistoryDialogueNodeId, node.NodeId, StringComparison.Ordinal))
                {
                    _panel.AppendDialogueHistory(_storyId, new StoryNodeId(node.NodeId), node.SpeakerCharacterId, speaker, text);
                    _lastHistoryDialogueNodeId = node.NodeId;
                }
                _panel.ShowDialogue(new StoryDialogueView(speaker, text), ResolvePortrait(node), Advance);
                return;
            }
            if (node.Type == StoryNodeType.Choice)
            {
                var choices = new List<StoryChoiceView>();
                if (node.Choices != null)
                    foreach (StoryChoiceDefinition choice in node.Choices)
                        if (choice != null && !string.IsNullOrWhiteSpace(choice.ChoiceId))
                            choices.Add(new StoryChoiceView(
                                new ChoiceId(choice.ChoiceId),
                                SelectStoryText(choice.TextZhCn, choice.TextEnUs, node)
                            ));
                _panel.ShowChoices(
                    choices.AsReadOnly(),
                    choice =>
                    {
                        StoryChoiceDefinition selected = null;
                        foreach (StoryChoiceDefinition definition in node.Choices)
                            if (definition != null && definition.ChoiceId == choice.Value)
                                selected = definition;
                        Choose(choice, node, selected);
                    }
                );
                return;
            }
            if (node.Type == StoryNodeType.ShowCharacter)
            {
                // 立绘已由下一 Dialogue 节点通过当前形象查询解析；此处仅记录并在落地时刷新。
                _panel.ShowCharacter(node.SpeakerCharacterId, node.AppearanceOverride, ResolvePortrait(node));
                Advance();
                return;
            }
            if (node.Type == StoryNodeType.ShowCg)
            {
                _panel.ShowCg(node.AssetId);
                Advance();
                return;
            }
            if (node.Type == StoryNodeType.SetBackground)
            {
                _panel.SetBackground(node.BackgroundId);
                Advance();
                return;
            }
            if (node.Type == StoryNodeType.HideCharacter)
            {
                _panel.HideCharacter(node.SpeakerCharacterId);
                Advance();
                return;
            }
            if (node.Type == StoryNodeType.MoveCharacter)
            {
                _panel.MoveCharacter(node.SpeakerCharacterId, node.CharacterPosition);
                Advance();
                return;
            }
            if (node.Type == StoryNodeType.PlayAudio)
            {
                PlayNodeAudio(node);
                Advance();
                return;
            }
            if (node.Type == StoryNodeType.ScreenEffect)
            {
                _panel.PlayScreenEffect(node.EffectType);
                Advance();
                return;
            }
            if (node.Type == StoryNodeType.Wait)
            {
                _panel.ShowWait(node.WaitSeconds, Advance);
                return;
            }

            Advance();
        }

        /// <summary>按当前语言选择剧情文本，并在两种语言都缺失时输出定位日志。</summary>
        /// <param name="zhCn">中文文本。</param>
        /// <param name="enUs">英文文本。</param>
        /// <param name="node">当前节点，用于错误定位。</param>
        /// <param name="required">是否要求至少存在一种文本。</param>
        /// <returns>当前语言文本或可用回退文本。</returns>
        private string SelectStoryText(string zhCn, string enUs, StoryNodeDefinition node, bool required = true)
        {
            bool english = string.Equals(_localization?.CurrentLocaleCode, "en-US", StringComparison.OrdinalIgnoreCase);
            string selected = english && !string.IsNullOrWhiteSpace(enUs) ? enUs : zhCn;
            if (string.IsNullOrWhiteSpace(selected))
                selected = enUs;
            if (required && string.IsNullOrWhiteSpace(selected))
                Debug.LogError("Missing full JSON story text: " + node.NodeId, this);
            return selected ?? string.Empty;
        }

        /// <summary>解析节点立绘：显式覆盖优先，否则查询角色当前默认形象。</summary>
        /// <param name="node">当前剧情节点。</param>
        /// <returns>立绘精灵；无角色或资源缺失时为 null。</returns>
        private Sprite ResolvePortrait(StoryNodeDefinition node)
        {
            if (_runtimeServices == null || string.IsNullOrWhiteSpace(node.SpeakerCharacterId))
                return null;
            var characterId = new CharacterId(node.SpeakerCharacterId);
            AppearanceId appearance = string.IsNullOrWhiteSpace(node.AppearanceOverride)
                ? _runtimeServices.Characters.GetDefaultAppearance(characterId)
                : new AppearanceId(node.AppearanceOverride);
            if (appearance == null)
                return null;
            // 通过直持的注册表解析, 避免 Characters as ICharacterAssetRegistry 向下转型。
            ICharacterAssetRegistry registry = _runtimeServices.CharacterAssets;
            ExpressionId expression = string.IsNullOrWhiteSpace(node.ExpressionId)
                ? null : new ExpressionId(node.ExpressionId);
            return registry == null ? null : registry.GetPortrait(characterId, appearance, expression);
        }

        /// <summary>按节点音频类别播放音乐或音效。</summary>
        /// <param name="node">包含 AudioId 与 AudioKind 的演出节点。</param>
        private void PlayNodeAudio(StoryNodeDefinition node)
        {
            if (_runtimeServices == null || string.IsNullOrWhiteSpace(node.AudioId))
                return;
            if (node.AudioKind == StoryAudioKind.Music)
                _runtimeServices.Audio.PlayMusic(new MusicId(node.AudioId), MusicTransition.Immediate);
            else
                _runtimeServices.Audio.PlaySfx(new SfxId(node.AudioId));
        }

        /// <summary>推进对白或无表现节点。</summary>
        private void Advance()
        {
            Result result = _runner.Advance();
            if (!result.IsSuccess)
            {
                Debug.LogError("Story advance failed: " + result.Message, this);
                return;
            }
            _lastHistoryDialogueNodeId = null;
            RenderCurrentNode();
        }

        /// <summary>处理选项点击、记录选择并显示目标节点。</summary>
        /// <param name="choiceId">被点击的选项标识。</param>
        /// <param name="choiceNode">当前选项节点。</param>
        /// <param name="selected">实际选择的分支。</param>
        private void Choose(ChoiceId choiceId, StoryNodeDefinition choiceNode, StoryChoiceDefinition selected)
        {
            Result result = _runner.Choose(choiceId);
            if (!result.IsSuccess)
            {
                Debug.LogError("Story choice failed: " + result.Message, this);
                return;
            }
            if (selected != null)
                _panel.AppendChoiceHistory(
                    _storyId,
                    new StoryNodeId(choiceNode.NodeId),
                    choiceId.Value,
                    SelectStoryText(selected.TextZhCn, selected.TextEnUs, choiceNode)
                );
            _lastHistoryDialogueNodeId = null;
            RenderCurrentNode();
        }

        /// <summary>执行整段剧情跳过并清理表现。</summary>
        private void Skip()
        {
            // 逐节点推进而不是直接调用 Runner.Skip，确保背景、角色、CG、音频和效果
            // 等表现节点在跳过时仍然落地到最终状态。
            for (int steps = 0; steps < 256; steps++)
            {
                StorySnapshot snapshot = _runner.GetSnapshot();
                if (snapshot == null || snapshot.IsCompleted)
                {
                    RenderCurrentNode();
                    return;
                }
                StoryNodeDefinition node = snapshot.CurrentNode;
                ApplySkipNode(node);
                Result result;
                if (node.Type == StoryNodeType.Choice)
                {
                    if (node.Choices == null || node.Choices.Count == 0)
                    {
                        Debug.LogError("Story skip failed: choice has no branch.", this);
                        return;
                    }
                    StoryChoiceDefinition choice = node.Choices[0];
                    _panel.AppendChoiceHistory(_storyId, new StoryNodeId(node.NodeId),
                        choice.ChoiceId, SelectStoryText(choice.TextZhCn, choice.TextEnUs, node));
                    result = _runner.Choose(new ChoiceId(choice.ChoiceId));
                }
                else
                {
                    result = _runner.Advance();
                }
                if (!result.IsSuccess)
                {
                    Debug.LogError("Story skip failed: " + result.Message, this);
                    return;
                }
            }
            Debug.LogError("Story skip failed: execution step limit exceeded.", this);
        }

        /// <summary>在跳过期间应用当前节点的最终表现状态。</summary>
        /// <param name="node">当前待跳过节点。</param>
        private void ApplySkipNode(StoryNodeDefinition node)
        {
            if (node == null)
                return;
            switch (node.Type)
            {
                case StoryNodeType.Dialogue:
                    if (!string.Equals(_lastHistoryDialogueNodeId, node.NodeId, StringComparison.Ordinal))
                    {
                        _panel.AppendDialogueHistory(_storyId, new StoryNodeId(node.NodeId),
                            node.SpeakerCharacterId,
                            SelectStoryText(node.SpeakerTextZhCn, node.SpeakerTextEnUs, node, false),
                            SelectStoryText(node.TextZhCn, node.TextEnUs, node));
                        _lastHistoryDialogueNodeId = node.NodeId;
                    }
                    break;
                case StoryNodeType.ShowCharacter:
                    _panel.ShowCharacter(node.SpeakerCharacterId, node.AppearanceOverride, ResolvePortrait(node));
                    break;
                case StoryNodeType.HideCharacter:
                    _panel.HideCharacter(node.SpeakerCharacterId);
                    break;
                case StoryNodeType.MoveCharacter:
                    _panel.MoveCharacter(node.SpeakerCharacterId, node.CharacterPosition);
                    break;
                case StoryNodeType.SetBackground:
                    _panel.SetBackground(node.BackgroundId);
                    break;
                case StoryNodeType.ShowCg:
                    _panel.ShowCg(node.AssetId);
                    break;
                case StoryNodeType.PlayAudio:
                    PlayNodeAudio(node);
                    break;
                case StoryNodeType.ScreenEffect:
                    _panel.PlayScreenEffect(node.EffectType);
                    break;
            }
        }

        /// <summary>剧情结束后提交完成事实，再按流程返回地图或进入占位关卡。</summary>
        private void ReturnAfterStory()
        {
            if (_runtimeServices == null || _isReturning)
                return;
            Game.Flow.GameFlowService flow = _runtimeServices.Flow as Game.Flow.GameFlowService;
            if (flow == null || !flow.LastStoryReturnTarget.HasValue)
                return;
            _ = CompleteAndReturnAsync(flow);
        }

        /// <summary>提交剧情完成事实后再执行返回跳转；写档失败时阻断跳转供重试。</summary>
        /// <param name="flow">应用流程服务。</param>
        private async Task CompleteAndReturnAsync(Game.Flow.GameFlowService flow)
        {
            _isReturning = true;
            CancellationToken token = _lifetime.Token;
            StoryReturnTarget target = flow.LastStoryReturnTarget.Value;
            try
            {
                StorySnapshot finished = _runner.GetSnapshot();
                if (finished == null || !finished.IsCompleted)
                    return;
                if (finished != null && finished.IsCompleted)
                {
                    SaveResult commit = await _runtimeServices.SaveStoryCompletedAsync(
                        finished.StoryId,
                        token
                    );
                    token.ThrowIfCancellationRequested();
                    if (!commit.IsSuccess)
                    {
                        Debug.LogError("剧情完成事实提交失败, 返回跳转被阻断: " + commit.Message, this);
                        _globalCanvas?.ShowFeedback(commit.Message);
                        _panel.SetRetryAction(ReturnAfterStory);
                        return;
                    }
                }

                token.ThrowIfCancellationRequested();
                // 导航由应用 Flow 接管；卸载本场景会销毁 Presenter，不能用其令牌取消导航自身。
                if (target.Kind == StoryReturnKind.Level && target.Level != null)
                    await flow.EnterLevelAsync(target.Level, CancellationToken.None);
                else if (target.Kind == StoryReturnKind.MetaPage)
                    await flow.OpenMetaHubAsync(target.MetaPage, CancellationToken.None);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!token.IsCancellationRequested)
                {
                    _globalCanvas?.ShowFeedback(exception.Message);
                    _panel.SetRetryAction(ReturnAfterStory);
                }
            }
            finally { if (!token.IsCancellationRequested) _isReturning = false; }
        }
    }
}
