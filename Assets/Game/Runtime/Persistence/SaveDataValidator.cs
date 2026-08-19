using System;
using System.Collections.Generic;
using Game.Contracts.Persistence;
using Game.Foundation.Results;

namespace Game.Persistence
{
    internal static class SaveDataValidator
    {
        public static ErrorCode Validate(SettingsSave data)
        {
            if (data == null)
                return SaveErrors.Invalid;
            if (data.SchemaVersion != SettingsSave.CurrentSchemaVersion)
                return SaveErrors.UnsupportedVersion;
            if (!IsUnitValue(data.MasterVolume) || !IsUnitValue(data.MusicVolume) ||
                !IsUnitValue(data.SfxVolume))
                return SaveErrors.Invalid;
            if (data.ResolutionWidth <= 0 || data.ResolutionHeight <= 0)
                return SaveErrors.Invalid;
            if (string.IsNullOrWhiteSpace(data.LanguageCode))
                return SaveErrors.Invalid;

            return ErrorCode.None;
        }

        public static ErrorCode Validate(ProfileSave data)
        {
            if (data == null)
                return SaveErrors.Invalid;
            if (data.SchemaVersion != ProfileSave.CurrentSchemaVersion)
                return SaveErrors.UnsupportedVersion;
            if (data.Revision < 0 || string.IsNullOrWhiteSpace(data.ProfileId) ||
                string.IsNullOrWhiteSpace(data.PlayerNickname))
                return SaveErrors.Invalid;
            if (!Guid.TryParseExact(data.ProfileId, "N", out _))
                return SaveErrors.Invalid;
            if (!IsUtcTimestamp(data.CreatedAtUtc) || !IsUtcTimestamp(data.LastModifiedAtUtc))
                return SaveErrors.Invalid;
            if (HasNullCollections(data))
                return SaveErrors.Invalid;

            return ErrorCode.None;
        }

        private static bool IsUnitValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }

        private static bool IsUtcTimestamp(string value)
        {
            return DateTimeOffset.TryParse(value, out DateTimeOffset timestamp) &&
                   timestamp.Offset == TimeSpan.Zero;
        }

        private static bool HasNullCollections(ProfileSave data)
        {
            return data.CompletedLevelIds == null || data.LevelRecords == null ||
                   data.CompletedStoryIds == null || data.GrantedUnlockIds == null ||
                   data.LocalStats == null || data.AppliedCompletionRunIds == null;
        }
    }
}
