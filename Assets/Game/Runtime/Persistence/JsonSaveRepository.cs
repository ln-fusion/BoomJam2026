using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Foundation;
using Newtonsoft.Json;

namespace Game.Persistence
{
    public sealed class JsonSaveRepository : ISaveRepository, IDisposable
    {
        private readonly string _settingsPath;
        private readonly string _profilePath;
        private readonly IAtomicFileWriter _writer;
        private readonly IClock _clock;
        private readonly IGameLogger _logger;
        private readonly string _deviceId;
        private readonly SemaphoreSlim _settingsGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _profileGate = new SemaphoreSlim(1, 1);
        private bool _disposed;

        public JsonSaveRepository(string saveDirectory, IClock clock = null,
            IGameLogger logger = null, string deviceId = null)
            : this(saveDirectory, new AtomicFileWriter(), clock, logger, deviceId)
        {
        }

        internal JsonSaveRepository(string saveDirectory, IAtomicFileWriter writer,
            IClock clock = null, IGameLogger logger = null, string deviceId = null)
        {
            if (string.IsNullOrWhiteSpace(saveDirectory))
                throw new ArgumentException("A save directory is required.", nameof(saveDirectory));

            _settingsPath = Path.Combine(saveDirectory, "settings.json");
            _profilePath = Path.Combine(saveDirectory, "profile.json");
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _clock = clock ?? new SystemClock();
            _logger = logger ?? NullLogger.Instance;
            _deviceId = string.IsNullOrWhiteSpace(deviceId) ? Environment.MachineName : deviceId;
        }

        public async Task<LoadResult<SettingsSave>> LoadSettingsAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await _settingsGate.WaitAsync(cancellationToken);
            try
            {
                return Load(_settingsPath, SettingsSave.CreateDefault,
                    SaveDataValidator.Validate, false);
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        public async Task<LoadResult<ProfileSave>> LoadProfileAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await _profileGate.WaitAsync(cancellationToken);
            try
            {
                return Load<ProfileSave>(_profilePath, () => null,
                    SaveDataValidator.Validate, true);
            }
            finally
            {
                _profileGate.Release();
            }
        }

        public async Task<SaveResult> SaveSettingsAsync(SettingsSave data,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ErrorCode validation = SaveDataValidator.Validate(data);
            if (validation != ErrorCode.None)
                return SaveResult.Failure(validation, "Settings data is invalid.");

            await _settingsGate.WaitAsync(cancellationToken);
            try
            {
                return await SaveAsync(_settingsPath, data, SaveDataValidator.Validate,
                    cancellationToken);
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        public async Task<SaveResult> SaveProfileAsync(ProfileSave data, SaveReason reason,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (data == null)
                return SaveResult.Failure(SaveErrors.Invalid, "Profile data is required.");

            await _profileGate.WaitAsync(cancellationToken);
            try
            {
                ProfileSave snapshot = Clone(data);
                snapshot.Revision++;
                snapshot.LastModifiedAtUtc = _clock.UtcNow.ToString("O");
                snapshot.LastWriterDeviceId = _deviceId;

                ErrorCode validation = SaveDataValidator.Validate(snapshot);
                if (validation != ErrorCode.None)
                    return SaveResult.Failure(validation, "Profile data is invalid.");

                SaveResult result = await SaveAsync(_profilePath, snapshot,
                    SaveDataValidator.Validate, cancellationToken);
                if (result.IsSuccess)
                {
                    data.Revision = snapshot.Revision;
                    data.LastModifiedAtUtc = snapshot.LastModifiedAtUtc;
                    data.LastWriterDeviceId = snapshot.LastWriterDeviceId;
                }

                return result;
            }
            finally
            {
                _profileGate.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _settingsGate.Dispose();
            _profileGate.Dispose();
        }

        private LoadResult<T> Load<T>(string primaryPath, Func<T> createDefault,
            Func<T, ErrorCode> validate, bool missingIsNotFound)
        {
            ReadStatus primaryStatus = TryRead(primaryPath, validate, out T primary,
                out ErrorCode primaryError);
            if (primaryStatus == ReadStatus.Valid)
                return new LoadResult<T>(primary, LoadSource.Primary, ErrorCode.None);

            string backupPath = Path.ChangeExtension(primaryPath, ".bak");
            ReadStatus backupStatus = TryRead(backupPath, validate, out T backup,
                out ErrorCode backupError);
            if (backupStatus == ReadStatus.Valid)
            {
                ErrorCode warning = primaryStatus == ReadStatus.Missing
                    ? ErrorCode.None
                    : primaryError;
                return new LoadResult<T>(backup, LoadSource.Backup, warning);
            }

            bool noFiles = primaryStatus == ReadStatus.Missing &&
                           backupStatus == ReadStatus.Missing;
            ErrorCode recoveryError = noFiles
                ? ErrorCode.None
                : primaryError != ErrorCode.None ? primaryError : backupError;
            LoadSource source = missingIsNotFound && noFiles
                ? LoadSource.NotFound
                : LoadSource.Default;
            return new LoadResult<T>(createDefault(), source, recoveryError);
        }

        private ReadStatus TryRead<T>(string path, Func<T, ErrorCode> validate,
            out T data, out ErrorCode error)
        {
            data = default;
            error = ErrorCode.None;
            if (!File.Exists(path))
                return ReadStatus.Missing;

            try
            {
                string json = File.ReadAllText(path);
                data = JsonConvert.DeserializeObject<T>(json);
                error = validate(data);
                return error == ErrorCode.None ? ReadStatus.Valid : ReadStatus.Invalid;
            }
            catch (JsonException exception)
            {
                error = SaveErrors.Corrupt;
                LogReadFailure(path, error, exception);
                return ReadStatus.Invalid;
            }
            catch (IOException exception)
            {
                error = SaveErrors.Io;
                LogReadFailure(path, error, exception);
                return ReadStatus.Invalid;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = SaveErrors.Io;
                LogReadFailure(path, error, exception);
                return ReadStatus.Invalid;
            }
        }

        private async Task<SaveResult> SaveAsync<T>(string path, T data,
            Func<T, ErrorCode> validate, CancellationToken cancellationToken)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                await _writer.WriteAsync(path, json, temporaryPath =>
                {
                    try
                    {
                        T roundTrip = JsonConvert.DeserializeObject<T>(
                            File.ReadAllText(temporaryPath));
                        return validate(roundTrip) == ErrorCode.None;
                    }
                    catch (JsonException)
                    {
                        return false;
                    }
                }, cancellationToken);
                return SaveResult.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException ||
                                              exception is UnauthorizedAccessException ||
                                              exception is InvalidDataException)
            {
                _logger.LogError(LogContext.Empty.With("fileName", Path.GetFileName(path)),
                    $"Saving a local file failed: {exception}");
                return SaveResult.Failure(SaveErrors.Io, "The save file could not be written.");
            }
        }

        private static ProfileSave Clone(ProfileSave data)
        {
            string json = JsonConvert.SerializeObject(data);
            return JsonConvert.DeserializeObject<ProfileSave>(json);
        }

        private void LogReadFailure(string path, ErrorCode error, Exception exception)
        {
            _logger.LogWarning(LogContext.Empty.With("fileName", Path.GetFileName(path))
                    .With("errorCode", error.ToString()),
                $"Loading a local file failed: {exception}");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(JsonSaveRepository));
        }

        private enum ReadStatus
        {
            Missing,
            Valid,
            Invalid
        }
    }
}
