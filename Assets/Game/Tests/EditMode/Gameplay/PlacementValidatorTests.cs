using System.Globalization;
using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Gameplay.Deployment;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 验证放置校验的错误码、判定顺序、多区域行为与坐标量级无关性。
    /// </summary>
    public sealed class PlacementValidatorTests
    {
        /// <summary>校验器必须持有几何判定实现, 否则无法独立完成判定。</summary>
        [Test]
        public void Constructor_RejectsNullGeometry()
        {
            Assert.Throws<System.ArgumentNullException>(() => new PlacementValidator(null));
        }

        /// <summary>完整落在部署区内的矩形应当通过校验。</summary>
        [Test]
        public void Validate_AcceptsRectInsideDeployableZone()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = PlacementZones.Level(new[] { PlacementZones.Square("zone.a", 0f, 0f, 5f) });

            PlacementError error = validator.Validate(PlacementZones.Rect(0f, 0f, 1f, 1f), definition);

            Assert.That(error, Is.EqualTo(PlacementError.None));
        }

        /// <summary>与部署区相切的矩形仍算落在区域内。</summary>
        [Test]
        public void Validate_AcceptsRectFlushAgainstDeployableZone()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = PlacementZones.Level(new[] { PlacementZones.Square("zone.a", 0f, 0f, 5f) });

            PlacementError error = validator.Validate(PlacementZones.Rect(2f, 0f, 1f, 1f), definition);

            Assert.That(error, Is.EqualTo(PlacementError.None));
        }

        /// <summary>完全落在所有部署区之外时必须报区域外。</summary>
        [Test]
        public void Validate_RejectsRectOutsideAllZones()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = PlacementZones.Level(new[] { PlacementZones.Square("zone.a", 0f, 0f, 5f) });

            PlacementError error = validator.Validate(PlacementZones.Rect(10f, 0f, 1f, 1f), definition);

            Assert.That(error, Is.EqualTo(PlacementError.OutsideDeployableArea));
        }

        /// <summary>越界恰好一个网格必须被拒绝。</summary>
        [Test]
        public void Validate_RejectsRectOneGridOutsideDeployableZone()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = PlacementZones.Level(new[] { PlacementZones.Square("zone.a", 0f, 0f, 5f) });

            PlacementError error = validator.Validate(PlacementZones.Rect(2.001f, 0f, 1f, 1f), definition);

            Assert.That(error, Is.EqualTo(PlacementError.OutsideDeployableArea));
        }

        /// <summary>与禁放区真正相交时必须报冲突。</summary>
        [Test]
        public void Validate_RejectsRectOverlappingForbiddenZone()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = LevelWithForbiddenPit();

            PlacementError error = validator.Validate(PlacementZones.Rect(0f, 0f, 2f, 2f), definition);

            Assert.That(error, Is.EqualTo(PlacementError.OverlapsForbiddenArea));
        }

        /// <summary>仅贴着禁放区边界摆放必须被允许。</summary>
        [Test]
        public void Validate_AllowsRectTouchingForbiddenZoneBoundary()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = LevelWithForbiddenPit();

            PlacementError error = validator.Validate(PlacementZones.Rect(2f, 0f, 2f, 2f), definition);

            Assert.That(error, Is.EqualTo(PlacementError.None));
        }

        /// <summary>伸入禁放区恰好一个网格必须被拒绝。</summary>
        [Test]
        public void Validate_RejectsRectPokingIntoForbiddenZoneByOneGridCell()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = LevelWithForbiddenPit();

            PlacementError error = validator.Validate(PlacementZones.Rect(1.999f, 0f, 2f, 2f), definition);

            Assert.That(error, Is.EqualTo(PlacementError.OverlapsForbiddenArea));
        }

        /// <summary>同时违反两项时, 判定顺序保证只报先命中的那一项。</summary>
        [Test]
        public void Validate_ReportsOutsideDeployableBeforeForbiddenOverlap()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = PlacementZones.Level(
                new[] { PlacementZones.Square("zone.far", 100f, 0f, 5f) },
                new[] { PlacementZones.Square("zone.pit", 0f, 0f, 2f) }
            );

            PlacementError error = validator.Validate(PlacementZones.Rect(0f, 0f, 1f, 1f), definition);

            Assert.That(error, Is.EqualTo(PlacementError.OutsideDeployableArea));
        }

        /// <summary>
        /// 非法矩形必须在几何判定之前被拦截: 若先走几何, NaN 参与比较恒为 false,
        /// 非有限数值会被静默判为合法。
        /// </summary>
        [Test]
        public void Validate_RejectsInvalidRectBeforeGeometry()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            // 部署区覆盖原点, 若跳过有限性校验则该矩形会被误判为合法。
            LevelDefinition definition = PlacementZones.Level(new[] { PlacementZones.Square("zone.a", 0f, 0f, 100f) });

            PlacementError error = validator.Validate(default(PlacementRect), definition);

            Assert.That(error, Is.EqualTo(PlacementError.InvalidNumber));
        }

        /// <summary>缺少关卡定义时不存在任何可部署区域, 必须判为区域外而不是放过。</summary>
        [Test]
        public void Validate_RejectsWhenDefinitionIsNull()
        {
            IPlacementValidator validator = PlacementZones.Validator();

            PlacementError error = validator.Validate(PlacementZones.Rect(0f, 0f, 1f, 1f), null);

            Assert.That(error, Is.EqualTo(PlacementError.OutsideDeployableArea));
        }

        /// <summary>部署区集合为空时必须判为区域外。</summary>
        [Test]
        public void Validate_RejectsWhenDeployableZonesEmpty()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = PlacementZones.Level(System.Array.Empty<ZoneData>());

            PlacementError error = validator.Validate(PlacementZones.Rect(0f, 0f, 1f, 1f), definition);

            Assert.That(error, Is.EqualTo(PlacementError.OutsideDeployableArea));
        }

        /// <summary>落在任一部署区内即算合法, 不要求落在第一个区域内。</summary>
        [Test]
        public void Validate_AcceptsRectInsideAnyOfMultipleZones()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = PlacementZones.Level(
                new[]
                {
                    PlacementZones.Square("zone.left", -10f, 0f, 5f),
                    PlacementZones.Square("zone.right", 10f, 0f, 5f),
                }
            );

            Assert.That(
                validator.Validate(PlacementZones.Rect(-10f, 0f, 1f, 1f), definition),
                Is.EqualTo(PlacementError.None)
            );
            Assert.That(
                validator.Validate(PlacementZones.Rect(10f, 0f, 1f, 1f), definition),
                Is.EqualTo(PlacementError.None)
            );
            Assert.That(
                validator.Validate(PlacementZones.Rect(0f, 0f, 1f, 1f), definition),
                Is.EqualTo(PlacementError.OutsideDeployableArea)
            );
        }

        /// <summary>同一局部配置平移到不同坐标量级后结论必须完全一致。</summary>
        /// <param name="offsetX">平移量 X。</param>
        /// <param name="offsetY">平移量 Y。</param>
        [TestCase(0f, 0f)]
        [TestCase(0.001f, -0.002f)]
        [TestCase(500f, 500f)]
        [TestCase(-1000f, 250f)]
        public void Validate_IsTranslationInvariant(float offsetX, float offsetY)
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = PlacementZones.Level(
                new[] { PlacementZones.Square("zone.a", offsetX, offsetY, 5f) }
            );

            Assert.That(
                validator.Validate(PlacementZones.Rect(offsetX, offsetY, 1f, 1f), definition),
                Is.EqualTo(PlacementError.None),
                "中心处应当合法。"
            );
            Assert.That(
                validator.Validate(PlacementZones.Rect(offsetX + 2f, offsetY, 1f, 1f), definition),
                Is.EqualTo(PlacementError.None),
                "贴右边界应当合法。"
            );
            Assert.That(
                validator.Validate(PlacementZones.Rect(offsetX + 2.001f, offsetY, 1f, 1f), definition),
                Is.EqualTo(PlacementError.OutsideDeployableArea),
                "越界一个网格应当被拒绝。"
            );
        }

        /// <summary>
        /// 沿 X 轴扫描一个 1×1 框的中心, 输出合法性翻转位置供人工核验:
        /// 翻转必须落在 ±2.0 这种可解释的整数值上, 而不是 ±1.9995 之类的含糊值。
        /// </summary>
        [Test]
        public void Validate_BoundaryFlipTable()
        {
            IPlacementValidator validator = PlacementZones.Validator();
            LevelDefinition definition = PlacementZones.Level(new[] { PlacementZones.Square("zone.a", 0f, 0f, 5f) });
            const float halfSpan = 3f;
            const float step = 0.0005f;
            int steps = (int)(halfSpan * 2f / step);

            float firstAccepted = float.NaN;
            float lastAccepted = float.NaN;
            float firstRejectedAfterLastAccept = float.NaN;
            for (int i = 0; i <= steps; i++)
            {
                float x = -halfSpan + i * step;
                bool accepted =
                    validator.Validate(PlacementZones.Rect(x, 0f, 1f, 1f), definition) == PlacementError.None;
                if (accepted)
                {
                    if (float.IsNaN(firstAccepted))
                        firstAccepted = x;
                    lastAccepted = x;
                }
                else if (!float.IsNaN(firstAccepted) && float.IsNaN(firstRejectedAfterLastAccept))
                {
                    firstRejectedAfterLastAccept = x;
                }
            }

            TestContext.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[边界翻转表] 区域 x∈[-2.5,2.5], 1×1 框, 扫描步长 {0}\n  左翻转为合法 x = {1}\n  右翻转仍合法 x = {2}\n  其后首个拒绝 x = {3}",
                    step,
                    firstAccepted,
                    lastAccepted,
                    firstRejectedAfterLastAccept
                )
            );

            Assert.That(firstAccepted, Is.EqualTo(-2f).Within(1e-4f), "左翻转必须落在 -2.0。");
            Assert.That(lastAccepted, Is.EqualTo(2f).Within(1e-4f), "右翻转必须落在 2.0。");
            Assert.That(firstRejectedAfterLastAccept, Is.EqualTo(2.0005f).Within(1e-3f));
        }

        /// <summary>构造带中央禁放坑的关卡: 部署区 10×10, 禁放区 2×2。</summary>
        /// <returns>关卡定义。</returns>
        private static LevelDefinition LevelWithForbiddenPit() =>
            PlacementZones.Level(
                new[] { PlacementZones.Square("zone.floor", 0f, 0f, 10f) },
                new[] { PlacementZones.Square("zone.pit", 0f, 0f, 2f) }
            );
    }
}
