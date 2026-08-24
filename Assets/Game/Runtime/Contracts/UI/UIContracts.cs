#nullable enable
using System;
using System.Collections.Generic;
using Game.Foundation;

namespace Game.Contracts.UI
{
    /// <summary>
    /// 视图渲染接口：View 通过 Render 呈现 ViewModel，禁止直接访问领域可变集合.
    /// </summary>
    /// <typeparam name="TViewModel">只读 ViewModel 类型</typeparam>
    public interface IView<in TViewModel>
    {
        /// <summary>使用最新 ViewModel 刷新视图.</summary>
        /// <param name="viewModel">不可变快照</param>
        void Render(TViewModel viewModel);
    }

    /// <summary>
    /// 开始菜单页面 ViewModel：主操作与首次/继续状态.
    /// </summary>
    public sealed class StartMenuViewModel
    {
        /// <summary>是否存在已有档案（决定“开始游戏”按钮文案）</summary>
        public bool HasProfile { get; }

        /// <summary>玩家昵称（已有档案时有效）</summary>
        public string PlayerNickname { get; }

        /// <summary>设置按钮是否可用</summary>
        public bool CanOpenSettings { get; }

        public StartMenuViewModel(bool hasProfile, string playerNickname, bool canOpenSettings = true)
        {
            HasProfile = hasProfile;
            PlayerNickname = playerNickname ?? string.Empty;
            CanOpenSettings = canOpenSettings;
        }
    }

    /// <summary>
    /// 设置界面对话框 ViewModel：当前生效的设置快照.
    /// </summary>
    public sealed class SettingsDialogViewModel
    {
        /// <summary>语言代码（如 zh-CN）</summary>
        public string LanguageCode { get; }

        /// <summary>主音量 0..1</summary>
        public float MasterVolume { get; }

        /// <summary>音乐音量 0..1</summary>
        public float MusicVolume { get; }

        /// <summary>音效音量 0..1</summary>
        public float SfxVolume { get; }

        /// <summary>是否全屏</summary>
        public bool Fullscreen { get; }

        /// <summary>分辨率宽</summary>
        public int ResolutionWidth { get; }

        /// <summary>分辨率高</summary>
        public int ResolutionHeight { get; }

        public SettingsDialogViewModel(
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
    }

    /// <summary>
    /// 主界面壳层级 ViewModel：页面导航状态.
    /// </summary>
    public sealed class MetaHubShellViewModel
    {
        /// <summary>当前激活页面</summary>
        public MetaPageId CurrentPage { get; }

        /// <summary>玩家昵称（上栏显示）</summary>
        public string PlayerNickname { get; }

        /// <summary>当前章节名称（占位，来自 Registry）</summary>
        public string ChapterTitle { get; }

        /// <summary>最后页面 ID（用于恢复）</summary>
        public string LastPageId { get; }

        public MetaHubShellViewModel(
            MetaPageId currentPage,
            string playerNickname,
            string chapterTitle = "",
            string lastPageId = "map"
        )
        {
            CurrentPage = currentPage;
            PlayerNickname = playerNickname ?? string.Empty;
            ChapterTitle = chapterTitle ?? string.Empty;
            LastPageId = lastPageId ?? "map";
        }
    }

    /// <summary>
    /// 主界面时间显示 ViewModel：下栏实时时钟.
    /// </summary>
    public sealed class MetaHubClockViewModel
    {
        /// <summary>当前本地时间（展示用）</summary>
        public DateTimeOffset LocalNow { get; }

        public MetaHubClockViewModel(DateTimeOffset localNow)
        {
            LocalNow = localNow;
        }
    }
}
