using Game.Contracts.Gameplay;
using Game.Foundation;
using Game.Gameplay.Deployment;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 验证 C23 部署方案容器的记账、标识分配与快照语义。
    /// </summary>
    /// <remarks>
    /// 本类只覆盖容器自身行为; 白名单与几何规则由 <see cref="PlacementServiceTests"/> 覆盖。
    /// </remarks>
    public sealed class DeploymentPlanTests
    {
        private const int DefaultLimit = 10;

        /// <summary>新建方案必须为空且带上配置的容量上限。</summary>
        [Test]
        public void NewPlan_StartsEmptyWithConfiguredLimit()
        {
            var plan = new DeploymentPlan(DefaultLimit);

            Assert.That(plan.Count, Is.Zero);
            Assert.That(plan.TotalCapacity, Is.Zero);
            Assert.That(plan.CapacityLimit, Is.EqualTo(DefaultLimit));
            Assert.That(plan.HasCapacityLimit, Is.True);
            Assert.That(plan.Snapshot().Placements, Is.Empty);
        }

        /// <summary>容量上限小于等于 0 表示未配置上限, 此时不限制累计费用。</summary>
        [TestCase(0)]
        [TestCase(-5)]
        public void Plan_WithNonPositiveLimit_HasNoCapacityLimit(int limit)
        {
            var plan = new DeploymentPlan(limit);

            Assert.That(plan.HasCapacityLimit, Is.False);
            Assert.That(plan.CapacityLimit, Is.EqualTo(limit));
            Assert.That(plan.Add("ability", "size", 1_000_000, Vector2.zero).IsSuccess, Is.True);
        }

        /// <summary>放置必须分配递增且零填充的放置稳定标识。</summary>
        [Test]
        public void Add_AllocatesSequentialPlacementIds()
        {
            var plan = new DeploymentPlan(DefaultLimit);

            PlacementId first = plan.Add("ability", "size", 1, Vector2.zero).Value;
            PlacementId second = plan.Add("ability", "size", 1, Vector2.zero).Value;

            Assert.That(first.Value, Is.EqualTo("placement.0001"));
            Assert.That(second.Value, Is.EqualTo("placement.0002"));
        }

        /// <summary>放置必须累计容量费用。</summary>
        [Test]
        public void Add_AccumulatesTotalCapacity()
        {
            var plan = new DeploymentPlan(DefaultLimit);

            plan.Add("ability", "size", 3, Vector2.zero);
            plan.Add("ability", "size", 4, Vector2.zero);

            Assert.That(plan.TotalCapacity, Is.EqualTo(7));
        }

        /// <summary>累计费用恰好等于上限时必须允许放置（技术设计文档 §6.4: canStart 用 &lt;= 比较）。</summary>
        [Test]
        public void Add_WhenTotalReachesLimitExactly_IsAllowed()
        {
            var plan = new DeploymentPlan(5);

            plan.Add("ability", "size", 3, Vector2.zero);
            PlacementResult<PlacementId> result = plan.Add("ability", "size", 2, Vector2.zero);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(plan.TotalCapacity, Is.EqualTo(5));
        }

        /// <summary>超出上限时必须拒绝, 且不改变已有记账。</summary>
        [Test]
        public void Add_WhenCostWouldExceedLimit_ReturnsCapacityExceededAndKeepsPlan()
        {
            var plan = new DeploymentPlan(5);
            plan.Add("ability", "size", 4, Vector2.zero);

            PlacementResult<PlacementId> result = plan.Add("ability", "size", 2, Vector2.zero);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlacementError.CapacityExceeded));
            Assert.That(plan.TotalCapacity, Is.EqualTo(4));
            Assert.That(plan.Count, Is.EqualTo(1));
        }

        /// <summary>失败的放置不得消耗放置稳定标识, 否则存档里会出现空洞且难以断言。</summary>
        [Test]
        public void Add_AfterRejectedPlacement_DoesNotConsumePlacementId()
        {
            var plan = new DeploymentPlan(2);
            plan.Add("ability", "size", 2, Vector2.zero);
            Assert.That(plan.Add("ability", "size", 1, Vector2.zero).IsSuccess, Is.False);

            plan.Remove(new PlacementId("placement.0001"));
            PlacementResult<PlacementId> result = plan.Add("ability", "size", 1, Vector2.zero);

            Assert.That(result.Value.Value, Is.EqualTo("placement.0002"), "被拒绝的放置不应占用序号。");
        }

        /// <summary>位置必须按调用方传入的原值保存: 量化在校验之前完成, 容器不得再动它。</summary>
        [Test]
        public void Add_StoresPositionVerbatim()
        {
            var plan = new DeploymentPlan(DefaultLimit);

            plan.Add("ability", "size", 1, new Vector2(1.234567f, -2.5f));

            AbilityPlacementData stored = plan.Snapshot().Placements[0];
            Assert.That(stored.PositionX, Is.EqualTo(1.234567f));
            Assert.That(stored.PositionY, Is.EqualTo(-2.5f));
        }

        /// <summary>空能力类型属于调用方缺陷, 必须立即抛出而不是写入非法方案。</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Add_WithEmptyAbilityTypeId_Throws(string abilityTypeId)
        {
            var plan = new DeploymentPlan(DefaultLimit);

            Assert.Throws<System.ArgumentException>(() => plan.Add(abilityTypeId, "size", 1, Vector2.zero));
        }

        /// <summary>负容量费用属于调用方缺陷, 必须立即抛出。</summary>
        [Test]
        public void Add_WithNegativeCost_Throws()
        {
            var plan = new DeploymentPlan(DefaultLimit);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => plan.Add("ability", "size", -1, Vector2.zero));
        }

        /// <summary>快照必须按放置稳定标识序数升序排列, 使世界实例化顺序稳定。</summary>
        [Test]
        public void Snapshot_OrdersPlacementsByIdentifier()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            plan.Add("ability", "size", 1, Vector2.zero);
            plan.Add("ability", "size", 1, Vector2.zero);
            plan.Add("ability", "size", 1, Vector2.zero);

            var snapshot = plan.Snapshot();

            Assert.That(snapshot.Placements[0].PlacementId, Is.EqualTo("placement.0001"));
            Assert.That(snapshot.Placements[1].PlacementId, Is.EqualTo("placement.0002"));
            Assert.That(snapshot.Placements[2].PlacementId, Is.EqualTo("placement.0003"));
        }

        /// <summary>快照内的放置数据必须是副本, 调用方修改不得污染方案本体。</summary>
        [Test]
        public void Snapshot_EntriesAreCopies()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            plan.Add("ability", "size", 1, new Vector2(1f, 1f));

            DeploymentPlanSnapshot snapshot = plan.Snapshot();
            snapshot.Placements[0].PositionX = 999f;
            snapshot.Placements[0].PlacementId = "tampered";

            AbilityPlacementData stored = plan.Snapshot().Placements[0];
            Assert.That(stored.PositionX, Is.EqualTo(1f));
            Assert.That(stored.PlacementId, Is.EqualTo("placement.0001"));
        }

        /// <summary>方案变化后快照必须反映新内容, 不能返回过期的缓存。</summary>
        [Test]
        public void Snapshot_ReflectsSubsequentMutations()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            plan.Add("ability", "size", 1, Vector2.zero);
            Assert.That(plan.Snapshot().Placements, Has.Count.EqualTo(1));

            plan.Add("ability", "size", 1, Vector2.zero);
            Assert.That(plan.Snapshot().Placements, Has.Count.EqualTo(2));

            plan.Clear();
            Assert.That(plan.Snapshot().Placements, Is.Empty);
        }

        /// <summary>快照必须带上当前累计费用与容量上限, 供界面直接显示。</summary>
        [Test]
        public void Snapshot_CarriesCapacityLedger()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            plan.Add("ability", "size", 3, Vector2.zero);

            DeploymentPlanSnapshot snapshot = plan.Snapshot();

            Assert.That(snapshot.TotalCapacity, Is.EqualTo(3));
            Assert.That(snapshot.CapacityLimit, Is.EqualTo(DefaultLimit));
        }

        /// <summary>就地替换必须保持放置稳定标识不变。</summary>
        [Test]
        public void Replace_UpdatesSizeAndPositionWithoutChangingIdentifier()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            PlacementId id = plan.Add("ability", "size.small", 1, Vector2.zero).Value;

            PlacementResult result = plan.Replace(id, "size.large", 3, new Vector2(2f, 3f));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            AbilityPlacementData stored = plan.Snapshot().Placements[0];
            Assert.That(stored.PlacementId, Is.EqualTo(id.Value));
            Assert.That(stored.SizeOptionId, Is.EqualTo("size.large"));
            Assert.That(stored.PositionX, Is.EqualTo(2f));
            Assert.That(stored.PositionY, Is.EqualTo(3f));
        }

        /// <summary>替换必须按费用增量更新账本: 变小释放容量, 变大占用容量。</summary>
        [Test]
        public void Replace_AppliesCostDelta()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            PlacementId id = plan.Add("ability", "size.small", 4, Vector2.zero).Value;

            plan.Replace(id, "size.large", 7, Vector2.zero);
            Assert.That(plan.TotalCapacity, Is.EqualTo(7));

            plan.Replace(id, "size.small", 2, Vector2.zero);
            Assert.That(plan.TotalCapacity, Is.EqualTo(2));
        }

        /// <summary>替换后超出上限时必须整体拒绝, 方案保持原尺寸与原位置。</summary>
        [Test]
        public void Replace_WhenCostGrowthExceedsLimit_ReturnsCapacityExceededAndKeepsOldValues()
        {
            var plan = new DeploymentPlan(5);
            PlacementId id = plan.Add("ability", "size.small", 4, new Vector2(1f, 1f)).Value;

            PlacementResult result = plan.Replace(id, "size.huge", 6, new Vector2(9f, 9f));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlacementError.CapacityExceeded));
            Assert.That(plan.TotalCapacity, Is.EqualTo(4));
            AbilityPlacementData stored = plan.Snapshot().Placements[0];
            Assert.That(stored.SizeOptionId, Is.EqualTo("size.small"));
            Assert.That(stored.PositionX, Is.EqualTo(1f));
        }

        /// <summary>替换不存在的放置必须返回 PlacementNotFound。</summary>
        [Test]
        public void Replace_WithUnknownIdentifier_ReturnsPlacementNotFound()
        {
            var plan = new DeploymentPlan(DefaultLimit);

            PlacementResult result = plan.Replace(new PlacementId("placement.9999"), "size", 1, Vector2.zero);

            Assert.That(result.Error, Is.EqualTo(PlacementError.PlacementNotFound));
        }

        /// <summary>替换的放置标识为 null 必须安全失败。</summary>
        [Test]
        public void Replace_WithNullIdentifier_ReturnsPlacementNotFound()
        {
            var plan = new DeploymentPlan(DefaultLimit);

            PlacementResult result = plan.Replace(null, "size", 1, Vector2.zero);

            Assert.That(result.Error, Is.EqualTo(PlacementError.PlacementNotFound));
        }

        /// <summary>移除必须退回容量费用。</summary>
        [Test]
        public void Remove_ReturnsCapacityToTheLedger()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            PlacementId id = plan.Add("ability", "size", 4, Vector2.zero).Value;

            PlacementResult result = plan.Remove(id);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(plan.TotalCapacity, Is.Zero);
            Assert.That(plan.Count, Is.Zero);
        }

        /// <summary>移除不存在的放置必须返回 PlacementNotFound, 且不影响账本。</summary>
        [Test]
        public void Remove_WithUnknownIdentifier_ReturnsPlacementNotFound()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            plan.Add("ability", "size", 4, Vector2.zero);

            PlacementResult result = plan.Remove(new PlacementId("placement.9999"));

            Assert.That(result.Error, Is.EqualTo(PlacementError.PlacementNotFound));
            Assert.That(plan.TotalCapacity, Is.EqualTo(4));
        }

        /// <summary>移除后必须能重新读取方案, 且剩余项保持原顺序。</summary>
        [Test]
        public void Remove_KeepsRemainingOrderStable()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            PlacementId first = plan.Add("ability", "size", 1, Vector2.zero).Value;
            PlacementId second = plan.Add("ability", "size", 1, Vector2.zero).Value;
            PlacementId third = plan.Add("ability", "size", 1, Vector2.zero).Value;

            plan.Remove(second);

            Assert.That(plan.Snapshot().Placements[0].PlacementId, Is.EqualTo(first.Value));
            Assert.That(plan.Snapshot().Placements[1].PlacementId, Is.EqualTo(third.Value));
        }

        /// <summary>
        /// 清空必须只清方案: 容量归零, 但放置稳定标识继续递增, 不在同一次会话内复用。
        /// </summary>
        [Test]
        public void Clear_EmptiesPlanButContinuesIdentifierSequence()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            plan.Add("ability", "size", 3, Vector2.zero);

            plan.Clear();
            PlacementId next = plan.Add("ability", "size", 1, Vector2.zero).Value;

            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan.TotalCapacity, Is.EqualTo(1));
            Assert.That(next.Value, Is.EqualTo("placement.0002"), "清空后不得复用已分配过的标识。");
        }

        /// <summary>按标识读取必须返回副本; 修改副本不影响方案。</summary>
        [Test]
        public void TryGetPlacement_ReturnsCopy()
        {
            var plan = new DeploymentPlan(DefaultLimit);
            PlacementId id = plan.Add("ability", "size", 1, new Vector2(1f, 1f)).Value;

            Assert.That(plan.TryGetPlacement(id, out AbilityPlacementData copy), Is.True);
            copy.PositionX = 50f;

            Assert.That(plan.Snapshot().Placements[0].PositionX, Is.EqualTo(1f));
        }

        /// <summary>读取不存在的标识或 null 必须安全失败。</summary>
        [Test]
        public void TryGetPlacement_WithUnknownIdentifier_ReturnsFalse()
        {
            var plan = new DeploymentPlan(DefaultLimit);

            Assert.That(
                plan.TryGetPlacement(new PlacementId("placement.9999"), out AbilityPlacementData unknown),
                Is.False
            );
            Assert.That(unknown, Is.Null);
            Assert.That(plan.TryGetPlacement(null, out _), Is.False);
        }
    }
}
