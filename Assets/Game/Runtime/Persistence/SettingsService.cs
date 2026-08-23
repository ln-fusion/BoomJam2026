using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Foundation;

namespace Game.Persistence
{
    /// <summary>
    /// 设置用例服务：从 <see cref="ISaveRepository"/> 加载独立设置，按固定顺序应用运行时副作用后再保存。
    /// </summary>
    public sealed class SettingsService : ISettingsService, IDisposable
    {
        private readonly ISaveRepository _repository;
        private readonly IAudioService _audioService;
        private readonly ILocalizationService _localizationService;
        private readonly IWindowSettingsApplier _windowSettingsApplier;
        private readonly IDomainEventBus _eventBus;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private SettingsSnapshot _current = SettingsSnapshot.FromSave(SettingsSave.CreateDefault());
        private bool _initialized;
        private bool _disposed;

        /// <summary>设置已成功保存并应用后的事件。</summary>
        public event Action<SettingsSnapshot> SettingsApplied;

        /// <summary>当前设置快照。</summary>
        public SettingsSnapshot Current => _current;

        /// <summary>创建设置服务。</summary>
        /// <param name="repository">设置/Profile 存档仓储。</param>
        /// <param name="audioService">音频应用端口。</param>
        /// <param name="localizationService">本地化服务。</param>
        /// <param name="windowSettingsApplier">窗口设置应用端口。</param>
        public SettingsService(ISaveRepository repository, IAudioService audioService,
            ILocalizationService localizationService, IWindowSettingsApplier windowSettingsApplier)
            : this(repository, audioService, localizationService, windowSettingsApplier, null)
        {
        }

        /// <summary>创建带领域事件通知的设置服务。</summary>
        /// <param name="repository">设置/Profile 存档仓储。</param>
        /// <param name="audioService">音频应用端口。</param>
        /// <param name="localizationService">本地化服务。</param>
        /// <param name="windowSettingsApplier">窗口设置应用端口。</param>
        /// <param name="eventBus">可选领域事件总线。</param>
        public SettingsService(ISaveRepository repository, IAudioService audioService,
            ILocalizationService localizationService, IWindowSettingsApplier windowSettingsApplier,
            IDomainEventBus eventBus)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _localizationService = localizationService ??
                throw new ArgumentNullException(nameof(localizationService));
            _windowSettingsApplier = windowSettingsApplier ??
                throw new ArgumentNullException(nameof(windowSettingsApplier));
            _eventBus = eventBus;
        }

        /// <summary>
        /// 加载设置并应用到音频、Locale 和窗口；设置损坏时使用仓储提供的默认恢复值。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化结果。</returns>
        public async Task<Result> InitializeAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_initialized)
                    return Result.Success();

                LoadResult<SettingsSave> loaded = await _repository.LoadSettingsAsync(cancellationToken);
                SettingsSave save = loaded.Data ?? SettingsSave.CreateDefault();
                Result applied = await ApplyRuntimeAsync(save, cancellationToken);
                if (!applied.IsSuccess)
                    return applied;

                _current = SettingsSnapshot.FromSave(save);
                _initialized = true;
                return Result.Success();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>校验、应用并原子保存设置草稿。</summary>
        /// <param name="draft">待应用设置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>保存结果。</returns>
        public async Task<Result> ApplyAsync(SettingsDraft draft, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (draft == null)
                return Result.Failure(ErrorCode.SettingsInvalid, "Settings draft is required.");

            Result validation = Validate(draft);
            if (!validation.IsSuccess)
                return validation;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                var save = new SettingsSave
                {
                    LanguageCode = draft.LanguageCode.Trim(),
                    MasterVolume = draft.MasterVolume,
                    MusicVolume = draft.MusicVolume,
                    SfxVolume = draft.SfxVolume,
                    Fullscreen = draft.Fullscreen,
                    ResolutionWidth = draft.ResolutionWidth,
                    ResolutionHeight = draft.ResolutionHeight
                };

                Result applied = await ApplyRuntimeAsync(save, cancellationToken);
                if (!applied.IsSuccess)
                    return applied;

                SaveResult saved = await _repository.SaveSettingsAsync(save, cancellationToken);
                if (!saved.IsSuccess)
                    return Result.Failure(ErrorCode.SettingsSaveFailed, saved.Message);

                _current = SettingsSnapshot.FromSave(save);
                _initialized = true;
                SettingsApplied?.Invoke(_current);
                _eventBus?.Publish(new SettingsAppliedEvent(_current));
                return Result.Success();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>恢复默认设置并保存。</summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>恢复结果。</returns>
        public Task<Result> RestoreDefaultsAsync(CancellationToken cancellationToken)
        {
            return ApplyAsync(SettingsDraft.CreateDefault(), cancellationToken);
        }

        /// <summary>释放设置服务使用的互斥资源。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _gate.Dispose();
        }

        /// <summary>校验设置草稿的范围和必填字段。</summary>
        /// <param name="draft">待校验草稿。</param>
        /// <returns>校验结果。</returns>
        private static Result Validate(SettingsDraft draft)
        {
            if (string.IsNullOrWhiteSpace(draft.LanguageCode) ||
                !IsUnitValue(draft.MasterVolume) || !IsUnitValue(draft.MusicVolume) ||
                !IsUnitValue(draft.SfxVolume) || draft.ResolutionWidth <= 0 ||
                draft.ResolutionHeight <= 0)
            {
                return Result.Failure(ErrorCode.SettingsInvalid, "Settings values are invalid.");
            }

            return Result.Success();
        }

        /// <summary>把设置应用到运行时服务；保存前不改变当前快照。</summary>
        /// <param name="save">待应用 DTO。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>运行时应用结果。</returns>
        private async Task<Result> ApplyRuntimeAsync(SettingsSave save,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result localeResult = await _localizationService.SetLocaleAsync(save.LanguageCode,
                cancellationToken);
            if (!localeResult.IsSuccess)
                return localeResult;

            _audioService.ApplyVolumes(save.MasterVolume, save.MusicVolume, save.SfxVolume);
            Result windowResult = _windowSettingsApplier.Apply(save.ResolutionWidth,
                save.ResolutionHeight, save.Fullscreen);
            return windowResult.IsSuccess
                ? Result.Success()
                : Result.Failure(ErrorCode.WindowApplyFailed, windowResult.Message);
        }

        /// <summary>判断浮点音量是否处于合法线性范围。</summary>
        /// <param name="value">待校验音量。</param>
        /// <returns>合法返回 true。</returns>
        private static bool IsUnitValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }

        /// <summary>检查服务是否已释放。</summary>
        /// <exception cref="ObjectDisposedException">服务已释放时抛出。</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SettingsService));
        }
    }
}
