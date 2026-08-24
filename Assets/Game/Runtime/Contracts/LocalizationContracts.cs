#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;

namespace Game.Contracts
{
    /// <summary>
    /// 本地化服务：包装 Unity Localization，防止业务接口泄漏包专有类型.
    /// </summary>
    /// <remarks>
    /// 对应技术设计文档 §12.2/§12.6：切换语言后由 <see cref="SettingsAppliedEvent"/> 驱动当前页面重新查询 ViewModel.
    /// </remarks>
    public interface ILocalizationService
    {
        /// <summary>当前生效的 Locale 代码（如 zh-CN）.</summary>
        string CurrentLocaleCode { get; }

        /// <summary>异步切换语言，返回是否成功.</summary>
        /// <param name="localeCode">目标语言代码</param>
        /// <param name="ct">取消令牌</param>
        Task<Result> SetLocaleAsync(string localeCode, CancellationToken ct);

        /// <summary>同步读取本地化文本（Key 缺失时回退 Key 本身）.</summary>
        /// <param name="key">本地化 Key</param>
        /// <param name="arguments">格式化参数（可选）</param>
        string Resolve(string key, params object[] arguments);
    }

    /// <summary>
    /// 本地化 Key 常量：UI 与基础内容专用.
    /// </summary>
    /// <remarks>
    /// 稳定内容 ID、存档枚举和 Steam API Name 不随语言变化，只有玩家可见文本走此通道.
    /// </remarks>
    public static class LocalizationKeys
    {
        public const string StartMenuTitle = "ui.start_menu.title";
        public const string StartMenuStart = "ui.start_menu.start";
        public const string StartMenuContinue = "ui.start_menu.continue";
        public const string StartMenuSettings = "ui.start_menu.settings";
        public const string StartMenuQuit = "ui.start_menu.quit";

        public const string SettingsTitle = "ui.settings.title";
        public const string SettingsMasterVolume = "ui.settings.master_volume";
        public const string SettingsMusicVolume = "ui.settings.music_volume";
        public const string SettingsSfxVolume = "ui.settings.sfx_volume";
        public const string SettingsLanguage = "ui.settings.language";
        public const string SettingsFullscreen = "ui.settings.fullscreen";
        public const string SettingsResolution = "ui.settings.resolution";
        public const string SettingsApply = "ui.settings.apply";
        public const string SettingsClose = "ui.settings.close";
    }

    /// <summary>
    /// 本地化字符串表条目：供编辑器工具与运行时使用.
    /// </summary>
    public sealed class LocalizedStringEntry
    {
        public string Key { get; }
        public string Value { get; }

        public LocalizedStringEntry(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}
