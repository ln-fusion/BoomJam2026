using System;

namespace Game.Editor.Level
{
    /// <summary>
    /// 关卡区域种类; 对应 <see cref="Game.Contracts.Content.LevelDefinition"/> 的两个区域集合。
    /// </summary>
    public enum ZoneKind
    {
        /// <summary>允许玩家部署能力框的区域。</summary>
        Deployable = 0,

        /// <summary>禁止玩家部署能力框的区域。</summary>
        Forbidden = 1,
    }

    /// <summary>
    /// 世界边界矩形的四个角; 顺序为逆时针。
    /// </summary>
    public enum WorldBoundsCorner
    {
        /// <summary>左下角; 同时决定 MinX 与 MinY。</summary>
        LeftBottom = 0,

        /// <summary>右下角; 同时决定 MaxX 与 MinY。</summary>
        RightBottom = 1,

        /// <summary>右上角; 同时决定 MaxX 与 MaxY。</summary>
        RightTop = 2,

        /// <summary>左上角; 同时决定 MinX 与 MaxY。</summary>
        LeftTop = 3,
    }

    /// <summary>
    /// 视口与属性面板共用的选中项种类。
    /// </summary>
    public enum LevelSelectionKind
    {
        /// <summary>无选中。</summary>
        None = 0,

        /// <summary>选中关卡静态对象; 由 <see cref="LevelSelection.ObjectId"/> 定位。</summary>
        StageObject = 1,

        /// <summary>选中小车出生点。</summary>
        SpawnPoint = 2,

        /// <summary>选中终点区域。</summary>
        GoalPoint = 3,

        /// <summary>选中区域整体。</summary>
        Zone = 4,

        /// <summary>选中区域的一个顶点。</summary>
        ZoneVertex = 5,

        /// <summary>选中世界边界的一个角。</summary>
        WorldBoundsCorner = 6,
    }

    /// <summary>
    /// 关卡编辑器的选中项; 以值语义传递, 使视口、属性面板与校验结果可以互相指认同一目标。
    /// </summary>
    /// <remarks>
    /// 组件字段按 <see cref="Kind"/> 解释: 只有 <see cref="LevelSelectionKind.StageObject"/> 使用
    /// <see cref="ObjectId"/>; 区域相关种类使用 <see cref="ZoneKind"/>、
    /// <see cref="ZoneIndex"/> 与 <see cref="VertexIndex"/>; 世界边界角使用 <see cref="Corner"/>。
    /// 其余字段保持默认值, 不参与相等比较以外的语义。
    /// </remarks>
    public readonly struct LevelSelection : IEquatable<LevelSelection>
    {
        /// <summary>选中项种类。</summary>
        public LevelSelectionKind Kind { get; }

        /// <summary>对象稳定 ID; 仅 <see cref="LevelSelectionKind.StageObject"/> 有意义。</summary>
        public string ObjectId { get; }

        /// <summary>区域所属种类; 仅区域相关种类有意义。</summary>
        public ZoneKind ZoneKind { get; }

        /// <summary>区域在所属集合中的下标; 仅区域相关种类有意义。</summary>
        public int ZoneIndex { get; }

        /// <summary>顶点在区域内的下标; 仅 <see cref="LevelSelectionKind.ZoneVertex"/> 有意义。</summary>
        public int VertexIndex { get; }

        /// <summary>世界边界角; 仅 <see cref="LevelSelectionKind.WorldBoundsCorner"/> 有意义。</summary>
        public WorldBoundsCorner Corner { get; }

        /// <summary>创建选中项; 由静态工厂方法使用。</summary>
        /// <param name="kind">选中项种类。</param>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <param name="zoneKind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <param name="vertexIndex">顶点下标。</param>
        /// <param name="corner">世界边界角。</param>
        private LevelSelection(
            LevelSelectionKind kind,
            string objectId,
            ZoneKind zoneKind,
            int zoneIndex,
            int vertexIndex,
            WorldBoundsCorner corner
        )
        {
            Kind = kind;
            ObjectId = objectId;
            ZoneKind = zoneKind;
            ZoneIndex = zoneIndex;
            VertexIndex = vertexIndex;
            Corner = corner;
        }

        /// <summary>无选中项。</summary>
        public static LevelSelection None { get; } =
            new LevelSelection(
                LevelSelectionKind.None,
                string.Empty,
                ZoneKind.Deployable,
                -1,
                -1,
                WorldBoundsCorner.LeftBottom
            );

        /// <summary>是否为空选中。</summary>
        public bool IsNone => Kind == LevelSelectionKind.None;

        /// <summary>是否指向区域的一个顶点。</summary>
        public bool IsZoneVertex => Kind == LevelSelectionKind.ZoneVertex;

        /// <summary>是否指向区域整体。</summary>
        public bool IsZone => Kind == LevelSelectionKind.Zone || Kind == LevelSelectionKind.ZoneVertex;

        /// <summary>创建指向关卡静态对象的选中项。</summary>
        /// <param name="objectId">对象稳定 ID; 为空或空白时返回 <see cref="None"/>。</param>
        /// <returns>选中项。</returns>
        public static LevelSelection StageObject(string objectId) =>
            string.IsNullOrWhiteSpace(objectId)
                ? None
                : new LevelSelection(
                    LevelSelectionKind.StageObject,
                    objectId,
                    ZoneKind.Deployable,
                    -1,
                    -1,
                    WorldBoundsCorner.LeftBottom
                );

        /// <summary>创建指向小车出生点的选中项。</summary>
        /// <returns>选中项。</returns>
        public static LevelSelection SpawnPoint() =>
            new LevelSelection(
                LevelSelectionKind.SpawnPoint,
                string.Empty,
                ZoneKind.Deployable,
                -1,
                -1,
                WorldBoundsCorner.LeftBottom
            );

        /// <summary>创建指向终点区域的选中项。</summary>
        /// <returns>选中项。</returns>
        public static LevelSelection GoalPoint() =>
            new LevelSelection(
                LevelSelectionKind.GoalPoint,
                string.Empty,
                ZoneKind.Deployable,
                -1,
                -1,
                WorldBoundsCorner.LeftBottom
            );

        /// <summary>创建指向区域整体的选中项。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="zoneIndex">区域下标; 负值返回 <see cref="None"/>。</param>
        /// <returns>选中项。</returns>
        public static LevelSelection Zone(ZoneKind kind, int zoneIndex) =>
            zoneIndex < 0
                ? None
                : new LevelSelection(
                    LevelSelectionKind.Zone,
                    string.Empty,
                    kind,
                    zoneIndex,
                    -1,
                    WorldBoundsCorner.LeftBottom
                );

        /// <summary>创建指向区域顶点的选中项。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <param name="vertexIndex">顶点下标。</param>
        /// <returns>选中项; 任一非负下标不成立时返回 <see cref="None"/>。</returns>
        public static LevelSelection ZoneVertex(ZoneKind kind, int zoneIndex, int vertexIndex) =>
            zoneIndex < 0 || vertexIndex < 0
                ? None
                : new LevelSelection(
                    LevelSelectionKind.ZoneVertex,
                    string.Empty,
                    kind,
                    zoneIndex,
                    vertexIndex,
                    WorldBoundsCorner.LeftBottom
                );

        /// <summary>创建指向世界边界角的选中项。</summary>
        /// <param name="corner">世界边界角。</param>
        /// <returns>选中项。</returns>
        public static LevelSelection WorldBounds(WorldBoundsCorner corner) =>
            new LevelSelection(LevelSelectionKind.WorldBoundsCorner, string.Empty, ZoneKind.Deployable, -1, -1, corner);

        /// <summary>判断两个选中项是否指向同一目标。</summary>
        /// <param name="other">待比较的选中项。</param>
        /// <returns>种类与全部组件字段相同时返回 true。</returns>
        public bool Equals(LevelSelection other) =>
            Kind == other.Kind
            && string.Equals(ObjectId, other.ObjectId, StringComparison.Ordinal)
            && ZoneKind == other.ZoneKind
            && ZoneIndex == other.ZoneIndex
            && VertexIndex == other.VertexIndex
            && Corner == other.Corner;

        /// <summary>判断对象是否为相同选中项。</summary>
        /// <param name="obj">待比较对象。</param>
        /// <returns>类型与值均相同时返回 true。</returns>
        public override bool Equals(object obj) => obj is LevelSelection other && Equals(other);

        /// <summary>返回选中项的哈希码。</summary>
        /// <returns>基于全部组件字段计算的哈希码。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 397 ^ (ObjectId == null ? 0 : StringComparer.Ordinal.GetHashCode(ObjectId));
                hash = hash * 397 ^ (int)ZoneKind;
                hash = hash * 397 ^ ZoneIndex;
                hash = hash * 397 ^ VertexIndex;
                hash = hash * 397 ^ (int)Corner;
                return hash;
            }
        }

        /// <summary>判断两个选中项是否相等。</summary>
        /// <param name="left">左侧选中项。</param>
        /// <param name="right">右侧选中项。</param>
        /// <returns>相等时返回 true。</returns>
        public static bool operator ==(LevelSelection left, LevelSelection right) => left.Equals(right);

        /// <summary>判断两个选中项是否不相等。</summary>
        /// <param name="left">左侧选中项。</param>
        /// <param name="right">右侧选中项。</param>
        /// <returns>不相等时返回 true。</returns>
        public static bool operator !=(LevelSelection left, LevelSelection right) => !left.Equals(right);

        /// <summary>返回便于日志定位的文本描述。</summary>
        /// <returns>形如 <c>ZoneVertex(Deployable,1,2)</c> 的描述。</returns>
        public override string ToString()
        {
            switch (Kind)
            {
                case LevelSelectionKind.StageObject:
                    return "Object(" + ObjectId + ")";
                case LevelSelectionKind.Zone:
                    return "Zone(" + ZoneKind + "," + ZoneIndex + ")";
                case LevelSelectionKind.ZoneVertex:
                    return "ZoneVertex(" + ZoneKind + "," + ZoneIndex + "," + VertexIndex + ")";
                case LevelSelectionKind.WorldBoundsCorner:
                    return "WorldBounds(" + Corner + ")";
                default:
                    return Kind.ToString();
            }
        }
    }
}
