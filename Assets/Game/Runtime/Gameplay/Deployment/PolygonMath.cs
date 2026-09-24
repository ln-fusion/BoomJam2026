using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;

namespace Game.Gameplay.Deployment
{
    /// <summary>
    /// 部署几何使用的多边形与线段判定原语; 全部在 <see cref="double"/> 下计算。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 坐标量级与边长都达到 1000 时, <see cref="float"/> 叉积的绝对误差可达 0.1 量级,
    /// 除以边长后距离误差约 1e-4, 与 <see cref="PlacementGrid.Epsilon"/> 同量级,
    /// 会让相切判定随机失败。改用 <see cref="double"/> 后误差远小于容差,
    /// 容差因此只表达语义（相切算不算接触）, 不再用于掩盖精度损失。
    /// </para>
    /// <para>
    /// 容差 <see cref="PlacementGrid.Epsilon"/> 只用于"点是否落在线段上"这一类
    /// 需要吸收量化误差的判定; "线段是否真正穿越"使用严格符号判定, 精确表示相切不算穿越。
    /// </para>
    /// </remarks>
    internal static class PolygonMath
    {
        /// <summary>判断区域是否具备参与面积判定的顶点。</summary>
        /// <param name="zone">区域数据。</param>
        /// <returns>区域非空、顶点集合非空、顶点数不少于 3 且无空顶点时返回 true。</returns>
        internal static bool HasUsablePolygon(ZoneData zone)
        {
            if (zone?.Vertices == null || zone.Vertices.Count < 3)
                return false;
            IReadOnlyList<PointData> vertices = zone.Vertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                if (vertices[i] == null)
                    return false;
            }
            return true;
        }

        /// <summary>判断点是否落在区域内部或边界上。</summary>
        /// <param name="x">点 X 坐标。</param>
        /// <param name="y">点 Y 坐标。</param>
        /// <param name="zone">区域多边形; 调用方需保证 <see cref="HasUsablePolygon"/> 已通过。</param>
        /// <returns>点位于内部或边界上时返回 true。</returns>
        internal static bool IsPointInsideOrOn(double x, double y, ZoneData zone)
        {
            IReadOnlyList<PointData> vertices = zone.Vertices;
            int count = vertices.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                if (IsPointOnSegment(x, y, vertices[j].X, vertices[j].Y, vertices[i].X, vertices[i].Y))
                    return true;
            }
            return IsPointStrictlyInside(x, y, zone);
        }

        /// <summary>判断点是否严格落在区域内部, 边界点视为不在内部。</summary>
        /// <param name="x">点 X 坐标。</param>
        /// <param name="y">点 Y 坐标。</param>
        /// <param name="zone">区域多边形; 调用方需保证 <see cref="HasUsablePolygon"/> 已通过。</param>
        /// <returns>点严格位于内部时返回 true。</returns>
        internal static bool IsPointStrictlyInside(double x, double y, ZoneData zone)
        {
            IReadOnlyList<PointData> vertices = zone.Vertices;
            int count = vertices.Count;
            bool inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                double yi = vertices[i].Y;
                double yj = vertices[j].Y;
                // 只统计跨越射线所在水平线的边; 该判断同时排除了水平边除以零的情形。
                if ((yi > y) == (yj > y))
                    continue;
                double xIntersection = vertices[j].X + (y - yj) * (vertices[i].X - vertices[j].X) / (yi - yj);
                if (x < xIntersection)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>判断点是否落在线段上, 容差为 <see cref="PlacementGrid.Epsilon"/>。</summary>
        /// <param name="x">点 X 坐标。</param>
        /// <param name="y">点 Y 坐标。</param>
        /// <param name="ax">线段起点 X。</param>
        /// <param name="ay">线段起点 Y。</param>
        /// <param name="bx">线段终点 X。</param>
        /// <param name="by">线段终点 Y。</param>
        /// <returns>点到线段的距离不超过容差且投影落在线段范围内时返回 true。</returns>
        internal static bool IsPointOnSegment(double x, double y, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax;
            double dy = by - ay;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 0d)
            {
                double ox = x - ax;
                double oy = y - ay;
                return ox * ox + oy * oy <= PlacementGrid.Epsilon * PlacementGrid.Epsilon;
            }
            double length = Math.Sqrt(lengthSquared);
            // 叉积除以边长即点到直线的垂距。
            double perpendicular = Math.Abs((x - ax) * dy - (y - ay) * dx) / length;
            if (perpendicular > PlacementGrid.Epsilon)
                return false;
            double along = (x - ax) * dx + (y - ay) * dy;
            return along >= -PlacementGrid.Epsilon * length && along <= lengthSquared + PlacementGrid.Epsilon * length;
        }

        /// <summary>
        /// 判断两条线段是否真正穿越, 即交点严格位于两条线段内部。
        /// </summary>
        /// <remarks>端点接触、共线重叠与相切一律不算穿越, 判定使用严格符号比较而不使用容差。</remarks>
        /// <param name="ax">第一条线段起点 X。</param>
        /// <param name="ay">第一条线段起点 Y。</param>
        /// <param name="bx">第一条线段终点 X。</param>
        /// <param name="by">第一条线段终点 Y。</param>
        /// <param name="cx">第二条线段起点 X。</param>
        /// <param name="cy">第二条线段起点 Y。</param>
        /// <param name="dx">第二条线段终点 X。</param>
        /// <param name="dy">第二条线段终点 Y。</param>
        /// <returns>两条线段真正穿越时返回 true。</returns>
        internal static bool DoSegmentsProperlyCross(
            double ax,
            double ay,
            double bx,
            double by,
            double cx,
            double cy,
            double dx,
            double dy
        )
        {
            double firstC = Cross(cx, cy, dx, dy, ax, ay);
            double firstD = Cross(cx, cy, dx, dy, bx, by);
            if (!Straddles(firstC, firstD))
                return false;
            double secondA = Cross(ax, ay, bx, by, cx, cy);
            double secondB = Cross(ax, ay, bx, by, dx, dy);
            return Straddles(secondA, secondB);
        }

        /// <summary>计算三个点构成的有向面积两倍; 符号表示第三点相对前两点连线的方位。</summary>
        /// <param name="ax">起始点 X。</param>
        /// <param name="ay">起始点 Y。</param>
        /// <param name="bx">终点 X。</param>
        /// <param name="by">终点 Y。</param>
        /// <param name="cx">被判定点 X。</param>
        /// <param name="cy">被判定点 Y。</param>
        /// <returns>叉积值; 为零表示三点共线。</returns>
        private static double Cross(double ax, double ay, double bx, double by, double cx, double cy) =>
            (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);

        /// <summary>判断两个叉积是否位于零的两侧, 且都不为零。</summary>
        /// <param name="left">第一个叉积。</param>
        /// <param name="right">第二个叉积。</param>
        /// <returns>两者严格异号时返回 true。</returns>
        private static bool Straddles(double left, double right) =>
            (left > 0d && right < 0d) || (left < 0d && right > 0d);
    }
}
