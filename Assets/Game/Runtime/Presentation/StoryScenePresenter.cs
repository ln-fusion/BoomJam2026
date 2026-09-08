using System;
using System.Collections.Generic;
using System.Threading;
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
        private bool _isReturning;

        /// <summary>创建面板、启动当前流程指定的剧情并显示首个节点。</summary>
        /// <param name="runtimeServices">运行时服务容器；为空时只播放兼容测试剧情且不执行返回流程。</param>
        public void Initialize(GameRuntimeServices runtimeServices = null)
        {
            if (_runner != null)
                return;
            var content = runtimeServices?.Content ??
                new OfficialContentService(OfficialTestMapCatalog.CreateProvider());
            _runner = new StoryRunner(id => content.GetStory(id));
            GameObject panelObject = new GameObject("StoryDialoguePanel");
            panelObject.transform.SetParent(transform, false);
            _panel = panelObject.AddComponent<StoryDialoguePanel>();
            _panel.SetSkipAction(Skip);
            _globalCanvas = FindObjectOfType<GlobalCanvasLayer>();
            _runtimeServices = runtimeServices;
            Game.Flow.GameFlowService flow = _runtimeServices?.Flow as Game.Flow.GameFlowService;
            _runner.Start(flow?.ActiveStoryId ?? new StoryId(TestStoryId));
            RenderCurrentNode();
        }

        /// <summary>每帧同步设置弹窗对剧情输入的阻塞状态。</summary>
        private void Update()
        {
            if (_globalCanvas == null || _panel == null) return;
            _panel.SetInputBlocked(_globalCanvas.ModalCanvas != null &&
                _globalCanvas.ModalCanvas.transform.Find("ModalBlocker")?.gameObject.activeSelf == true);
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
                _panel.AppendHistory(new StoryHistoryEntry(new StoryNodeId(node.NodeId),
                    string.Empty, node.TextKey));
                _panel.ShowDialogue(new StoryDialogueView(string.Empty, node.TextKey), Advance);
                return;
            }
            if (node.Type == StoryNodeType.Choice)
            {
                var choices = new List<StoryChoiceView>();
                if (node.Choices != null)
                    foreach (StoryChoiceDefinition choice in node.Choices)
                        if (choice != null && !string.IsNullOrWhiteSpace(choice.ChoiceId))
                            choices.Add(new StoryChoiceView(new ChoiceId(choice.ChoiceId), choice.TextKey));
                _panel.ShowChoices(choices.AsReadOnly(), choice =>
                {
                    foreach (StoryChoiceDefinition definition in node.Choices)
                        if (definition != null && definition.ChoiceId == choice.Value)
                            _panel.AppendHistory(new StoryHistoryEntry(new StoryNodeId(node.NodeId),
                                string.Empty, string.Empty, definition.TextKey));
                    Choose(choice);
                });
                return;
            }

            Advance();
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

        /// <summary>剧情结束后先保存完成事实，再按流程返回地图或进入占位关卡。</summary>
        private async void ReturnAfterStory()
        {
            if (_runtimeServices == null || _isReturning) return;
            _isReturning = true;
            Game.Flow.GameFlowService flow = _runtimeServices.Flow as Game.Flow.GameFlowService;
            StoryId storyId = flow?.ActiveStoryId;
            if (flow == null || storyId == null || !flow.LastStoryReturnTarget.HasValue)
            {
                _isReturning = false;
                return;
            }

            SaveResult saveResult;
            try
            {
                saveResult = await _runtimeServices.CompleteStoryAsync(storyId,
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                _isReturning = false;
                return;
            }
            if (!saveResult.IsSuccess)
            {
                Debug.LogError("Story completion save failed: " + saveResult.Message, this);
                _isReturning = false;
                return;
            }

            if (flow.LastStoryReturnTarget.Value.Kind == StoryReturnKind.Level &&
                flow.LastStoryReturnTarget.Value.Level != null)
                await flow.EnterLevelAsync(flow.LastStoryReturnTarget.Value.Level,
                    CancellationToken.None);
            else if (flow.LastStoryReturnTarget.Value.Kind == StoryReturnKind.MetaPage)
                await flow.OpenMetaHubAsync(flow.LastStoryReturnTarget.Value.MetaPage,
                    CancellationToken.None);
        }
    }
}
