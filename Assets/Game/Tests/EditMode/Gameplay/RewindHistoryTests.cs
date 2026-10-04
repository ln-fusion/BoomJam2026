using System;
using System.Runtime.InteropServices;
using Game.Contracts.Gameplay;
using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>验证环形历史的有界内存、时间截断和阻挡回退规则。</summary>
    public sealed class RewindHistoryTests
    {
        /// <summary>长时间记录只保留固定窗口，旧目标自动截断到最早可用历史。</summary>
        [Test]
        public void CapacityStaysBoundedAndShortHistoryClamps()
        {
            var history = new RewindHistory(121);
            for (int tick = 0; tick < 10000; tick++) history.Record(State(tick));
            Assert.That(history.Count, Is.EqualTo(121));
            Assert.That(history.TryFind(0, state => true, out var first), Is.True);
            Assert.That(first.Tick, Is.EqualTo(9879));
            Assert.That(Marshal.SizeOf<RewindBodyState>(), Is.EqualTo(32));
            Assert.That(64 * 121 * Marshal.SizeOf<RewindBodyState>(), Is.EqualTo(247808));
        }

        /// <summary>目标时刻阻挡时按时间向现在选择首个可用状态，而不是跳到最早历史。</summary>
        [Test]
        public void BlockedDestinationSearchesOnlyTowardsPresent()
        {
            var history = new RewindHistory(10);
            for (int tick = 0; tick < 10; tick++) history.Record(State(tick));
            Assert.That(history.TryFind(4, state => state.Tick < 2 || state.Tick >= 7, out var found), Is.True);
            Assert.That(found.Tick, Is.EqualTo(7));
            Assert.That(history.TryFind(4, state => false, out _), Is.False);
        }

        /// <summary>无历史时返回失败，重复 Tick 被拒绝以免污染采样顺序。</summary>
        [Test]
        public void EmptyHistoryAndDuplicateTicksAreExplicit()
        {
            var history = new RewindHistory(2);
            Assert.That(history.TryFind(-120, state => true, out _), Is.False);
            history.Record(State(0));
            Assert.Throws<ArgumentException>(() => history.Record(State(0)));
        }

        /// <summary>生成便于断言时间与位置一致性的测试快照。</summary>
        /// <param name="tick">测试 Tick。</param>
        /// <returns>位置等于 Tick 的状态。</returns>
        private static RewindBodyState State(long tick) => new RewindBodyState(tick, new Vector2(tick, 0), 0, Vector2.right, 0);
    }
}
