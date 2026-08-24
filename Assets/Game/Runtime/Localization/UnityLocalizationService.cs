#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Foundation;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Game.Localization
{
    /// <summary>
    /// 基于 Unity Localization 的本地化服务：同步读取字符串表并切换 Locale.
    /// </summary>
    /// <remarks>
    /// 包装包 API，业务层一律通过 <see cref="ILocalizationService"/> 读取文本;
    /// Key 缺失时回退 Key 本身便于 UI 开发期发现遗漏.
    /// </remarks>
    public sealed class UnityLocalizationService : ILocalizationService
    {
        private readonly LocalizedStringTable _table;
        private readonly IDomainEventBus _eventBus;
        private readonly IGameLogger _logger;

        private string _currentLocaleCode = "zh-CN";

        /// <summary>当前生效的 Locale 代码.</summary>
        public string CurrentLocaleCode => _currentLocaleCode;

        /// <summary>
        /// 构造函数：绑定字符串表与事件总线.
        /// </summary>
        /// <param name="table">LocalizedStringTable 引用（编辑器或代码创建）</param>
        /// <param name="eventBus">事件总线；语言切换成功后广播</param>
        /// <param name="logger">日志</param>
        public UnityLocalizationService(
            LocalizedStringTable table,
            IDomainEventBus eventBus,
            IGameLogger? logger = null
        )
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>切换 Locale 并发布语言变化事件.</summary>
        public Task<Result> SetLocaleAsync(string localeCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
                return Task.FromResult(
                    Result.Failure(
                        new ErrorCode(ErrorCategory.Validation, "localization.invalid_locale"),
                        "Locale code is required."
                    )
                );

            ct.ThrowIfCancellationRequested();

            if (string.Equals(localeCode, _currentLocaleCode, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Result.Success());
            }

            // 在 LocalizationSettings 中查找匹配的 Locale 资源
            var locale = FindLocale(localeCode);
            if (locale == null)
            {
                _logger.LogWarning(LogContext.Empty, $"[Localization] Locale 未找到:{localeCode},保持当前");
                return Task.FromResult(
                    Result.Failure(
                        new ErrorCode(ErrorCategory.Validation, "localization.locale_not_found"),
                        $"Locale '{localeCode}' was not found."
                    )
                );
            }

            LocalizationSettings.SelectedLocale = locale;
            _currentLocaleCode = localeCode;

            _eventBus.Publish(new LanguageChangedEvent(localeCode));

            return Task.FromResult(Result.Success());
        }

        /// <summary>同步读取本地化文本；Key 缺失时回退 Key 本身.</summary>
        public string Get(string key, params object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            try
            {
                StringTable table = _table.GetTable();
                if (table == null)
                    return key;

                StringTableEntry entry = table[key];
                if (entry == null)
                    return key;

                return entry.GetLocalizedString(arguments);
            }
            catch (Exception ex)
            {
                _logger.LogError(LogContext.Empty, $"[Localization] 读取失败 key={key}: {ex.Message}");
                return key;
            }
        }

        private static Locale? FindLocale(string localeCode)
        {
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
            {
                if (string.Equals(locale.Identifier.Code, localeCode, StringComparison.OrdinalIgnoreCase))
                {
                    return locale;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 语言切换成功事件：页面 UI 文本刷新入口.
    /// </summary>
    public sealed class LanguageChangedEvent : IDomainEvent
    {
        /// <summary>切换后的语言代码</summary>
        public string LocaleCode { get; }

        public LanguageChangedEvent(string localeCode)
        {
            LocaleCode = localeCode;
        }
    }
}
