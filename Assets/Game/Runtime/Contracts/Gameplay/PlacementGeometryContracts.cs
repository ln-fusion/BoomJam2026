using System;
using Game.Contracts.Content;
using Game.Foundation;
using UnityEngine;

namespace Game.Contracts.Gameplay
{
    /// <summary>
    /// 部署几何使用的世界网格精度与容差常量。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 位置在写入部署方案前量化到 <see cref="Unit"/> 的整数倍, 消除鼠标浮点抖动;
    /// 网格精度作为项目常量参与内容版本（技术设计文档 §6.4）。
    /// </para>
    /// <para>
    /// <see cref="Epsilon"/> 只承担"相切算不算接触"的语义判断, 不承担数值容错:
    /// 几何谓词全部在 <see cref="double"/> 下计算, 因此容差不需要掩盖坐标量级带来的误差。
    /// </para>
    /// </remarks>
    public static class PlacementGrid
    {
        /// <summary>网格精度版本; 步长或容差变化时必须递增, 使旧成绩不再可比。</summary>
        public const int Version = 1;

        /// <summary>网格步长; 1/1000 世界单位。</summary>
        public const double Unit = 0.001d;

        /// <summary>
        /// 相切容差; 取网格步长的十分之一。
        /// </summary>
        /// <remarks>取值必须显著小于 <see cref="Unit"/> 的一半, 否则会吃掉一个完整网格里的合法位置。</remarks>
        public const double Epsilon = Unit / 10.0d;

        /// <summary>
        /// 重叠判定的探针内缩量; 取容差的四倍, 即 0.4 个网格步长。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 判断"是否真正相交"时先把矩形向内收缩该量再做闭合相交判定。
        /// 内缩量必须明显大于 <see cref="Epsilon"/>: 判定链上"点落在线段上"与
        /// "顶点落在矩形上"两处各自带一个 <see cref="Epsilon"/> 的容差,
        /// 若内缩量只取一个 <see cref="Epsilon"/>, 容差会把内缩量重新加回来,
        /// 贴边摆放会被误判为冲突。
        /// </para>
        /// <para>
        /// 内缩量必须小于 <see cref="Unit"/>, 保证占满一个网格的真实重叠不会被漏判。
        /// 因此小于 <see cref="OverlapProbeInset"/> 的重叠按相切处理。
        /// </para>
        /// </remarks>
        public const double OverlapProbeInset = Epsilon * 4d;

        /// <summary>把世界坐标分量量化到最近网格点。</summary>
        /// <param name="value">待量化的坐标分量。</param>
        /// <returns>
        /// 量化后的值; 恰好落在两个网格点中点时向远离零的方向取整。
        /// NaN 与无穷按原值返回, 调用方必须在此之前完成有限性校验。
        /// </returns>
        public static double Quantize(double value) => Math.Round(value / Unit, MidpointRounding.AwayFromZero) * Unit;

        /// <summary>把二维世界坐标量化到最近网格点。</summary>
        /// <param name="value">待量化的坐标。</param>
        /// <returns>量化后的坐标; 分量为 NaN 或无穷时按原值返回。</returns>
        public static Vector2 Quantize(Vector2 value) =>
            new Vector2((float)Quantize(value.x), (float)Quantize(value.y));
    }

    /// <summary>
    /// 轴对齐的放置矩形; 以中心点与全宽全高描述, 内部按 <see cref="double"/> 保存。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 能力框不可旋转（技术设计文档 §6.4）, 因此矩形无需承载角度。
    /// 内部保存半宽半高并以 <see cref="double"/> 计算边界, 避免 <see cref="float"/> 相减
    /// 在坐标量级较大时损失精度。
    /// </para>
    /// <para>
    /// 默认值 <c>default(PlacementRect)</c> 的 <see cref="IsValid"/> 为 <c>false</c>,
    /// 校验入口据此把它判为 <see cref="PlacementError.InvalidNumber"/>。
    /// </para>
    /// </remarks>
    public readonly struct PlacementRect
    {
        private readonly double _centerX;
        private readonly double _centerY;
        private readonly double _halfWidth;
        private readonly double _halfHeight;

        /// <summary>创建矩形; 调用方保证尺寸为正。</summary>
        /// <param name="centerX">中心 X。</param>
        /// <param name="centerY">中心 Y。</param>
        /// <param name="halfWidth">半宽。</param>
        /// <param name="halfHeight">半高。</param>
        private PlacementRect(double centerX, double centerY, double halfWidth, double halfHeight)
        {
            _centerX = centerX;
            _centerY = centerY;
            _halfWidth = halfWidth;
            _halfHeight = halfHeight;
        }

        /// <summary>是否为一个尺寸为正的可用矩形。</summary>
        public bool IsValid => _halfWidth > 0d && _halfHeight > 0d;

        /// <summary>中心 X 坐标。</summary>
        public double CenterX => _centerX;

        /// <summary>中心 Y 坐标。</summary>
        public double CenterY => _centerY;

        /// <summary>全宽。</summary>
        public double Width => _halfWidth * 2d;

        /// <summary>全高。</summary>
        public double Height => _halfHeight * 2d;

        /// <summary>最小 X; 左边界。</summary>
        public double MinX => _centerX - _halfWidth;

        /// <summary>最大 X; 右边界。</summary>
        public double MaxX => _centerX + _halfWidth;

        /// <summary>最小 Y; 下边界。</summary>
        public double MinY => _centerY - _halfHeight;

        /// <summary>最大 Y; 上边界。</summary>
        public double MaxY => _centerY + _halfHeight;

        /// <summary>
        /// 按中心点与全宽全高创建矩形, 并校验数值可参与几何判定。
        /// </summary>
        /// <param name="center">中心点世界坐标。</param>
        /// <param name="width">全宽; 必须为有限正数。</param>
        /// <param name="height">全高; 必须为有限正数。</param>
        /// <param name="rect">创建出的矩形; 失败时为 <c>default</c>。</param>
        /// <returns>
        /// 成功返回 <see cref="PlacementError.None"/>; 中心或尺寸为 NaN、无穷或非正数时
        /// 返回 <see cref="PlacementError.InvalidNumber"/>。
        /// </returns>
        public static PlacementError TryCreate(Vector2 center, float width, float height, out PlacementRect rect)
        {
            rect = default;
            if (!IsFinite(center.x) || !IsFinite(center.y) || !IsFinite(width) || !IsFinite(height))
                return PlacementError.InvalidNumber;
            // 用否定形式书写, 使 NaN 与零、负数走同一条拒绝路径。
            if (!(width > 0f) || !(height > 0f))
                return PlacementError.InvalidNumber;
            rect = new PlacementRect(center.x, center.y, width * 0.5d, height * 0.5d);
            return PlacementError.None;
        }

        /// <summary>
        /// 生成向内收缩指定量的矩形, 用于把"相切"与"真正相交"区分开。
        /// </summary>
        /// <param name="amount">向内收缩量; 必须非负。</param>
        /// <param name="inset">收缩后的矩形; 失败时为 <c>default</c>。</param>
        /// <returns>收缩后宽高仍为正时返回 true; 否则返回 false 且 <paramref name="inset"/> 为 <c>default</c>。</returns>
        public bool TryInset(double amount, out PlacementRect inset)
        {
            inset = default;
            if (!IsValid || !(amount >= 0d) || double.IsNaN(amount))
                return false;
            double halfWidth = _halfWidth - amount;
            double halfHeight = _halfHeight - amount;
            if (!(halfWidth > 0d) || !(halfHeight > 0d))
                return false;
            inset = new PlacementRect(_centerX, _centerY, halfWidth, halfHeight);
            return true;
        }

        /// <summary>判断浮点分量是否为有限值。</summary>
        /// <param name="value">待检查的分量。</param>
        /// <returns>非 NaN 且非无穷时返回 true。</returns>
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 放置矩形与关卡区域之间的几何判定。
    /// </summary>
    /// <remarks>
    /// 编辑器预览与运行时权威校验必须共用同一实现, 避免出现"编辑器能放、游戏不能放"
    /// （技术设计文档 §6.4）。
    /// </remarks>
    public interface IPlacementGeometry
    {
        /// <summary>
        /// 判断矩形是否完整落在区域内; 贴合区域边界视为落在区域内。
        /// </summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="zone">区域多边形; 顶点少于 3 个视为不具备包含能力。</param>
        /// <returns>
        /// 矩形完整落在区域内时返回 true。越界量不超过 <see cref="PlacementGrid.Epsilon"/>
        /// 时按贴合处理, 越界达到一个完整网格步长时返回 false。
        /// </returns>
        bool IsRectInsideZone(PlacementRect rect, ZoneData zone);

        /// <summary>
        /// 判断矩形是否与区域内部真正相交; 仅沿边界贴合不算相交。
        /// </summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="zone">区域多边形; 顶点少于 3 个视为不具备面积。</param>
        /// <returns>
        /// 存在正面积重叠时返回 true。重叠厚度小于 <see cref="PlacementGrid.OverlapProbeInset"/>
        /// 时按相切处理, 重叠达到一个完整网格步长时必然返回 true。
        /// </returns>
        bool DoesRectOverlapZone(PlacementRect rect, ZoneData zone);
    }

    /// <summary>
    /// 放置位置的几何合法性校验入口。
    /// </summary>
    /// <remarks>
    /// 只判定几何条件（部署区包含与禁放区冲突）; 白名单、尺寸选项与容量属于部署领域规则,
    /// 由 C23 的部署服务在调用本接口之前或之后处理。
    /// </remarks>
    public interface IPlacementValidator
    {
        /// <summary>
        /// 校验一个放置矩形是否满足关卡的几何要求。
        /// </summary>
        /// <param name="rect">放置矩形。</param>
        /// <param name="definition">关卡定义; 为 null 时因不存在可部署区域而判定为区域外。</param>
        /// <returns>
        /// 合法时返回 <see cref="PlacementError.None"/>; 否则返回
        /// <see cref="PlacementError.InvalidNumber"/>（矩形非法）、
        /// <see cref="PlacementError.OutsideDeployableArea"/>（未完整落在任一部署区内）或
        /// <see cref="PlacementError.OverlapsForbiddenArea"/>（与任一禁放区真正相交）。
        /// 判定顺序固定为 有限性 → 部署区 → 禁放区。
        /// </returns>
        PlacementError Validate(PlacementRect rect, LevelDefinition definition);
    }
}
