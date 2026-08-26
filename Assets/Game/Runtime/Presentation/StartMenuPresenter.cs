using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Foundation;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 开始界面 Presenter：协调档案首次/继续判断、设置弹窗和场景流程，并隔离重复点击。
    /// </summary>
    public sealed class StartMenuPresenter : MonoBehaviour
    {
        private StartMenuView _view;
        private GameRuntimeServices _runtimeServices;
        private GlobalCanvasLayer _globalCanvasLayer;
        private CancellationTokenSource _lifetime;
        private bool _busy;
        private bool _waitingForNickname;
        private bool _nicknameSubmissionInFlight;

        /// <summary>注入 View、运行时服务和全局 UI，并开始订阅按钮事件。</summary>
        /// <param name="view">开始界面 View。</param>
        /// <param name="runtimeServices">Bootstrap 创建的运行时服务容器。</param>
        /// <param name="globalCanvasLayer">全局 UI 层。</param>
        public void Initialize(StartMenuView view, GameRuntimeServices runtimeServices,
            GlobalCanvasLayer globalCanvasLayer)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _runtimeServices = runtimeServices ??
                throw new ArgumentNullException(nameof(runtimeServices));
            _globalCanvasLayer = globalCanvasLayer;
            _view.Initialize(_runtimeServices.Localization);
            _lifetime = new CancellationTokenSource();
            _view.StartRequested += OnStartRequested;
            _view.SettingsRequested += OnSettingsRequested;
            _view.QuitRequested += OnQuitRequested;
            _view.NicknameSubmitted += OnNicknameSubmitted;
            _view.NicknameCancelled += OnNicknameCancelled;
            _ = RefreshProfileStateAsync();
        }

        /// <summary>取消按钮订阅和异步操作。</summary>
        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.StartRequested -= OnStartRequested;
                _view.SettingsRequested -= OnSettingsRequested;
                _view.QuitRequested -= OnQuitRequested;
                _view.NicknameSubmitted -= OnNicknameSubmitted;
                _view.NicknameCancelled -= OnNicknameCancelled;
            }

            _lifetime?.Cancel();
            _lifetime?.Dispose();
        }

        /// <summary>刷新开始按钮的首次/继续状态。</summary>
        private async Task RefreshProfileStateAsync()
        {
            if (_runtimeServices == null || _lifetime == null)
                return;

            try
            {
                Result<ProfileStartupDecision> decision = await _runtimeServices.ProfileLifecycle
                    .LoadOrDecideAsync(_lifetime.Token);
                if (!decision.IsSuccess)
                {
                    _view.Render(new StartMenuViewModel(false, decision.Message));
                    return;
                }

                if (decision.Value.Mode == ProfileStartupMode.Continue)
                    _runtimeServices.SetCurrentProfile(decision.Value.Profile);
                _view.Render(new StartMenuViewModel(
                    decision.Value.Mode == ProfileStartupMode.Continue, string.Empty));
            }
            catch (OperationCanceledException)
            {
                // Scene 卸载时取消是正常生命周期行为。
            }
            catch (Exception exception)
            {
                _view.ShowFeedback(exception.Message);
            }
        }

        /// <summary>响应开始/继续按钮。</summary>
        private void OnStartRequested()
        {
            if (!_busy)
                _ = StartOrContinueAsync();
        }

        /// <summary>执行首次/继续档案判断和导航。</summary>
        private async Task StartOrContinueAsync()
        {
            if (_runtimeServices == null)
            {
                _view.ShowFeedback("Game services are unavailable.");
                return;
            }

            _busy = true;
            _view.ShowFeedback(Text(UiTextKeys.FeedbackLoading));
            try
            {
                Result<ProfileStartupDecision> decision = await _runtimeServices.ProfileLifecycle
                    .LoadOrDecideAsync(_lifetime.Token);
                if (!decision.IsSuccess)
                {
                    _view.ShowFeedback(decision.Message);
                    return;
                }

                if (decision.Value.Mode == ProfileStartupMode.CreateNew)
                {
                    _waitingForNickname = true;
                    _view.ShowNicknamePrompt();
                    _view.ShowFeedback(string.Empty);
                    return;
                }

                _runtimeServices.SetCurrentProfile(decision.Value.Profile);
                // 场景跳转会销毁当前 Presenter；不能把当前场景的生命周期令牌传给跳转请求。
                await _runtimeServices.Flow.StartOrContinueAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Scene 卸载时取消是正常生命周期行为。
            }
            catch (Exception exception)
            {
                _view.ShowFeedback(exception.Message);
            }
            finally
            {
                if (!_waitingForNickname)
                    _busy = false;
            }
        }

        /// <summary>响应昵称确认并创建单一玩家档案。</summary>
        /// <param name="nickname">玩家输入的昵称。</param>
        private void OnNicknameSubmitted(string nickname)
        {
            if (!_waitingForNickname || _nicknameSubmissionInFlight)
                return;

            _nicknameSubmissionInFlight = true;
            _ = CreateProfileAndEnterAsync(nickname);
        }

        /// <summary>保存昵称成功后进入主界面。</summary>
        /// <param name="nickname">玩家昵称。</param>
        private async Task CreateProfileAndEnterAsync(string nickname)
        {
            if (_runtimeServices == null)
            {
                _nicknameSubmissionInFlight = false;
                _busy = false;
                return;
            }

            try
            {
                Result<ProfileSave> created = await _runtimeServices.ProfileLifecycle
                    .CreateProfileAsync(nickname, _lifetime.Token);
                if (!created.IsSuccess)
                {
                    _view.ShowNicknameError(string.IsNullOrWhiteSpace(created.Message)
                        ? Text(UiTextKeys.NicknameRequired) : created.Message);
                    return;
                }

                _runtimeServices.SetCurrentProfile(created.Value);
                _view.HideNicknamePrompt();
                _waitingForNickname = false;
                // 场景跳转会销毁当前 Presenter；不能把当前场景的生命周期令牌传给跳转请求。
                await _runtimeServices.Flow.StartOrContinueAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Scene 卸载时取消是正常生命周期行为。
            }
            catch (Exception exception)
            {
                _view.ShowNicknameError(exception.Message);
            }
            finally
            {
                _nicknameSubmissionInFlight = false;
                _busy = false;
            }
        }

        /// <summary>响应昵称弹窗取消。</summary>
        private void OnNicknameCancelled()
        {
            _waitingForNickname = false;
            _busy = false;
            _view.ShowFeedback(Text(UiTextKeys.FeedbackReady));
        }

        /// <summary>打开全局设置弹窗。</summary>
        private void OnSettingsRequested()
        {
            _globalCanvasLayer?.OpenSettings();
        }

        /// <summary>通过流程服务请求退出。</summary>
        private void OnQuitRequested()
        {
            if (_busy || _runtimeServices == null)
                return;

            _ = QuitAsync();
        }

        /// <summary>执行可取消的退出请求。</summary>
        private async Task QuitAsync()
        {
            _busy = true;
            try
            {
                await _runtimeServices.Flow.QuitGameAsync(_lifetime.Token);
#if UNITY_EDITOR
                _view.ShowFeedback(Text(UiTextKeys.FeedbackQuitEditor));
#endif
            }
            catch (OperationCanceledException)
            {
                // Scene 卸载时取消是正常生命周期行为。
            }
            catch (Exception exception)
            {
                _view.ShowFeedback(exception.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>读取当前 Locale 文本。</summary>
        /// <param name="key">稳定键字符串。</param>
        /// <returns>本地化文本。</returns>
        private string Text(string key)
        {
            return _runtimeServices == null
                ? key
                : _runtimeServices.Localization.Get(new LocalizationKey(key));
        }
    }
}
