using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;

namespace Game.Gameplay.Deployment
{
    /// <summary>
    /// 放置位置几何校验的默认实现; 只判定几何条件, 不涉及白名单、尺寸与容量。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判定顺序固定为 有限性 → 部署区 → 禁放区。有限性必须排在最前:
    /// NaN 参与任何比较都返回 false, 若先走几何判定, 非法数值会被静默判为合法。
    /// </para>
    /// <para>
    /// 缺省配置的可部署区域集合为空时, 任何位置都判为区域外, 因此校验失败而不是放过。
    /// </para>
    /// </remarks>
    public sealed class PlacementValidator : IPlacementValidator
    {
        private readonly IPlacementGeometry _geometry;

        /// <summary>创建校验器。</summary>
        /// <param name="geometry">几何判定实现; 编辑器预览与运行时必须传入同一实现。</param>
        /// <exception cref="ArgumentNullException">几何判定实现为空时抛出。</exception>
        public PlacementValidator(IPlacementGeometry geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        /// <summary>
        /// 校验一个放置矩形是否满足关卡的几何要求。
        /// </summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="definition">关卡定义; 为 null 或缺少部署区时判定为区域外。</param>
        /// <returns>
        /// 合法时返回 <see cref="PlacementError.None"/>; 否则返回
        /// <see cref="PlacementError.InvalidNumber"/>、
        /// <see cref="PlacementError.OutsideDeployableArea"/> 或
        /// <see cref="PlacementError.OverlapsForbiddenArea"/>。
        /// </returns>
        public PlacementError Validate(PlacementRect rect, LevelDefinition definition)
        {
            if (!rect.IsValid)
                return PlacementError.InvalidNumber;
            if (!IsInsideAnyZone(rect, definition?.DeployableZones))
                return PlacementError.OutsideDeployableArea;
            if (OverlapsAnyZone(rect, definition?.ForbiddenZones))
                return PlacementError.OverlapsForbiddenArea;
            return PlacementError.None;
        }

        /// <summary>判断矩形是否完整落在集合中的某一个区域内。</summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="zones">区域集合; 为 null 时返回 false。</param>
        /// <returns>存在完整包含该矩形的区域时返回 true。</returns>
        private bool IsInsideAnyZone(PlacementRect rect, IReadOnlyList<ZoneData> zones)
        {
            if (zones == null)
                return false;
            for (int i = 0; i < zones.Count; i++)
            {
                if (_geometry.IsRectInsideZone(rect, zones[i]))
                    return true;
            }
            return false;
        }

        /// <summary>判断矩形是否与集合中的任意一个区域真正相交。</summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="zones">区域集合; 为 null 时返回 false。</param>
        /// <returns>与任一区域存在正面积重叠时返回 true。</returns>
        private bool OverlapsAnyZone(PlacementRect rect, IReadOnlyList<ZoneData> zones)
        {
            if (zones == null)
                return false;
            for (int i = 0; i < zones.Count; i++)
            {
                if (_geometry.DoesRectOverlapZone(rect, zones[i]))
                    return true;
            }
            return false;
        }
    }
}
