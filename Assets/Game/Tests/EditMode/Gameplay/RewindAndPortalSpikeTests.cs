using System.Collections.Generic;
using Game.Gameplay.Portals;
using Game.Gameplay.Rewind;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>验证 C20 回溯历史缓冲的容量、覆盖与恢复语义。</summary>
    public sealed class RewindHistoryTests
    {
        /// <summary>容量非法时必须立刻拒绝, 避免运行期出现静默的零容量缓冲。</summary>
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_RejectsNonPositiveCapacity(int capacity)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RewindHistory(capacity));
        }

        /// <summary>未写满时保留全部快照, 且按写入顺序可查。</summary>
        [Test]
        public void Record_KeepsAllSnapshotsBeforeCapacity()
        {
            var history = new RewindHistory(4);
            for (long tick = 0; tick < 3; tick++)
                history.Record(MakeSnapshot(tick));

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.LatestTick, Is.EqualTo(2));
            Assert.That(history.TryGetSnapshot(0, out _), Is.True);
            Assert.That(history.TryGetSnapshot(2, out _), Is.True);
            Assert.That(history.TryGetSnapshot(3, out _), Is.False);
        }

        /// <summary>环形缓冲满后必须覆盖最旧快照, 且不丢失最新数据。</summary>
        [Test]
        public void Record_OverwritesOldestWhenFull()
        {
            var history = new RewindHistory(3);
            for (long tick = 0; tick < 5; tick++)
                history.Record(MakeSnapshot(tick));

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.TryGetSnapshot(1, out _), Is.False);
            Assert.That(history.TryGetSnapshot(2, out _), Is.True);
            Assert.That(history.TryGetSnapshot(3, out _), Is.True);
            Assert.That(history.TryGetSnapshot(4, out _), Is.True);
        }

        /// <summary>回溯时长超过已积累历史时, 应取最旧可用快照而不是失败。</summary>
        [Test]
        public void TryGetSnapshotAtOrBefore_ClampsToOldestAvailable()
        {
            var history = new RewindHistory(8);
            for (long tick = 10; tick < 13; tick++)
                history.Record(MakeSnapshot(tick));

            Assert.That(history.TryGetSnapshotAtOrBefore(10, out RewindSnapshot snapshot), Is.True);
            Assert.That(snapshot.Tick, Is.EqualTo(10));
            Assert.That(history.TryGetSnapshotAtOrBefore(9, out _), Is.False);
        }

        /// <summary>空缓冲查询必须安全失败, 不能抛出。</summary>
        [Test]
        public void TryGetSnapshotAtOrBefore_OnEmptyHistoryReturnsFalse()
        {
            var history = new RewindHistory(4);

            Assert.That(history.HasData, Is.False);
            Assert.That(history.LatestTick, Is.EqualTo(-1));
            Assert.That(history.TryGetSnapshotAtOrBefore(100, out _), Is.False);
        }

        /// <summary>清空后不得残留旧数据。</summary>
        [Test]
        public void Clear_DropsAllData()
        {
            var history = new RewindHistory(3);
            history.Record(MakeSnapshot(0));
            history.Clear();

            Assert.That(history.HasData, Is.False);
            Assert.That(history.Count, Is.Zero);
            Assert.That(history.LatestTick, Is.EqualTo(-1));
        }

        /// <summary>构造一个可识别的快照。</summary>
        /// <param name="tick">模拟 Tick。</param>
        /// <returns>快照。</returns>
        private static RewindSnapshot MakeSnapshot(long tick) =>
            new RewindSnapshot(tick, new Vector2(tick, tick * 2f), tick * 3f, new Vector2(tick, 0f), tick);
    }

    /// <summary>验证 C20 单对象回溯运行时的采样、冷却与不足历史处理。</summary>
    public sealed class RewindAbilityRuntimeTests
    {
        /// <summary>按 Tick 采样后, 回溯应恢复到目标 Tick 的状态。</summary>
        [Test]
        public void TryRewind_RestoresSnapshotAtConfiguredDistance()
        {
            var target = new FakeRewindable(Vector2.zero);
            var runtime = new RewindAbilityRuntime(
                new Game.Foundation.EntityId("entity.cart"),
                target,
                new RewindHistory(16),
                rewindTicks: 3
            );
            for (long tick = 0; tick <= 5; tick++)
            {
                target.Position = new Vector2(tick, 0f);
                runtime.SampleTick(tick);
            }

            bool rewound = runtime.TryRewind(5, out long restoredTick);

            Assert.That(rewound, Is.True);
            Assert.That(restoredTick, Is.EqualTo(2));
            Assert.That(target.Position.x, Is.EqualTo(2f));
            Assert.That(target.RestoreCount, Is.EqualTo(1));
        }

        /// <summary>冷却期内不得重复回溯, 防止一次进入触发多次。</summary>
        [Test]
        public void TryRewind_RespectsCooldown()
        {
            var target = new FakeRewindable(Vector2.zero);
            var runtime = new RewindAbilityRuntime(
                new Game.Foundation.EntityId("entity.cart"),
                target,
                new RewindHistory(16),
                rewindTicks: 2,
                cooldownTicks: 5
            );
            for (long tick = 0; tick <= 10; tick++)
                runtime.SampleTick(tick);

            Assert.That(runtime.TryRewind(10, out _), Is.True);
            Assert.That(runtime.TryRewind(11, out _), Is.False);
            Assert.That(runtime.TryRewind(14, out _), Is.False);
            Assert.That(runtime.TryRewind(15, out _), Is.True);
            Assert.That(target.RestoreCount, Is.EqualTo(2));
        }

        /// <summary>历史不足时必须失败且不触碰目标状态。</summary>
        [Test]
        public void TryRewind_FailsWhenHistoryEmpty()
        {
            var target = new FakeRewindable(new Vector2(7f, 7f));
            var runtime = new RewindAbilityRuntime(
                new Game.Foundation.EntityId("entity.cart"),
                target,
                new RewindHistory(16),
                rewindTicks: 3
            );

            Assert.That(runtime.TryRewind(5, out long restoredTick), Is.False);
            Assert.That(restoredTick, Is.EqualTo(-1));
            Assert.That(target.RestoreCount, Is.Zero);
            Assert.That(target.Position, Is.EqualTo(new Vector2(7f, 7f)));
        }

        /// <summary>重置后冷却与历史都要清空, 避免跨运行串味。</summary>
        [Test]
        public void Reset_ClearsHistoryAndCooldown()
        {
            var target = new FakeRewindable(Vector2.zero);
            var runtime = new RewindAbilityRuntime(
                new Game.Foundation.EntityId("entity.cart"),
                target,
                new RewindHistory(16),
                rewindTicks: 1,
                cooldownTicks: 10
            );
            for (long tick = 0; tick <= 3; tick++)
                runtime.SampleTick(tick);
            Assert.That(runtime.TryRewind(3, out _), Is.True);

            runtime.Reset();

            Assert.That(runtime.LastTriggerTick, Is.EqualTo(long.MinValue));
            Assert.That(runtime.TryRewind(4, out _), Is.False);
        }

        /// <summary>记录并回放的假目标; 不依赖 Unity 物理。</summary>
        private sealed class FakeRewindable : IRewindableState
        {
            /// <summary>创建假目标并设定初始位置。</summary>
            /// <param name="position">初始位置。</param>
            public FakeRewindable(Vector2 position)
            {
                Position = position;
            }

            /// <summary>当前模拟位置; 测试直接读写。</summary>
            public Vector2 Position { get; set; }

            /// <summary>恢复调用次数。</summary>
            public int RestoreCount { get; private set; }

            /// <summary>按当前位置生成快照。</summary>
            /// <param name="tick">模拟 Tick。</param>
            /// <returns>快照。</returns>
            public RewindSnapshot Capture(long tick) => new RewindSnapshot(tick, Position, 0f, Vector2.zero, 0f);

            /// <summary>写回位置并记录调用次数。</summary>
            /// <param name="snapshot">待恢复的快照。</param>
            public void Restore(in RewindSnapshot snapshot)
            {
                Position = snapshot.Position;
                RestoreCount++;
            }
        }
    }

    /// <summary>验证 C20 传送门运行的配对、方向映射与防乒乓/防嵌入。</summary>
    public sealed class PortalRuntimeTests
    {
        /// <summary>重复 PortalId 必须被拒绝, 否则构建期校验无法落地。</summary>
        [Test]
        public void TryRegister_RejectsDuplicateAndEmptyIds()
        {
            var runtime = new PortalRuntime();
            PortalPair pair = MakePair("portal.a");

            Assert.That(runtime.TryRegister(pair), Is.True);
            Assert.That(runtime.TryRegister(MakePair("portal.a")), Is.False);
            Assert.That(runtime.TryRegister(MakePair("  ")), Is.False);
            Assert.That(runtime.TryRegister(null), Is.False);
            Assert.That(runtime.PortalCount, Is.EqualTo(1));
        }

        /// <summary>正向传送必须把位置移到出口并保留速度大小。</summary>
        [Test]
        public void TryTeleport_MovesToExitAndRotatesVelocity()
        {
            var runtime = new PortalRuntime();
            runtime.TryRegister(new PortalPair("portal.a", Vector2.zero, 0f, new Vector2(10f, 0f), 90f));
            var position = new Vector2(0f, 0f);
            var velocity = new Vector2(1f, 0f);

            PortalTeleportResult result = runtime.TryTeleport("entity.cart", "portal.a", ref position, ref velocity, 5);

            Assert.That(result, Is.EqualTo(PortalTeleportResult.Teleported));
            Assert.That(position, Is.EqualTo(new Vector2(10f, 0f)));
            Assert.That(velocity.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(velocity.y, Is.EqualTo(1f).Within(1e-4f));
        }

        /// <summary>传送后冷却期内不得再次传送, 这是防乒乓的核心。</summary>
        [Test]
        public void TryTeleport_SecondTriggerInsideCooldownIsRejected()
        {
            var runtime = new PortalRuntime(cooldownTicks: 3);
            runtime.TryRegister(new PortalPair("portal.a", Vector2.zero, 0f, new Vector2(10f, 0f), 0f));
            var position = Vector2.zero;
            var velocity = Vector2.zero;

            Assert.That(
                runtime.TryTeleport("entity.cart", "portal.a", ref position, ref velocity, 5),
                Is.EqualTo(PortalTeleportResult.Teleported)
            );
            Assert.That(runtime.IsInCooldown("entity.cart", 6), Is.True);
            Assert.That(
                runtime.TryTeleport("entity.cart", "portal.a", ref position, ref velocity, 6),
                Is.EqualTo(PortalTeleportResult.InCooldown)
            );
            Assert.That(runtime.IsInCooldown("entity.cart", 8), Is.False);
            Assert.That(
                runtime.TryTeleport("entity.cart", "portal.a", ref position, ref velocity, 8),
                Is.EqualTo(PortalTeleportResult.Teleported)
            );
        }

        /// <summary>冷却按对象隔离: 一个对象冷却不应阻塞另一个对象。</summary>
        [Test]
        public void TryTeleport_CooldownIsPerEntity()
        {
            var runtime = new PortalRuntime(cooldownTicks: 5);
            runtime.TryRegister(new PortalPair("portal.a", Vector2.zero, 0f, new Vector2(10f, 0f), 0f));
            var position = Vector2.zero;
            var velocity = Vector2.zero;
            runtime.TryTeleport("entity.cart", "portal.a", ref position, ref velocity, 1);

            var otherPosition = Vector2.zero;
            var otherVelocity = Vector2.zero;
            Assert.That(
                runtime.TryTeleport("entity.cargo", "portal.a", ref otherPosition, ref otherVelocity, 2),
                Is.EqualTo(PortalTeleportResult.Teleported)
            );
        }

        /// <summary>出口被占用时必须放弃传送并保持原位, 避免嵌入后被弹飞。</summary>
        [Test]
        public void TryTeleport_ReturnsBlockedWhenExitUnavailable()
        {
            var runtime = new PortalRuntime(new UnavailableExitProbe(), cooldownTicks: 1);
            runtime.TryRegister(MakePair("portal.a"));
            var position = new Vector2(1f, 2f);
            var velocity = new Vector2(3f, 0f);

            PortalTeleportResult result = runtime.TryTeleport("entity.cart", "portal.a", ref position, ref velocity, 1);

            Assert.That(result, Is.EqualTo(PortalTeleportResult.Blocked));
            Assert.That(position, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(velocity, Is.EqualTo(new Vector2(3f, 0f)));
            Assert.That(runtime.IsInCooldown("entity.cart", 1), Is.False);
        }

        /// <summary>未注册的 PortalId 必须明确失败, 而不是静默留在原位。</summary>
        [Test]
        public void TryTeleport_UnknownPortalReturnsNoPortal()
        {
            var runtime = new PortalRuntime();
            var position = Vector2.zero;
            var velocity = Vector2.zero;

            Assert.That(
                runtime.TryTeleport("entity.cart", "portal.missing", ref position, ref velocity, 1),
                Is.EqualTo(PortalTeleportResult.NoPortal)
            );
        }

        /// <summary>构造一个位置与朝向都固定的传送门对。</summary>
        /// <param name="portalId">传送门对稳定标识。</param>
        /// <returns>传送门对。</returns>
        private static PortalPair MakePair(string portalId) =>
            new PortalPair(portalId, Vector2.zero, 0f, new Vector2(10f, 0f), 0f);

        /// <summary>出口永远不可用的探测器。</summary>
        private sealed class UnavailableExitProbe : IPortalExitProbe
        {
            /// <summary>始终返回不可用。</summary>
            /// <param name="pair">待检查的传送门对。</param>
            /// <returns>始终返回 false。</returns>
            public bool IsExitAvailable(PortalPair pair) => false;
        }
    }
}
