#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;

namespace Game.Contracts
{
    /// <summary>
    /// 设置服务：读取/应用/恢复设置，应用成功后发布事件驱动 UI 刷新.
    /// </summary>
    /// <remarks>
    /// 对应技术设计文档 §12.4：ApplyAsync 固定顺序为 校验 -> 应用音频/语言/窗口 -> 保存 -> 通知 UI.
    /// </remarks>
    public interface ISettingsService
    {
        /// <summary>当前生效设置的只读快照.</summary>
        SettingsSnapshot Current { get; }

        /// <summary>应用设置草稿（含校验、持久化和事件发布）.</summary>
        /// <param name="draft">用户提交的设置</param>
        /// <param name="ct">取消令牌</param>
        Task<Result> ApplyAsync(SettingsDraft draft, CancellationToken ct);

        /// <summary>恢复默认设置并应用.</summary>
        Task<Result> RestoreDefaultsAsync(CancellationToken ct);
    }

    /// <summary>
    /// 设置快照：只读，供 UI 和事件消费方使用.
    /// </summary>
    public sealed class SettingsSnapshot
    {
        public string LanguageCode { get; }
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
        public bool Fullscreen { get; }
        public int ResolutionWidth { get; }
        public int ResolutionHeight { get; }

        public SettingsSnapshot(
            string languageCode,
            float masterVolume,
            float musicVolume,
            float sfxVolume,
            bool fullscreen,
            int resolutionWidth,
            int resolutionHeight
        )
        {
            LanguageCode = languageCode ?? "zh-CN";
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            Fullscreen = fullscreen;
            ResolutionWidth = resolutionWidth;
            ResolutionHeight = resolutionHeight;
        }

        /// <summary>构造一个允许缺省值的快照（用于测试与占位）.</summary>
        public static SettingsSnapshot Default() => new SettingsSnapshot("zh-CN", 1f, 1f, 1f, true, 1920, 1080);
    }

    /// <summary>
    /// 设置草稿：设置弹窗提交的待应用值（由 UI 层构造，UI 层不得直接改存档 DTO）.
    /// </summary>
    public sealed class SettingsDraft
    {
        public string LanguageCode { get; set; } = "zh-CN";
        public float MasterVolume { get; set; } = 1f;
        public float MusicVolume { get; set; } = 1f;
        public float SfxVolume { get; set; } = 1f;
        public bool Fullscreen { get; set; } = true;
        public int ResolutionWidth { get; set; } = 1920;
        public int ResolutionHeight { get; set; } = 1080;

        /// <summary>从只读快照创建草稿（编辑前复制）.</summary>
        public static SettingsDraft FromSnapshot(SettingsSnapshot snapshot) =>
            new SettingsDraft
            {
                LanguageCode = snapshot.LanguageCode,
                MasterVolume = snapshot.MasterVolume,
                MusicVolume = snapshot.MusicVolume,
                SfxVolume = snapshot.SfxVolume,
                Fullscreen = snapshot.Fullscreen,
                ResolutionWidth = snapshot.ResolutionWidth,
                ResolutionHeight = snapshot.ResolutionHeight,
            };
    }

    /// <summary>
    /// 设置已成功应用事件：注册完成后以此驱动页面刷新（本地化文本、音量等）.
    /// </summary>
    public sealed class SettingsAppliedEvent : IDomainEvent
    {
        /// <summary>应用后的设置快照</summary>
        public SettingsSnapshot Snapshot { get; }

        public SettingsAppliedEvent(SettingsSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }
}
