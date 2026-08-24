#nullable enable
using System;
using System.Threading.Tasks;
using Game.Audio;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Flow;
using Game.Foundation;
using Game.Localization;
using Game.Persistence;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Game.Bootstrap
{
    /// <summary>
    /// 应用组合根：驻留 00_Bootstrap 场景，组装 Flow/Settings/Localization/Audio 服务并执行启动导航.
    /// </summary>
    /// <remarks>
    /// C03-C05：组合根新增 SettingsService、UnityLocalizationService、UnityAudioService，
    /// 并创建 UIRootManager 监听场景激活事件动态挂载场景 UI.
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRoot : MonoBehaviour
    {
        [Tooltip("启动后加载的功能场景名（Build Settings 场景名）")]
        [SerializeField]
        private string startMenuSceneName = SceneNames.StartMenu;

        [Tooltip("本地化字符串表（社区/官方创建，Assets/Game/Localization 下）")]
        [SerializeField]
        private LocalizedStringTable? localizationTable;

        private GameFlowService? _flowService;
        private SettingsService? _settingsService;
        private UnityLocalizationService? _localizationService;
        private UnityAudioService? _audioService;
        private ISaveRepository? _saveRepository;
        private UIRootManager? _uiRootManager;
        private IDisposable? _pageSubscription;
        private IDisposable? _settingsAppliedSubscription;

        /// <summary>
        /// 创建流程服务并启动开始菜单导航（需先加载设置再装配服务）.
        /// </summary>
        private async void Start()
        {
            var logger = UnityDebugLogger.Instance;
            var clock = new SystemClock();
            var eventBus = new DomainEventBus(logger);

            _saveRepository = SaveRepositoryFactory.CreateDefault(clock, logger);

            if (localizationTable != null)
            {
                _localizationService = new UnityLocalizationService(localizationTable, eventBus, logger);
            }

            _settingsService = new SettingsService(
                _saveRepository,
                new UnitySettingsApplier(),
                eventBus,
                clock,
                logger,
                _localizationService
            );

            await _settingsService.LoadAsync(System.Threading.CancellationToken.None);

            _flowService = new GameFlowService(new UnitySceneLoader(), clock, logger, eventBus, startMenuSceneName);

            // 音频服务（无 AudioMixer 时使用 AudioListener 兜底）
            _audioService = new UnityAudioService(
                mixer: null,
                assetResolver: new NullAudioAssetResolver(),
                logger: logger
            );
            _audioService.ApplyVolumes(
                _settingsService.Current.MasterVolume,
                _settingsService.Current.MusicVolume,
                _settingsService.Current.SfxVolume
            );

            // 设置已应用：同步音量到音频服务（AudioMixer 落地前用 AudioListener 兜底）
            _settingsAppliedSubscription = eventBus.Subscribe<SettingsAppliedEvent>(OnSettingsApplied);

            // 页面切换时记录最后页面到 Profile（C05：重新进入 MetaHub 恢复）
            _pageSubscription = eventBus.Subscribe<MetaPageChangedEvent>(OnMetaPageChanged);

            _uiRootManager = new UIRootManager(_flowService, eventBus, _settingsService, _localizationService, clock);

            _ = RunStartupAsync(_flowService);
        }

        /// <summary>
        /// 设置已应用：把新音量同步到音频服务.
        /// </summary>
        private void OnSettingsApplied(SettingsAppliedEvent evt)
        {
            _audioService?.ApplyVolumes(evt.Snapshot.MasterVolume, evt.Snapshot.MusicVolume, evt.Snapshot.SfxVolume);
        }

        /// <summary>
        /// 页面切换：更新 Profile.LastMetaPageId 并保存（异步、失败仅记录日志）.
        /// </summary>
        private async void OnMetaPageChanged(MetaPageChangedEvent evt)
        {
            if (_saveRepository == null)
                return;

            var load = await _saveRepository.LoadProfileAsync(System.Threading.CancellationToken.None);
            if (load.Data == null)
                return;

            load.Data.LastMetaPageId = evt.Page.ToString().ToLowerInvariant();
            await _saveRepository.SaveProfileAsync(
                load.Data,
                SaveReason.PageChanged,
                System.Threading.CancellationToken.None
            );
        }

        /// <summary>执行启动导航并记录不可恢复的启动异常。</summary>
        /// <param name="flowService">应用流程服务。</param>
        private static async Task RunStartupAsync(GameFlowService flowService)
        {
            try
            {
                await flowService.EnterStartMenuAsync(System.Threading.CancellationToken.None);
            }
            catch (Exception ex)
            {
                // 启动导航失败不能静默丢弃：黑屏时这是唯一排查线索
                Debug.LogException(ex);
            }
        }

        /// <summary>销毁组合根时释放流程服务及其当前场景生命周期。</summary>
        private void OnDestroy()
        {
            _pageSubscription?.Dispose();
            _settingsAppliedSubscription?.Dispose();
            _uiRootManager?.Dispose();
            _flowService?.Dispose();
            _settingsService?.Dispose();
            (_saveRepository as IDisposable)?.Dispose();
        }
    }
}
