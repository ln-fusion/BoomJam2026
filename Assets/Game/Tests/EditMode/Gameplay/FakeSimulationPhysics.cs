using Game.Contracts.Gameplay;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 固定步长物理的替身: 只记账调用次数与步长, 不做任何真实物理。
    /// </summary>
    /// <remarks>
    /// 用替身而不是真实物理场景, 是为了让"步进了几次、每次步长是多少、失败后有没有继续计时"
    /// 这类调度问题可以被精确断言; 代价是无法覆盖物理本身, 那一部分由 PlayMode 的真实场景测试补上。
    /// </remarks>
    internal sealed class FakeSimulationPhysics : ISimulationPhysics
    {
        private int _simulateCount;
        private double _lastDelta;
        private double _totalDelta;

        /// <summary>成功推进的次数; 失败的调用不计入。</summary>
        internal int SimulateCount => _simulateCount;

        /// <summary>累计步长; 调度正确时应恒等于 Tick 数乘 Tick 时长。</summary>
        internal double TotalDelta => _totalDelta;

        /// <summary>最近一次成功推进的步长; 从未推进过时为 0。</summary>
        internal double LastDelta => _lastDelta;

        /// <summary>
        /// 成功推进多少次之后开始返回失败; 小于等于 0 表示永不失败。
        /// </summary>
        /// <remarks>用于制造"物理场景中途失效", 验证循环不会把失败的步进计成 Tick。</remarks>
        internal int FailAfterSimulations { get; set; }

        /// <summary>按配置推进或返回失败。</summary>
        /// <param name="fixedDeltaSeconds">本次步长。</param>
        /// <returns>未达到失败阈值时返回 true 并记账; 否则返回 false 且不改变任何计数。</returns>
        public bool Simulate(double fixedDeltaSeconds)
        {
            if (FailAfterSimulations > 0 && _simulateCount >= FailAfterSimulations)
                return false;

            _simulateCount++;
            _lastDelta = fixedDeltaSeconds;
            _totalDelta += fixedDeltaSeconds;
            return true;
        }
    }
}
