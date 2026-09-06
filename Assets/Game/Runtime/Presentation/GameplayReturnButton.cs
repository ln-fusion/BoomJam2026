using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>绑定 Gameplay 占位场景的返回地图按钮，不记录通关或修改进度。</summary>
    [RequireComponent(typeof(Button))]
    public sealed class GameplayReturnButton : MonoBehaviour
    {
        private Button _button;
        private IGameFlowService _flow;
        private GlobalCanvasLayer _globalCanvas;
        private bool _returning;

        /// <summary>注入已有流程服务并启用按钮；重复初始化不重复订阅事件。</summary>
        /// <param name="flow">负责返回地图的流程服务。</param>
        /// <param name="globalCanvas">用于显示异常反馈的全局 UI。</param>
        public void Initialize(IGameFlowService flow, GlobalCanvasLayer globalCanvas)
        {
            if (_flow != null)
                return;
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _globalCanvas = globalCanvas;
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnReturnRequested);
            _button.interactable = true;
        }

        /// <summary>响应返回请求，在当前请求结束前忽略重复点击。</summary>
        private void OnReturnRequested()
        {
            if (!_returning && _flow != null)
                _ = ReturnToMapAsync();
        }

        /// <summary>返回地图；使用不随 Gameplay 卸载取消的令牌，异常时显示反馈。</summary>
        /// <returns>返回请求结束时完成的任务；加载结果由流程服务记录。</returns>
        private async Task ReturnToMapAsync()
        {
            _returning = true;
            _button.interactable = false;
            try
            {
                await _flow.OpenMetaHubAsync(MetaPageId.Map, CancellationToken.None);
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
                }
            }
        }

        /// <summary>场景卸载时解除按钮事件订阅。</summary>
        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnReturnRequested);
        }
    }
}
