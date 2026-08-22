using System;
using System.Collections.Generic;
using Game.Contracts.Persistence;
using Game.Foundation;

namespace Game.Persistence
{
    /// <summary>
    /// 校验设置和玩家档案 DTO 是否满足当前存档版本与字段约束。
    /// </summary>
    internal static class SaveDataValidator
    {
        /// <summary>校验设置存档。</summary>
        /// <param name="data">待校验的设置数据。</param>
        /// <returns>通过返回 <see cref="ErrorCode.None"/>，否则返回具体错误码。</returns>
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

        /// <summary>校验玩家档案存档。</summary>
        /// <param name="data">待校验的档案数据。</param>
        /// <returns>通过返回 <see cref="ErrorCode.None"/>，否则返回具体错误码。</returns>
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

        /// <summary>检查浮点数是否为有限的 0 到 1 区间值。</summary>
        /// <param name="value">待检查数值。</param>
        /// <returns>满足约束返回 true，否则返回 false。</returns>
        private static bool IsUnitValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }

        /// <summary>检查字符串是否能解析为零时区 UTC 时间戳。</summary>
        /// <param name="value">待检查时间字符串。</param>
        /// <returns>满足 UTC 时间约束返回 true，否则返回 false。</returns>
        private static bool IsUtcTimestamp(string value)
        {
            return DateTimeOffset.TryParse(value, out DateTimeOffset timestamp) &&
                   timestamp.Offset == TimeSpan.Zero;
        }

        /// <summary>检查档案中的集合字段是否有 null。</summary>
        /// <param name="data">待检查的档案数据。</param>
        /// <returns>存在 null 集合返回 true，否则返回 false。</returns>
        private static bool HasNullCollections(ProfileSave data)
        {
            return data.CompletedLevelIds == null || data.LevelRecords == null ||
                   data.CompletedStoryIds == null || data.GrantedUnlockIds == null ||
                   data.LocalStats == null || data.AppliedCompletionRunIds == null;
        }
    }
}
