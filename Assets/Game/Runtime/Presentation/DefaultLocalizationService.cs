using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Foundation;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Game.Presentation
{
    /// <summary>
    /// Unity Localization String Table 适配器：运行时正文来自项目资源，不在 C# 中保存翻译文本。
    /// </summary>
    /// <remarks>
    /// 服务在初始化阶段加载指定集合的全部项目 Locale，并缓存已加载的表项；业务层只依赖
    /// <see cref="ILocalizationService"/>，不会泄漏 Unity Localization 专有类型。默认中文表缺少
    /// 必需 Key 时初始化失败，其他 Locale 的缺失条目则回退到默认中文表。
    /// </remarks>
    public sealed class DefaultLocalizationService : ILocalizationService, IDisposable
    {
        private readonly LocalizationSettings _settings;
        private readonly string _tableName;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly object _sync = new object();
        private readonly HashSet<string> _missingKeyWarnings =
            new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, Dictionary<string, StringTableEntry>> _tables =
            new Dictionary<string, Dictionary<string, StringTableEntry>>(
                StringComparer.OrdinalIgnoreCase);
        private string _currentLocaleCode = DefaultLocale;
        private bool _initialized;
        private bool _disposed;
        private bool _resourcesDisposed;
        private int _activeOperations;
        private bool _initializationWarningLogged;

        /// <summary>默认 Locale 代码。</summary>
        public const string DefaultLocale = "zh-CN";

        /// <summary>当前 UI 文本 String Table 集合名称。</summary>
        public const string DefaultTableName = "UI";

        /// <summary>Locale 切换成功后触发。</summary>
        public event Action<string> LocaleChanged;

        /// <summary>当前 Locale 代码。</summary>
        public string CurrentLocaleCode
        {
            get
            {
                lock (_sync)
                    return _currentLocaleCode;
            }
        }

        /// <summary>表资源是否已经成功初始化。</summary>
        public bool IsInitialized
        {
            get
            {
                lock (_sync)
                    return _initialized;
            }
        }

        /// <summary>
        /// 使用项目激活的 Localization Settings 创建服务。
        /// </summary>
        public DefaultLocalizationService()
            : this(LocalizationSettings.Instance, DefaultTableName)
        {
        }

        /// <summary>创建指定 Localization Settings 和表集合的服务。</summary>
        /// <param name="settings">项目激活的 Localization Settings 资源。</param>
        /// <param name="tableName">String Table 集合名称。</param>
        public DefaultLocalizationService(LocalizationSettings settings, string tableName)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _tableName = string.IsNullOrWhiteSpace(tableName)
                ? throw new ArgumentException("A String Table name is required.", nameof(tableName))
                : tableName.Trim();
        }

        /// <summary>
        /// 初始化 Addressables、Locale 和 String Table；重复调用会复用已完成状态。
        /// </summary>
        /// <param name="cancellationToken">初始化取消令牌。</param>
        /// <returns>初始化结果；默认 Locale 或必需 Key 缺失时返回失败。</returns>
        public async Task<Result> InitializeAsync(CancellationToken cancellationToken)
        {
            BeginOperation();
            try
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                           cancellationToken, _lifetime.Token))
                {
                    CancellationToken token = linked.Token;
                    await _gate.WaitAsync(token);
                    try
                    {
                        if (_initialized)
                            return Result.Success();

                        Result result = await InitializeCoreAsync(token);
                        if (result.IsSuccess)
                        {
                            lock (_sync)
                                _initialized = true;
                        }

                        return result;
                    }
                    finally
                    {
                        _gate.Release();
                    }
                }
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>切换 Locale，并在成功后通知订阅页面重新读取文本。</summary>
        /// <param name="localeCode">目标 BCP-47 Locale 代码。</param>
        /// <param name="cancellationToken">切换取消令牌。</param>
        /// <returns>切换结果；未配置的 Locale 返回失败。</returns>
        public async Task<Result> SetLocaleAsync(string localeCode,
            CancellationToken cancellationToken)
        {
            BeginOperation();
            try
            {
                Result initialized = await InitializeAsync(cancellationToken);
                if (!initialized.IsSuccess)
                    return initialized;

                if (string.IsNullOrWhiteSpace(localeCode))
                    return Result.Failure(ErrorCode.LocaleUnsupported,
                        "Locale code is required.");

                string normalizedCode = localeCode.Trim();
                Locale locale = _settings.GetAvailableLocales().GetLocale(normalizedCode);
                if (locale == null || !HasLocale(normalizedCode))
                {
                    return Result.Failure(ErrorCode.LocaleUnsupported,
                        "Unsupported locale: " + normalizedCode);
                }

                string resolvedCode = locale.Identifier.Code;
                if (string.IsNullOrWhiteSpace(resolvedCode) || !HasLocale(resolvedCode))
                {
                    return Result.Failure(ErrorCode.LocaleUnsupported,
                        "Locale table is not loaded: " + normalizedCode);
                }

                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                           cancellationToken, _lifetime.Token))
                {
                    await _gate.WaitAsync(linked.Token);
                    try
                    {
                        string previousCode;
                        lock (_sync)
                            previousCode = _currentLocaleCode;

                        if (string.Equals(previousCode, resolvedCode,
                                StringComparison.OrdinalIgnoreCase))
                            return Result.Success();

                        _settings.SetSelectedLocale(locale);
                        lock (_sync)
                            _currentLocaleCode = resolvedCode;
                    }
                    finally
                    {
                        _gate.Release();
                    }
                }

                NotifyLocaleChanged(resolvedCode);
                return Result.Success();
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>读取当前 Locale 的文本，缺失时按默认中文表回退，再缺失则返回稳定 Key。</summary>
        /// <param name="key">稳定本地化 Key。</param>
        /// <param name="arguments">传给 String Table 格式化器的参数。</param>
        /// <returns>解析后的文本；服务未初始化或 Key 缺失时返回 Key 本身。</returns>
        public string Get(LocalizationKey key, params object[] arguments)
        {
            ThrowIfDisposed();
            if (key == null)
                return string.Empty;

            StringTableEntry entry = FindEntry(key.Value);
            if (entry == null)
            {
                ReportMissingKey(key.Value);
                return key.Value;
            }

            try
            {
                Locale locale = _settings.GetAvailableLocales().GetLocale(
                    entry.Table.LocaleIdentifier);
                IFormatProvider formatter = locale == null ? null : locale.Formatter;
                IList<object> values = arguments == null || arguments.Length == 0
                    ? null
                    : new List<object>(arguments);
                return entry.GetLocalizedString(formatter, values, null) ?? key.Value;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to format localization key '" + key.Value + "': " +
                    exception.Message);
                return key.Value;
            }
        }

        /// <summary>取消未完成加载，并在并发操作退出后释放共享资源。</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _tables.Clear();
                _initialized = false;
                _missingKeyWarnings.Clear();
                LocaleChanged = null;
            }

            _lifetime.Cancel();
            DisposeResourcesIfReady();
        }

        /// <summary>加载 Unity Localization 资源并构建按 Locale 索引的缓存。</summary>
        /// <param name="cancellationToken">加载取消令牌。</param>
        /// <returns>资源加载和默认表校验结果。</returns>
        private async Task<Result> InitializeCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                await AwaitWithCancellationAsync(_settings.GetInitializationOperation().Task,
                    cancellationToken);

                ILocalesProvider localesProvider = _settings.GetAvailableLocales();
                if (localesProvider == null || localesProvider.Locales == null ||
                    localesProvider.Locales.Count == 0)
                {
                    return Result.Failure(ErrorCode.LocalizationInitializationFailed,
                        "No project Locales are configured.");
                }

                var loadedTables = new Dictionary<string, Dictionary<string, StringTableEntry>>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (Locale locale in localesProvider.Locales)
                {
                    if (locale == null || string.IsNullOrWhiteSpace(locale.Identifier.Code))
                        continue;

                    string localeCode = locale.Identifier.Code;
                    try
                    {
                        StringTable table = await AwaitWithCancellationAsync(
                            _settings.GetStringDatabase().GetTableAsync(_tableName, locale).Task,
                            cancellationToken);
                        if (table == null)
                            throw new InvalidOperationException("String Table is missing.");

                        loadedTables[localeCode] = CopyEntries(table);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        if (string.Equals(localeCode, DefaultLocale,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return Result.Failure(ErrorCode.LocalizationInitializationFailed,
                                "Default Locale table could not be loaded: " + exception.Message);
                        }

                        Debug.LogWarning("Locale table '" + localeCode + "' could not be loaded; " +
                            "the default table will be used as fallback. " + exception.Message);
                        loadedTables[localeCode] =
                            new Dictionary<string, StringTableEntry>(StringComparer.Ordinal);
                    }
                }

                if (!loadedTables.TryGetValue(DefaultLocale, out Dictionary<string, StringTableEntry>
                        defaultTable))
                {
                    return Result.Failure(ErrorCode.LocalizationInitializationFailed,
                        "Default Locale '" + DefaultLocale + "' is not configured.");
                }

                List<string> missingKeys = FindMissingKeys(defaultTable);
                if (missingKeys.Count > 0)
                {
                    return Result.Failure(ErrorCode.LocalizationDataInvalid,
                        "Default Locale is missing keys: " + string.Join(", ", missingKeys));
                }

                lock (_sync)
                {
                    _tables = loadedTables;
                    _currentLocaleCode = DefaultLocale;
                }

                Locale fallbackLocale = localesProvider.GetLocale(DefaultLocale);
                if (fallbackLocale == null)
                {
                    return Result.Failure(ErrorCode.LocalizationInitializationFailed,
                        "Default Locale asset could not be resolved.");
                }

                // The service has a deterministic project default. Persisted user settings are
                // applied immediately afterwards by SettingsService, so a machine/system locale
                // cannot make the first frame depend on the host environment.
                _settings.SetSelectedLocale(fallbackLocale);
                lock (_sync)
                    _currentLocaleCode = DefaultLocale;

                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Result.Failure(ErrorCode.LocalizationInitializationFailed,
                    exception.Message);
            }
        }

        /// <summary>复制表项引用，避免业务层依赖 Unity 表的可变集合。</summary>
        /// <param name="table">已加载的 String Table。</param>
        /// <returns>按稳定 Key 索引的表项。</returns>
        private static Dictionary<string, StringTableEntry> CopyEntries(StringTable table)
        {
            var entries = new Dictionary<string, StringTableEntry>(StringComparer.Ordinal);
            foreach (StringTableEntry entry in table.Values)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                if (entries.ContainsKey(entry.Key))
                    throw new InvalidOperationException("Duplicate localization key: " + entry.Key);

                entries.Add(entry.Key, entry);
            }

            return entries;
        }

        /// <summary>查找默认表中缺少的必需 UI Key。</summary>
        /// <param name="defaultTable">默认中文表。</param>
        /// <returns>缺失 Key 列表。</returns>
        private static List<string> FindMissingKeys(
            IReadOnlyDictionary<string, StringTableEntry> defaultTable)
        {
            var missing = new List<string>();
            foreach (string key in UiTextKeys.All)
            {
                if (!defaultTable.TryGetValue(key, out StringTableEntry entry) ||
                    !HasLocalizedValue(entry))
                    missing.Add(key);
            }

            return missing;
        }

        /// <summary>按当前 Locale 和默认 Locale 查找表项。</summary>
        /// <param name="key">稳定 Key。</param>
        /// <returns>找到的表项；不存在时返回 null。</returns>
        private StringTableEntry FindEntry(string key)
        {
            lock (_sync)
            {
                if (!_initialized && !_initializationWarningLogged)
                {
                    _initializationWarningLogged = true;
                    Debug.LogWarning("Localization was queried before its String Tables were initialized.");
                }

                if (_tables.TryGetValue(_currentLocaleCode, out Dictionary<string, StringTableEntry>
                        current) && current.TryGetValue(key, out StringTableEntry entry) &&
                    HasLocalizedValue(entry))
                    return entry;

                return _tables.TryGetValue(DefaultLocale,
                           out Dictionary<string, StringTableEntry> fallback) &&
                       fallback.TryGetValue(key, out StringTableEntry fallbackEntry) &&
                       HasLocalizedValue(fallbackEntry)
                    ? fallbackEntry
                    : null;
            }
        }

        /// <summary>判断表项是否包含可供 UI 使用的正文。</summary>
        /// <param name="entry">待检查表项。</param>
        /// <returns>包含非空正文返回 true。</returns>
        private static bool HasLocalizedValue(StringTableEntry entry)
        {
            return entry != null && !string.IsNullOrWhiteSpace(entry.Value);
        }

        /// <summary>判断缓存中是否存在指定 Locale。</summary>
        /// <param name="localeCode">Locale 代码。</param>
        /// <returns>存在返回 true。</returns>
        private bool HasLocale(string localeCode)
        {
            lock (_sync)
                return _tables.ContainsKey(localeCode);
        }

        /// <summary>只对每个缺失 Key 记录一次诊断警告。</summary>
        /// <param name="key">缺失的稳定 Key。</param>
        private void ReportMissingKey(string key)
        {
            lock (_sync)
            {
                if (!_missingKeyWarnings.Add(key))
                    return;
            }

            Debug.LogWarning("Missing localization key: " + key);
        }

        /// <summary>逐个调用订阅者并隔离单个页面刷新异常。</summary>
        /// <param name="localeCode">新的 Locale 代码。</param>
        private void NotifyLocaleChanged(string localeCode)
        {
            Action<string> handlers = LocaleChanged;
            if (handlers == null)
                return;

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<string>)handler)(localeCode);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>等待 Addressables 任务并响应调用方取消。</summary>
        /// <typeparam name="T">异步结果类型。</typeparam>
        /// <param name="operation">Addressables 异步任务。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步结果。</returns>
        private static async Task<T> AwaitWithCancellationAsync<T>(Task<T> operation,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return await operation;

            var cancellation = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => cancellation.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(operation, cancellation.Task);
                if (completed == cancellation.Task)
                    throw new OperationCanceledException(cancellationToken);

                return await operation;
            }
        }

        /// <summary>登记一个可能使用并发资源的异步操作。</summary>
        private void BeginOperation()
        {
            lock (_sync)
            {
                ThrowIfDisposedLocked();
                _activeOperations++;
            }
        }

        /// <summary>结束异步操作，并在没有活动操作后释放共享资源。</summary>
        private void EndOperation()
        {
            lock (_sync)
            {
                if (_activeOperations > 0)
                    _activeOperations--;
            }

            DisposeResourcesIfReady();
        }

        /// <summary>在没有活动操作时释放取消源和并发控制器。</summary>
        private void DisposeResourcesIfReady()
        {
            lock (_sync)
            {
                if (!_disposed || _activeOperations != 0 || _resourcesDisposed)
                    return;

                _resourcesDisposed = true;
            }

            _gate.Dispose();
            _lifetime.Dispose();
        }

        /// <summary>检查服务是否已经释放。</summary>
        /// <exception cref="ObjectDisposedException">服务已释放时抛出。</exception>
        private void ThrowIfDisposed()
        {
            lock (_sync)
                ThrowIfDisposedLocked();
        }

        /// <summary>在已持有状态锁时检查服务是否已经释放。</summary>
        private void ThrowIfDisposedLocked()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DefaultLocalizationService));
        }
    }

    /// <summary>运行时 UI 使用的稳定本地化 Key 常量。</summary>
    public static class UiTextKeys
    {
        /// <summary>游戏标题。</summary>
        public const string GameTitle = "ui.game_title";
        /// <summary>开始游戏按钮。</summary>
        public const string StartGame = "ui.start_game";
        /// <summary>继续游戏按钮。</summary>
        public const string ContinueGame = "ui.continue_game";
        /// <summary>设置按钮。</summary>
        public const string Settings = "ui.settings";
        /// <summary>退出按钮。</summary>
        public const string Quit = "ui.quit";
        /// <summary>设置标题。</summary>
        public const string SettingsTitle = "ui.settings_title";
        /// <summary>应用按钮。</summary>
        public const string Apply = "ui.apply";
        /// <summary>取消按钮。</summary>
        public const string Cancel = "ui.cancel";
        /// <summary>恢复默认按钮。</summary>
        public const string RestoreDefaults = "ui.restore_defaults";
        /// <summary>主音量标签。</summary>
        public const string MasterVolume = "ui.master_volume";
        /// <summary>音乐音量标签。</summary>
        public const string MusicVolume = "ui.music_volume";
        /// <summary>音效音量标签。</summary>
        public const string SfxVolume = "ui.sfx_volume";
        /// <summary>语言标签。</summary>
        public const string Language = "ui.language";
        /// <summary>分辨率标签。</summary>
        public const string Resolution = "ui.resolution";
        /// <summary>全屏标签。</summary>
        public const string Fullscreen = "ui.fullscreen";
        /// <summary>昵称标签。</summary>
        public const string Nickname = "ui.nickname";
        /// <summary>确认按钮。</summary>
        public const string Confirm = "ui.confirm";
        /// <summary>准备提示。</summary>
        public const string FeedbackReady = "ui.feedback_ready";
        /// <summary>加载提示。</summary>
        public const string FeedbackLoading = "ui.feedback_loading";
        /// <summary>保存提示。</summary>
        public const string FeedbackSaved = "ui.feedback_saved";
        /// <summary>编辑器退出提示。</summary>
        public const string FeedbackQuitEditor = "ui.feedback_quit_editor";
        /// <summary>地图入口。</summary>
        public const string MetaMap = "meta.map";
        /// <summary>档案入口。</summary>
        public const string MetaArchive = "meta.archive";
        /// <summary>人员入口。</summary>
        public const string MetaCharacter = "meta.character";
        /// <summary>休息室入口。</summary>
        public const string MetaLounge = "meta.lounge";
        /// <summary>休息室占位提示。</summary>
        public const string LoungeUnavailable = "meta.lounge_unavailable";
        /// <summary>地图页占位提示。</summary>
        public const string PageMap = "meta.page_map";
        /// <summary>档案页占位提示。</summary>
        public const string PageArchive = "meta.page_archive";
        /// <summary>人员页占位提示。</summary>
        public const string PageCharacter = "meta.page_character";
        /// <summary>昵称为空提示。</summary>
        public const string NicknameRequired = "ui.nickname_required";

        /// <summary>默认中文表必须包含的全部 UI Key。</summary>
        public static IReadOnlyList<string> All { get; } = new ReadOnlyCollection<string>(new[]
        {
            GameTitle, StartGame, ContinueGame, Settings, Quit, SettingsTitle, Apply, Cancel,
            RestoreDefaults, MasterVolume, MusicVolume, SfxVolume, Language, Resolution,
            Fullscreen, Nickname, Confirm, FeedbackReady, FeedbackLoading, FeedbackSaved,
            FeedbackQuitEditor, MetaMap, MetaArchive, MetaCharacter, MetaLounge,
            LoungeUnavailable, PageMap, PageArchive, PageCharacter, NicknameRequired
        });
    }
}
