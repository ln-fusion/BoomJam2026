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
        string Get(string key, params object[] arguments);
    }

    /// <summary>
    /// 本地化 Key 常量：UI 与基础内容专用.
    /// </summary>
    /// <remarks>
    /// 稳定内容 ID、存档枚举和 Steam API Name 不随语言变化，只有玩家可见文本走此通道.
    /// </remarks>
    public static class LocalizationKeys
    {
        public const string StartMenu_Title = "ui.start_menu.title";
        public const string StartMenu_Start = "ui.start_menu.start";
        public const string StartMenu_Continue = "ui.start_menu.continue";
        public const string StartMenu_Settings = "ui.start_menu.settings";
        public const string StartMenu_Quit = "ui.start_menu.quit";

        public const string Settings_Title = "ui.settings.title";
        public const string Settings_MasterVolume = "ui.settings.master_volume";
        public const string Settings_MusicVolume = "ui.settings.music_volume";
        public const string Settings_SfxVolume = "ui.settings.sfx_volume";
        public const string Settings_Language = "ui.settings.language";
        public const string Settings_Fullscreen = "ui.settings.fullscreen";
        public const string Settings_Resolution = "ui.settings.resolution";
        public const string Settings_Apply = "ui.settings.apply";
        public const string Settings_Close = "ui.settings.close";
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
