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
    /// <summary>
    /// 基于 JSON 文件的本地存档仓储，分别管理 settings.json 与 profile.json。
    /// </summary>
    /// <remarks>
    /// 设置和档案使用独立并发门闩；写入通过原子临时文件流程完成，读取失败时按主文件、备份和默认值顺序恢复。
    /// </remarks>
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

        /// <summary>创建使用默认原子写入器的 JSON 存档仓储。</summary>
        /// <param name="saveDirectory">存档目录。</param>
        /// <param name="clock">用于档案修订时间的时钟。</param>
        /// <param name="logger">用于记录读写恢复信息的日志记录器。</param>
        /// <param name="deviceId">写入档案时记录的设备标识。</param>
        public JsonSaveRepository(string saveDirectory, IClock clock = null,
            IGameLogger logger = null, string deviceId = null)
            : this(saveDirectory, new AtomicFileWriter(), clock, logger, deviceId)
        {
        }

        /// <summary>创建可注入底层写入器的 JSON 存档仓储。</summary>
        /// <param name="saveDirectory">存档目录。</param>
        /// <param name="writer">原子文件写入器。</param>
        /// <param name="clock">用于档案修订时间的时钟。</param>
        /// <param name="logger">用于记录读写恢复信息的日志记录器。</param>
        /// <param name="deviceId">写入档案时记录的设备标识。</param>
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

        /// <summary>加载设置文件，损坏时尝试备份，仍失败则返回默认设置。</summary>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>设置加载结果。</returns>
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

        /// <summary>加载玩家档案，缺失时返回 NotFound，损坏时尝试备份。</summary>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>档案加载结果。</returns>
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

        /// <summary>校验并原子保存设置文件。</summary>
        /// <param name="data">待保存的设置数据。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>写入结果。</returns>
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

        /// <summary>
        /// 克隆、递增修订号并原子保存玩家档案；只有写入成功才回写内存对象的修订字段。
        /// </summary>
        /// <param name="data">待保存的玩家档案。</param>
        /// <param name="reason">触发写入的业务原因。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>写入结果。</returns>
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

        /// <summary>释放设置和档案写入门闩；重复调用安全。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _settingsGate.Dispose();
            _profileGate.Dispose();
        }

        /// <summary>按主文件、备份文件和默认值顺序加载一类存档。</summary>
        /// <typeparam name="T">存档 DTO 类型。</typeparam>
        /// <param name="primaryPath">主文件路径。</param>
        /// <param name="createDefault">创建默认数据的函数。</param>
        /// <param name="validate">数据校验函数。</param>
        /// <param name="missingIsNotFound">没有文件时是否返回 NotFound 来源。</param>
        /// <returns>加载结果。</returns>
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

        /// <summary>读取并校验一个 JSON 存档文件。</summary>
        /// <typeparam name="T">存档 DTO 类型。</typeparam>
        /// <param name="path">文件路径。</param>
        /// <param name="validate">数据校验函数。</param>
        /// <param name="data">读取出的数据。</param>
        /// <param name="error">读取或校验错误码。</param>
        /// <returns>文件读取状态。</returns>
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

        /// <summary>序列化、原子写入并回读校验一类存档。</summary>
        /// <typeparam name="T">存档 DTO 类型。</typeparam>
        /// <param name="path">目标文件路径。</param>
        /// <param name="data">待保存数据。</param>
        /// <param name="validate">数据校验函数。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>写入结果。</returns>
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

        /// <summary>通过 JSON 往返创建玩家档案的独立快照。</summary>
        /// <param name="data">原始档案。</param>
        /// <returns>深拷贝后的档案。</returns>
        private static ProfileSave Clone(ProfileSave data)
        {
            string json = JsonConvert.SerializeObject(data);
            return JsonConvert.DeserializeObject<ProfileSave>(json);
        }

        /// <summary>记录一次存档读取失败及其恢复错误码。</summary>
        /// <param name="path">失败文件路径。</param>
        /// <param name="error">恢复错误码。</param>
        /// <param name="exception">底层异常。</param>
        private void LogReadFailure(string path, ErrorCode error, Exception exception)
        {
            _logger.LogWarning(LogContext.Empty.With("fileName", Path.GetFileName(path))
                    .With("errorCode", error.ToString()),
                $"Loading a local file failed: {exception}");
        }

        /// <summary>在仓储已经释放时抛出对象已释放异常。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(JsonSaveRepository));
        }

        /// <summary>单个存档文件的读取状态。</summary>
        private enum ReadStatus
        {
            /// <summary>文件不存在。</summary>
            Missing,
            /// <summary>文件存在且通过反序列化与校验。</summary>
            Valid,
            /// <summary>文件存在但损坏或未通过校验。</summary>
            Invalid
        }
    }
}
