using Game.Contracts.Content;
using Game.Contracts.Gameplay;

namespace Game.Gameplay.Deployment
{
    /// <summary>
    /// 放置矩形与关卡区域的几何判定默认实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 包含判定采用技术设计文档 §6.4 规定的规则: 完整矩形的四个角都必须落在区域内,
    /// 且没有任何区域边真正穿过矩形内部。只检查中心点是不够的——凹多边形的一个凹口
    /// 可以整块吞掉矩形而中心点仍在区域外, 反之矩形也可能横跨凹口且四角都在区域内。
    /// </para>
    /// <para>
    /// 两项判定共用同一个容差基准: 先把矩形向内收缩 <see cref="PlacementGrid.Epsilon"/> 再判定。
    /// 若角点判定带容差而边穿越判定不带, 亚容差越界会出现"角点判为贴合、穿越判为越界"
    /// 的自相矛盾结论, 从而误拒本应允许的贴合摆放。收缩后"越界不超过容差"与"贴合边界"
    /// 得到同一结论, 而越界一个完整网格仍被拒绝。
    /// </para>
    /// <para>
    /// 相交判定先把矩形向内收缩 <see cref="PlacementGrid.OverlapProbeInset"/> 再对区域做闭合相交,
    /// 因此"仅沿边界贴合"不会被判为冲突。收缩后的判定同时覆盖了区域边与矩形边共线重叠
    /// 这一容易漏判的情形。
    /// </para>
    /// </remarks>
    public sealed class PlacementGeometry : IPlacementGeometry
    {
        /// <summary>
        /// 判断矩形是否完整落在区域内; 贴合区域边界视为落在区域内。
        /// </summary>
        /// <param name="rect">放置矩形; 非法矩形一律返回 false。</param>
        /// <param name="zone">区域多边形; 顶点少于 3 个或含空顶点时返回 false。</param>
        /// <returns>矩形完整落在区域内时返回 true; 越界不超过 <see cref="PlacementGrid.Epsilon"/> 时按贴合处理。</returns>
        public bool IsRectInsideZone(PlacementRect rect, ZoneData zone)
        {
            if (!rect.IsValid || !PolygonMath.HasUsablePolygon(zone))
                return false;
            if (!rect.TryInset(PlacementGrid.Epsilon, out PlacementRect probe))
                return false;

            if (!PolygonMath.IsPointInsideOrOn(probe.MinX, probe.MinY, zone))
                return false;
            if (!PolygonMath.IsPointInsideOrOn(probe.MaxX, probe.MinY, zone))
                return false;
            if (!PolygonMath.IsPointInsideOrOn(probe.MinX, probe.MaxY, zone))
                return false;
            if (!PolygonMath.IsPointInsideOrOn(probe.MaxX, probe.MaxY, zone))
                return false;

            // 四角都在区域内仍不足以判定包含: 凹区域的边可能横穿矩形而四角都留在区域里。
            return !DoesZoneEdgeCrossRect(probe, zone);
        }

        /// <summary>
        /// 判断矩形是否与区域内部真正相交; 仅沿边界贴合不算相交。
        /// </summary>
        /// <param name="rect">放置矩形; 非法矩形一律返回 false。</param>
        /// <param name="zone">区域多边形; 顶点少于 3 个或含空顶点时视为没有面积, 返回 false。</param>
        /// <returns>存在正面积重叠时返回 true。</returns>
        public bool DoesRectOverlapZone(PlacementRect rect, ZoneData zone)
        {
            if (!rect.IsValid || !PolygonMath.HasUsablePolygon(zone))
                return false;
            if (!rect.TryInset(PlacementGrid.OverlapProbeInset, out PlacementRect probe))
                return false;
            return IntersectsClosed(probe, zone);
        }

        /// <summary>判断区域的任意一条边是否真正穿过矩形边界。</summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="zone">区域多边形; 调用方需保证顶点可用。</param>
        /// <returns>存在穿越时返回 true。</returns>
        private static bool DoesZoneEdgeCrossRect(PlacementRect rect, ZoneData zone)
        {
            int count = zone.Vertices.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                if (
                    DoesSegmentCrossRect(
                        rect,
                        zone.Vertices[j].X,
                        zone.Vertices[j].Y,
                        zone.Vertices[i].X,
                        zone.Vertices[i].Y
                    )
                )
                    return true;
            }
            return false;
        }

        /// <summary>判断一条线段是否真正穿过矩形的四条边之一。</summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="ax">线段起点 X。</param>
        /// <param name="ay">线段起点 Y。</param>
        /// <param name="bx">线段终点 X。</param>
        /// <param name="by">线段终点 Y。</param>
        /// <returns>存在真正穿越时返回 true。</returns>
        private static bool DoesSegmentCrossRect(PlacementRect rect, double ax, double ay, double bx, double by)
        {
            if (PolygonMath.DoSegmentsProperlyCross(ax, ay, bx, by, rect.MinX, rect.MinY, rect.MaxX, rect.MinY))
                return true;
            if (PolygonMath.DoSegmentsProperlyCross(ax, ay, bx, by, rect.MaxX, rect.MinY, rect.MaxX, rect.MaxY))
                return true;
            if (PolygonMath.DoSegmentsProperlyCross(ax, ay, bx, by, rect.MaxX, rect.MaxY, rect.MinX, rect.MaxY))
                return true;
            return PolygonMath.DoSegmentsProperlyCross(ax, ay, bx, by, rect.MinX, rect.MaxY, rect.MinX, rect.MinY);
        }

        /// <summary>
        /// 判断矩形与区域是否存在闭合相交（相切也算相交）。
        /// </summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="zone">区域多边形; 调用方需保证顶点可用。</param>
        /// <returns>两者闭合相交时返回 true。</returns>
        private static bool IntersectsClosed(PlacementRect rect, ZoneData zone)
        {
            if (PolygonMath.IsPointInsideOrOn(rect.MinX, rect.MinY, zone))
                return true;
            if (PolygonMath.IsPointInsideOrOn(rect.MaxX, rect.MinY, zone))
                return true;
            if (PolygonMath.IsPointInsideOrOn(rect.MinX, rect.MaxY, zone))
                return true;
            if (PolygonMath.IsPointInsideOrOn(rect.MaxX, rect.MaxY, zone))
                return true;

            int count = zone.Vertices.Count;
            for (int i = 0; i < count; i++)
            {
                if (IsPointInsideOrOnRect(rect, zone.Vertices[i].X, zone.Vertices[i].Y))
                    return true;
            }

            return DoesZoneEdgeCrossRect(rect, zone);
        }

        /// <summary>判断点是否落在轴对齐矩形内部或边界上。</summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="x">点 X 坐标。</param>
        /// <param name="y">点 Y 坐标。</param>
        /// <returns>点位于矩形内部或边界上时返回 true。</returns>
        private static bool IsPointInsideOrOnRect(PlacementRect rect, double x, double y) =>
            x >= rect.MinX - PlacementGrid.Epsilon
            && x <= rect.MaxX + PlacementGrid.Epsilon
            && y >= rect.MinY - PlacementGrid.Epsilon
            && y <= rect.MaxY + PlacementGrid.Epsilon;
    }
}
