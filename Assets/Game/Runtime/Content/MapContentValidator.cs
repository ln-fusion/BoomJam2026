using System;
using System.Collections.Generic;
using Game.Contracts.Content;

namespace Game.Content
{
    /// <summary>Validates map and level stable IDs and unlock references.</summary>
    public static class MapContentValidator
    {
        /// <summary>Validates all maps and levels in a provider.</summary>
        /// <param name="provider">Provider to validate.</param>
        /// <param name="error">Failure diagnostic, or null when valid.</param>
        /// <returns>True when all IDs and references are valid.</returns>
        public static bool TryValidate(OfficialContentProvider provider, out string error)
        {
            error = null;
            if (provider == null)
                return Fail("Provider is required.", out error);
            var levelIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelDefinition level in provider.Levels)
            {
                if (level == null || string.IsNullOrWhiteSpace(level.LevelId) || !levelIds.Add(level.LevelId))
                    return Fail("Level IDs must be non-empty and unique.", out error);
                if (string.IsNullOrWhiteSpace(level.MapId))
                    return Fail("Every level requires a map ID.", out error);
            }
            foreach (LevelDefinition level in provider.Levels)
                if (level.UnlockRequirement != null)
                    foreach (string required in level.UnlockRequirement.RequiredLevelIds)
                        if (!levelIds.Contains(required))
                            return Fail("Unlock requirement references an unknown level.", out error);
            foreach (LevelDefinition level in provider.Levels)
            {
                var visiting = new HashSet<string>(StringComparer.Ordinal);
                if (HasCycle(level.LevelId, provider, visiting, new HashSet<string>(StringComparer.Ordinal)))
                    return Fail("Unlock requirements contain a cycle.", out error);
            }
            var mapIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MapDefinition map in provider.Maps)
                if (map == null || string.IsNullOrWhiteSpace(map.MapId) || !mapIds.Add(map.MapId))
                    return Fail("Map IDs must be non-empty and unique.", out error);
            return true;
        }

        /// <summary>设置校验失败信息并返回失败结果。</summary>
        /// <param name="message">校验失败信息。</param>
        /// <param name="error">接收校验失败信息。</param>
        /// <returns>始终返回 false。</returns>
        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        /// <summary>深度优先检测关卡前置依赖环。</summary>
        /// <param name="levelId">当前检查的关卡稳定标识。</param>
        /// <param name="provider">提供全部关卡定义的内容源。</param>
        /// <param name="visiting">当前递归路径中正在访问的关卡集合。</param>
        /// <param name="visited">已经确认不存在依赖环的关卡集合。</param>
        /// <returns>发现依赖环时返回 true。</returns>
        private static bool HasCycle(string levelId, OfficialContentProvider provider,
            HashSet<string> visiting, HashSet<string> visited)
        {
            if (visited.Contains(levelId)) return false;
            if (!visiting.Add(levelId)) return true;
            foreach (LevelDefinition level in provider.Levels)
                if (level != null && level.LevelId == levelId && level.UnlockRequirement != null)
                    foreach (string required in level.UnlockRequirement.RequiredLevelIds)
                        if (HasCycle(required, provider, visiting, visited)) return true;
            visiting.Remove(levelId);
            visited.Add(levelId);
            return false;
        }
    }
}
