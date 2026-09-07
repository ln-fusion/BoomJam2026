using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>绑定 Gameplay 的普通返回和模拟通关按钮；仅模拟通关按钮提交完成事实。</summary>
    [RequireComponent(typeof(Button))]
    public sealed class GameplayReturnButton : MonoBehaviour
    {
        private Button _button;
        private IGameFlowService _flow;
        private GlobalCanvasLayer _globalCanvas;
        private bool _returning;
        private GameRuntimeServices _runtimeServices;
        [SerializeField] private Button completeButton;

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
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnReturnRequested);
            _button.interactable = true;
            if (completeButton != null)
            {
                completeButton.onClick.AddListener(OnCompleteRequested);
                completeButton.interactable = _runtimeServices.WhiteboxLevel != null;
            }
        }

        /// <summary>响应返回请求，在当前请求结束前忽略重复点击。</summary>
        private void OnReturnRequested()
        {
            if (!_returning && _flow != null)
                _ = ReturnToMapAsync(false);
        }

        /// <summary>响应模拟通关请求，与普通返回共享防重入状态。</summary>
        private void OnCompleteRequested()
        {
            if (!_returning && _runtimeServices != null && _runtimeServices.WhiteboxLevel != null)
                _ = ReturnToMapAsync(true);
        }

        /// <summary>可选地先模拟通关并保存，再返回地图；保存失败时留在 Gameplay，不发布完成进度。</summary>
        /// <param name="complete">是否提交当前白盒会话；普通返回传 false。</param>
        /// <returns>返回请求结束时完成的任务；加载结果由流程服务记录。</returns>
        private async Task ReturnToMapAsync(bool complete)
        {
            _returning = true;
            _button.interactable = false;
            if (completeButton != null)
                completeButton.interactable = false;
            try
            {
                if (complete)
                {
                    var saved = await _runtimeServices.CompleteWhiteboxLevelAsync(CancellationToken.None);
                    if (!saved.IsSuccess)
                    {
                        if (_globalCanvas != null)
                            _globalCanvas.ShowFeedback(saved.Message);
                        return;
                    }
                }
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
                if (this != null && _button != null)
                {
                    _returning = false;
                    _button.interactable = true;
                    if (completeButton != null)
                        completeButton.interactable = _runtimeServices.WhiteboxLevel != null;
                }
            }
        }

        /// <summary>场景卸载时解除按钮事件订阅。</summary>
        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnReturnRequested);
            if (completeButton != null)
                completeButton.onClick.RemoveListener(OnCompleteRequested);
        }
    }
}
