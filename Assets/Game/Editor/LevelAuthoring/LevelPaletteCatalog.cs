using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Editor.Level
{
    /// <summary>
    /// 调色板条目种类; 决定该条目在视口中被创建为哪一类关卡元素。
    /// </summary>
    public enum PaletteEntryKind
    {
        /// <summary>关卡静态对象; 保存为 <see cref="Game.Contracts.Content.StageObjectData"/>。</summary>
        StageObject,

        /// <summary>小车/角色出生点; 每关恰好一个。</summary>
        SpawnPoint,

        /// <summary>终点区域; 每关恰好一个。</summary>
        GoalPoint,
    }

    /// <summary>
    /// 调色板中的一个可放置条目。
    /// </summary>
    public sealed class PaletteEntry
    {
        /// <summary>条目种类。</summary>
        public PaletteEntryKind Kind { get; }

        /// <summary>条目显示名称; 仅用于编辑器界面。</summary>
        public string DisplayName { get; }

        /// <summary>预制体资源稳定标识; 起点与终点为空。</summary>
        public string PrefabId { get; }

        /// <summary>创建调色板条目。</summary>
        /// <param name="kind">条目种类。</param>
        /// <param name="displayName">编辑器显示名称。</param>
        /// <param name="prefabId">预制体资源稳定标识; 起点与终点可为空。</param>
        /// <exception cref="ArgumentException">显示名称为空时抛出。</exception>
        public PaletteEntry(PaletteEntryKind kind, string displayName, string prefabId)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A palette entry requires a display name.", nameof(displayName));
            Kind = kind;
            DisplayName = displayName;
            PrefabId = prefabId ?? string.Empty;
        }
    }

    /// <summary>
    /// 关卡编辑器占位调色板目录; 在正式美术资源就位前提供固定的可放置条目集合。
    /// </summary>
    /// <remarks>
    /// C20 只提供占位条目: 预制体稳定 ID 已按内容命名约定固定, 但对应的正式预制体
    /// 由后续周期的美术与内容流水线提供。视口按 <see cref="Game.Contracts.Content.StageObjectData.PrefabId"/>
    /// 之外的对象标识绘制占位图形, 不依赖预制体实际存在。
    /// </remarks>
    public static class LevelPaletteCatalog
    {
        /// <summary>地面/平台静态对象的预制体稳定标识。</summary>
        public const string GroundPrefabId = "official.prefab.ground_platform";

        /// <summary>墙体静态对象的预制体稳定标识。</summary>
        public const string WallPrefabId = "official.prefab.wall_block";

        /// <summary>斜坡静态对象的预制体稳定标识。</summary>
        public const string RampPrefabId = "official.prefab.ramp_slope";

        /// <summary>货物静态对象的预制体稳定标识。</summary>
        public const string CargoPrefabId = "official.prefab.cargo_box";

        /// <summary>危险区静态对象的预制体稳定标识。</summary>
        public const string HazardPrefabId = "official.prefab.hazard_water";

        /// <summary>获取全部占位调色板条目, 顺序固定便于界面稳定显示。</summary>
        /// <returns>只读条目列表。</returns>
        public static IReadOnlyList<PaletteEntry> GetEntries() =>
            new List<PaletteEntry>
            {
                new PaletteEntry(PaletteEntryKind.SpawnPoint, "起点", string.Empty),
                new PaletteEntry(PaletteEntryKind.GoalPoint, "终点", string.Empty),
                new PaletteEntry(PaletteEntryKind.StageObject, "地面平台", GroundPrefabId),
                new PaletteEntry(PaletteEntryKind.StageObject, "墙体", WallPrefabId),
                new PaletteEntry(PaletteEntryKind.StageObject, "斜坡", RampPrefabId),
                new PaletteEntry(PaletteEntryKind.StageObject, "货物箱", CargoPrefabId),
                new PaletteEntry(PaletteEntryKind.StageObject, "危险水域", HazardPrefabId),
            };

        /// <summary>按预制体稳定标识查询条目的显示名称。</summary>
        /// <param name="prefabId">预制体稳定标识。</param>
        /// <returns>匹配的显示名称; 未登记时返回预制体标识自身。</returns>
        public static string GetDisplayName(string prefabId)
        {
            if (string.IsNullOrWhiteSpace(prefabId))
                return string.Empty;
            PaletteEntry match = GetEntries()
                .FirstOrDefault(entry =>
                    entry.Kind == PaletteEntryKind.StageObject
                    && string.Equals(entry.PrefabId, prefabId, StringComparison.Ordinal)
                );
            return match?.DisplayName ?? prefabId;
        }

        /// <summary>
        /// 判断预制体稳定标识是否属于可在关卡中放置的官方静态对象。
        /// </summary>
        /// <param name="prefabId">预制体稳定标识。</param>
        /// <returns>登记为静态对象条目时返回 true。</returns>
        /// <remarks>
        /// 关卡对象的 <c>PrefabId</c> 必须来自本目录: 内容编译与运行时世界构建都只认这些登记项,
        /// 手写一个目录外的 ID 会在构建世界时以 <c>NotFound</c> 失败。
        /// </remarks>
        public static bool IsKnownStageObjectPrefab(string prefabId)
        {
            if (string.IsNullOrWhiteSpace(prefabId))
                return false;
            foreach (PaletteEntry entry in GetEntries())
            {
                if (
                    entry.Kind == PaletteEntryKind.StageObject
                    && string.Equals(entry.PrefabId, prefabId, StringComparison.Ordinal)
                )
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 从预制体稳定标识提取用于生成对象稳定 ID 的短名。
        /// </summary>
        /// <param name="prefabId">预制体稳定标识; 形如 <c>official.prefab.ground_platform</c>。</param>
        /// <returns>
        /// 末段短名, 例如 <c>ground_platform</c>; 标识为空、末段为空或末段清洗后无字符时返回 <c>obj</c>。
        /// </returns>
        /// <remarks>非字母数字下划线字符会被替换为下划线, 保证结果可作为稳定 ID 的一段。</remarks>
        public static string GetPrefabShortName(string prefabId)
        {
            if (string.IsNullOrWhiteSpace(prefabId))
                return "obj";
            int separator = prefabId.LastIndexOf('.');
            string tail = separator >= 0 ? prefabId.Substring(separator + 1) : prefabId;
            var builder = new System.Text.StringBuilder(tail.Length);
            foreach (char character in tail)
                builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
            return builder.Length == 0 ? "obj" : builder.ToString();
        }
    }
}
