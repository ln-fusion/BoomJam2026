using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Editor.Level;
using Game.Foundation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Editor
{
    /// <summary>
    /// 验证关卡编辑控制器的区域与世界边界操作: 参数校验、稳定 ID 分配与下标边界。
    /// </summary>
    public sealed class LevelEditControllerZoneTests
    {
        /// <summary>控制器要求传入 Authoring 数据, 且该数据必须已有关卡定义。</summary>
        [Test]
        public void Constructor_RejectsNullData()
        {
            Assert.Throws<System.ArgumentNullException>(() => new LevelEditController(null));
        }

        /// <summary>缺少关卡定义的数据无法编辑, 必须在构造时拒绝而不是后续崩溃。</summary>
        [Test]
        public void Constructor_RejectsDataWithoutDefinition()
        {
            var data = new LevelAuthoringData { Definition = null };

            Assert.Throws<System.ArgumentException>(() => new LevelEditController(data));
        }

        /// <summary>集合字段为 null 时必须补建, 使后续编辑无需判空。</summary>
        [Test]
        public void Constructor_RepairsMissingCollections()
        {
            var data = new LevelAuthoringData
            {
                Definition = new LevelDefinition { LevelId = "official.level.t_01_01" },
            };

            var controller = new LevelEditController(data);

            Assert.That(controller.Definition.Objects, Is.Not.Null);
            Assert.That(controller.Definition.DeployableZones, Is.Not.Null);
            Assert.That(controller.Definition.ForbiddenZones, Is.Not.Null);
        }

        /// <summary>矩形区域必须按逆时针给出四个顶点。</summary>
        [Test]
        public void AddRectZone_CreatesCounterClockwiseRectangle()
        {
            LevelEditController controller = NewController();

            Result result = controller.AddRectZone(ZoneKind.Deployable, -2f, -1f, 2f, 1f, out LevelSelection selection);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(selection.Kind, Is.EqualTo(LevelSelectionKind.Zone));
            Assert.That(selection.ZoneIndex, Is.EqualTo(0));
            ZoneData zone = controller.ZoneList(ZoneKind.Deployable)[0];
            Assert.That(zone.Vertices, Has.Count.EqualTo(4));
            Assert.That(zone.Vertices[0].X, Is.EqualTo(-2f));
            Assert.That(zone.Vertices[0].Y, Is.EqualTo(-1f));
            Assert.That(zone.Vertices[2].X, Is.EqualTo(2f));
            Assert.That(zone.Vertices[2].Y, Is.EqualTo(1f));
            Assert.That(Cross(zone, 0), Is.GreaterThan(0f), "顶点顺序必须为逆时针。");
        }

        /// <summary>退化矩形没有面积, 必须拒绝而不是产生一个不可用区域。</summary>
        [Test]
        public void AddRectZone_RejectsDegenerateRectangle()
        {
            LevelEditController controller = NewController();

            Assert.That(
                controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 0f, 1f, out _).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
            Assert.That(
                controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 1f, -1f, out _).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
            Assert.That(controller.ZoneList(ZoneKind.Deployable), Is.Empty);
        }

        /// <summary>非有限坐标必须被拒绝, 否则会污染关卡数据。</summary>
        [Test]
        public void AddRectZone_RejectsNonFiniteNumbers()
        {
            LevelEditController controller = NewController();

            Assert.That(
                controller.AddRectZone(ZoneKind.Deployable, float.NaN, 0f, 1f, 1f, out _).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
            Assert.That(
                controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, float.PositiveInfinity, 1f, out _).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
        }

        /// <summary>顶点数量少于下限时不能构成区域。</summary>
        [Test]
        public void AddZone_RejectsTooFewVertices()
        {
            LevelEditController controller = NewController();

            Result result = controller.AddZone(
                ZoneKind.Deployable,
                new List<Vector2> { Vector2.zero, Vector2.right },
                out _
            );

            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>三个顶点即构成合法区域, 不要求必须是矩形。</summary>
        [Test]
        public void AddZone_AcceptsTriangle()
        {
            LevelEditController controller = NewController();

            Result result = controller.AddZone(
                ZoneKind.Forbidden,
                new List<Vector2> { Vector2.zero, new Vector2(2f, 0f), new Vector2(0f, 2f) },
                out LevelSelection selection
            );

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(controller.ZoneList(ZoneKind.Forbidden), Has.Count.EqualTo(1));
            Assert.That(controller.ZoneList(ZoneKind.Forbidden)[0].Vertices, Has.Count.EqualTo(3));
            Assert.That(selection.ZoneKind, Is.EqualTo(ZoneKind.Forbidden));
        }

        /// <summary>区域顶点含非有限值时必须拒绝。</summary>
        [Test]
        public void AddZone_RejectsNonFiniteVertex()
        {
            LevelEditController controller = NewController();

            Result result = controller.AddZone(
                ZoneKind.Deployable,
                new List<Vector2> { Vector2.zero, new Vector2(1f, 0f), new Vector2(float.NaN, 1f) },
                out _
            );

            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>稳定 ID 必须按种类各自计数, 从 01 开始且互不干扰。</summary>
        [Test]
        public void AllocateZoneId_UsesPerKindPrefixAndSequence()
        {
            LevelEditController controller = NewController();

            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 1f, 1f, out _);
            controller.AddRectZone(ZoneKind.Deployable, 2f, 0f, 3f, 1f, out _);
            controller.AddRectZone(ZoneKind.Forbidden, 0f, 0f, 1f, 1f, out _);

            Assert.That(controller.ZoneList(ZoneKind.Deployable)[0].ZoneId, Is.EqualTo("zone.deployable.01"));
            Assert.That(controller.ZoneList(ZoneKind.Deployable)[1].ZoneId, Is.EqualTo("zone.deployable.02"));
            Assert.That(controller.ZoneList(ZoneKind.Forbidden)[0].ZoneId, Is.EqualTo("zone.forbidden.01"));
        }

        /// <summary>删除中间区域后再新增必须复用空出的序号之外的最小可用值, 不产生重复 ID。</summary>
        [Test]
        public void AllocateZoneId_AvoidsCollisionAfterRemoval()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 1f, 1f, out _);
            controller.AddRectZone(ZoneKind.Deployable, 2f, 0f, 3f, 1f, out _);
            controller.AddRectZone(ZoneKind.Deployable, 4f, 0f, 5f, 1f, out _);
            controller.RemoveZone(ZoneKind.Deployable, 1);

            controller.AddRectZone(ZoneKind.Deployable, 6f, 0f, 7f, 1f, out _);

            var ids = new List<string>();
            foreach (ZoneData zone in controller.ZoneList(ZoneKind.Deployable))
                ids.Add(zone.ZoneId);
            Assert.That(ids, Is.Unique, "区域稳定 ID 不得重复。");
            Assert.That(ids, Does.Contain("zone.deployable.04"));
        }

        /// <summary>删除越界区域下标必须报未找到, 且不修改集合。</summary>
        [Test]
        public void RemoveZone_RejectsOutOfRangeIndex()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 1f, 1f, out _);

            Assert.That(controller.RemoveZone(ZoneKind.Deployable, 5).ErrorCode, Is.EqualTo(ErrorCode.NotFound));
            Assert.That(controller.RemoveZone(ZoneKind.Deployable, -1).ErrorCode, Is.EqualTo(ErrorCode.NotFound));
            Assert.That(controller.ZoneList(ZoneKind.Deployable), Has.Count.EqualTo(1));
        }

        /// <summary>删除区域必须只影响目标区域。</summary>
        [Test]
        public void RemoveZone_RemovesOnlyTargetZone()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 1f, 1f, out _);
            controller.AddRectZone(ZoneKind.Deployable, 2f, 0f, 3f, 1f, out _);

            Result result = controller.RemoveZone(ZoneKind.Deployable, 0);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(controller.ZoneList(ZoneKind.Deployable), Has.Count.EqualTo(1));
            Assert.That(controller.ZoneList(ZoneKind.Deployable)[0].ZoneId, Is.EqualTo("zone.deployable.02"));
        }

        /// <summary>整体平移必须移动全部顶点且保持形状。</summary>
        [Test]
        public void TranslateZone_MovesAllVertices()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);

            Result result = controller.TranslateZone(ZoneKind.Deployable, 0, new Vector2(3f, -1f));

            Assert.That(result.IsSuccess, Is.True);
            ZoneData zone = controller.ZoneList(ZoneKind.Deployable)[0];
            Assert.That(zone.Vertices[0].X, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(zone.Vertices[0].Y, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(zone.Vertices[2].X, Is.EqualTo(5f).Within(1e-5f));
            Assert.That(zone.Vertices[2].Y, Is.EqualTo(1f).Within(1e-5f));
        }

        /// <summary>平移量非有限时必须拒绝, 且顶点保持原值。</summary>
        [Test]
        public void TranslateZone_RejectsNonFiniteDelta()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);

            Result result = controller.TranslateZone(ZoneKind.Deployable, 0, new Vector2(float.NaN, 0f));

            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
            Assert.That(controller.ZoneList(ZoneKind.Deployable)[0].Vertices[0].X, Is.EqualTo(0f));
        }

        /// <summary>移动单个顶点不得影响其他顶点。</summary>
        [Test]
        public void SetZoneVertex_MovesOnlyTargetVertex()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);

            Result result = controller.SetZoneVertex(ZoneKind.Deployable, 0, 2, new Vector2(9f, 9f));

            Assert.That(result.IsSuccess, Is.True);
            ZoneData zone = controller.ZoneList(ZoneKind.Deployable)[0];
            Assert.That(zone.Vertices[2].X, Is.EqualTo(9f));
            Assert.That(zone.Vertices[2].Y, Is.EqualTo(9f));
            Assert.That(zone.Vertices[0].X, Is.EqualTo(0f));
            Assert.That(zone.Vertices[1].X, Is.EqualTo(2f));
        }

        /// <summary>顶点下标越界必须报未找到。</summary>
        [Test]
        public void SetZoneVertex_RejectsOutOfRangeVertexIndex()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);

            Assert.That(
                controller.SetZoneVertex(ZoneKind.Deployable, 0, 4, Vector2.one).ErrorCode,
                Is.EqualTo(ErrorCode.NotFound)
            );
            Assert.That(
                controller.SetZoneVertex(ZoneKind.Deployable, 0, -1, Vector2.one).ErrorCode,
                Is.EqualTo(ErrorCode.NotFound)
            );
        }

        /// <summary>插入顶点必须落在指定位置并返回该顶点的选中项。</summary>
        [Test]
        public void InsertZoneVertex_InsertsAtRequestedIndex()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);

            Result result = controller.InsertZoneVertex(
                ZoneKind.Deployable,
                0,
                1,
                new Vector2(1f, -1f),
                out LevelSelection selection
            );

            Assert.That(result.IsSuccess, Is.True);
            ZoneData zone = controller.ZoneList(ZoneKind.Deployable)[0];
            Assert.That(zone.Vertices, Has.Count.EqualTo(5));
            Assert.That(zone.Vertices[1].X, Is.EqualTo(1f));
            Assert.That(zone.Vertices[1].Y, Is.EqualTo(-1f));
            Assert.That(selection.Kind, Is.EqualTo(LevelSelectionKind.ZoneVertex));
            Assert.That(selection.VertexIndex, Is.EqualTo(1));
        }

        /// <summary>插入位置必须允许落在末尾, 且越界必须拒绝。</summary>
        [Test]
        public void InsertZoneVertex_BoundsCheck()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);

            Assert.That(
                controller.InsertZoneVertex(ZoneKind.Deployable, 0, 4, Vector2.zero, out _).IsSuccess,
                Is.True,
                "插入位置等于顶点数量表示追加到末尾。"
            );
            Assert.That(
                controller.InsertZoneVertex(ZoneKind.Deployable, 0, 6, Vector2.zero, out _).ErrorCode,
                Is.EqualTo(ErrorCode.InvalidArgument)
            );
        }

        /// <summary>删除顶点后顶点数不得低于下限, 否则区域失去面积。</summary>
        [Test]
        public void RemoveZoneVertex_RefusesBelowMinimum()
        {
            LevelEditController controller = NewController();
            controller.AddZone(
                ZoneKind.Deployable,
                new List<Vector2> { Vector2.zero, new Vector2(2f, 0f), new Vector2(0f, 2f) },
                out _
            );

            Result result = controller.RemoveZoneVertex(ZoneKind.Deployable, 0, 0);

            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.OperationNotAllowed));
            Assert.That(controller.ZoneList(ZoneKind.Deployable)[0].Vertices, Has.Count.EqualTo(3));
        }

        /// <summary>顶点数超过下限时允许删除。</summary>
        [Test]
        public void RemoveZoneVertex_RemovesWhenAboveMinimum()
        {
            LevelEditController controller = NewController();
            controller.AddRectZone(ZoneKind.Deployable, 0f, 0f, 2f, 2f, out _);

            Result result = controller.RemoveZoneVertex(ZoneKind.Deployable, 0, 1);

            Assert.That(result.IsSuccess, Is.True);
            ZoneData zone = controller.ZoneList(ZoneKind.Deployable)[0];
            Assert.That(zone.Vertices, Has.Count.EqualTo(3));
            Assert.That(zone.Vertices[1].X, Is.EqualTo(2f), "删除后原下标 2 的顶点前移。");
        }

        /// <summary>世界边界必须有正面积。</summary>
        [Test]
        public void SetWorldBounds_RejectsInvertedOrDegenerateRange()
        {
            LevelEditController controller = NewController();

            Assert.That(controller.SetWorldBounds(1f, 0f, 1f, 1f).ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
            Assert.That(controller.SetWorldBounds(0f, 1f, 1f, 1f).ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
            Assert.That(controller.SetWorldBounds(1f, 0f, -1f, 1f).ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
        }

        /// <summary>合法世界边界必须写入并可按角点顺序读回。</summary>
        [Test]
        public void SetWorldBounds_WritesCornersInEnumOrder()
        {
            LevelEditController controller = NewController();

            Result result = controller.SetWorldBounds(-4f, -2f, 6f, 3f);
            Vector2[] corners = controller.GetWorldBoundsCorners();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(corners[0], Is.EqualTo(new Vector2(-4f, -2f)));
            Assert.That(corners[1], Is.EqualTo(new Vector2(6f, -2f)));
            Assert.That(corners[2], Is.EqualTo(new Vector2(6f, 3f)));
            Assert.That(corners[3], Is.EqualTo(new Vector2(-4f, 3f)));
        }

        /// <summary>拖动一个角必须保持对角不动。</summary>
        [Test]
        public void MoveWorldBoundsCorner_KeepsOppositeCornerFixed()
        {
            LevelEditController controller = NewController();
            controller.SetWorldBounds(-4f, -2f, 6f, 3f);

            Result result = controller.MoveWorldBoundsCorner(WorldBoundsCorner.LeftBottom, new Vector2(-1f, -1f));

            Assert.That(result.IsSuccess, Is.True);
            Vector2[] corners = controller.GetWorldBoundsCorners();
            Assert.That(corners[0], Is.EqualTo(new Vector2(-1f, -1f)));
            Assert.That(corners[2], Is.EqualTo(new Vector2(6f, 3f)), "对角不得移动。");
        }

        /// <summary>拖动导致边界反向时必须拒绝, 且边界保持原值。</summary>
        [Test]
        public void MoveWorldBoundsCorner_RefusesInversion()
        {
            LevelEditController controller = NewController();
            controller.SetWorldBounds(-4f, -2f, 6f, 3f);

            Result result = controller.MoveWorldBoundsCorner(WorldBoundsCorner.LeftBottom, new Vector2(10f, 10f));

            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.InvalidArgument));
            Assert.That(controller.GetWorldBoundsCorners()[0], Is.EqualTo(new Vector2(-4f, -2f)));
        }

        /// <summary>缺少世界边界时无法拖动角点, 应报操作不允许。</summary>
        [Test]
        public void MoveWorldBoundsCorner_RefusesWhenBoundsMissing()
        {
            LevelEditController controller = NewController();

            Result result = controller.MoveWorldBoundsCorner(WorldBoundsCorner.LeftBottom, Vector2.zero);

            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCode.OperationNotAllowed));
        }

        /// <summary>缺少世界边界时按角点顺序读回四个零向量, 供视口安全绘制。</summary>
        [Test]
        public void GetWorldBoundsCorners_ReturnsZeroesWhenBoundsMissing()
        {
            LevelEditController controller = NewController();

            Vector2[] corners = controller.GetWorldBoundsCorners();

            Assert.That(corners, Has.Length.EqualTo(4));
            foreach (Vector2 corner in corners)
                Assert.That(corner, Is.EqualTo(Vector2.zero));
        }

        /// <summary>构造一个不含世界边界的空关卡控制器。</summary>
        /// <returns>编辑控制器。</returns>
        private static LevelEditController NewController() =>
            new LevelEditController(
                new LevelAuthoringData { Definition = new LevelDefinition { LevelId = "official.level.t_01_01" } }
            );

        /// <summary>计算区域前三个顶点构成的有向面积符号, 用于验证绕向。</summary>
        /// <param name="zone">区域。</param>
        /// <param name="startIndex">起始顶点下标。</param>
        /// <returns>有向面积两倍; 逆时针为正。</returns>
        private static float Cross(ZoneData zone, int startIndex)
        {
            PointData a = zone.Vertices[startIndex];
            PointData b = zone.Vertices[startIndex + 1];
            PointData c = zone.Vertices[startIndex + 2];
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
    }
}
