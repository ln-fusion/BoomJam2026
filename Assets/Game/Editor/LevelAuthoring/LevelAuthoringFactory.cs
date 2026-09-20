using System.Collections.Generic;
using Game.Contracts.Content;
using UnityEngine;

namespace Game.Editor.Level
{
    /// <summary>
    /// 关卡 Authoring 数据工厂; 产出满足编辑器最低可用标准的新关卡模板。
    /// </summary>
    /// <remarks>
    /// C20 生成的模板只包含世界边界、起点与终点, 对象、区域、能力白名单与胜负条件
    /// 留空, 由 C21-C24 的编辑能力补齐。模板保证 <see cref="LevelAuthoringData.Definition"/>
    /// 字段完整非 null, 使序列化与视口绘制无需额外判空分支。
    /// </remarks>
    public static class LevelAuthoringFactory
    {
        /// <summary>新建关卡的默认世界边界宽度; 世界单位。</summary>
        public const float DefaultWorldWidth = 40f;

        /// <summary>新建关卡的默认世界边界高度; 世界单位。</summary>
        public const float DefaultWorldHeight = 20f;

        /// <summary>按关卡稳定 ID 创建空白关卡模板。</summary>
        /// <param name="levelId">关卡稳定 ID; 例如 official.level.test_01_01。</param>
        /// <returns>可直接保存的关卡源数据。</returns>
        public static LevelAuthoringData CreateNew(string levelId)
        {
            float halfWidth = DefaultWorldWidth * 0.5f;
            float halfHeight = DefaultWorldHeight * 0.5f;
            var definition = new LevelDefinition
            {
                Header = new ContentHeader { FormatVersion = 1, ContentRevision = 1 },
                LevelId = levelId,
                MapId = DeriveMapId(levelId),
                DisplayNameKey = "level." + levelId + ".name",
                SortOrder = 0,
                CapacityLimit = 10,
                PhysicsProfile = new PhysicsProfileData(),
                WorldBounds = new BoundsData
                {
                    MinX = -halfWidth,
                    MinY = -halfHeight,
                    MaxX = halfWidth,
                    MaxY = halfHeight,
                },
                DeployableZones = new List<ZoneData>(),
                ForbiddenZones = new List<ZoneData>(),
                StartPoint = new SpawnPointData { PositionX = -halfWidth + 2f, PositionY = -halfHeight + 1f },
                GoalPoint = new GoalPointData { PositionX = halfWidth - 2f, PositionY = -halfHeight + 1f },
                Objects = new List<StageObjectData>(),
                AllowedAbilities = new List<AllowedAbilityData>(),
                SuccessConditions = new List<ConditionData>(),
                FailureConditions = new List<ConditionData>(),
                UnlockRequirement = new UnlockRequirementData(),
            };
            return new LevelAuthoringData
            {
                FormatVersion = LevelAuthoringSerializer.CurrentFormatVersion,
                ContentRevision = 1,
                Definition = definition,
                EditorViewState = new EditorViewStateData
                {
                    ViewCenterX = 0f,
                    ViewCenterY = 0f,
                    Zoom = 24f,
                },
            };
        }

        /// <summary>从关卡稳定 ID 推导所属地图标识; 无法推导时返回空字符串由人工填写。</summary>
        /// <param name="levelId">关卡稳定 ID。</param>
        /// <returns>地图稳定标识; 例如 official.map.test_01。</returns>
        private static string DeriveMapId(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
                return string.Empty;
            string[] segments = levelId.Split('.');
            if (segments.Length < 4)
                return string.Empty;
            // official.level.test_01_01 -> official.map.test_01
            int lastSegment = segments.Length - 1;
            string levelNumber = segments[lastSegment];
            int separator = levelNumber.LastIndexOf('_');
            if (separator <= 0)
                return string.Empty;
            return string.Join(".", segments, 0, lastSegment)
                + "."
                + segments[lastSegment - 1]
                + "."
                + levelNumber.Substring(0, separator);
        }
    }
}
