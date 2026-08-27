using System;
using System.Collections.Generic;
using Game.Content;
using Game.Contracts;
using Game.Contracts.Content;
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

        /// <summary>创建面板、启动测试剧情并显示首个节点。</summary>
        public void Initialize(GameRuntimeServices runtimeServices = null)
        {
            if (_runner != null)
                return;
            var provider = OfficialTestMapCatalog.CreateProvider();
            _runner = new StoryRunner(id =>
            {
                provider.TryGetStory(id, out StoryDefinition definition);
                return definition;
            });
            GameObject panelObject = new GameObject("StoryDialoguePanel");
            panelObject.transform.SetParent(transform, false);
            _panel = panelObject.AddComponent<StoryDialoguePanel>();
            _panel.SetSkipAction(Skip);
            _globalCanvas = FindObjectOfType<GlobalCanvasLayer>();
            _runtimeServices = runtimeServices;
            _localization = runtimeServices == null ? null : runtimeServices.Localization;
            if (_localization != null)
                _panel.SetLocalization(_localization);
            _runner.Start(new StoryId(TestStoryId));
            RenderCurrentNode();
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
                _panel.Clear();
                ReturnAfterStory();
                return;
            }

            StoryNodeDefinition node = snapshot.CurrentNode;
            if (node.Type == StoryNodeType.Dialogue)
            {
                string speakerKey = node.SpeakerKey ?? string.Empty;
                _panel.AppendHistory(new StoryHistoryEntry(new StoryNodeId(node.NodeId), speakerKey, node.TextKey));
                _panel.ShowDialogue(new StoryDialogueView(speakerKey, node.TextKey), ResolvePortrait(node), Advance);
                return;
            }
            if (node.Type == StoryNodeType.Choice)
            {
                var choices = new List<StoryChoiceView>();
                if (node.Choices != null)
                    foreach (StoryChoiceDefinition choice in node.Choices)
                        if (choice != null && !string.IsNullOrWhiteSpace(choice.ChoiceId))
                            choices.Add(new StoryChoiceView(new ChoiceId(choice.ChoiceId), choice.TextKey));
                _panel.ShowChoices(
                    choices.AsReadOnly(),
                    choice =>
                    {
                        foreach (StoryChoiceDefinition definition in node.Choices)
                            if (definition != null && definition.ChoiceId == choice.Value)
                                _panel.AppendHistory(
                                    new StoryHistoryEntry(
                                        new StoryNodeId(node.NodeId),
                                        string.Empty,
                                        string.Empty,
                                        definition.TextKey
                                    )
                                );
                        Choose(choice);
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
            ICharacterAssetRegistry registry = _runtimeServices.Characters as ICharacterAssetRegistry;
            return registry == null ? null : registry.GetPortrait(characterId, appearance, null);
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
            RenderCurrentNode();
        }

        /// <summary>处理选项点击并显示目标节点。</summary>
        /// <param name="choiceId">被点击的选项标识。</param>
        private void Choose(ChoiceId choiceId)
        {
            Result result = _runner.Choose(choiceId);
            if (!result.IsSuccess)
            {
                Debug.LogError("Story choice failed: " + result.Message, this);
                return;
            }
            RenderCurrentNode();
        }

        /// <summary>执行整段剧情跳过并清理表现。</summary>
        private void Skip()
        {
            Result result = _runner.Skip();
            if (!result.IsSuccess)
                Debug.LogError("Story skip failed: " + result.Message, this);
            else
                RenderCurrentNode();
        }

        /// <summary>剧情结束后按流程返回地图或进入占位关卡。</summary>
        private void ReturnAfterStory()
        {
            if (_runtimeServices == null)
                return;
            Game.Flow.GameFlowService flow = _runtimeServices.Flow as Game.Flow.GameFlowService;
            if (flow == null || !flow.LastStoryReturnTarget.HasValue)
                return;
            if (
                flow.LastStoryReturnTarget.Value.Kind == StoryReturnKind.Level
                && flow.LastStoryReturnTarget.Value.Level != null
            )
                _ = flow.EnterLevelAsync(
                    flow.LastStoryReturnTarget.Value.Level,
                    System.Threading.CancellationToken.None
                );
            else if (flow.LastStoryReturnTarget.Value.Kind == StoryReturnKind.MetaPage)
                _ = flow.OpenMetaHubAsync(
                    flow.LastStoryReturnTarget.Value.MetaPage,
                    System.Threading.CancellationToken.None
                );
        }
    }
}
