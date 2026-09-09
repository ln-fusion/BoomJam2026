using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Content;
using Game.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>绑定 Gameplay 白盒的关卡标识、成功失败结果和设置入口。</summary>
    public sealed class GameplayReturnButton : MonoBehaviour
    {
        private IGameFlowService _flow;
        private GlobalCanvasLayer _globalCanvas;
        private bool _returning;
        private GameRuntimeServices _runtimeServices;
        [SerializeField] private Button simulateSuccessButton;
        [SerializeField] private Button simulateFailureButton;
        [SerializeField] private GameObject successPanel;
        [SerializeField] private Button submitSuccessButton;
        [SerializeField] private GameObject failurePanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Text levelIdText;

        /// <summary>注入已有流程服务并启用按钮；重复初始化不重复订阅事件。</summary>
        /// <param name="runtimeServices">包含返回流程、当前白盒会话和档案保存的服务。</param>
        /// <param name="globalCanvas">用于显示异常反馈的全局 UI。</param>
        public void Initialize(GameRuntimeServices runtimeServices, GlobalCanvasLayer globalCanvas)
        {
            if (_flow != null)
                return;
            _runtimeServices = runtimeServices ?? throw new ArgumentNullException(nameof(runtimeServices));
            _flow = runtimeServices.Flow;
            _globalCanvas = globalCanvas;
            if (levelIdText != null)
                levelIdText.text = _runtimeServices.WhiteboxLevel == null
                    ? "LevelId：未从地图选关"
                    : "LevelId：" + _runtimeServices.WhiteboxLevel.Value;
            SetResultPanel(successPanel, false);
            SetResultPanel(failurePanel, false);
            if (simulateSuccessButton != null)
            {
                simulateSuccessButton.onClick.AddListener(OnSuccessSimulated);
                simulateSuccessButton.interactable = _runtimeServices.WhiteboxLevel != null;
            }
            if (simulateFailureButton != null)
            {
                simulateFailureButton.onClick.AddListener(OnFailureSimulated);
                simulateFailureButton.interactable = _runtimeServices.WhiteboxLevel != null;
            }
            if (submitSuccessButton != null)
            {
                submitSuccessButton.onClick.AddListener(OnCompleteRequested);
                submitSuccessButton.interactable = _runtimeServices.WhiteboxLevel != null;
            }
            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRetryRequested);
                retryButton.interactable = true;
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsRequested);
                settingsButton.interactable = true;
            }
        }

        /// <summary>模拟玩法成功，只显示成功结果，不在此时写入通关进度。</summary>
        private void OnSuccessSimulated()
        {
            if (!_returning && _runtimeServices?.WhiteboxLevel != null)
                ShowResult(successPanel);
        }

        /// <summary>模拟玩法失败，只显示失败结果，不调用通关提交。</summary>
        private void OnFailureSimulated()
        {
            if (!_returning && _runtimeServices?.WhiteboxLevel != null)
                ShowResult(failurePanel);
        }

        /// <summary>关闭失败结果并恢复白盒模拟按钮，表示重新开始当前关卡。</summary>
        private void OnRetryRequested()
        {
            if (!_returning)
                ShowResult(null);
        }

        /// <summary>响应模拟通关请求，在当前提交结束前忽略重复点击。</summary>
        private void OnCompleteRequested()
        {
            if (!_returning && _runtimeServices != null && _runtimeServices.WhiteboxLevel != null)
                _ = CompleteAndReturnToMapAsync();
        }

        /// <summary>打开全局设置弹窗；Gameplay 的暂停生命周期由全局 Canvas 统一管理。</summary>
        private void OnSettingsRequested()
        {
            if (!_returning)
                _globalCanvas?.OpenSettings();
        }

        /// <summary>首次通关保存成功后播放已配置关后剧情，重复通关直接返回地图；保存失败时留在结果面板。</summary>
        /// <returns>返回请求结束时完成的任务；加载结果由流程服务记录。</returns>
        private async Task CompleteAndReturnToMapAsync()
        {
            _returning = true;
            SetSimulationInteractable(false);
            if (submitSuccessButton != null)
                submitSuccessButton.interactable = false;
            if (settingsButton != null)
                settingsButton.interactable = false;
            try
            {
                var content = _runtimeServices.Content;
                bool firstCompletion = !_runtimeServices.CurrentProfile.CompletedLevelIds.Contains(
                    _runtimeServices.WhiteboxLevel.Value);
                string postludeId = content.GetLevel(_runtimeServices.WhiteboxLevel)?.ResolvedPostludeStoryId;
                StoryId postlude = string.IsNullOrWhiteSpace(postludeId) ? null : new StoryId(postludeId);
                if (postlude != null && content.GetStory(postlude) == null)
                    throw new InvalidOperationException("找不到关后剧情：" + postludeId);
                var saved = await _runtimeServices.CompleteWhiteboxLevelAsync(
                    CancellationToken.None);
                if (!saved.IsSuccess)
                {
                    if (_globalCanvas != null)
                        _globalCanvas.ShowFeedback(saved.Message);
                    return;
                }
                if (postlude != null && firstCompletion)
                    await _flow.PlayStoryAsync(postlude,
                        StoryReturnTarget.ToMetaPage(MetaPageId.Map), CancellationToken.None);
                else
                    await _flow.OpenMetaHubAsync(MetaPageId.Map, CancellationToken.None);
                if (this == null)
                    _runtimeServices.EndWhiteboxLevel();
            }
            catch (OperationCanceledException)
            {
                // 场景仍存在时由 finally 恢复交互。
            }
            catch (Exception exception)
            {
                if (_globalCanvas != null)
                    _globalCanvas.ShowFeedback(exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                if (this != null)
                {
                    _returning = false;
                    bool resultVisible = successPanel != null && successPanel.activeSelf ||
                        failurePanel != null && failurePanel.activeSelf;
                    SetSimulationInteractable(!resultVisible &&
                        _runtimeServices.WhiteboxLevel != null);
                    if (submitSuccessButton != null)
                        submitSuccessButton.interactable = _runtimeServices.WhiteboxLevel != null;
                    if (settingsButton != null)
                        settingsButton.interactable = true;
                }
            }
        }

        /// <summary>仅显示指定结果面板，并在结果显示期间禁用成功和失败模拟入口。</summary>
        /// <param name="panel">需要显示的结果面板；为空时关闭全部结果。</param>
        private void ShowResult(GameObject panel)
        {
            SetResultPanel(successPanel, panel == successPanel);
            SetResultPanel(failurePanel, panel == failurePanel);
            SetSimulationInteractable(panel == null && _runtimeServices?.WhiteboxLevel != null);
        }

        /// <summary>切换一个可选结果面板的显示状态。</summary>
        /// <param name="panel">需要切换的面板。</param>
        /// <param name="visible">是否显示。</param>
        private static void SetResultPanel(GameObject panel, bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }

        /// <summary>统一切换成功和失败模拟按钮的可交互状态。</summary>
        /// <param name="interactable">按钮是否可交互。</param>
        private void SetSimulationInteractable(bool interactable)
        {
            if (simulateSuccessButton != null)
                simulateSuccessButton.interactable = interactable;
            if (simulateFailureButton != null)
                simulateFailureButton.interactable = interactable;
        }

        /// <summary>场景卸载时解除按钮事件订阅。</summary>
        private void OnDestroy()
        {
            if (simulateSuccessButton != null)
                simulateSuccessButton.onClick.RemoveListener(OnSuccessSimulated);
            if (simulateFailureButton != null)
                simulateFailureButton.onClick.RemoveListener(OnFailureSimulated);
            if (submitSuccessButton != null)
                submitSuccessButton.onClick.RemoveListener(OnCompleteRequested);
            if (retryButton != null)
                retryButton.onClick.RemoveListener(OnRetryRequested);
            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(OnSettingsRequested);
        }
    }
}
