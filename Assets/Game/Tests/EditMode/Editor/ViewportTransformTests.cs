using Game.Editor.Level;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Editor
{
    /// <summary>
    /// 验证视口视图变换的坐标换算、缩放锚定与"缩放到适应"。
    /// </summary>
    /// <remarks>
    /// 这些用例是 C21A 视口交互的回归保护: 曾经中心点与平移量各用一套 Y 公式,
    /// 导致打开关卡后内容落在视野之外, 表现为"缩放与平移都没反应"。
    /// 断言只依赖纯数学, 不需要布局, 因此可以在 EditMode 中直接运行。
    /// </remarks>
    public sealed class ViewportTransformTests
    {
        private const float Tolerance = 1e-4f;

        private static ViewportTransform Create(float zoom = 32f, float centerX = 0f, float centerY = 0f)
        {
            var transform = new ViewportTransform { Size = new Vector2(800f, 400f) };
            transform.SetView(new Vector2(centerX, centerY), zoom);
            return transform;
        }

        /// <summary>世界坐标与视口坐标必须严格互为逆运算; 任意缩放与中心都成立。</summary>
        [TestCase(2f, 0f, 0f)]
        [TestCase(24f, 5f, -3.5f)]
        [TestCase(200f, -18f, 9.25f)]
        public void WorldToLocal_ThenLocalToWorld_RoundTrips(float zoom, float centerX, float centerY)
        {
            ViewportTransform transform = Create(zoom, centerX, centerY);
            var world = new Vector2(centerX + 3.75f, centerY - 6.5f);
            Vector2 back = transform.LocalToWorld(transform.WorldToLocal(world));
            Assert.That(back.x, Is.EqualTo(world.x).Within(Tolerance));
            Assert.That(back.y, Is.EqualTo(world.y).Within(Tolerance));
        }

        /// <summary>中心点对应的世界坐标必须映到视口正中; 这是中心点与平移量脱节的直接回归。</summary>
        [TestCase(2f, 0f, 0f)]
        [TestCase(24f, 0f, 0f)]
        [TestCase(24f, -18f, 9f)]
        [TestCase(200f, 40f, -25f)]
        public void CenterWorldPoint_MapsToViewportMiddle(float zoom, float centerX, float centerY)
        {
            ViewportTransform transform = Create(zoom, centerX, centerY);
            Vector2 local = transform.WorldToLocal(new Vector2(centerX, centerY));
            Assert.That(local.x, Is.EqualTo(transform.Size.x * 0.5f).Within(Tolerance));
            Assert.That(local.y, Is.EqualTo(transform.Size.y * 0.5f).Within(Tolerance));
        }

        /// <summary>世界 Y 轴向上而视口 Y 轴向下, 因此 Y 必须翻转。</summary>
        [Test]
        public void WorldToLocal_FlipsVerticalAxis()
        {
            ViewportTransform transform = Create();
            float lower = transform.WorldToLocal(new Vector2(0f, 1f)).y;
            float upper = transform.WorldToLocal(new Vector2(0f, 2f)).y;
            Assert.That(upper, Is.LessThan(lower));
        }

        /// <summary>缩放后锚点下方原有的世界坐标必须留在原处。</summary>
        [TestCase(1.15f)]
        [TestCase(0.869565f)]
        public void ZoomBy_KeepsAnchorWorldPointAtSameLocalPosition(float factor)
        {
            ViewportTransform transform = Create();
            var anchor = new Vector2(613f, 87f);
            Vector2 worldBefore = transform.LocalToWorld(anchor);
            transform.ZoomBy(factor, anchor);
            Vector2 worldAfter = transform.LocalToWorld(anchor);
            Assert.That(worldAfter.x, Is.EqualTo(worldBefore.x).Within(1e-3f));
            Assert.That(worldAfter.y, Is.EqualTo(worldBefore.y).Within(1e-3f));
        }

        /// <summary>缩放必须夹取到允许区间, 使取值恒为正。</summary>
        [Test]
        public void ZoomBy_ClampsToConfiguredRange()
        {
            ViewportTransform transform = Create(zoom: 100f);
            for (int i = 0; i < 100; i++)
                transform.ZoomBy(1.15f);
            Assert.That(transform.Zoom, Is.EqualTo(ViewportTransform.MaxZoom).Within(Tolerance));

            for (int i = 0; i < 400; i++)
                transform.ZoomBy(0.869565f);
            Assert.That(transform.Zoom, Is.EqualTo(ViewportTransform.MinZoom).Within(Tolerance));
        }

        /// <summary>非法倍率必须被忽略, 不得污染缩放或中心。</summary>
        [TestCase(0f)]
        [TestCase(-1.5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ZoomBy_WithInvalidFactor_LeavesViewUnchanged(float factor)
        {
            ViewportTransform transform = Create(zoom: 32f, centerX: 4f, centerY: -2f);
            Vector2 centerBefore = transform.Center;
            transform.ZoomBy(factor, new Vector2(10f, 20f));
            Assert.That(transform.Zoom, Is.EqualTo(32f).Within(Tolerance));
            Assert.That(transform.Center, Is.EqualTo(centerBefore));
        }

        /// <summary>"缩放到适应"必须让四个角都落在视口内并居中于视口正中。</summary>
        [Test]
        public void FitTo_FitsAllCornersInsideViewportAndCentersBounds()
        {
            ViewportTransform transform = Create(zoom: 200f, centerX: 300f, centerY: 300f);
            var bounds = new Rect(-20f, -10f, 40f, 20f);
            transform.FitTo(bounds, ViewportTransform.FitPaddingRatio);

            foreach (
                Vector2 corner in new[]
                {
                    new Vector2(bounds.xMin, bounds.yMin),
                    new Vector2(bounds.xMax, bounds.yMin),
                    new Vector2(bounds.xMax, bounds.yMax),
                    new Vector2(bounds.xMin, bounds.yMax),
                }
            )
            {
                Vector2 local = transform.WorldToLocal(corner);
                Assert.That(local.x, Is.InRange(0f, transform.Size.x));
                Assert.That(local.y, Is.InRange(0f, transform.Size.y));
            }
            Assert.That(transform.Center, Is.EqualTo(bounds.center));
        }

        /// <summary>适应必须取两个方向中更受限的那个倍率, 否则内容仍会溢出。</summary>
        [Test]
        public void FitTo_UsesTheMoreRestrictiveAxis()
        {
            ViewportTransform transform = Create();
            var bounds = new Rect(-5f, -20f, 10f, 40f);
            transform.FitTo(bounds, ViewportTransform.FitPaddingRatio);

            // 竖长矩形放进扁视口时, 受限的是高度方向: 400 * 0.84 / 40 = 8.4, 明显小于宽度方向的 67.2。
            float usable = 1f - 2f * ViewportTransform.FitPaddingRatio;
            float expected = 400f * usable / bounds.height;
            Assert.That(transform.Zoom, Is.EqualTo(expected).Within(1e-3f));
            Assert.That(transform.WorldLengthToPixels(bounds.height), Is.LessThanOrEqualTo(400f));
            Assert.That(transform.WorldLengthToPixels(bounds.width), Is.LessThanOrEqualTo(800f));
        }

        /// <summary>尺寸尚未量到时应只居中不改缩放, 交给布局完成后重试。</summary>
        [Test]
        public void FitTo_WithoutMeasuredSize_OnlyCenters()
        {
            var transform = new ViewportTransform { Size = Vector2.zero };
            transform.SetView(Vector2.zero, 32f);
            transform.FitTo(new Rect(-20f, -10f, 40f, 20f), ViewportTransform.FitPaddingRatio);
            Assert.That(transform.HasUsableSize, Is.False);
            Assert.That(transform.Zoom, Is.EqualTo(32f).Within(Tolerance));
            Assert.That(transform.Center, Is.EqualTo(Vector2.zero));
        }

        /// <summary>退化矩形只居中, 不得产生除零或把缩放压到下限之外。</summary>
        [Test]
        public void FitTo_WithDegenerateBounds_KeepsZoom()
        {
            ViewportTransform transform = Create();
            transform.FitTo(new Rect(3f, 4f, 0f, 0f), ViewportTransform.FitPaddingRatio);
            Assert.That(transform.Zoom, Is.EqualTo(32f).Within(Tolerance));
            Assert.That(transform.Center, Is.EqualTo(new Vector2(3f, 4f)));
        }

        /// <summary>视口尺寸变化时中心对应的世界坐标不得漂移。</summary>
        [Test]
        public void SizeChange_KeepsCenterWorldPoint()
        {
            ViewportTransform transform = Create(zoom: 24f, centerX: -18f, centerY: -9f);
            Vector2 worldAtCenter = transform.LocalToWorld(transform.Size * 0.5f);
            transform.Size = new Vector2(1200f, 700f);
            Vector2 worldAtCenterAfter = transform.LocalToWorld(transform.Size * 0.5f);
            Assert.That(worldAtCenterAfter.x, Is.EqualTo(worldAtCenter.x).Within(Tolerance));
            Assert.That(worldAtCenterAfter.y, Is.EqualTo(worldAtCenter.y).Within(Tolerance));
        }

        /// <summary>像素与世界的长度换算必须互为逆运算。</summary>
        [Test]
        public void LengthConversion_RoundTrips()
        {
            ViewportTransform transform = Create(zoom: 37f);
            Assert.That(
                transform.PixelsToWorldLength(transform.WorldLengthToPixels(2.5f)),
                Is.EqualTo(2.5f).Within(Tolerance)
            );
        }

        /// <summary>平移后, 原本位于某视口位置的 world 点必须正好跟着指针走完同样的位移。</summary>
        /// <remarks>
        /// 锁定平移的 Y 符号: 视口 Y 轴向下而世界 Y 轴向上, X 与 Y 的符号天然相反,
        /// 写成"中心点减位移"会让竖直方向反向。
        /// </remarks>
        [TestCase(32f, 100f, 300f, 37f, -52f)]
        [TestCase(2f, -40f, 60f, -5f, 220f)]
        [TestCase(200f, 640f, 120f, 12f, 12f)]
        public void PanBy_MovesContentWithPointer(float zoom, float originX, float originY, float deltaX, float deltaY)
        {
            ViewportTransform transform = Create(zoom);
            var origin = new Vector2(originX, originY);
            Vector2 world = transform.LocalToWorld(origin);
            var delta = new Vector2(deltaX, deltaY);

            transform.PanBy(delta);

            Vector2 after = transform.WorldToLocal(world);
            Assert.That(after.x, Is.EqualTo(origin.x + delta.x).Within(1e-3f));
            Assert.That(after.y, Is.EqualTo(origin.y + delta.y).Within(1e-3f));
        }

        /// <summary>平移不改变缩放。</summary>
        [Test]
        public void PanBy_KeepsZoom()
        {
            ViewportTransform transform = Create(zoom: 45f);
            transform.PanBy(new Vector2(120f, -75f));
            Assert.That(transform.Zoom, Is.EqualTo(45f).Within(Tolerance));
        }

        /// <summary>内容是否完整可见必须同时考虑缩放与中心点; 这是打开关卡是否需要重新取景的判据。</summary>
        [Test]
        public void ContainsWorldRect_DetectsCroppedAndOversizedViews()
        {
            var bounds = new Rect(-20f, -10f, 40f, 20f);
            ViewportTransform transform = Create(zoom: 24f, centerX: 0f, centerY: 0f);
            // 800x400 的视口在 24 px/单位下只能看到 33.3x16.7 个世界单位, 装不下 40x20。
            Assert.That(transform.ContainsWorldRect(bounds), Is.False);

            transform.FitTo(bounds, ViewportTransform.FitPaddingRatio);
            Assert.That(transform.ContainsWorldRect(bounds), Is.True);

            // 看到内容却偏到一边时, 必须判为不可见; 只看缩放会漏掉中心点错误。
            transform.PanBy(new Vector2(0f, 10000f));
            Assert.That(transform.ContainsWorldRect(bounds), Is.False);
        }

        /// <summary>尺寸未量到时不得判定为可见, 否则会跳过首次取景。</summary>
        [Test]
        public void ContainsWorldRect_WithoutMeasuredSize_ReturnsFalse()
        {
            var transform = new ViewportTransform { Size = Vector2.zero };
            transform.FitTo(new Rect(-1f, -1f, 2f, 2f), ViewportTransform.FitPaddingRatio);
            Assert.That(transform.ContainsWorldRect(new Rect(-1f, -1f, 2f, 2f)), Is.False);
        }
    }
}
