#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Content;
using Game.Flow;
using Game.Foundation;
using Game.Persistence;
using Game.Progression;
using Game.Presentation;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap
{
    /// <summary>
    /// 应用组合根：驻留 00_Bootstrap 场景，组装 Flow 服务并执行启动导航。
    /// </summary>
    /// <remarks>
    /// C02 起：GameRoot 不再直接调 SceneManager，改由 <see cref="IGameFlowService"/> 接管流转
    /// （Additive 加载/卸载/激活、防重入、取消生命周期）。
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRoot : MonoBehaviour
    {
        [Tooltip("启动后加载的功能场景名（Build Settings 场景名）")]
        [SerializeField]
        private string startMenuSceneName = SceneNames.StartMenu;

        [Tooltip("可选 AudioMixer；暴露 MasterVolume/MusicVolume/SfxVolume 参数后启用 Mixer 控制。")]
        [SerializeField]
        private AudioMixer? audioMixer;

        [Tooltip("可选官方资源 Registry；配置 UI 预制体后运行时优先实例化预制体。")]
        [SerializeField]
        private ContentAssetRegistry? contentAssetRegistry;

        private GameFlowService? _flowService;
        private SettingsService? _settingsService;
        private DefaultLocalizationService? _localizationService;
        private ISaveRepository? _saveRepository;
        private IDisposable? _saveRepositoryLifetime;
        private GameObject? _globalUiRoot;
        private GlobalCanvasLayer? _globalCanvasLayer;
        private GameObject? _audioRoot;
        private CancellationTokenSource? _startupLifetime;
        private GameRuntimeServices? _runtimeServices;

        /// <summary>创建组合根服务、全局 Canvas 并启动设置加载和开始菜单导航。</summary>
        private void Start()
        {
            _startupLifetime = new CancellationTokenSource();
            var clock = new SystemClock();
            IGameLogger logger = UnityDebugLogger.Instance;
            var eventBus = new DomainEventBus(logger);
            _saveRepository = SaveRepositoryFactory.CreateDefault(clock, logger);
            _saveRepositoryLifetime = _saveRepository as IDisposable;

            var localization = new DefaultLocalizationService();
            _localizationService = localization;
            _audioRoot = new GameObject("GlobalAudio");
            DontDestroyOnLoad(_audioRoot);
            var musicSource = _audioRoot.AddComponent<AudioSource>();
            var sfxSource = _audioRoot.AddComponent<AudioSource>();
            AudioMixerGroup? musicGroup = FindMixerGroup(audioMixer, "Music");
            AudioMixerGroup? sfxGroup = FindMixerGroup(audioMixer, "SFX");
            musicSource.outputAudioMixerGroup = musicGroup;
            sfxSource.outputAudioMixerGroup = sfxGroup;
            var audio = new UnityAudioService(audioMixer, null, musicSource, sfxSource);
            _settingsService = new SettingsService(_saveRepository, audio, localization,
                new UnityWindowSettingsApplier(), eventBus);
            var profileLifecycle = new ProfileLifecycleService(_saveRepository, clock);

            var flowService = new GameFlowService(
                new UnitySceneLoader(),
                clock,
                logger,
                eventBus,
                startMenuSceneName
            );
            _flowService = flowService;

            _runtimeServices = new GameRuntimeServices(flowService, _settingsService, localization,
                audio, profileLifecycle, new EmptyProgressQuery(), clock,
                _saveRepository.SaveProfileAsync);
            _globalUiRoot = new GameObject("GlobalUi");
            DontDestroyOnLoad(_globalUiRoot);
            _globalCanvasLayer = _globalUiRoot.AddComponent<GlobalCanvasLayer>();
            _globalCanvasLayer.Initialize(_runtimeServices, contentAssetRegistry);
            SceneManager.sceneLoaded += OnSceneLoaded;

            _ = RunStartupAsync(flowService, _settingsService, localization, _startupLifetime.Token);
        }

        /// <summary>执行设置初始化和启动导航，记录不可恢复的启动异常。</summary>
        /// <param name="flowService">应用流程服务。</param>
        /// <param name="settingsService">设置服务。</param>
        /// <param name="localizationService">负责预加载 String Table 的本地化服务。</param>
        /// <param name="cancellationToken">启动生命周期令牌。</param>
        private static async Task RunStartupAsync(GameFlowService flowService,
            SettingsService settingsService, DefaultLocalizationService localizationService,
            CancellationToken cancellationToken)
        {
            try
            {
                Result localizationResult = await localizationService.InitializeAsync(
                    cancellationToken);
                if (!localizationResult.IsSuccess)
                {
                    Debug.LogError("Localization initialization failed: " +
                        localizationResult.Message);
                    return;
                }

                Result settingsResult = await settingsService.InitializeAsync(cancellationToken);
                if (!settingsResult.IsSuccess)
                    Debug.LogWarning("Settings initialization failed: " + settingsResult.Message);

                await flowService.EnterStartMenuAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 应用销毁时取消是正常生命周期行为。
            }
            catch (Exception ex)
            {
                // 启动导航失败不能静默丢弃：黑屏时这是唯一排查线索
                Debug.LogException(ex);
            }
        }

        /// <summary>功能场景加载后安装对应的 View/Presenter。</summary>
        /// <param name="scene">刚加载的场景。</param>
        /// <param name="mode">场景加载模式。</param>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_runtimeServices != null && _globalCanvasLayer != null)
                SceneUiInstaller.Install(scene, _runtimeServices, _globalCanvasLayer,
                    contentAssetRegistry);
        }

        /// <summary>销毁组合根时释放服务、取消启动并清理全局 UI。</summary>
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _startupLifetime?.Cancel();
            _startupLifetime?.Dispose();
            _flowService?.Dispose();
            _settingsService?.Dispose();
            _localizationService?.Dispose();
            _saveRepositoryLifetime?.Dispose();
            if (_audioRoot != null)
                Destroy(_audioRoot);
            if (_globalUiRoot != null)
                Destroy(_globalUiRoot);
            _globalCanvasLayer = null;
            _runtimeServices = null;
            _localizationService = null;
        }

        /// <summary>从显式引用的 Mixer 中查找稳定名称的输出组。</summary>
        /// <param name="mixer">可选 AudioMixer。</param>
        /// <param name="groupName">目标组名称。</param>
        /// <returns>找到的组；Mixer 或组缺失时返回 null。</returns>
        private static AudioMixerGroup? FindMixerGroup(AudioMixer? mixer, string groupName)
        {
            if (mixer == null || string.IsNullOrWhiteSpace(groupName))
                return null;

            AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }
    }
}
