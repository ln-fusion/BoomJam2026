using System;
using Game.Contracts.Gameplay;
using Game.Gameplay.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 验证 C25 固定 Tick 循环的调度语义: 累加、单帧预算、落后终止与帧率无关性。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 全部用例使用二进制可精确表示的时长（0.25、0.125、0.0625 等）, 避免浮点残差把
    /// "恰好一个 Tick" 变成"差一个 Tick", 让断言失败指向真实缺陷而不是数值误差。
    /// </para>
    /// <para>
    /// 真实物理参与的行为不在此覆盖: 本地物理场景是否被真正推进、默认场景是否不受影响,
    /// 由 PlayMode 的 <c>SimulationLoopPhysicsTests</c> 覆盖。
    /// </para>
    /// </remarks>
    public sealed class SimulationLoopTests
    {
        private const double Tolerance = 1e-12;

        /// <summary>默认参数必须是设计文档 §6.6 推荐的 60 Hz 与已声明的预算、阈值。</summary>
        [Test]
        public void Default_UsesSixtyHertzWithDocumentedBudgetAndThreshold()
        {
            SimulationLoopSettings settings = SimulationLoopSettings.Default;

            Assert.That(settings.TickSeconds, Is.EqualTo(1.0 / 60.0));
            Assert.That(settings.MaxTicksPerFrame, Is.EqualTo(8));
            Assert.That(settings.MaxPendingSeconds, Is.EqualTo(1.0));
        }

        /// <summary>新建循环必须从 Tick 0 开始且处于运行状态, 不因构造而步进物理。</summary>
        [Test]
        public void NewLoop_StartsAtTickZeroWithoutStepping()
        {
            var physics = new FakeSimulationPhysics();

            var loop = new SimulationLoop(physics, Settings());

            Assert.That(loop.CurrentTick, Is.Zero);
            Assert.That(loop.Status, Is.EqualTo(SimulationLoopStatus.Running));
            Assert.That(loop.PendingSeconds, Is.Zero);
            Assert.That(loop.TickSeconds, Is.EqualTo(0.25));
            Assert.That(physics.SimulateCount, Is.Zero);
        }

        /// <summary>单帧时长不足一个 Tick 时不得步进, 但必须把时长留下而不是丢弃。</summary>
        [Test]
        public void AdvanceFrame_WithLessThanOneTick_KeepsTimeWithoutStepping()
        {
            var physics = new FakeSimulationPhysics();
            SimulationLoop loop = CreateLoop(physics);

            loop.AdvanceFrame(0.1875);

            Assert.That(loop.CurrentTick, Is.Zero);
            Assert.That(physics.SimulateCount, Is.Zero);
            Assert.That(loop.PendingSeconds, Is.EqualTo(0.1875).Within(Tolerance));
        }

        /// <summary>跨帧累加满一个 Tick 时恰好步进一次, 并扣掉一个 Tick 时长。</summary>
        [Test]
        public void AdvanceFrame_AccumulatesAcrossFramesUntilOneTick()
        {
            var physics = new FakeSimulationPhysics();
            SimulationLoop loop = CreateLoop(physics);

            loop.AdvanceFrame(0.125);
            Assert.That(loop.CurrentTick, Is.Zero, "第一帧只累加到半个 Tick, 不应步进。");
            loop.AdvanceFrame(0.125);

            Assert.That(loop.CurrentTick, Is.EqualTo(1));
            Assert.That(loop.PendingSeconds, Is.Zero.Within(Tolerance));
            Assert.That(physics.LastDelta, Is.EqualTo(0.25));
        }

        /// <summary>每次步进收到的步长必须恒等于 Tick 时长, 与当帧交付的时长无关。</summary>
        [Test]
        public void AdvanceFrame_PassesTickSecondsToEveryStep()
        {
            var physics = new FakeSimulationPhysics();
            SimulationLoop loop = CreateLoop(physics);

            loop.AdvanceFrame(0.875);

            Assert.That(loop.CurrentTick, Is.EqualTo(3));
            Assert.That(physics.SimulateCount, Is.EqualTo(3));
            Assert.That(physics.LastDelta, Is.EqualTo(0.25));
            Assert.That(physics.TotalDelta, Is.EqualTo(0.75).Within(Tolerance));
        }

        /// <summary>
        /// 帧率无关性: 同样的总时长无论切成几帧、怎样抖动, 都必须得到相同的 Tick 数。
        /// 这是技术设计文档 §6.6 "物理结果只取决于 Tick 数" 的直接判据。
        /// </summary>
        [Test]
        public void AdvanceFrame_IsFrameRateIndependent()
        {
            double[][] partitions =
            {
                new[] { 1.0 },
                new[] { 0.25, 0.25, 0.25, 0.25 },
                new[] { 0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125 },
                new[] { 0.5, 0.0625, 0.1875, 0.25 },
            };
            const long expectedTicks = 4;

            foreach (double[] partition in partitions)
            {
                var physics = new FakeSimulationPhysics();
                SimulationLoop loop = CreateLoop(physics);
                foreach (double frame in partition)
                    loop.AdvanceFrame(frame);

                Assert.That(
                    loop.CurrentTick,
                    Is.EqualTo(expectedTicks),
                    $"总时长 1 秒切成 {partition.Length} 帧后 Tick 数发生变化。"
                );
                Assert.That(physics.SimulateCount, Is.EqualTo(expectedTicks));
            }
        }

        /// <summary>
        /// 达到单帧预算时必须停止步进但保留未模拟的时长, 不能丢弃:
        /// 丢弃会让卡顿表现为计时变快, 与设计文档 §6.6 的要求相反。
        /// </summary>
        [Test]
        public void AdvanceFrame_WhenLagExceedsBudget_CapsTicksAndKeepsPendingTime()
        {
            var physics = new FakeSimulationPhysics();
            var loop = new SimulationLoop(physics, new SimulationLoopSettings(0.25, 3, 100.0));

            loop.AdvanceFrame(2.0);

            Assert.That(loop.CurrentTick, Is.EqualTo(3), "单帧最多执行 3 个 Tick。");
            Assert.That(loop.PendingSeconds, Is.EqualTo(1.25).Within(Tolerance));
            AssertConserved(loop, 2.0);
        }

        /// <summary>预算不改变总账: 已步进时长加上未模拟时长必须恒等于累计交付时长。</summary>
        [Test]
        public void AdvanceFrame_AcrossManyFrames_NeverLosesOrInventsTime()
        {
            var physics = new FakeSimulationPhysics();
            var loop = new SimulationLoop(physics, new SimulationLoopSettings(0.125, 2, 1000.0));
            double delivered = 0.0;

            foreach (double frame in new[] { 0.5, 0.0625, 1.0, 0.25, 0.1875, 0.03125 })
            {
                loop.AdvanceFrame(frame);
                delivered += frame;
                AssertConserved(loop, delivered);
            }

            Assert.That(loop.Status, Is.EqualTo(SimulationLoopStatus.Running));
        }

        /// <summary>未模拟时长刚好等于阈值时不得终止, 只有严格超过才算落后。</summary>
        [Test]
        public void AdvanceFrame_WithPendingAtThresholdExactly_KeepsRunning()
        {
            var physics = new FakeSimulationPhysics();
            var loop = new SimulationLoop(physics, new SimulationLoopSettings(0.25, 1, 0.75));

            loop.AdvanceFrame(1.0);

            Assert.That(loop.PendingSeconds, Is.EqualTo(0.75).Within(Tolerance));
            Assert.That(loop.Status, Is.EqualTo(SimulationLoopStatus.Running));
        }

        /// <summary>落后超过阈值时必须终止, 并且此后的帧不再步进物理。</summary>
        [Test]
        public void AdvanceFrame_WhenPendingExceedsThreshold_TerminatesAndStopsStepping()
        {
            var physics = new FakeSimulationPhysics();
            var loop = new SimulationLoop(physics, new SimulationLoopSettings(0.25, 1, 0.75));

            loop.AdvanceFrame(1.25);

            Assert.That(loop.PendingSeconds, Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(loop.Status, Is.EqualTo(SimulationLoopStatus.PerformanceTerminated));

            long ticksAtTermination = loop.CurrentTick;
            int stepsAtTermination = physics.SimulateCount;
            loop.AdvanceFrame(10.0);

            Assert.That(loop.CurrentTick, Is.EqualTo(ticksAtTermination));
            Assert.That(physics.SimulateCount, Is.EqualTo(stepsAtTermination));
        }

        /// <summary>
        /// 物理步进失败必须终止且不得把失败的步进计成 Tick:
        /// Tick 数要与真实发生的物理步数一致, 否则通关耗时与成绩会失真。
        /// </summary>
        [Test]
        public void AdvanceFrame_WhenPhysicsStepFails_TerminatesWithoutCountingTheTick()
        {
            var physics = new FakeSimulationPhysics { FailAfterSimulations = 1 };
            var loop = new SimulationLoop(physics, new SimulationLoopSettings(0.25, 8, 10.0));

            loop.AdvanceFrame(1.0);

            Assert.That(loop.Status, Is.EqualTo(SimulationLoopStatus.PhysicsUnavailable));
            Assert.That(loop.CurrentTick, Is.EqualTo(1), "只有第一次步进成功, 后续失败不得计入。");
            Assert.That(physics.SimulateCount, Is.EqualTo(1));
            Assert.That(loop.PendingSeconds, Is.EqualTo(0.75).Within(Tolerance));
        }

        /// <summary>
        /// 非有限或非正的帧时长必须被忽略: 它们来自暂停、编辑器单步或异常时间源,
        /// 既不代表物理性能不足, 也不应该被当成 0 累加进账。
        /// </summary>
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1.0)]
        [TestCase(0.0)]
        public void AdvanceFrame_WithNonFiniteOrNonPositiveDuration_IsIgnored(double frameSeconds)
        {
            var physics = new FakeSimulationPhysics();
            SimulationLoop loop = CreateLoop(physics);

            loop.AdvanceFrame(frameSeconds);

            Assert.That(loop.CurrentTick, Is.Zero);
            Assert.That(loop.PendingSeconds, Is.Zero);
            Assert.That(physics.SimulateCount, Is.Zero);
            Assert.That(loop.Status, Is.EqualTo(SimulationLoopStatus.Running));
        }

        /// <summary>非法参数属于配置错误, 必须在构造时立即抛出而不是静默纠正。</summary>
        [TestCase(0.0, 8, 1.0)]
        [TestCase(-0.25, 8, 1.0)]
        [TestCase(double.NaN, 8, 1.0)]
        [TestCase(double.PositiveInfinity, 8, 1.0)]
        [TestCase(0.25, 0, 1.0)]
        [TestCase(0.25, -1, 1.0)]
        [TestCase(0.25, 8, 0.0)]
        [TestCase(0.25, 8, double.NaN)]
        [TestCase(0.25, 8, double.PositiveInfinity)]
        public void Constructor_WithInvalidSettings_ThrowsArgumentOutOfRangeException(
            double tickSeconds,
            int maxTicksPerFrame,
            double maxPendingSeconds
        )
        {
            var physics = new FakeSimulationPhysics();
            var settings = new SimulationLoopSettings(tickSeconds, maxTicksPerFrame, maxPendingSeconds);

            Assert.That(() => new SimulationLoop(physics, settings), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        /// <summary>物理推进为 null 时无法构造, 必须抛出。</summary>
        [Test]
        public void Constructor_WithNullPhysics_ThrowsArgumentNullException()
        {
            Assert.That(() => new SimulationLoop(null, Settings()), Throws.ArgumentNullException);
        }

        /// <summary>构造一个默认 0.25 秒 Tick、预算与阈值都足够宽松的循环。</summary>
        /// <param name="physics">物理推进替身。</param>
        /// <returns>尚未步进的循环。</returns>
        private static SimulationLoop CreateLoop(FakeSimulationPhysics physics) =>
            new SimulationLoop(physics, Settings());

        /// <summary>构造本类用例通用的循环参数: 0.25 秒 Tick, 预算 8, 阈值 100 秒。</summary>
        /// <returns>循环参数。</returns>
        private static SimulationLoopSettings Settings() => new SimulationLoopSettings(0.25, 8, 100.0);

        /// <summary>
        /// 断言时间守恒: 已步进时长加未模拟时长必须等于累计交付时长。
        /// </summary>
        /// <param name="loop">被测循环。</param>
        /// <param name="deliveredSeconds">累计交付给循环的时长。</param>
        private static void AssertConserved(SimulationLoop loop, double deliveredSeconds)
        {
            double accounted = loop.CurrentTick * loop.TickSeconds + loop.PendingSeconds;
            Assert.That(
                accounted,
                Is.EqualTo(deliveredSeconds).Within(1e-9),
                "Tick 数与未模拟时长之和必须等于交付时长。"
            );
        }
    }
}
