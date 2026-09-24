using Game.Contracts.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 验证部署网格量化与放置矩形构造、收缩的边界行为。
    /// </summary>
    public sealed class PlacementRectAndGridTests
    {
        /// <summary>容差必须显著小于网格步长的一半, 否则会吃掉一个完整网格内的合法位置。</summary>
        [Test]
        public void Epsilon_IsFarSmallerThanHalfGridStep()
        {
            Assert.That(PlacementGrid.Unit, Is.EqualTo(0.001d).Within(1e-12));
            Assert.That(PlacementGrid.Epsilon, Is.LessThan(PlacementGrid.Unit / 2d));
            Assert.That(PlacementGrid.Version, Is.GreaterThan(0));
        }

        /// <summary>
        /// 重叠探针内缩量必须大于容差（否则容差抵消内缩、贴边被误判为冲突）,
        /// 同时必须小于一个网格步长（否则会漏判占满一个网格的真实重叠）。
        /// </summary>
        [Test]
        public void OverlapProbeInset_SitsBetweenEpsilonAndGridStep()
        {
            Assert.That(PlacementGrid.OverlapProbeInset, Is.GreaterThan(PlacementGrid.Epsilon * 2d));
            Assert.That(PlacementGrid.OverlapProbeInset, Is.LessThan(PlacementGrid.Unit));
        }

        /// <summary>量化必须把任意坐标吸附到最近网格点。</summary>
        /// <param name="value">输入坐标。</param>
        /// <param name="expected">期望的量化结果。</param>
        [TestCase(0d, 0d)]
        [TestCase(0.0004d, 0d)]
        [TestCase(0.0006d, 0.001d)]
        [TestCase(1.234d, 1.234d)]
        [TestCase(-1.234d, -1.234d)]
        [TestCase(12.3456d, 12.346d)]
        public void Quantize_SnapsToNearestGridPoint(double value, double expected)
        {
            Assert.That(PlacementGrid.Quantize(value), Is.EqualTo(expected).Within(1e-9));
        }

        /// <summary>恰好落在中点时必须远离零取整, 而不是银行家舍入。</summary>
        /// <param name="value">中点坐标。</param>
        /// <param name="expected">期望的量化结果。</param>
        [TestCase(0.0005d, 0.001d)]
        [TestCase(-0.0005d, -0.001d)]
        [TestCase(0.0025d, 0.003d)]
        [TestCase(-0.0025d, -0.003d)]
        public void Quantize_RoundsMidpointAwayFromZero(double value, double expected)
        {
            Assert.That(PlacementGrid.Quantize(value), Is.EqualTo(expected).Within(1e-9));
        }

        /// <summary>量化必须幂等, 否则反复写入会让坐标缓慢漂移。</summary>
        /// <param name="value">输入坐标。</param>
        [TestCase(0d)]
        [TestCase(0.0004d)]
        [TestCase(-1.2345d)]
        [TestCase(123.4567d)]
        public void Quantize_IsIdempotent(double value)
        {
            double once = PlacementGrid.Quantize(value);
            Assert.That(PlacementGrid.Quantize(once), Is.EqualTo(once).Within(1e-12));
        }

        /// <summary>NaN 与无穷按原值返回, 有限性校验由调用方负责。</summary>
        [Test]
        public void Quantize_LeavesNonFiniteValuesUntouched()
        {
            Assert.That(double.IsNaN(PlacementGrid.Quantize(double.NaN)), Is.True);
            Assert.That(double.IsPositiveInfinity(PlacementGrid.Quantize(double.PositiveInfinity)), Is.True);
        }

        /// <summary>二维量化必须逐分量执行。</summary>
        [Test]
        public void Quantize_SnapsBothComponents()
        {
            Vector2 quantized = PlacementGrid.Quantize(new Vector2(1.0004f, -2.0006f));

            Assert.That(quantized.x, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(quantized.y, Is.EqualTo(-2.001f).Within(1e-6f));
        }

        /// <summary>有限正尺寸必须创建成功并给出正确的边界。</summary>
        [Test]
        public void TryCreate_AcceptsFinitePositiveSize()
        {
            PlacementError error = PlacementRect.TryCreate(new Vector2(1f, 2f), 4f, 6f, out PlacementRect rect);

            Assert.That(error, Is.EqualTo(PlacementError.None));
            Assert.That(rect.IsValid, Is.True);
            Assert.That(rect.Width, Is.EqualTo(4d).Within(1e-9));
            Assert.That(rect.Height, Is.EqualTo(6d).Within(1e-9));
            Assert.That(rect.MinX, Is.EqualTo(-1d).Within(1e-9));
            Assert.That(rect.MaxX, Is.EqualTo(3d).Within(1e-9));
            Assert.That(rect.MinY, Is.EqualTo(-1d).Within(1e-9));
            Assert.That(rect.MaxY, Is.EqualTo(5d).Within(1e-9));
        }

        /// <summary>零或负尺寸不构成一个可放置的框体。</summary>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(-1f, 1f)]
        [TestCase(1f, -1f)]
        public void TryCreate_RejectsNonPositiveSize(float width, float height)
        {
            PlacementError error = PlacementRect.TryCreate(Vector2.zero, width, height, out PlacementRect rect);

            Assert.That(error, Is.EqualTo(PlacementError.InvalidNumber));
            Assert.That(rect.IsValid, Is.False);
        }

        /// <summary>NaN 与无穷必须先被有限性校验拦截, 不能流入几何判定。</summary>
        [Test]
        public void TryCreate_RejectsNonFiniteValues()
        {
            Assert.That(
                PlacementRect.TryCreate(new Vector2(float.NaN, 0f), 1f, 1f, out _),
                Is.EqualTo(PlacementError.InvalidNumber)
            );
            Assert.That(
                PlacementRect.TryCreate(new Vector2(0f, float.PositiveInfinity), 1f, 1f, out _),
                Is.EqualTo(PlacementError.InvalidNumber)
            );
            Assert.That(
                PlacementRect.TryCreate(Vector2.zero, float.NaN, 1f, out _),
                Is.EqualTo(PlacementError.InvalidNumber)
            );
            Assert.That(
                PlacementRect.TryCreate(Vector2.zero, 1f, float.NegativeInfinity, out _),
                Is.EqualTo(PlacementError.InvalidNumber)
            );
        }

        /// <summary>默认值的矩形不可用, 校验入口据此判为非法数值。</summary>
        [Test]
        public void DefaultRect_IsInvalid()
        {
            Assert.That(default(PlacementRect).IsValid, Is.False);
        }

        /// <summary>收缩必须对称地减小四边, 中心保持不变。</summary>
        [Test]
        public void TryInset_ShrinksBoundsSymmetrically()
        {
            PlacementRect rect = PlacementZones.Rect(0f, 0f, 4f, 2f);

            Assert.That(rect.TryInset(0.25d, out PlacementRect inset), Is.True);
            Assert.That(inset.CenterX, Is.EqualTo(rect.CenterX).Within(1e-9));
            Assert.That(inset.CenterY, Is.EqualTo(rect.CenterY).Within(1e-9));
            Assert.That(inset.Width, Is.EqualTo(rect.Width - 0.5d).Within(1e-9));
            Assert.That(inset.Height, Is.EqualTo(rect.Height - 0.5d).Within(1e-9));
        }

        /// <summary>收缩量达到或超过半宽半高时矩形消失, 必须返回失败而不是给出负尺寸。</summary>
        [Test]
        public void TryInset_FailsWhenAmountSwallowsRect()
        {
            PlacementRect rect = PlacementZones.Rect(0f, 0f, 4f, 2f);

            Assert.That(rect.TryInset(1d, out PlacementRect inset), Is.False);
            Assert.That(inset.IsValid, Is.False);
            Assert.That(rect.TryInset(2d, out _), Is.False);
        }

        /// <summary>负收缩量意味着放大, 不属于收缩语义, 必须拒绝。</summary>
        [Test]
        public void TryInset_FailsForNegativeAmount()
        {
            PlacementRect rect = PlacementZones.Rect(0f, 0f, 4f, 2f);

            Assert.That(rect.TryInset(-0.1d, out _), Is.False);
        }

        /// <summary>非法矩形不能参与收缩。</summary>
        [Test]
        public void TryInset_FailsForInvalidRect()
        {
            Assert.That(default(PlacementRect).TryInset(0.1d, out _), Is.False);
        }
    }
}
