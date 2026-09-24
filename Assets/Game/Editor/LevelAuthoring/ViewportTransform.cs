using UnityEngine;

namespace Game.Editor.Level
{
    /// <summary>
    /// 关卡编辑器视口的二维视图变换; 只做"世界坐标 ↔ 视口像素"的换算, 不依赖任何 VisualElement。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 视图状态表达为"视口中心对应的世界坐标 + 缩放 + 视口像素尺寸", 而不是滚动偏移:
    /// 平移量由前三者推导, 于是视口尺寸变化（窗口缩放、拖动分栏）时中心自动保持不动,
    /// 也不会出现"宽高参与运算但只在一处参与"的不对称公式。
    /// </para>
    /// <para>
    /// 世界 Y 轴向上, 视口 Y 轴向下, 因此 <see cref="WorldToLocal"/> 对 Y 做翻转。
    /// 本类不依赖布局, 可在 EditMode 中直接构造并断言, 视口交互的回归保护由
    /// <c>ViewportTransformTests</c> 承担。
    /// </para>
    /// </remarks>
    public sealed class ViewportTransform
    {
        /// <summary>新建视口使用的默认缩放; 单位为像素每世界单位。</summary>
        public const float DefaultZoom = 24f;

        /// <summary>允许的最小缩放。</summary>
        public const float MinZoom = 2f;

        /// <summary>允许的最大缩放。</summary>
        public const float MaxZoom = 200f;

        /// <summary>"缩放到适应"在边界四周保留的留白比例; 相对视口较短边。</summary>
        public const float FitPaddingRatio = 0.08f;

        /// <summary>尺寸未完成布局时的哨兵; 用于区分"还没量到尺寸"和"尺寸很小"。</summary>
        public const float UnmeasuredSize = 0f;

        private float _zoom = DefaultZoom;

        /// <summary>视口中心对应的世界坐标; 平移视图即改写本值。</summary>
        public Vector2 Center { get; set; }

        /// <summary>
        /// 缩放; 单位为像素每世界单位。赋值时夹取到 [<see cref="MinZoom"/>, <see cref="MaxZoom"/>],
        /// 因此取值恒为正, 除法与翻转运算不需要额外判零。
        /// </summary>
        public float Zoom
        {
            get => _zoom;
            set => _zoom = Mathf.Clamp(value, MinZoom, MaxZoom);
        }

        /// <summary>视口在像素下的尺寸; 由布局写入。</summary>
        public Vector2 Size { get; set; }

        /// <summary>是否已经量到可用的视口尺寸; 为 false 时"缩放到适应"无法计算缩放。</summary>
        public bool HasUsableSize => Size.x > UnmeasuredSize && Size.y > UnmeasuredSize;

        /// <summary>
        /// 世界坐标到视口坐标的平移量; 由 <see cref="Center"/>、<see cref="Zoom"/> 与
        /// <see cref="Size"/> 推导, 不单独保存, 避免与中心点脱节。
        /// </summary>
        /// <remarks>
        /// 水平方向要求 <c>Center.x</c> 映到 <c>Size.x * 0.5</c>, 垂直方向因 Y 轴翻转
        /// 要求 <c>Center.y</c> 映到 <c>Size.y * 0.5</c>, 两式分别解出 X 与 Y 的平移量。
        /// </remarks>
        public Vector2 Pan => new Vector2(Size.x * 0.5f - Center.x * _zoom, Size.y * 0.5f - Center.y * _zoom);

        /// <summary>把世界坐标转换为视口本地坐标; 世界 Y 轴向上, 视口 Y 轴向下。</summary>
        /// <param name="world">世界坐标。</param>
        /// <returns>视口本地坐标。</returns>
        public Vector2 WorldToLocal(Vector2 world)
        {
            Vector2 pan = Pan;
            return new Vector2(world.x * _zoom + pan.x, Size.y - (world.y * _zoom + pan.y));
        }

        /// <summary>把视口本地坐标转换为世界坐标; 与 <see cref="WorldToLocal"/> 互为逆运算。</summary>
        /// <param name="local">视口本地坐标。</param>
        /// <returns>世界坐标。</returns>
        public Vector2 LocalToWorld(Vector2 local)
        {
            Vector2 pan = Pan;
            return new Vector2((local.x - pan.x) / _zoom, (Size.y - local.y - pan.y) / _zoom);
        }

        /// <summary>把世界长度换算为像素长度。</summary>
        /// <param name="worldLength">世界单位长度。</param>
        /// <returns>像素长度。</returns>
        public float WorldLengthToPixels(float worldLength) => worldLength * _zoom;

        /// <summary>把像素长度换算为世界长度; 命中容差按像素给定后需要换算回世界单位。</summary>
        /// <param name="pixels">像素长度。</param>
        /// <returns>世界单位长度。</returns>
        public float PixelsToWorldLength(float pixels) => pixels / _zoom;

        /// <summary>以某个视口位置为锚点缩放; 锚点下方的世界坐标保持不动。</summary>
        /// <param name="factor">缩放倍率; 非有限值或非正时不做任何改动。</param>
        /// <param name="anchorLocal">锚点的视口本地坐标, 常见取值是鼠标位置。</param>
        /// <remarks>
        /// 先记下锚点当前的世界坐标, 改缩放后再求一次, 把两者之差补进中心点。
        /// 这样缩放不会把用户正在看的位置滑出屏幕, 也是滚轮缩放手感正确的前提。
        /// </remarks>
        public void ZoomBy(float factor, Vector2 anchorLocal)
        {
            if (!IsFinitePositive(factor))
                return;
            Vector2 worldAnchor = LocalToWorld(anchorLocal);
            Zoom = _zoom * factor;
            Center += worldAnchor - LocalToWorld(anchorLocal);
        }

        /// <summary>以视口中心为锚点缩放。</summary>
        /// <param name="factor">缩放倍率; 非有限值或非正时不做任何改动。</param>
        public void ZoomBy(float factor) => ZoomBy(factor, Size * 0.5f);

        /// <summary>把指定的世界矩形完整纳入视野并居中。</summary>
        /// <param name="worldBounds">需要看到的世界矩形。</param>
        /// <param name="paddingRatio">四周留白比例; 夹取到 [0, 0.4]。</param>
        /// <remarks>
        /// 尺寸尚未量到时只记录中心, 不改缩放: 此时无法算出合适倍率, 让调用方在布局完成后重试
        /// （见 <see cref="LevelViewportElement"/> 的待定适应标记）。
        /// </remarks>
        public void FitTo(Rect worldBounds, float paddingRatio)
        {
            Center = worldBounds.center;
            if (!HasUsableSize || worldBounds.width <= 0f || worldBounds.height <= 0f)
                return;
            float usable = 1f - 2f * Mathf.Clamp(paddingRatio, 0f, 0.4f);
            Zoom = Mathf.Min(Size.x * usable / worldBounds.width, Size.y * usable / worldBounds.height);
        }

        /// <summary>把中心点移到指定世界坐标并设置缩放; 视图重置复用本方法。</summary>
        /// <param name="worldCenter">新的中心世界坐标。</param>
        /// <param name="zoom">新的缩放; 由属性夹取。</param>
        public void SetView(Vector2 worldCenter, float zoom)
        {
            Center = worldCenter;
            Zoom = zoom;
        }

        /// <summary>按视口位移平移视图, 效果是内容跟随指针移动。</summary>
        /// <param name="deltaLocal">指针在视口坐标系中的位移。</param>
        /// <remarks>
        /// X 分量取反而 Y 分量保持同号, 因为视口 Y 轴向下、世界 Y 轴向上, 而平移量在
        /// Y 方向又以减号进入换算。两处符号不一致是这里的固有性质, 所以写成显式公式并
        /// 由 <c>PanBy_MovesContentWithPointer</c> 锁定, 不靠"中心点减位移"这类直觉推导。
        /// </remarks>
        public void PanBy(Vector2 deltaLocal) => Center += new Vector2(-deltaLocal.x, deltaLocal.y) / _zoom;

        /// <summary>判断给定的世界矩形是否完整落在视口内。</summary>
        /// <param name="worldRect">待判断的世界矩形。</param>
        /// <returns>四个角都在视口范围内时返回 true; 尺寸未量到时返回 false。</returns>
        /// <remarks>
        /// 供"打开关卡时是否需要重新取景"使用: 保存的取景可能来自关卡尺寸更小的时期,
        /// 此时沿用旧取景会让整个关卡落在视野之外。
        /// </remarks>
        public bool ContainsWorldRect(Rect worldRect)
        {
            if (!HasUsableSize)
                return false;
            Vector2[] corners =
            {
                new Vector2(worldRect.xMin, worldRect.yMin),
                new Vector2(worldRect.xMax, worldRect.yMin),
                new Vector2(worldRect.xMax, worldRect.yMax),
                new Vector2(worldRect.xMin, worldRect.yMax),
            };
            foreach (Vector2 corner in corners)
            {
                Vector2 local = WorldToLocal(corner);
                if (local.x < 0f || local.x > Size.x || local.y < 0f || local.y > Size.y)
                    return false;
            }
            return true;
        }

        /// <summary>判断缩放倍率是否可用于乘法。</summary>
        /// <param name="value">待判断的值。</param>
        /// <returns>有限且为正时返回 true。</returns>
        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
    }
}
