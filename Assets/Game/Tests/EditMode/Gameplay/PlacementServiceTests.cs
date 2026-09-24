using Game.Contracts.Content;
using Game.Contracts.Gameplay;
using Game.Foundation;
using Game.Gameplay.Deployment;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 验证 C23 部署服务：判定顺序、量化、容量与权威校验。
    /// </summary>
    /// <remarks>
    /// 默认关卡为原点为中心的 20×20 可部署正方形; 默认能力小尺寸 1×1 费用 1, 大尺寸 4×4 费用 3。
    /// </remarks>
    public sealed class PlacementServiceTests
    {
        /// <summary>合法放置必须成功并出现在方案中。</summary>
        [Test]
        public void Place_WithAllowedAbility_Succeeds()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            PlacementResult<PlacementId> result = service.Place(DeploymentFixtures.Place(0f, 0f));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(service.Plan.Count, Is.EqualTo(1));
            Assert.That(
                service.Plan.Snapshot().Placements[0].AbilityTypeId,
                Is.EqualTo(DeploymentFixtures.SpeedAbility)
            );
        }

        /// <summary>写入方案的位置必须是量化后的值, 而不是鼠标原始值（技术设计文档 §6.4）。</summary>
        [Test]
        public void Place_StoresQuantizedPosition()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            service.Place(DeploymentFixtures.Place(1.234567f, -2.5004f));

            AbilityPlacementData stored = service.Plan.Snapshot().Placements[0];
            Assert.That(stored.PositionX, Is.EqualTo(1.235f).Within(1e-6f));
            Assert.That(stored.PositionY, Is.EqualTo(-2.5f).Within(1e-6f));
        }

        /// <summary>恰好落在网格中点时必须向远离零的方向取整, 保证同一输入在任何机器上得到同一结果。</summary>
        [Test]
        public void Place_AtGridMidpoint_RoundsAwayFromZero()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            service.Place(DeploymentFixtures.Place(0.0005f, -0.0005f));

            AbilityPlacementData stored = service.Plan.Snapshot().Placements[0];
            Assert.That(stored.PositionX, Is.EqualTo(0.001f).Within(1e-6f));
            Assert.That(stored.PositionY, Is.EqualTo(-0.001f).Within(1e-6f));
        }

        /// <summary>不在白名单内的能力必须返回 AbilityNotAllowed。</summary>
        [Test]
        public void Place_WithUnknownAbility_ReturnsAbilityNotAllowed()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            PlacementResult<PlacementId> result = service.Place(
                DeploymentFixtures.Place("official.ability.other", DeploymentFixtures.SmallSize, 0f, 0f)
            );

            Assert.That(result.Error, Is.EqualTo(PlacementError.AbilityNotAllowed));
        }

        /// <summary>白名单内有该能力但没有该尺寸时必须返回 UnknownSize。</summary>
        [Test]
        public void Place_WithUnknownSize_ReturnsUnknownSize()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            PlacementResult<PlacementId> result = service.Place(
                DeploymentFixtures.Place(DeploymentFixtures.SpeedAbility, "speed.unknown", 0f, 0f)
            );

            Assert.That(result.Error, Is.EqualTo(PlacementError.UnknownSize));
        }

        /// <summary>NaN 位置必须被拒绝: NaN 参与比较恒为 false, 若先走几何判定会被静默放行。</summary>
        [Test]
        public void Place_WithNaNPosition_ReturnsInvalidNumber()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            PlacementResult<PlacementId> result = service.Place(DeploymentFixtures.Place(float.NaN, 0f));

            Assert.That(result.Error, Is.EqualTo(PlacementError.InvalidNumber));
        }

        /// <summary>无穷位置必须被拒绝。</summary>
        [Test]
        public void Place_WithInfinitePosition_ReturnsInvalidNumber()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            PlacementResult<PlacementId> result = service.Place(DeploymentFixtures.Place(0f, float.PositiveInfinity));

            Assert.That(result.Error, Is.EqualTo(PlacementError.InvalidNumber));
        }

        /// <summary>完整落在可部署区之外必须被拒绝。</summary>
        [Test]
        public void Place_OutsideDeployableArea_ReturnsOutsideDeployableArea()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            PlacementResult<PlacementId> result = service.Place(DeploymentFixtures.Place(100f, 0f));

            Assert.That(result.Error, Is.EqualTo(PlacementError.OutsideDeployableArea));
        }

        /// <summary>与禁放区真正相交必须被拒绝。</summary>
        [Test]
        public void Place_OverlappingForbiddenZone_ReturnsOverlapsForbiddenArea()
        {
            PlacementService service = DeploymentFixtures.Service(
                DeploymentFixtures.Level(
                    6,
                    DeploymentFixtures.DeployableZones(),
                    new[] { PlacementZones.Square("zone.forbidden.hazard", 2f, 0f, 2f) },
                    DeploymentFixtures.DefaultAbility()
                )
            );

            PlacementResult<PlacementId> result = service.Place(DeploymentFixtures.Place(1.5f, 0f));

            Assert.That(result.Error, Is.EqualTo(PlacementError.OverlapsForbiddenArea));
        }

        /// <summary>贴边摆放不算相交, 必须放行: 容差只用于区分相切与真正重叠。</summary>
        [Test]
        public void Place_FlushAgainstForbiddenZone_Succeeds()
        {
            PlacementService service = DeploymentFixtures.Service(
                DeploymentFixtures.Level(
                    6,
                    DeploymentFixtures.DeployableZones(),
                    new[] { PlacementZones.Square("zone.forbidden.hazard", 2f, 0f, 2f) },
                    DeploymentFixtures.DefaultAbility()
                )
            );

            // 1×1 能力框中心在 x=0.5 时右边界恰好落在禁放区左边界 x=1 上。
            PlacementResult<PlacementId> result = service.Place(DeploymentFixtures.Place(0.5f, 0f));

            Assert.That(result.IsSuccess, Is.True, result.Message);
        }

        /// <summary>
        /// 能力框之间允许重叠: 重叠由运行期的效果优先级决议处理, 且每个框各自计费
        /// （技术设计文档 §6.8）。
        /// </summary>
        [Test]
        public void Place_OverlappingAnotherPlacement_SucceedsAndChargesBoth()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(6));

            Assert.That(service.Place(DeploymentFixtures.Place(0f, 0f)).IsSuccess, Is.True);
            PlacementResult<PlacementId> second = service.Place(DeploymentFixtures.Place(0.2f, 0f));

            Assert.That(second.IsSuccess, Is.True, second.Message);
            Assert.That(service.Plan.TotalCapacity, Is.EqualTo(2), "重叠并不免除任何一个框的费用。");
        }

        /// <summary>累计费用加上新费用超出上限时必须拒绝。</summary>
        [Test]
        public void Place_WhenCapacityWouldBeExceeded_ReturnsCapacityExceeded()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(1));
            Assert.That(service.Place(DeploymentFixtures.Place(0f, 0f)).IsSuccess, Is.True);

            PlacementResult<PlacementId> result = service.Place(DeploymentFixtures.Place(5f, 5f));

            Assert.That(result.Error, Is.EqualTo(PlacementError.CapacityExceeded));
            Assert.That(service.Plan.Count, Is.EqualTo(1));
        }

        /// <summary>累计费用恰好等于上限时必须放行（技术设计文档 §6.4 的 &lt;= 语义）。</summary>
        [Test]
        public void Place_WhenTotalReachesLimitExactly_Succeeds()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(2));

            Assert.That(service.Place(DeploymentFixtures.Place(0f, 0f)).IsSuccess, Is.True);
            PlacementResult<PlacementId> result = service.Place(DeploymentFixtures.Place(5f, 5f));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(service.Plan.TotalCapacity, Is.EqualTo(2));
        }

        /// <summary>未配置容量上限的关卡不限制放置数量。</summary>
        [Test]
        public void Place_WithoutCapacityLimit_IsUnlimited()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(0));

            for (int i = 0; i < 5; i++)
            {
                PlacementResult<PlacementId> result = service.Place(DeploymentFixtures.Place(i * 2f, 0f));
                Assert.That(result.IsSuccess, Is.True, result.Message);
            }

            Assert.That(service.Plan.Count, Is.EqualTo(5));
        }

        /// <summary>移动时省略尺寸必须保持原尺寸与原费用。</summary>
        [Test]
        public void Move_WithoutSize_KeepsCurrentSizeAndCost()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(6));
            PlacementId id = DeploymentFixtures.Placed(
                service.Place(
                    DeploymentFixtures.Place(DeploymentFixtures.SpeedAbility, DeploymentFixtures.LargeSize, 0f, 0f)
                )
            );

            PlacementResult result = service.Move(new MoveAbilityCommand(id, null, new Vector2(3f, 3f)));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            AbilityPlacementData stored = service.Plan.Snapshot().Placements[0];
            Assert.That(stored.SizeOptionId, Is.EqualTo(DeploymentFixtures.LargeSize));
            Assert.That(stored.PositionX, Is.EqualTo(3f));
            Assert.That(service.Plan.TotalCapacity, Is.EqualTo(3));
        }

        /// <summary>换小尺寸必须退回容量差额。</summary>
        [Test]
        public void Move_ToSmallerSize_ReturnsCapacityDifference()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(6));
            PlacementId id = DeploymentFixtures.Placed(
                service.Place(
                    DeploymentFixtures.Place(DeploymentFixtures.SpeedAbility, DeploymentFixtures.LargeSize, 0f, 0f)
                )
            );

            PlacementResult result = service.Move(
                new MoveAbilityCommand(id, new AbilitySizeId(DeploymentFixtures.SmallSize), new Vector2(0f, 0f))
            );

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(service.Plan.TotalCapacity, Is.EqualTo(1));
            Assert.That(service.Plan.Snapshot().Placements[0].SizeOptionId, Is.EqualTo(DeploymentFixtures.SmallSize));
        }

        /// <summary>换成更大尺寸导致超出上限时, 移动必须整体拒绝并保持原位置与原尺寸。</summary>
        [Test]
        public void Move_ToLargerSizeBeyondCapacity_ReturnsCapacityExceededAndKeepsOldPlacement()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(4));
            PlacementId id = DeploymentFixtures.Placed(service.Place(DeploymentFixtures.Place(0f, 0f)));
            Assert.That(service.Place(DeploymentFixtures.Place(6f, 0f)).IsSuccess, Is.True);
            Assert.That(service.Place(DeploymentFixtures.Place(-6f, 0f)).IsSuccess, Is.True);
            Assert.That(service.Place(DeploymentFixtures.Place(0f, 6f)).IsSuccess, Is.True);

            PlacementResult result = service.Move(
                new MoveAbilityCommand(id, new AbilitySizeId(DeploymentFixtures.LargeSize), new Vector2(0f, 0f))
            );

            Assert.That(result.Error, Is.EqualTo(PlacementError.CapacityExceeded));
            AbilityPlacementData stored = service.Plan.Snapshot().Placements[0];
            Assert.That(stored.SizeOptionId, Is.EqualTo(DeploymentFixtures.SmallSize));
            Assert.That(stored.PositionX, Is.EqualTo(0f));
            Assert.That(service.Plan.TotalCapacity, Is.EqualTo(4));
        }

        /// <summary>移动到非法位置必须拒绝且保持原位置。</summary>
        [Test]
        public void Move_ToInvalidPosition_ReturnsGeometryErrorAndKeepsOldPosition()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(6));
            PlacementId id = DeploymentFixtures.Placed(service.Place(DeploymentFixtures.Place(1f, 1f)));

            PlacementResult result = service.Move(new MoveAbilityCommand(id, null, new Vector2(100f, 0f)));

            Assert.That(result.Error, Is.EqualTo(PlacementError.OutsideDeployableArea));
            Assert.That(service.Plan.Snapshot().Placements[0].PositionX, Is.EqualTo(1f));
        }

        /// <summary>移动到 NaN 位置必须返回 InvalidNumber。</summary>
        [Test]
        public void Move_ToNaNPosition_ReturnsInvalidNumber()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(6));
            PlacementId id = DeploymentFixtures.Placed(service.Place(DeploymentFixtures.Place(0f, 0f)));

            PlacementResult result = service.Move(new MoveAbilityCommand(id, null, new Vector2(float.NaN, 0f)));

            Assert.That(result.Error, Is.EqualTo(PlacementError.InvalidNumber));
        }

        /// <summary>移动不存在的放置必须返回 PlacementNotFound。</summary>
        [Test]
        public void Move_WithUnknownIdentifier_ReturnsPlacementNotFound()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            PlacementResult result = service.Move(
                new MoveAbilityCommand(new PlacementId("placement.9999"), null, Vector2.zero)
            );

            Assert.That(result.Error, Is.EqualTo(PlacementError.PlacementNotFound));
        }

        /// <summary>移除必须收回该框的容量费用。</summary>
        [Test]
        public void Remove_DropsPlacementAndReturnsCapacity()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(3));
            PlacementId id = DeploymentFixtures.Placed(
                service.Place(
                    DeploymentFixtures.Place(DeploymentFixtures.SpeedAbility, DeploymentFixtures.LargeSize, 0f, 0f)
                )
            );

            PlacementResult result = service.Remove(id);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(service.Plan.Count, Is.Zero);
            Assert.That(service.Plan.TotalCapacity, Is.Zero);
        }

        /// <summary>清空必须只清玩家方案, 关卡数据不受影响。</summary>
        [Test]
        public void Clear_EmptiesPlan()
        {
            LevelDefinition definition = DeploymentFixtures.Level(6);
            PlacementService service = DeploymentFixtures.Service(definition);
            service.Place(DeploymentFixtures.Place(0f, 0f));

            PlacementResult result = service.Clear();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(service.Plan.Count, Is.Zero);
            Assert.That(service.Plan.TotalCapacity, Is.Zero);
            Assert.That(definition.Objects, Is.Empty, "清空部署方案不得触碰关卡静态对象。");
            Assert.That(definition.DeployableZones, Has.Count.EqualTo(1), "清空部署方案不得改动关卡几何。");
        }

        /// <summary>健康方案通过权威校验时必须返回 None。</summary>
        [Test]
        public void ValidateCurrentPlan_ForHealthyPlan_ReturnsNone()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(6));
            service.Place(DeploymentFixtures.Place(0f, 0f));
            service.Place(DeploymentFixtures.Place(5f, 5f));

            Assert.That(service.ValidateCurrentPlan(), Is.EqualTo(PlacementError.None));
        }

        /// <summary>null 方案按空方案处理, 不抛出。</summary>
        [Test]
        public void Validate_WithNullPlan_ReturnsNone()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level());

            Assert.That(service.Validate(null), Is.EqualTo(PlacementError.None));
        }

        /// <summary>方案中越界的放置必须被权威校验发现（模拟关卡几何在界面缓存之后被改动）。</summary>
        [Test]
        public void Validate_ForOutOfZonePlacement_ReturnsOutsideDeployableArea()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(6));
            var plan = new DeploymentPlan(6);
            plan.Add(DeploymentFixtures.SpeedAbility, DeploymentFixtures.SmallSize, 1, new Vector2(100f, 0f));

            Assert.That(service.Validate(plan), Is.EqualTo(PlacementError.OutsideDeployableArea));
        }

        /// <summary>方案中未知能力的放置必须被权威校验发现。</summary>
        [Test]
        public void Validate_ForUnknownAbility_ReturnsAbilityNotAllowed()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(6));
            var plan = new DeploymentPlan(6);
            plan.Add("official.ability.ghost", DeploymentFixtures.SmallSize, 1, Vector2.zero);

            Assert.That(service.Validate(plan), Is.EqualTo(PlacementError.AbilityNotAllowed));
        }

        /// <summary>方案中未知尺寸的放置必须被权威校验发现。</summary>
        [Test]
        public void Validate_ForUnknownSize_ReturnsUnknownSize()
        {
            PlacementService service = DeploymentFixtures.Service(DeploymentFixtures.Level(6));
            var plan = new DeploymentPlan(6);
            plan.Add(DeploymentFixtures.SpeedAbility, "speed.ghost", 1, Vector2.zero);

            Assert.That(service.Validate(plan), Is.EqualTo(PlacementError.UnknownSize));
        }

        /// <summary>
        /// 白名单内容与方案记账不一致时（例如关卡内容被修订而界面仍持旧方案）,
        /// 权威校验必须按白名单重算费用并判定超容量。
        /// </summary>
        [Test]
        public void Validate_WhenRecomputedCostExceedsLimit_ReturnsCapacityExceeded()
        {
            LevelDefinition cheap = DeploymentFixtures.Level(
                4,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(
                    DeploymentFixtures.Carrier,
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, 1)
                )
            );
            PlacementService placing = DeploymentFixtures.Service(cheap);
            for (int i = 0; i < 4; i++)
            {
                PlacementResult<PlacementId> placed = placing.Place(DeploymentFixtures.Place(i * 2f, 0f));
                Assert.That(placed.IsSuccess, Is.True, placed.Message);
            }
            Assert.That(placing.Plan.TotalCapacity, Is.EqualTo(4));

            LevelDefinition expensive = DeploymentFixtures.Level(
                4,
                DeploymentFixtures.DeployableZones(),
                null,
                DeploymentFixtures.Allowed(
                    DeploymentFixtures.Carrier,
                    DeploymentFixtures.SpeedAbility,
                    DeploymentFixtures.Size(DeploymentFixtures.SmallSize, 1f, 1f, 2)
                )
            );
            PlacementService validating = DeploymentFixtures.Service(expensive);

            Assert.That(validating.Validate(placing.Plan), Is.EqualTo(PlacementError.CapacityExceeded));
        }
    }
}
