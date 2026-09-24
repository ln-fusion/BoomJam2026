using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 验证放置矩形与区域多边形的包含、相交判定, 重点覆盖相切、凹区域与退化数据。
    /// </summary>
    public sealed class PlacementGeometryTests
    {
        private const float Grid = 0.001f;

        /// <summary>完全落在区域内部的矩形必须被接受。</summary>
        [Test]
        public void IsRectInsideZone_AcceptsRectWellInside()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool inside = geometry.IsRectInsideZone(
                PlacementZones.Rect(0f, 0f, 1f, 1f),
                PlacementZones.Square("zone.a", 0f, 0f, 5f)
            );

            Assert.That(inside, Is.True);
        }

        /// <summary>与区域四边完全重合的矩形必须被接受, 否则贴边摆放永远失败。</summary>
        [Test]
        public void IsRectInsideZone_AcceptsRectFlushAgainstBoundary()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool inside = geometry.IsRectInsideZone(
                PlacementZones.Rect(0f, 0f, 5f, 5f),
                PlacementZones.Square("zone.a", 0f, 0f, 5f)
            );

            Assert.That(inside, Is.True);
        }

        /// <summary>矩形贴住区域每一条边时都必须被接受。</summary>
        /// <param name="centerX">矩形中心 X。</param>
        /// <param name="centerY">矩形中心 Y。</param>
        [TestCase(2f, 0f)]
        [TestCase(-2f, 0f)]
        [TestCase(0f, 2f)]
        [TestCase(0f, -2f)]
        public void IsRectInsideZone_AcceptsRectFlushAtEveryEdge(float centerX, float centerY)
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool inside = geometry.IsRectInsideZone(
                PlacementZones.Rect(centerX, centerY, 1f, 1f),
                PlacementZones.Square("zone.a", 0f, 0f, 5f)
            );

            Assert.That(inside, Is.True);
        }

        /// <summary>越界恰好一个网格就必须被拒绝, 容差不得吃掉合法网格。</summary>
        [Test]
        public void IsRectInsideZone_RejectsRectPokingOutByOneGridCell()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool inside = geometry.IsRectInsideZone(
                PlacementZones.Rect(2f + Grid, 0f, 1f, 1f),
                PlacementZones.Square("zone.a", 0f, 0f, 5f)
            );

            Assert.That(inside, Is.False);
        }

        /// <summary>完全位于区域外的矩形必须被拒绝。</summary>
        [Test]
        public void IsRectInsideZone_RejectsRectEntirelyOutside()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool inside = geometry.IsRectInsideZone(
                PlacementZones.Rect(10f, 10f, 1f, 1f),
                PlacementZones.Square("zone.a", 0f, 0f, 5f)
            );

            Assert.That(inside, Is.False);
        }

        /// <summary>矩形大部分在区域内但越过一条边时必须被拒绝。</summary>
        [Test]
        public void IsRectInsideZone_RejectsRectCrossingOneEdge()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool inside = geometry.IsRectInsideZone(
                PlacementZones.Rect(2.4f, 0f, 1f, 1f),
                PlacementZones.Square("zone.a", 0f, 0f, 5f)
            );

            Assert.That(inside, Is.False);
        }

        /// <summary>
        /// 凹区域的凹口横穿矩形时, 即使四角都落在区域内也必须拒绝。
        /// 这是"只检查四角"会漏判的典型情形。
        /// </summary>
        [Test]
        public void IsRectInsideZone_RejectsRectStraddlingConcaveNotch()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool inside = geometry.IsRectInsideZone(PlacementZones.Rect(0f, 0f, 2f, 4f), NotchedZone());

            Assert.That(inside, Is.False);
        }

        /// <summary>凹区域下方完整区域内的矩形必须被接受。</summary>
        [Test]
        public void IsRectInsideZone_AcceptsRectBelowConcaveNotch()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool inside = geometry.IsRectInsideZone(PlacementZones.Rect(0f, -1.5f, 4f, 2f), NotchedZone());

            Assert.That(inside, Is.True);
        }

        /// <summary>凹区域左侧臂内的矩形必须被接受。</summary>
        [Test]
        public void IsRectInsideZone_AcceptsRectInsideOneArmOfConcaveZone()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool inside = geometry.IsRectInsideZone(PlacementZones.Rect(-2f, 1.5f, 1.5f, 3f), NotchedZone());

            Assert.That(inside, Is.True);
        }

        /// <summary>顺时针与逆时针给点必须得到相同结论。</summary>
        [Test]
        public void IsRectInsideZone_IgnoresWindingOrder()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            PlacementRect rect = PlacementZones.Rect(0f, 0f, 1f, 1f);

            bool counterClockwise = geometry.IsRectInsideZone(rect, PlacementZones.Square("zone.a", 0f, 0f, 5f));
            bool clockwise = geometry.IsRectInsideZone(
                rect,
                PlacementZones.Square("zone.b", 0f, 0f, 5f, clockwise: true)
            );

            Assert.That(clockwise, Is.EqualTo(counterClockwise));
            Assert.That(clockwise, Is.True);
        }

        /// <summary>空区域必须被判为不包含, 而不是抛异常。</summary>
        [Test]
        public void IsRectInsideZone_RejectsNullZone()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(0f, 0f, 1f, 1f), null), Is.False);
        }

        /// <summary>顶点集合为空引用时区域数据已损坏, 必须判为不包含。</summary>
        [Test]
        public void IsRectInsideZone_RejectsZoneWithNullVertices()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            var zone = new ZoneData { ZoneId = "zone.broken", Vertices = null };

            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(0f, 0f, 1f, 1f), zone), Is.False);
        }

        /// <summary>顶点集合含空项时区域数据已损坏, 必须判为不包含。</summary>
        [Test]
        public void IsRectInsideZone_RejectsZoneWithNullVertex()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            var zone = PlacementZones.Zone("zone.broken", (-1f, -1f), (1f, -1f), (1f, 1f));
            zone.Vertices.Insert(1, null);

            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(0f, 0f, 0.5f, 0.5f), zone), Is.False);
        }

        /// <summary>顶点少于 3 个的区域没有面积, 不能包含任何矩形。</summary>
        /// <param name="vertexCount">顶点数量。</param>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void IsRectInsideZone_RejectsDegenerateZone(int vertexCount)
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            var zone = new ZoneData { ZoneId = "zone.degenerate", Vertices = new List<PointData>() };
            for (int i = 0; i < vertexCount; i++)
                zone.Vertices.Add(new PointData { X = i, Y = i });

            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(0f, 0f, 1f, 1f), zone), Is.False);
        }

        /// <summary>非法矩形不参与几何判定。</summary>
        [Test]
        public void IsRectInsideZone_RejectsInvalidRect()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            Assert.That(
                geometry.IsRectInsideZone(default(PlacementRect), PlacementZones.Square("zone.a", 0f, 0f, 5f)),
                Is.False
            );
        }

        /// <summary>大坐标下贴合右边界的矩形仍必须被接受, 精度损失不得改变结论。</summary>
        [Test]
        public void IsRectInsideZone_AcceptsFlushRectAtLargeCoordinateMagnitude()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            ZoneData zone = PlacementZones.Square("zone.big", 1000f, 1000f, 1000f);

            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(1499.5f, 1000f, 1f, 1f), zone), Is.True);
            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(500.5f, 1000f, 1f, 1f), zone), Is.True);
        }

        /// <summary>大坐标下越界一个网格仍必须被拒绝。</summary>
        [Test]
        public void IsRectInsideZone_RejectsOutOfBoundsRectAtLargeCoordinateMagnitude()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            ZoneData zone = PlacementZones.Square("zone.big", 1000f, 1000f, 1000f);

            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(1500f + Grid, 1000f, 1f, 1f), zone), Is.False);
            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(500f - Grid, 1000f, 1f, 1f), zone), Is.False);
        }

        /// <summary>斜边区域在大坐标下仍必须给出正确结论。</summary>
        [Test]
        public void IsRectInsideZone_HandlesDiagonalEdgeAtLargeCoordinateMagnitude()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            ZoneData triangle = PlacementZones.Zone("zone.diag", (500f, 500f), (1500f, 500f), (1500f, 1500f));

            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(1000f, 900f, 1f, 1f), triangle), Is.True);
            Assert.That(geometry.IsRectInsideZone(PlacementZones.Rect(900f, 1000f, 1f, 1f), triangle), Is.False);
        }

        /// <summary>完全落在区域内的矩形必然与区域存在正面积重叠。</summary>
        [Test]
        public void DoesRectOverlapZone_DetectsOverlapWhenFullyContained()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool overlaps = geometry.DoesRectOverlapZone(
                PlacementZones.Rect(0f, 0f, 1f, 1f),
                PlacementZones.Square("zone.a", 0f, 0f, 5f)
            );

            Assert.That(overlaps, Is.True);
        }

        /// <summary>部分覆盖区域时视为重叠。</summary>
        [Test]
        public void DoesRectOverlapZone_DetectsPartialOverlap()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool overlaps = geometry.DoesRectOverlapZone(
                PlacementZones.Rect(2.4f, 0f, 1f, 1f),
                PlacementZones.Square("zone.a", 0f, 0f, 5f)
            );

            Assert.That(overlaps, Is.True);
        }

        /// <summary>
        /// 区域边与矩形边落在同一条直线上、且区域伸入矩形内部时必须判为重叠。
        /// 只做"角点在内 / 顶点在内 / 边穿越"三项判定会漏掉这一情形。
        /// </summary>
        [Test]
        public void DoesRectOverlapZone_DetectsOverlapWhenRectEdgeLiesOnZoneEdge()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            ZoneData zone = PlacementZones.Zone("zone.a", (0f, 0f), (10f, 0f), (10f, 5f), (0f, 5f));

            bool overlaps = geometry.DoesRectOverlapZone(PlacementZones.Rect(5f, 5f, 10f, 10f), zone);

            Assert.That(overlaps, Is.True);
        }

        /// <summary>仅沿一条边贴合不算冲突, 否则贴着禁放区摆放永远失败。</summary>
        [Test]
        public void DoesRectOverlapZone_IgnoresFlushContactAlongEdge()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            ZoneData zone = PlacementZones.Zone("zone.a", (0f, 0f), (10f, 0f), (10f, 5f), (0f, 5f));

            bool overlaps = geometry.DoesRectOverlapZone(PlacementZones.Rect(5f, 10f, 10f, 10f), zone);

            Assert.That(overlaps, Is.False);
        }

        /// <summary>伸入恰好一个网格的微小重叠必须被检出, 探针内缩不得吃掉整个网格。</summary>
        [Test]
        public void DoesRectOverlapZone_DetectsOverlapOfOneGridCell()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            ZoneData zone = PlacementZones.Zone("zone.a", (0f, 0f), (10f, 0f), (10f, 5f), (0f, 5f));
            // 矩形下边界落在 4.999, 与区域上边界 5.0 之间重叠恰好一个网格。
            PlacementRect rect = PlacementZones.Rect(5f, 5.4995f, 10f, 1.001f);

            Assert.That(rect.MinY, Is.EqualTo(4.999d).Within(1e-6));
            Assert.That(geometry.DoesRectOverlapZone(rect, zone), Is.True);
        }

        /// <summary>只有一个角相接触不算冲突。</summary>
        [Test]
        public void DoesRectOverlapZone_IgnoresCornerTouch()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            ZoneData zone = PlacementZones.Zone("zone.a", (0f, 0f), (10f, 0f), (10f, 10f), (0f, 10f));

            bool overlaps = geometry.DoesRectOverlapZone(PlacementZones.Rect(11f, 11f, 2f, 2f), zone);

            Assert.That(overlaps, Is.False);
        }

        /// <summary>完全分离的矩形不算冲突。</summary>
        [Test]
        public void DoesRectOverlapZone_IgnoresDistantRect()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            bool overlaps = geometry.DoesRectOverlapZone(
                PlacementZones.Rect(20f, 20f, 1f, 1f),
                PlacementZones.Square("zone.a", 0f, 0f, 5f)
            );

            Assert.That(overlaps, Is.False);
        }

        /// <summary>顶点少于 3 个的区域没有面积, 不可能与矩形重叠。</summary>
        /// <param name="vertexCount">顶点数量。</param>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DoesRectOverlapZone_RejectsDegenerateZone(int vertexCount)
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            var zone = new ZoneData { ZoneId = "zone.degenerate", Vertices = new List<PointData>() };
            for (int i = 0; i < vertexCount; i++)
                zone.Vertices.Add(new PointData { X = i, Y = i });

            Assert.That(geometry.DoesRectOverlapZone(PlacementZones.Rect(0f, 0f, 4f, 4f), zone), Is.False);
        }

        /// <summary>空区域必须被安全处理。</summary>
        [Test]
        public void DoesRectOverlapZone_RejectsNullZone()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            Assert.That(geometry.DoesRectOverlapZone(PlacementZones.Rect(0f, 0f, 1f, 1f), null), Is.False);
        }

        /// <summary>重叠判定必须与绕向无关。</summary>
        [Test]
        public void DoesRectOverlapZone_IgnoresWindingOrder()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();
            PlacementRect rect = PlacementZones.Rect(2.4f, 0f, 1f, 1f);

            bool counterClockwise = geometry.DoesRectOverlapZone(rect, PlacementZones.Square("zone.a", 0f, 0f, 5f));
            bool clockwise = geometry.DoesRectOverlapZone(
                rect,
                PlacementZones.Square("zone.b", 0f, 0f, 5f, clockwise: true)
            );

            Assert.That(clockwise, Is.EqualTo(counterClockwise));
        }

        /// <summary>非法矩形不参与重叠判定。</summary>
        [Test]
        public void DoesRectOverlapZone_RejectsInvalidRect()
        {
            IPlacementGeometry geometry = PlacementZones.Geometry();

            Assert.That(
                geometry.DoesRectOverlapZone(default(PlacementRect), PlacementZones.Square("zone.a", 0f, 0f, 5f)),
                Is.False
            );
        }

        /// <summary>构造带细缝的凹区域: 外形为 6×6 正方形, 顶部中间被切出一条窄缝。</summary>
        /// <returns>带窄缝的凹区域。</returns>
        private static ZoneData NotchedZone() =>
            PlacementZones.Zone(
                "zone.notched",
                (-3f, -3f),
                (3f, -3f),
                (3f, 3f),
                (0.1f, 3f),
                (0.1f, 0f),
                (-0.1f, 0f),
                (-0.1f, 3f),
                (-3f, 3f)
            );
    }
}
