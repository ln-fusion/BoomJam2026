using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Contracts.Content;
using Game.Foundation;
using UnityEngine;

namespace Game.Editor.Level
{
    /// <summary>
    /// 关卡 Authoring 数据的编辑操作集合; 只改数据, 不涉及 UI 与绘制。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 视口与属性面板都通过本类修改关卡, 保证所有输入校验与稳定 ID 分配只有一处实现。
    /// 每个操作在参数非法时返回携带错误码的失败结果, 且不留下部分修改。
    /// </para>
    /// <para>
    /// 稳定 ID 分配是确定性的: 在同一份数据上重复分配同一前缀会得到同一序号,
    /// 使保存结果可复现、便于版本比对。区域与对象在保存时**不重排**,
    /// 顺序稳定性由内容编译器（C28）在 Authoring → Generated 阶段负责。
    /// </para>
    /// </remarks>
    public sealed class LevelEditController
    {
        /// <summary>区域顶点数量的下限; 少于三个顶点无法构成面积。</summary>
        public const int MinZoneVertexCount = 3;

        /// <summary>区域稳定 ID 的可部署区前缀。</summary>
        public const string DeployableZoneIdPrefix = "zone.deployable.";

        /// <summary>区域稳定 ID 的禁放区前缀。</summary>
        public const string ForbiddenZoneIdPrefix = "zone.forbidden.";

        /// <summary>对象稳定 ID 的固定前缀。</summary>
        public const string ObjectIdPrefix = "object.";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly LevelAuthoringData _data;

        /// <summary>创建编辑控制器。</summary>
        /// <param name="data">待编辑的 Authoring 数据; 其 <see cref="LevelAuthoringData.Definition"/> 必须非空。</param>
        /// <exception cref="ArgumentNullException">数据为空或其定义为空时抛出。</exception>
        public LevelEditController(LevelAuthoringData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (_data.Definition == null)
                throw new ArgumentException("The authoring data requires a level definition.", nameof(data));
            EnsureCollections();
        }

        /// <summary>当前正在编辑的关卡定义。</summary>
        public LevelDefinition Definition => _data.Definition;

        /// <summary>把四个世界边界角按 <see cref="WorldBoundsCorner"/> 顺序取出。</summary>
        /// <returns>
        /// 长度为 4 的数组, 下标与枚举值对应; 世界边界缺失时返回四个零向量。
        /// </returns>
        public Vector2[] GetWorldBoundsCorners()
        {
            BoundsData bounds = _data.Definition.WorldBounds;
            if (bounds == null)
                return new Vector2[4];
            return new[]
            {
                new Vector2(bounds.MinX, bounds.MinY),
                new Vector2(bounds.MaxX, bounds.MinY),
                new Vector2(bounds.MaxX, bounds.MaxY),
                new Vector2(bounds.MinX, bounds.MaxY),
            };
        }

        /// <summary>整体设置世界边界。</summary>
        /// <param name="minX">最小 X。</param>
        /// <param name="minY">最小 Y。</param>
        /// <param name="maxX">最大 X。</param>
        /// <param name="maxY">最大 Y。</param>
        /// <returns>
        /// 成功返回 <see cref="Result.Success"/>; 任一值为非有限值, 或最大边不大于最小边时
        /// 返回 <see cref="ErrorCode.InvalidArgument"/>。
        /// </returns>
        public Result SetWorldBounds(float minX, float minY, float maxX, float maxY)
        {
            if (!IsFinite(minX) || !IsFinite(minY) || !IsFinite(maxX) || !IsFinite(maxY))
                return Result.Failure(ErrorCode.InvalidArgument, "World bounds must be finite numbers.");
            if (!(maxX > minX) || !(maxY > minY))
                return Result.Failure(ErrorCode.InvalidArgument, "World bounds require MaxX > MinX and MaxY > MinY.");
            _data.Definition.WorldBounds = new BoundsData
            {
                MinX = minX,
                MinY = minY,
                MaxX = maxX,
                MaxY = maxY,
            };
            return Result.Success();
        }

        /// <summary>拖动世界边界的一个角; 对角保持不动。</summary>
        /// <param name="corner">被拖动的角。</param>
        /// <param name="position">该角的新世界坐标。</param>
        /// <returns>失败语义同 <see cref="SetWorldBounds"/>: 新位置会使边界反向或退化时拒绝。</returns>
        public Result MoveWorldBoundsCorner(WorldBoundsCorner corner, Vector2 position)
        {
            BoundsData bounds = _data.Definition.WorldBounds;
            if (bounds == null)
                return Result.Failure(ErrorCode.OperationNotAllowed, "The level has no world bounds to resize.");
            if (!IsFinite(position.x) || !IsFinite(position.y))
                return Result.Failure(ErrorCode.InvalidArgument, "A world bounds corner must be a finite position.");

            float minX = bounds.MinX;
            float minY = bounds.MinY;
            float maxX = bounds.MaxX;
            float maxY = bounds.MaxY;
            switch (corner)
            {
                case WorldBoundsCorner.LeftBottom:
                    minX = position.x;
                    minY = position.y;
                    break;
                case WorldBoundsCorner.RightBottom:
                    maxX = position.x;
                    minY = position.y;
                    break;
                case WorldBoundsCorner.RightTop:
                    maxX = position.x;
                    maxY = position.y;
                    break;
                default:
                    minX = position.x;
                    maxY = position.y;
                    break;
            }
            return SetWorldBounds(minX, minY, maxX, maxY);
        }

        /// <summary>按矩形范围新增一个区域, 顶点按逆时针给出。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="minX">最小 X。</param>
        /// <param name="minY">最小 Y。</param>
        /// <param name="maxX">最大 X。</param>
        /// <param name="maxY">最大 Y。</param>
        /// <param name="selection">新增区域的选中项; 失败时为 <see cref="LevelSelection.None"/>。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 范围退化或非有限时返回 <see cref="ErrorCode.InvalidArgument"/>。</returns>
        public Result AddRectZone(
            ZoneKind kind,
            float minX,
            float minY,
            float maxX,
            float maxY,
            out LevelSelection selection
        )
        {
            selection = LevelSelection.None;
            if (!IsFinite(minX) || !IsFinite(minY) || !IsFinite(maxX) || !IsFinite(maxY))
                return Result.Failure(ErrorCode.InvalidArgument, "A zone rectangle must use finite numbers.");
            if (!(maxX > minX) || !(maxY > minY))
                return Result.Failure(
                    ErrorCode.InvalidArgument,
                    "A zone rectangle requires positive width and height."
                );
            var vertices = new List<Vector2>
            {
                new Vector2(minX, minY),
                new Vector2(maxX, minY),
                new Vector2(maxX, maxY),
                new Vector2(minX, maxY),
            };
            return AddZone(kind, vertices, out selection);
        }

        /// <summary>新增一个区域; 稳定 ID 按现有同种区域的最大序号递增分配。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="vertices">按顺序排列的顶点; 数量不得少于 <see cref="MinZoneVertexCount"/>。</param>
        /// <param name="selection">新增区域的选中项; 失败时为 <see cref="LevelSelection.None"/>。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 顶点数量不足或含非有限值时返回 <see cref="ErrorCode.InvalidArgument"/>。</returns>
        public Result AddZone(ZoneKind kind, IReadOnlyList<Vector2> vertices, out LevelSelection selection)
        {
            selection = LevelSelection.None;
            if (vertices == null)
                return Result.Failure(ErrorCode.InvalidArgument, "A zone requires a vertex list.");
            if (vertices.Count < MinZoneVertexCount)
                return Result.Failure(
                    ErrorCode.InvalidArgument,
                    $"A zone requires at least {MinZoneVertexCount} vertices."
                );
            for (int i = 0; i < vertices.Count; i++)
            {
                if (!IsFinite(vertices[i].x) || !IsFinite(vertices[i].y))
                    return Result.Failure(ErrorCode.InvalidArgument, "Zone vertices must be finite positions.");
            }

            List<ZoneData> target = ZoneList(kind);
            var zone = new ZoneData { ZoneId = AllocateZoneId(kind), Vertices = new List<PointData>(vertices.Count) };
            for (int i = 0; i < vertices.Count; i++)
                zone.Vertices.Add(new PointData { X = vertices[i].x, Y = vertices[i].y });
            target.Add(zone);
            selection = LevelSelection.Zone(kind, target.Count - 1);
            return Result.Success();
        }

        /// <summary>删除一个区域。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 下标越界返回 <see cref="ErrorCode.NotFound"/>。</returns>
        public Result RemoveZone(ZoneKind kind, int zoneIndex)
        {
            List<ZoneData> target = ZoneList(kind);
            if (!IsValidZoneIndex(target, zoneIndex))
                return Result.Failure(ErrorCode.NotFound, "The zone index is out of range.");
            target.RemoveAt(zoneIndex);
            return Result.Success();
        }

        /// <summary>整体平移一个区域的所有顶点。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <param name="delta">平移量; 两个分量都必须为有限值。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 下标越界或平移量非有限时返回失败。</returns>
        public Result TranslateZone(ZoneKind kind, int zoneIndex, Vector2 delta)
        {
            if (!IsFinite(delta.x) || !IsFinite(delta.y))
                return Result.Failure(ErrorCode.InvalidArgument, "A zone translation must be finite.");
            if (!TryGetZone(kind, zoneIndex, out ZoneData zone))
                return Result.Failure(ErrorCode.NotFound, "The zone index is out of range.");
            foreach (PointData vertex in zone.Vertices)
            {
                vertex.X += delta.x;
                vertex.Y += delta.y;
            }
            return Result.Success();
        }

        /// <summary>移动区域的一个顶点。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <param name="vertexIndex">顶点下标。</param>
        /// <param name="position">顶点的新世界坐标。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 下标越界或坐标非有限时返回失败。</returns>
        public Result SetZoneVertex(ZoneKind kind, int zoneIndex, int vertexIndex, Vector2 position)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y))
                return Result.Failure(ErrorCode.InvalidArgument, "A zone vertex must be a finite position.");
            if (!TryGetZone(kind, zoneIndex, out ZoneData zone))
                return Result.Failure(ErrorCode.NotFound, "The zone index is out of range.");
            if (!IsValidVertexIndex(zone, vertexIndex))
                return Result.Failure(ErrorCode.NotFound, "The zone vertex index is out of range.");
            zone.Vertices[vertexIndex].X = position.x;
            zone.Vertices[vertexIndex].Y = position.y;
            return Result.Success();
        }

        /// <summary>在区域指定位置插入一个顶点; 用于把一条边细分为两条。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <param name="insertIndex">插入位置; 取值范围为 0 到顶点数量之间。</param>
        /// <param name="position">新顶点的世界坐标。</param>
        /// <param name="selection">新顶点的选中项; 失败时为 <see cref="LevelSelection.None"/>。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 下标越界或坐标非有限时返回失败。</returns>
        public Result InsertZoneVertex(
            ZoneKind kind,
            int zoneIndex,
            int insertIndex,
            Vector2 position,
            out LevelSelection selection
        )
        {
            selection = LevelSelection.None;
            if (!IsFinite(position.x) || !IsFinite(position.y))
                return Result.Failure(ErrorCode.InvalidArgument, "A zone vertex must be a finite position.");
            if (!TryGetZone(kind, zoneIndex, out ZoneData zone))
                return Result.Failure(ErrorCode.NotFound, "The zone index is out of range.");
            if (insertIndex < 0 || insertIndex > zone.Vertices.Count)
                return Result.Failure(ErrorCode.InvalidArgument, "The insert index is out of range.");

            zone.Vertices.Insert(insertIndex, new PointData { X = position.x, Y = position.y });
            selection = LevelSelection.ZoneVertex(kind, zoneIndex, insertIndex);
            return Result.Success();
        }

        /// <summary>删除区域的一个顶点; 结果顶点数不得少于 <see cref="MinZoneVertexCount"/>。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <param name="vertexIndex">顶点下标。</param>
        /// <returns>
        /// 成功返回 <see cref="Result.Success"/>; 下标越界返回 <see cref="ErrorCode.NotFound"/>,
        /// 删除后顶点数会低于下限时返回 <see cref="ErrorCode.OperationNotAllowed"/>。
        /// </returns>
        public Result RemoveZoneVertex(ZoneKind kind, int zoneIndex, int vertexIndex)
        {
            if (!TryGetZone(kind, zoneIndex, out ZoneData zone))
                return Result.Failure(ErrorCode.NotFound, "The zone index is out of range.");
            if (!IsValidVertexIndex(zone, vertexIndex))
                return Result.Failure(ErrorCode.NotFound, "The zone vertex index is out of range.");
            if (zone.Vertices.Count <= MinZoneVertexCount)
                return Result.Failure(
                    ErrorCode.OperationNotAllowed,
                    $"A zone cannot have fewer than {MinZoneVertexCount} vertices."
                );
            zone.Vertices.RemoveAt(vertexIndex);
            return Result.Success();
        }

        /// <summary>新增一个关卡静态对象。</summary>
        /// <param name="prefabId">预制体稳定标识; 必须来自 <see cref="LevelPaletteCatalog"/>。</param>
        /// <param name="position">世界坐标。</param>
        /// <param name="selection">新增对象的选中项; 失败时为 <see cref="LevelSelection.None"/>。</param>
        /// <returns>
        /// 成功返回 <see cref="Result.Success"/>; 预制体标识未登记返回 <see cref="ErrorCode.NotFound"/>,
        /// 坐标非有限返回 <see cref="ErrorCode.InvalidArgument"/>。
        /// </returns>
        public Result AddObject(string prefabId, Vector2 position, out LevelSelection selection)
        {
            selection = LevelSelection.None;
            if (!IsFinite(position.x) || !IsFinite(position.y))
                return Result.Failure(ErrorCode.InvalidArgument, "An object position must be finite.");
            if (!LevelPaletteCatalog.IsKnownStageObjectPrefab(prefabId))
                return Result.Failure(
                    ErrorCode.NotFound,
                    $"PrefabId '{prefabId}' is not registered in the level palette."
                );

            string objectId = AllocateObjectId(prefabId);
            var data = new StageObjectData
            {
                ObjectId = objectId,
                PrefabId = prefabId,
                PositionX = position.x,
                PositionY = position.y,
                RotationZ = 0f,
                ScaleX = 1f,
                ScaleY = 1f,
                Parameters = new List<ParameterData>(),
            };
            Definition.Objects.Add(data);
            selection = LevelSelection.StageObject(objectId);
            return Result.Success();
        }

        /// <summary>删除一个关卡静态对象。</summary>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 未找到返回 <see cref="ErrorCode.NotFound"/>。</returns>
        public Result RemoveObject(string objectId)
        {
            if (!TryGetObject(objectId, out StageObjectData data))
                return Result.Failure(ErrorCode.NotFound, "The object id does not exist in this level.");
            Definition.Objects.Remove(data);
            return Result.Success();
        }

        /// <summary>设置对象的世界坐标。</summary>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <param name="position">新的世界坐标。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 未找到或坐标非有限时返回失败。</returns>
        public Result SetObjectPosition(string objectId, Vector2 position)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y))
                return Result.Failure(ErrorCode.InvalidArgument, "An object position must be finite.");
            if (!TryGetObject(objectId, out StageObjectData data))
                return Result.Failure(ErrorCode.NotFound, "The object id does not exist in this level.");
            data.PositionX = position.x;
            data.PositionY = position.y;
            return Result.Success();
        }

        /// <summary>设置对象的绕 Z 轴旋转角度。</summary>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <param name="rotationZ">角度（度）。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 未找到或角度非有限时返回失败。</returns>
        public Result SetObjectRotation(string objectId, float rotationZ)
        {
            if (!IsFinite(rotationZ))
                return Result.Failure(ErrorCode.InvalidArgument, "An object rotation must be finite.");
            if (!TryGetObject(objectId, out StageObjectData data))
                return Result.Failure(ErrorCode.NotFound, "The object id does not exist in this level.");
            data.RotationZ = rotationZ;
            return Result.Success();
        }

        /// <summary>设置对象的缩放; 关卡静态对象允许非等比缩放。</summary>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <param name="scaleX">X 缩放; 必须为正。</param>
        /// <param name="scaleY">Y 缩放; 必须为正。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 未找到或缩放非正/非有限时返回失败。</returns>
        public Result SetObjectScale(string objectId, float scaleX, float scaleY)
        {
            if (!IsFinite(scaleX) || !IsFinite(scaleY))
                return Result.Failure(ErrorCode.InvalidArgument, "An object scale must be finite.");
            if (!(scaleX > 0f) || !(scaleY > 0f))
                return Result.Failure(ErrorCode.InvalidArgument, "An object scale must be positive.");
            if (!TryGetObject(objectId, out StageObjectData data))
                return Result.Failure(ErrorCode.NotFound, "The object id does not exist in this level.");
            data.ScaleX = scaleX;
            data.ScaleY = scaleY;
            return Result.Success();
        }

        /// <summary>写入或覆盖对象的白名单参数。</summary>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <param name="key">参数键; 不能为空或含首尾空白。</param>
        /// <param name="value">参数值; 以字符串承载, 由白名单校验解释, 可为空。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 未找到、键非法或键重复时返回失败。</returns>
        public Result SetObjectParameter(string objectId, string key, string value)
        {
            if (!TryGetObject(objectId, out StageObjectData data))
                return Result.Failure(ErrorCode.NotFound, "The object id does not exist in this level.");
            Result keyCheck = ValidateParameterKey(key);
            if (!keyCheck.IsSuccess)
                return keyCheck;
            // 键必须唯一: 重复键会让白名单解释顺序依赖数组顺序, 破坏可复现性。
            for (int i = 0; i < data.Parameters.Count; i++)
            {
                if (string.Equals(data.Parameters[i]?.Key, key, StringComparison.Ordinal))
                {
                    data.Parameters[i].Value = value ?? string.Empty;
                    return Result.Success();
                }
            }
            data.Parameters.Add(new ParameterData { Key = key, Value = value ?? string.Empty });
            return Result.Success();
        }

        /// <summary>删除对象的一个白名单参数。</summary>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <param name="key">参数键。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 未找到返回 <see cref="ErrorCode.NotFound"/>。</returns>
        public Result RemoveObjectParameter(string objectId, string key)
        {
            if (!TryGetObject(objectId, out StageObjectData data))
                return Result.Failure(ErrorCode.NotFound, "The object id does not exist in this level.");
            for (int i = 0; i < data.Parameters.Count; i++)
            {
                if (string.Equals(data.Parameters[i]?.Key, key, StringComparison.Ordinal))
                {
                    data.Parameters.RemoveAt(i);
                    return Result.Success();
                }
            }
            return Result.Failure(ErrorCode.NotFound, "The object has no parameter with that key.");
        }

        /// <summary>设置小车出生点; 出生点缺失时创建。</summary>
        /// <param name="position">世界坐标。</param>
        /// <param name="rotationZ">初始朝向角（度）。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 坐标或角度非有限时返回 <see cref="ErrorCode.InvalidArgument"/>。</returns>
        public Result SetStartPoint(Vector2 position, float rotationZ)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(rotationZ))
                return Result.Failure(ErrorCode.InvalidArgument, "The start point requires finite numbers.");
            if (Definition.StartPoint == null)
                Definition.StartPoint = new SpawnPointData();
            Definition.StartPoint.PositionX = position.x;
            Definition.StartPoint.PositionY = position.y;
            Definition.StartPoint.RotationZ = rotationZ;
            return Result.Success();
        }

        /// <summary>设置终点区域; 终点缺失时创建。</summary>
        /// <param name="position">区域中心世界坐标。</param>
        /// <param name="width">区域宽度; 必须为正。</param>
        /// <param name="height">区域高度; 必须为正。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 坐标非有限或宽高非正时返回 <see cref="ErrorCode.InvalidArgument"/>。</returns>
        public Result SetGoalPoint(Vector2 position, float width, float height)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(width) || !IsFinite(height))
                return Result.Failure(ErrorCode.InvalidArgument, "The goal point requires finite numbers.");
            if (!(width > 0f) || !(height > 0f))
                return Result.Failure(ErrorCode.InvalidArgument, "The goal area requires positive width and height.");
            if (Definition.GoalPoint == null)
                Definition.GoalPoint = new GoalPointData();
            Definition.GoalPoint.PositionX = position.x;
            Definition.GoalPoint.PositionY = position.y;
            Definition.GoalPoint.Width = width;
            Definition.GoalPoint.Height = height;
            return Result.Success();
        }

        /// <summary>删除小车出生点; 用于让校验报出"缺少起点"。</summary>
        /// <returns>成功返回 <see cref="Result.Success"/>; 本就没有起点时返回 <see cref="ErrorCode.NotFound"/>。</returns>
        public Result RemoveStartPoint()
        {
            if (Definition.StartPoint == null)
                return Result.Failure(ErrorCode.NotFound, "The level has no start point.");
            Definition.StartPoint = null;
            return Result.Success();
        }

        /// <summary>删除终点区域; 用于让校验报出"缺少终点"。</summary>
        /// <returns>成功返回 <see cref="Result.Success"/>; 本就没有终点时返回 <see cref="ErrorCode.NotFound"/>。</returns>
        public Result RemoveGoalPoint()
        {
            if (Definition.GoalPoint == null)
                return Result.Failure(ErrorCode.NotFound, "The level has no goal point.");
            Definition.GoalPoint = null;
            return Result.Success();
        }

        /// <summary>按稳定 ID 查找关卡静态对象。</summary>
        /// <param name="objectId">对象稳定 ID。</param>
        /// <param name="data">查找到的对象; 未命中时为 null。</param>
        /// <returns>存在时返回 true。</returns>
        public bool TryGetObject(string objectId, out StageObjectData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(objectId) || Definition.Objects == null)
                return false;
            foreach (StageObjectData item in Definition.Objects)
            {
                if (item != null && string.Equals(item.ObjectId, objectId, StringComparison.Ordinal))
                {
                    data = item;
                    return true;
                }
            }
            return false;
        }

        /// <summary>按下标查找区域。</summary>
        /// <param name="kind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <param name="zone">查找到的区域; 未命中时为 null。</param>
        /// <returns>存在时返回 true。</returns>
        public bool TryGetZone(ZoneKind kind, int zoneIndex, out ZoneData zone)
        {
            zone = null;
            List<ZoneData> target = ZoneList(kind);
            if (!IsValidZoneIndex(target, zoneIndex))
                return false;
            zone = target[zoneIndex];
            return zone != null;
        }

        /// <summary>取得指定种类的区域集合; 集合缺失时补建。</summary>
        /// <param name="kind">区域种类。</param>
        /// <returns>可直接修改的区域集合引用。</returns>
        public List<ZoneData> ZoneList(ZoneKind kind) =>
            kind == ZoneKind.Deployable ? Definition.DeployableZones : Definition.ForbiddenZones;

        /// <summary>分配下一个可用的区域稳定 ID。</summary>
        /// <param name="kind">区域种类。</param>
        /// <returns>形如 <c>zone.deployable.03</c> 的稳定 ID; 序号取现有同前缀 ID 的最大值加一。</returns>
        public string AllocateZoneId(ZoneKind kind)
        {
            string prefix = kind == ZoneKind.Deployable ? DeployableZoneIdPrefix : ForbiddenZoneIdPrefix;
            List<ZoneData> target = ZoneList(kind);
            int next = 1;
            foreach (ZoneData zone in target)
            {
                if (zone?.ZoneId == null || !zone.ZoneId.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (!int.TryParse(zone.ZoneId.AsSpan(prefix.Length), NumberStyles.None, Invariant, out int value))
                    continue;
                if (value >= next)
                    next = value + 1;
            }
            return prefix + next.ToString("D2", Invariant);
        }

        /// <summary>分配下一个可用的对象稳定 ID。</summary>
        /// <param name="prefabId">预制体稳定标识; 其短名参与对象 ID 组成。</param>
        /// <returns>形如 <c>object.ground_platform.02</c> 的稳定 ID; 同短名序号取最大值加一。</returns>
        public string AllocateObjectId(string prefabId)
        {
            string prefix = ObjectIdPrefix + LevelPaletteCatalog.GetPrefabShortName(prefabId) + ".";
            int next = 1;
            foreach (StageObjectData item in Definition.Objects)
            {
                if (item?.ObjectId == null || !item.ObjectId.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (!int.TryParse(item.ObjectId.AsSpan(prefix.Length), NumberStyles.None, Invariant, out int value))
                    continue;
                if (value >= next)
                    next = value + 1;
            }
            return prefix + next.ToString("D2", Invariant);
        }

        /// <summary>补建可能为 null 的集合字段, 使后续编辑不必到处判空。</summary>
        private void EnsureCollections()
        {
            if (Definition.Objects == null)
                Definition.Objects = new List<StageObjectData>();
            if (Definition.DeployableZones == null)
                Definition.DeployableZones = new List<ZoneData>();
            if (Definition.ForbiddenZones == null)
                Definition.ForbiddenZones = new List<ZoneData>();
        }

        /// <summary>校验数组下标是否落在区域集合范围内。</summary>
        /// <param name="zones">区域集合。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <returns>下标有效时返回 true。</returns>
        private static bool IsValidZoneIndex(List<ZoneData> zones, int zoneIndex) =>
            zones != null && zoneIndex >= 0 && zoneIndex < zones.Count;

        /// <summary>校验区域顶点下标是否有效。</summary>
        /// <param name="zone">区域; 可为 null。</param>
        /// <param name="vertexIndex">顶点下标。</param>
        /// <returns>下标有效时返回 true。</returns>
        private static bool IsValidVertexIndex(ZoneData zone, int vertexIndex) =>
            zone?.Vertices != null && vertexIndex >= 0 && vertexIndex < zone.Vertices.Count;

        /// <summary>校验参数键的合法性。</summary>
        /// <param name="key">参数键。</param>
        /// <returns>成功返回 <see cref="Result.Success"/>; 为空或含首尾空白时返回失败。</returns>
        private static Result ValidateParameterKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Result.Failure(ErrorCode.InvalidArgument, "A parameter key is required.");
            if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidArgument, "A parameter key cannot have surrounding whitespace.");
            return Result.Success();
        }

        /// <summary>判断浮点值是否为有限值。</summary>
        /// <param name="value">待检查的值。</param>
        /// <returns>非 NaN 且非无穷时返回 true。</returns>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
