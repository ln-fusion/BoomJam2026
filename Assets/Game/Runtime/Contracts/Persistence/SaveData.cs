using System;
using System.Collections.Generic;

namespace Game.Contracts.Persistence
{
    [Serializable]
    public sealed class SettingsSave
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string LanguageCode = "zh-CN";
        public float MasterVolume = 1f;
        public float MusicVolume = 1f;
        public float SfxVolume = 1f;
        public bool Fullscreen = true;
        public int ResolutionWidth = 1920;
        public int ResolutionHeight = 1080;

        public static SettingsSave CreateDefault()
        {
            return new SettingsSave();
        }
    }

    [Serializable]
    public sealed class ProfileSave
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public long Revision;
        public string ProfileId;
        public string PlayerNickname;
        public string CreatedAtUtc;
        public string LastModifiedAtUtc;
        public string LastWriterDeviceId;
        public string LastMetaPageId = "map";
        public List<string> CompletedLevelIds = new List<string>();
        public List<LevelRecordSave> LevelRecords = new List<LevelRecordSave>();
        public List<string> CompletedStoryIds = new List<string>();
        public List<string> GrantedUnlockIds = new List<string>();
        public List<LocalStatSave> LocalStats = new List<LocalStatSave>();
        public List<string> AppliedCompletionRunIds = new List<string>();
    }

    [Serializable]
    public sealed class LevelRecordSave
    {
        public string LevelId;
        public bool Completed;
        public BestScoreSave CurrentBest;
        public List<BestScoreSave> LegacyBests = new List<BestScoreSave>();
    }

    [Serializable]
    public sealed class BestScoreSave
    {
        public long ElapsedTicks;
        public int TickRate;
        public int CapacityUsed;
        public string GameBuildVersion;
        public int LevelContentRevision;
        public int ScoreRuleVersion;
        public string PhysicsProfileHash;
        public string CompletedAtUtc;
    }

    [Serializable]
    public sealed class LocalStatSave
    {
        public string StatId;
        public long Value;
    }
}
