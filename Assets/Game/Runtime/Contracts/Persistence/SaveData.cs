using System;
using System.Collections.Generic;

namespace Game.Contracts.Persistence
{
    /// <summary>
    /// 玩家设置存档 DTO，对应本地 <c>settings.json</c>。
    /// </summary>
    /// <remarks>
    /// 设置与玩家进度分文件保存；设置损坏时只恢复默认设置，不影响 <see cref="ProfileSave"/>。
    /// </remarks>
    [Serializable]
    public sealed class SettingsSave
    {
        /// <summary>当前设置文件结构版本。</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>设置文件结构版本。</summary>
        public int SchemaVersion = CurrentSchemaVersion;
        /// <summary>当前语言的稳定区域标识，例如 zh-CN。</summary>
        public string LanguageCode = "zh-CN";
        /// <summary>主音量，取值范围 0 到 1。</summary>
        public float MasterVolume = 1f;
        /// <summary>音乐音量倍率，取值范围 0 到 1。</summary>
        public float MusicVolume = 1f;
        /// <summary>音效音量倍率，取值范围 0 到 1。</summary>
        public float SfxVolume = 1f;
        /// <summary>是否使用全屏显示。</summary>
        public bool Fullscreen = true;
        /// <summary>屏幕分辨率宽度。</summary>
        public int ResolutionWidth = 1920;
        /// <summary>屏幕分辨率高度。</summary>
        public int ResolutionHeight = 1080;

        /// <summary>创建一份安全默认设置。</summary>
        /// <returns>带默认语言、音量和分辨率的设置存档对象。</returns>
        public static SettingsSave CreateDefault()
        {
            return new SettingsSave();
        }
    }

    /// <summary>
    /// 单一玩家档案存档 DTO，对应本地 <c>profile.json</c>。
    /// </summary>
    /// <remarks>
    /// 档案保存游戏进度、最佳成绩、剧情完成事实、本地统计和幂等提交记录；地图与谜题结构属于官方内容，不进入玩家存档。
    /// </remarks>
    [Serializable]
    public sealed class ProfileSave
    {
        /// <summary>当前档案文件结构版本。</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>档案文件结构版本。</summary>
        public int SchemaVersion = CurrentSchemaVersion;
        /// <summary>每次成功写入档案后递增的本地修订号。</summary>
        public long Revision;
        /// <summary>单一玩家档案的稳定实例标识。</summary>
        public string ProfileId;
        /// <summary>玩家昵称。</summary>
        public string PlayerNickname;
        /// <summary>档案创建时间，使用 UTC ISO-8601 字符串。</summary>
        public string CreatedAtUtc;
        /// <summary>档案最后修改时间，使用 UTC ISO-8601 字符串。</summary>
        public string LastModifiedAtUtc;
        /// <summary>最后写入该档案的设备标识，用于云存档冲突诊断。</summary>
        public string LastWriterDeviceId;
        /// <summary>上次打开的主界面页面稳定标识。</summary>
        public string LastMetaPageId = "map";
        /// <summary>已经完成的关卡 ID 列表。</summary>
        public List<string> CompletedLevelIds = new List<string>();
        /// <summary>每关完成状态和最佳成绩记录。</summary>
        public List<LevelRecordSave> LevelRecords = new List<LevelRecordSave>();
        /// <summary>已经完成或已解锁重播的剧情 ID 列表。</summary>
        public List<string> CompletedStoryIds = new List<string>();
        /// <summary>已经授予的解锁事实 ID 列表。</summary>
        public List<string> GrantedUnlockIds = new List<string>();
        /// <summary>本地统计值列表，用于未来 Steam 统计同步。</summary>
        public List<LocalStatSave> LocalStats = new List<LocalStatSave>();
        /// <summary>已经处理过的通关提交运行 ID，防止重复提交。</summary>
        public List<string> AppliedCompletionRunIds = new List<string>();
    }

    /// <summary>
    /// 单个关卡在玩家档案中的进度记录。
    /// </summary>
    [Serializable]
    public sealed class LevelRecordSave
    {
        /// <summary>关卡稳定标识。</summary>
        public string LevelId;
        /// <summary>玩家是否已经完成该关卡。</summary>
        public bool Completed;
        /// <summary>当前规则版本下的最佳成绩。</summary>
        public BestScoreSave CurrentBest;
        /// <summary>旧规则、旧内容或旧物理配置下保留的历史最佳成绩。</summary>
        public List<BestScoreSave> LegacyBests = new List<BestScoreSave>();
    }

    /// <summary>
    /// 关卡最佳成绩存档 DTO。
    /// </summary>
    /// <remarks>
    /// 设计文档要求最佳成绩优先比较通关耗时，耗时相同时比较容量消耗；此 DTO 保存比较和回溯所需的版本信息。
    /// </remarks>
    [Serializable]
    public sealed class BestScoreSave
    {
        /// <summary>完成关卡所用的 Tick 数。</summary>
        public long ElapsedTicks;
        /// <summary>记录成绩时使用的模拟 Tick 频率。</summary>
        public int TickRate;
        /// <summary>完成关卡时使用的能力框容量。</summary>
        public int CapacityUsed;
        /// <summary>记录成绩时的游戏构建版本。</summary>
        public string GameBuildVersion;
        /// <summary>记录成绩时的关卡内容修订号。</summary>
        public int LevelContentRevision;
        /// <summary>记录成绩时的成绩规则版本。</summary>
        public int ScoreRuleVersion;
        /// <summary>记录成绩时的物理配置摘要。</summary>
        public string PhysicsProfileHash;
        /// <summary>完成关卡的 UTC ISO-8601 时间戳。</summary>
        public string CompletedAtUtc;
    }

    /// <summary>
    /// 本地统计项存档 DTO。
    /// </summary>
    [Serializable]
    public sealed class LocalStatSave
    {
        /// <summary>统计项稳定标识。</summary>
        public string StatId;
        /// <summary>统计项当前数值。</summary>
        public long Value;
    }
}
