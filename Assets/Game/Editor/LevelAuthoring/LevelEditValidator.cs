using System;
using System.Collections.Generic;
using Game.Contracts.Content;

namespace Game.Editor.Level
{
    /// <summary>
    /// 关卡编辑期校验问题的种类。
    /// </summary>
    public enum LevelValidationCode
    {
        /// <summary>缺少世界边界。</summary>
        MissingWorldBounds = 0,

        /// <summary>世界边界的最大边不大于最小边。</summary>
        InvalidWorldBounds = 1,

        /// <summary>缺少小车出生点。</summary>
        MissingStartPoint = 2,

        /// <summary>缺少终点区域。</summary>
        MissingGoalPoint = 3,

        /// <summary>终点区域宽高非正。</summary>
        InvalidGoalSize = 4,

        /// <summary>区域缺少稳定 ID。</summary>
        ZoneIdMissing = 5,

        /// <summary>同种区域间稳定 ID 重复。</summary>
        ZoneIdDuplicate = 6,

        /// <summary>区域顶点数量不足或顶点数据缺失。</summary>
        ZoneDegenerate = 7,

        /// <summary>区域顶点数量不足以下限但仍可编辑。</summary>
        ZoneTooFewVertices = 8,

        /// <summary>对象缺少稳定 ID。</summary>
        ObjectIdMissing = 9,

        /// <summary>对象稳定 ID 重复。</summary>
        ObjectIdDuplicate = 10,

        /// <summary>对象引用的预制体不在调色板内。</summary>
        ObjectPrefabUnknown = 11,

        /// <summary>对象缩放非正。</summary>
        ObjectScaleInvalid = 12,

        /// <summary>对象存在重复的参数键。</summary>
        ObjectParameterKeyDuplicate = 13,

        /// <summary>对象参数键为空。</summary>
        ObjectParameterKeyMissing = 14,
    }

    /// <summary>
    /// 一条关卡校验问题; 携带可定位到具体编辑目标的选中项。
    /// </summary>
    /// <remarks>
    /// 携带 <see cref="Target"/> 是为了让编辑器把错误直接跳到对应配置
    /// （技术设计文档 §5.5 的"一键校验"与开发计划 C31A 的"从编辑器错误跳到对应配置"）。
    /// </remarks>
    public readonly struct LevelValidationIssue
    {
        /// <summary>问题种类。</summary>
        public LevelValidationCode Code { get; }

        /// <summary>问题对应的编辑目标; 无法定位时为 <see cref="LevelSelection.None"/>。</summary>
        public LevelSelection Target { get; }

        /// <summary>面向编辑者的问题描述。</summary>
        public string Message { get; }

        /// <summary>创建一条校验问题。</summary>
        /// <param name="code">问题种类。</param>
        /// <param name="target">问题对应的编辑目标。</param>
        /// <param name="message">问题描述。</param>
        public LevelValidationIssue(LevelValidationCode code, LevelSelection target, string message)
        {
            Code = code;
            Target = target;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// 关卡编辑期校验; 报告"能否保存并进入编译"所需的完整性问题。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类只做编辑期完整性检查, 不是内容构建门禁。构建门禁（稳定 ID 全局唯一、
    /// 本地化 Key 存在、解锁图无环等）属于 C28 的 <c>IContentValidator</c>
    /// （技术设计文档 §15.4）。
    /// </para>
    /// <para>
    /// 判定是纯读取, 不修改关卡数据, 也不依赖 Unity Scene。
    /// </para>
    /// </remarks>
    public static class LevelEditValidator
    {
        /// <summary>校验一个关卡, 返回全部问题; 无问题时返回空列表。</summary>
        /// <param name="data">待校验的 Authoring 数据; 为 null 或其定义为空时返回单条问题。</param>
        /// <returns>按检查顺序排列的问题列表, 顺序稳定便于比对。</returns>
        public static IReadOnlyList<LevelValidationIssue> Validate(LevelAuthoringData data)
        {
            var issues = new List<LevelValidationIssue>();
            LevelDefinition definition = data?.Definition;
            if (definition == null)
            {
                issues.Add(
                    new LevelValidationIssue(
                        LevelValidationCode.MissingWorldBounds,
                        LevelSelection.None,
                        "关卡缺少定义。"
                    )
                );
                return issues;
            }

            ValidateWorldBounds(definition, issues);
            ValidateStartAndGoal(definition, issues);
            ValidateZones(definition.DeployableZones, ZoneKind.Deployable, issues);
            ValidateZones(definition.ForbiddenZones, ZoneKind.Forbidden, issues);
            ValidateObjects(definition, issues);
            return issues;
        }

        /// <summary>判断校验结果是否无问题。</summary>
        /// <param name="issues">校验问题列表。</param>
        /// <returns>列表为 null 或无元素时返回 true。</returns>
        public static bool IsValid(IReadOnlyList<LevelValidationIssue> issues) => issues == null || issues.Count == 0;

        /// <summary>校验世界边界。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="issues">问题收集列表。</param>
        private static void ValidateWorldBounds(LevelDefinition definition, List<LevelValidationIssue> issues)
        {
            BoundsData bounds = definition.WorldBounds;
            if (bounds == null)
            {
                issues.Add(
                    new LevelValidationIssue(
                        LevelValidationCode.MissingWorldBounds,
                        LevelSelection.None,
                        "关卡缺少世界边界。"
                    )
                );
                return;
            }
            if (!(bounds.MaxX > bounds.MinX) || !(bounds.MaxY > bounds.MinY))
            {
                issues.Add(
                    new LevelValidationIssue(
                        LevelValidationCode.InvalidWorldBounds,
                        LevelSelection.WorldBounds(WorldBoundsCorner.RightTop),
                        "世界边界需要 MaxX 大于 MinX 且 MaxY 大于 MinY。"
                    )
                );
            }
        }

        /// <summary>校验起点与终点的存在性与合法性。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="issues">问题收集列表。</param>
        private static void ValidateStartAndGoal(LevelDefinition definition, List<LevelValidationIssue> issues)
        {
            if (definition.StartPoint == null)
            {
                issues.Add(
                    new LevelValidationIssue(
                        LevelValidationCode.MissingStartPoint,
                        LevelSelection.SpawnPoint(),
                        "关卡必须恰好有一个起点。"
                    )
                );
            }
            if (definition.GoalPoint == null)
            {
                issues.Add(
                    new LevelValidationIssue(
                        LevelValidationCode.MissingGoalPoint,
                        LevelSelection.GoalPoint(),
                        "关卡必须恰好有一个终点。"
                    )
                );
                return;
            }
            if (!(definition.GoalPoint.Width > 0f) || !(definition.GoalPoint.Height > 0f))
            {
                issues.Add(
                    new LevelValidationIssue(
                        LevelValidationCode.InvalidGoalSize,
                        LevelSelection.GoalPoint(),
                        "终点区域的宽和高必须大于 0。"
                    )
                );
            }
        }

        /// <summary>校验一组区域。</summary>
        /// <param name="zones">区域集合; 可为 null。</param>
        /// <param name="kind">区域种类。</param>
        /// <param name="issues">问题收集列表。</param>
        private static void ValidateZones(List<ZoneData> zones, ZoneKind kind, List<LevelValidationIssue> issues)
        {
            if (zones == null)
                return;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < zones.Count; i++)
            {
                ZoneData zone = zones[i];
                if (zone == null)
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ZoneDegenerate,
                            LevelSelection.Zone(kind, i),
                            "区域数据为空。"
                        )
                    );
                    continue;
                }
                if (string.IsNullOrWhiteSpace(zone.ZoneId))
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ZoneIdMissing,
                            LevelSelection.Zone(kind, i),
                            "区域缺少稳定 ID。"
                        )
                    );
                }
                else if (!seenIds.Add(zone.ZoneId))
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ZoneIdDuplicate,
                            LevelSelection.Zone(kind, i),
                            $"区域稳定 ID '{zone.ZoneId}' 重复。"
                        )
                    );
                }

                if (zone.Vertices == null)
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ZoneDegenerate,
                            LevelSelection.Zone(kind, i),
                            "区域缺少顶点集合。"
                        )
                    );
                    continue;
                }
                var hasNullVertex = false;
                for (int v = 0; v < zone.Vertices.Count; v++)
                {
                    if (zone.Vertices[v] == null)
                        hasNullVertex = true;
                }
                if (hasNullVertex)
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ZoneDegenerate,
                            LevelSelection.Zone(kind, i),
                            "区域包含空顶点。"
                        )
                    );
                    continue;
                }
                if (zone.Vertices.Count < LevelEditController.MinZoneVertexCount)
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ZoneTooFewVertices,
                            LevelSelection.Zone(kind, i),
                            $"区域顶点少于 {LevelEditController.MinZoneVertexCount} 个, 无法构成面积。"
                        )
                    );
                }
            }
        }

        /// <summary>校验关卡静态对象。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="issues">问题收集列表。</param>
        private static void ValidateObjects(LevelDefinition definition, List<LevelValidationIssue> issues)
        {
            if (definition.Objects == null)
                return;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (StageObjectData item in definition.Objects)
            {
                if (item == null)
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ObjectIdMissing,
                            LevelSelection.None,
                            "关卡对象数据为空。"
                        )
                    );
                    continue;
                }
                LevelSelection target = LevelSelection.StageObject(item.ObjectId);
                if (string.IsNullOrWhiteSpace(item.ObjectId))
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ObjectIdMissing,
                            LevelSelection.None,
                            "关卡对象缺少稳定 ID。"
                        )
                    );
                }
                else if (!seenIds.Add(item.ObjectId))
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ObjectIdDuplicate,
                            target,
                            $"关卡对象稳定 ID '{item.ObjectId}' 重复。"
                        )
                    );
                }
                if (!LevelPaletteCatalog.IsKnownStageObjectPrefab(item.PrefabId))
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ObjectPrefabUnknown,
                            target,
                            $"对象 '{item.ObjectId}' 的预制体 '{item.PrefabId}' 不在关卡调色板内。"
                        )
                    );
                }
                if (!(item.ScaleX > 0f) || !(item.ScaleY > 0f))
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ObjectScaleInvalid,
                            target,
                            $"对象 '{item.ObjectId}' 的缩放必须大于 0。"
                        )
                    );
                }
                ValidateParameters(item, target, issues);
            }
        }

        /// <summary>校验单个对象的白名单参数。</summary>
        /// <param name="item">关卡对象。</param>
        /// <param name="target">该对象的选中项。</param>
        /// <param name="issues">问题收集列表。</param>
        private static void ValidateParameters(
            StageObjectData item,
            LevelSelection target,
            List<LevelValidationIssue> issues
        )
        {
            if (item.Parameters == null)
                return;
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ParameterData parameter in item.Parameters)
            {
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key))
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ObjectParameterKeyMissing,
                            target,
                            $"对象 '{item.ObjectId}' 存在空参数键。"
                        )
                    );
                    continue;
                }
                if (!seenKeys.Add(parameter.Key))
                {
                    issues.Add(
                        new LevelValidationIssue(
                            LevelValidationCode.ObjectParameterKeyDuplicate,
                            target,
                            $"对象 '{item.ObjectId}' 的参数键 '{parameter.Key}' 重复。"
                        )
                    );
                }
            }
        }
    }
}
