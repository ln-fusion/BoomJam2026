using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;

namespace Game.Contracts
{
    /// <summary>音乐切换方式。</summary>
    public enum MusicTransition
    {
        /// <summary>立即切换。</summary>
        Immediate = 0,
        /// <summary>使用淡入淡出切换。</summary>
        CrossFade
    }

    /// <summary>音频服务：把稳定音频 ID 和 Unity 播放实现隔离。</summary>
    public interface IAudioService
    {
        /// <summary>应用线性主音量、音乐音量和音效音量。</summary>
        /// <param name="master">主音量，范围为 0 到 1。</param>
        /// <param name="music">音乐音量，范围为 0 到 1。</param>
        /// <param name="sfx">音效音量，范围为 0 到 1。</param>
        void ApplyVolumes(float master, float music, float sfx);

        /// <summary>播放指定音乐。</summary>
        /// <param name="musicId">音乐稳定 ID。</param>
        /// <param name="transition">切换方式。</param>
        void PlayMusic(MusicId musicId, MusicTransition transition);

        /// <summary>停止当前音乐。</summary>
        /// <param name="transition">停止方式。</param>
        void StopMusic(MusicTransition transition);

        /// <summary>播放指定音效。</summary>
        /// <param name="sfxId">音效稳定 ID。</param>
        void PlaySfx(SfxId sfxId);
    }

    /// <summary>本地化服务：提供默认 Locale 和稳定键查找。</summary>
    public interface ILocalizationService
    {
        /// <summary>当前 Locale 代码。</summary>
        string CurrentLocaleCode { get; }

        /// <summary>Locale 变更后触发；页面应重新查询并刷新文本。</summary>
        event Action<string> LocaleChanged;

        /// <summary>异步切换 Locale。</summary>
        /// <param name="localeCode">BCP-47 Locale 代码。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>切换结果；不支持的 Locale 返回失败。</returns>
        Task<Result> SetLocaleAsync(string localeCode, CancellationToken cancellationToken);

        /// <summary>读取当前 Locale 下的文本。</summary>
        /// <param name="key">稳定本地化键。</param>
        /// <param name="arguments">格式化参数。</param>
        /// <returns>本地化文本；缺失键回退为键值。</returns>
        string Get(LocalizationKey key, params object[] arguments);
    }

    /// <summary>设置服务对外暴露的不可变快照。</summary>
    public sealed class SettingsSnapshot
    {
        /// <summary>Locale 代码。</summary>
        public string LanguageCode { get; }
        /// <summary>主音量。</summary>
        public float MasterVolume { get; }
        /// <summary>音乐音量。</summary>
        public float MusicVolume { get; }
        /// <summary>音效音量。</summary>
        public float SfxVolume { get; }
        /// <summary>是否全屏。</summary>
        public bool Fullscreen { get; }
        /// <summary>窗口宽度。</summary>
        public int ResolutionWidth { get; }
        /// <summary>窗口高度。</summary>
        public int ResolutionHeight { get; }

        /// <summary>创建设置快照。</summary>
        /// <param name="languageCode">Locale 代码。</param>
        /// <param name="masterVolume">主音量。</param>
        /// <param name="musicVolume">音乐音量。</param>
        /// <param name="sfxVolume">音效音量。</param>
        /// <param name="fullscreen">是否全屏。</param>
        /// <param name="resolutionWidth">窗口宽度。</param>
        /// <param name="resolutionHeight">窗口高度。</param>
        public SettingsSnapshot(string languageCode, float masterVolume, float musicVolume,
            float sfxVolume, bool fullscreen, int resolutionWidth, int resolutionHeight)
        {
            LanguageCode = languageCode ?? string.Empty;
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            Fullscreen = fullscreen;
            ResolutionWidth = resolutionWidth;
            ResolutionHeight = resolutionHeight;
        }

        /// <summary>从存档 DTO 创建快照。</summary>
        /// <param name="save">设置存档。</param>
        /// <returns>对应快照。</returns>
        public static SettingsSnapshot FromSave(Persistence.SettingsSave save)
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));

            return new SettingsSnapshot(save.LanguageCode, save.MasterVolume, save.MusicVolume,
                save.SfxVolume, save.Fullscreen, save.ResolutionWidth, save.ResolutionHeight);
        }
    }

    /// <summary>设置弹窗编辑中的可变草稿。</summary>
    public sealed class SettingsDraft
    {
        /// <summary>Locale 代码。</summary>
        public string LanguageCode { get; set; }
        /// <summary>主音量。</summary>
        public float MasterVolume { get; set; }
        /// <summary>音乐音量。</summary>
        public float MusicVolume { get; set; }
        /// <summary>音效音量。</summary>
        public float SfxVolume { get; set; }
        /// <summary>是否全屏。</summary>
        public bool Fullscreen { get; set; }
        /// <summary>窗口宽度。</summary>
        public int ResolutionWidth { get; set; }
        /// <summary>窗口高度。</summary>
        public int ResolutionHeight { get; set; }

        /// <summary>创建设置草稿。</summary>
        /// <param name="snapshot">初始快照。</param>
        public SettingsDraft(SettingsSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            LanguageCode = snapshot.LanguageCode;
            MasterVolume = snapshot.MasterVolume;
            MusicVolume = snapshot.MusicVolume;
            SfxVolume = snapshot.SfxVolume;
            Fullscreen = snapshot.Fullscreen;
            ResolutionWidth = snapshot.ResolutionWidth;
            ResolutionHeight = snapshot.ResolutionHeight;
        }

        /// <summary>从默认设置创建草稿。</summary>
        /// <returns>默认设置草稿。</returns>
        public static SettingsDraft CreateDefault() =>
            new SettingsDraft(SettingsSnapshot.FromSave(Persistence.SettingsSave.CreateDefault()));
    }

    /// <summary>窗口设置的运行时应用端口。</summary>
    public interface IWindowSettingsApplier
    {
        /// <summary>应用分辨率和全屏状态。</summary>
        /// <param name="width">窗口宽度。</param>
        /// <param name="height">窗口高度。</param>
        /// <param name="fullscreen">是否全屏。</param>
        /// <returns>应用结果。</returns>
        Result Apply(int width, int height, bool fullscreen);
    }

    /// <summary>设置服务：校验、应用并持久化玩家设置。</summary>
    public interface ISettingsService
    {
        /// <summary>当前设置快照。</summary>
        SettingsSnapshot Current { get; }

        /// <summary>设置已保存并应用后触发。</summary>
        event Action<SettingsSnapshot> SettingsApplied;

        /// <summary>读取本地设置并应用到运行时。</summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化结果。</returns>
        Task<Result> InitializeAsync(CancellationToken cancellationToken);

        /// <summary>校验、应用并保存设置草稿。</summary>
        /// <param name="draft">待应用草稿。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>应用结果。</returns>
        Task<Result> ApplyAsync(SettingsDraft draft, CancellationToken cancellationToken);

        /// <summary>恢复默认设置并保存。</summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>恢复结果。</returns>
        Task<Result> RestoreDefaultsAsync(CancellationToken cancellationToken);
    }

    /// <summary>运行时 View 的最小渲染契约。</summary>
    /// <typeparam name="TViewModel">ViewModel 类型。</typeparam>
    public interface IView<in TViewModel>
    {
        /// <summary>以不可变 ViewModel 刷新界面。</summary>
        /// <param name="viewModel">待渲染模型。</param>
        void Render(TViewModel viewModel);
    }

    /// <summary>设置已应用并持久化的领域事实。</summary>
    public sealed class SettingsAppliedEvent : IDomainEvent
    {
        /// <summary>已保存的设置快照。</summary>
        public SettingsSnapshot Settings { get; }

        /// <summary>创建设置已应用事件。</summary>
        /// <param name="settings">已保存设置。</param>
        public SettingsAppliedEvent(SettingsSnapshot settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
    }
}
