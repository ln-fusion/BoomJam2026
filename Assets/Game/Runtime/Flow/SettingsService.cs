#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Foundation;

namespace Game.Flow
{
    /// <summary>
    /// 设置应用器：把设置值应用到声音/语言/窗口（实现方在 Presentation 或 Platform 层）.
    /// </summary>
    /// <remarks>
    /// 设置服务只负责校验/持久化/发布事件，具体硬件和系统 API 调用由实现方承担.
    /// </remarks>
    public interface ISettingsApplier
    {
        /// <summary>应用音量（线性值）.</summary>
        void ApplyVolumes(float master, float music, float sfx);

        /// <summary>应用窗口模式与分辨率.</summary>
        void ApplyWindow(bool fullscreen, int width, int height);

        /// <summary>应用语言；返回 false 表示切换失败.</summary>
        bool ApplyLanguage(string languageCode);
    }

    /// <summary>
    /// 平台设置应用器：使用 UnityEngine 的 Screen/AudioMixer 等 API.
    /// </summary>
    public sealed class UnitySettingsApplier : ISettingsApplier
    {
        /// <summary>应用音量：线性转 dB，0 表示静音.</summary>
        public void ApplyVolumes(float master, float music, float sfx)
        {
            UnityEngine.AudioListener.volume = master;
            // 简化：只应用主音量到 AudioListener；Music/SFX 在 Audio 模块落地后接 AudioMixer
        }

        /// <summary>应用窗口模式与分辨率.</summary>
        public void ApplyWindow(bool fullscreen, int width, int height)
        {
            UnityEngine.Screen.SetResolution(width, height, fullscreen);
        }

        /// <summary>应用语言：本实现仅记录，实际切换由 Localization 服务完成.</summary>
        public bool ApplyLanguage(string languageCode)
        {
            // TODO(preca): Localization 模块落地后接入 SetLocaleAsync
            return true;
        }
    }

    /// <summary>
    /// 设置服务实现：校验 -> 应用 -> 保存 -> 发布事件（技术设计文档 §12.4）.
    /// </summary>
    public sealed class SettingsService : ISettingsService, IDisposable
    {
        private static readonly ErrorCode InvalidDraft = new ErrorCode(ErrorCategory.Validation, "settings.invalid");
        private static readonly ErrorCode SaveFailed = new ErrorCode(ErrorCategory.SaveIo, "settings.save_failed");

        private readonly ISaveRepository _repository;
        private readonly ISettingsApplier _applier;
        private readonly IDomainEventBus _eventBus;
        private readonly IGameLogger _logger;
        private readonly ILocalizationService? _localization;

        private SettingsSnapshot _current;

        /// <summary>当前生效设置快照.</summary>
        public SettingsSnapshot Current => _current;

        /// <summary>
        /// 构造函数：注入存档/应用器/事件总线依赖.
        /// </summary>
        /// <param name="repository">保存仓库</param>
        /// <param name="applier">设置应用器</param>
        /// <param name="eventBus">事件总线</param>
        /// <param name="clock">时钟</param>
        /// <param name="logger">日志</param>
        /// <param name="localization">本地化服务（可选，提供语言切换）</param>
        public SettingsService(
            ISaveRepository repository,
            ISettingsApplier applier,
            IDomainEventBus eventBus,
            IClock? clock = null,
            IGameLogger? logger = null,
            ILocalizationService? localization = null
        )
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? NullLogger.Instance;
            _localization = localization;
            _current = SettingsSnapshot.Default();
        }

        /// <summary>从持久层加载并应用设置（启动时调用）.</summary>
        public async Task<Result> LoadAsync(CancellationToken cancellationToken)
        {
            LoadResult<SettingsSave> load = await _repository.LoadSettingsAsync(cancellationToken);
            SettingsSave data = load.Data ?? SettingsSave.CreateDefault();

            var snapshot = new SettingsSnapshot(
                data.LanguageCode,
                data.MasterVolume,
                data.MusicVolume,
                data.SfxVolume,
                data.Fullscreen,
                data.ResolutionWidth,
                data.ResolutionHeight
            );
            _current = snapshot;
            ApplyToSystem();

            if (load.HasRecoveryWarning)
            {
                _logger.LogWarning(LogContext.Empty, $"[SettingsService] 设置从备份/默认恢复:{load.Source}");
            }

            return Result.Success();
        }

        /// <summary>应用设置草稿（校验 -> 应用到系统 -> 保存 -> 发布事件）.</summary>
        public async Task<Result> ApplyAsync(SettingsDraft draft, CancellationToken ct)
        {
            if (draft == null)
                return Result.Failure(InvalidDraft, "Settings draft is required.");

            var validation = Validate(draft);
            if (validation.IsSuccess == false)
                return validation;

            var snapshot = new SettingsSnapshot(
                draft.LanguageCode,
                draft.MasterVolume,
                draft.MusicVolume,
                draft.SfxVolume,
                draft.Fullscreen,
                draft.ResolutionWidth,
                draft.ResolutionHeight
            );

            // 1. 应用到系统（音频/窗口/语言）——先应用后保存，失败时回滚到旧快照
            ApplyToSystem(snapshot);

            // 1.5 语言切换（若注入本地化服务）：失败则整体回滚
            if (
                _localization != null
                && !string.Equals(
                    _localization.CurrentLocaleCode,
                    snapshot.LanguageCode,
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                Result localeResult = await _localization.SetLocaleAsync(snapshot.LanguageCode, ct);
                if (!localeResult.IsSuccess)
                {
                    ApplyToSystem(_current);
                    return localeResult;
                }
            }

            // 2. 持久化
            SaveResult saved = await _repository.SaveSettingsAsync(ToSaveData(snapshot), ct);
            if (!saved.IsSuccess)
            {
                // 回滚：应用失败或保存失败，恢复到旧设置
                ApplyToSystem(_current);
                return Result.Failure(SaveFailed, saved.Message);
            }

            // 3. 更新当前快照并通知 UI
            _current = snapshot;
            _eventBus.Publish(new SettingsAppliedEvent(snapshot));

            return Result.Success();
        }

        /// <summary>恢复默认设置并应用.</summary>
        public async Task<Result> RestoreDefaultsAsync(CancellationToken ct)
        {
            var defaultSnapshot = SettingsSnapshot.Default();
            var draft = SettingsDraft.FromSnapshot(defaultSnapshot);
            return await ApplyAsync(draft, ct);
        }

        private static Result Validate(SettingsDraft draft)
        {
            if (string.IsNullOrWhiteSpace(draft.LanguageCode))
                return Result.Failure(InvalidDraft, "Language code is required.");
            if (!IsUnit(draft.MasterVolume) || !IsUnit(draft.MusicVolume) || !IsUnit(draft.SfxVolume))
                return Result.Failure(InvalidDraft, "Volumes must be in [0,1].");
            if (draft.ResolutionWidth <= 0 || draft.ResolutionHeight <= 0)
                return Result.Failure(InvalidDraft, "Resolution must be positive.");

            return Result.Success();
        }

        private void ApplyToSystem(SettingsSnapshot? snapshot = null)
        {
            var value = snapshot ?? _current;
            _applier.ApplyVolumes(value.MasterVolume, value.MusicVolume, value.SfxVolume);
            _applier.ApplyWindow(value.Fullscreen, value.ResolutionWidth, value.ResolutionHeight);
            _applier.ApplyLanguage(value.LanguageCode);
        }

        private static SettingsSave ToSaveData(SettingsSnapshot snapshot)
        {
            return new SettingsSave
            {
                SchemaVersion = SettingsSave.CurrentSchemaVersion,
                LanguageCode = snapshot.LanguageCode,
                MasterVolume = snapshot.MasterVolume,
                MusicVolume = snapshot.MusicVolume,
                SfxVolume = snapshot.SfxVolume,
                Fullscreen = snapshot.Fullscreen,
                ResolutionWidth = snapshot.ResolutionWidth,
                ResolutionHeight = snapshot.ResolutionHeight,
            };
        }

        private static bool IsUnit(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }

        public void Dispose() { }
    }
}
