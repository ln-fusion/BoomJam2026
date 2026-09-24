using System;
using Game.Contracts.Gameplay;

namespace Game.Gameplay.Simulation
{
    /// <summary>
    /// <see cref="ISimulationLoop"/> 的默认实现: 时间累加器加固定步长物理推进。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 调度语义见技术设计文档 §6.6。每个渲染帧先累加时长, 再执行至多
    /// <see cref="SimulationLoopSettings.MaxTicksPerFrame"/> 个 Tick, 每执行一个就扣掉一个 Tick 时长。
    /// 扣不掉的余量留在累加器里而不是丢弃, 因此卡顿只会让画面变慢, 不会让计时变快。
    /// </para>
    /// <para>
    /// 累加器在被扣减前始终保持"尚未模拟的真实时长"的含义。它同时承担终止判据:
    /// 余量超过 <see cref="SimulationLoopSettings.MaxPendingSeconds"/> 说明持续追不平,
    /// 此时转入 <see cref="SimulationLoopStatus.PerformanceTerminated"/> 并停止步进。
    /// </para>
    /// <para>
    /// 单个 Tick 内的固定阶段（技术设计文档 §6.6）目前只落地第 3 步（物理步进）与第 8 步
    /// （Tick 递增）。第 1、2、4、5 步依赖能力运行时与效果解析器, 属 C26;
    /// 第 6、7 步依赖条件引擎与成败决议, 属 C27。接入这些阶段时不得改变本类的调度语义,
    /// 否则同一份输入在接入前后会得到不同的 Tick 数。
    /// </para>
    /// </remarks>
    public sealed class SimulationLoop : ISimulationLoop
    {
        private readonly ISimulationPhysics _physics;
        private readonly int _maxTicksPerFrame;
        private readonly double _maxPendingSeconds;
        private double _pendingSeconds;
        private long _currentTick;

        /// <summary>创建固定 Tick 循环。</summary>
        /// <param name="physics">固定步长物理推进; 每次 Tick 调用一次。</param>
        /// <param name="settings">循环参数; 由组合根注入。</param>
        /// <exception cref="ArgumentNullException"><paramref name="physics"/> 为 null 时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="settings"/> 的 Tick 时长或落后阈值不是有限正数,
        /// 或单帧 Tick 预算小于 1 时抛出。这些值来自配置而非玩家输入, 非法即属编程错误,
        /// 静默纠正会把配置问题变成难以定位的计时偏差。
        /// </exception>
        public SimulationLoop(ISimulationPhysics physics, SimulationLoopSettings settings)
        {
            _physics = physics ?? throw new ArgumentNullException(nameof(physics));
            if (!IsFinitePositive(settings.TickSeconds))
                throw new ArgumentOutOfRangeException(nameof(settings), "TickSeconds must be a finite positive value.");
            if (settings.MaxTicksPerFrame < 1)
                throw new ArgumentOutOfRangeException(nameof(settings), "MaxTicksPerFrame must be at least 1.");
            if (!IsFinitePositive(settings.MaxPendingSeconds))
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    "MaxPendingSeconds must be a finite positive value."
                );

            TickSeconds = settings.TickSeconds;
            _maxTicksPerFrame = settings.MaxTicksPerFrame;
            _maxPendingSeconds = settings.MaxPendingSeconds;
        }

        /// <summary>已完成的 Tick 数; 每次开始模拟从 0 重新计数。</summary>
        public long CurrentTick => _currentTick;

        /// <summary>单个固定 Tick 的时长, 单位为秒。</summary>
        public double TickSeconds { get; }

        /// <summary>当前运行状态; 非 Running 时 <see cref="AdvanceFrame"/> 为空操作。</summary>
        public SimulationLoopStatus Status { get; private set; } = SimulationLoopStatus.Running;

        /// <summary>尚未模拟的累积时长, 单位为秒; 用于诊断与测试, 不参与规则判定。</summary>
        public double PendingSeconds => _pendingSeconds;

        /// <summary>
        /// 推进一个渲染帧: 累加时长后按预算执行零到若干个固定 Tick。
        /// </summary>
        /// <param name="unscaledFrameSeconds">渲染帧时长, 单位为秒; 应为未缩放时长。</param>
        /// <remarks>
        /// 非有限值与非正值被忽略而不是当成 0 累加, 也不会终止循环: 这两种输入来自暂停、
        /// 编辑器单步或异常的时间源, 都不代表物理性能不足。
        /// </remarks>
        public void AdvanceFrame(double unscaledFrameSeconds)
        {
            if (Status != SimulationLoopStatus.Running)
                return;
            if (!IsFinitePositive(unscaledFrameSeconds))
                return;

            _pendingSeconds += unscaledFrameSeconds;

            int budget = _maxTicksPerFrame;
            while (budget > 0 && _pendingSeconds >= TickSeconds)
            {
                if (!_physics.Simulate(TickSeconds))
                {
                    // 不扣减余量也不递增 Tick: Tick 数必须始终等于真实发生的物理步数,
                    // 否则通关耗时与成绩都会失真。
                    Status = SimulationLoopStatus.PhysicsUnavailable;
                    return;
                }
                _pendingSeconds -= TickSeconds;
                _currentTick++;
                budget--;
            }

            if (_pendingSeconds > _maxPendingSeconds)
                Status = SimulationLoopStatus.PerformanceTerminated;
        }

        /// <summary>判断一个时长或阈值是否为有限正数。</summary>
        /// <param name="value">待判断的值。</param>
        /// <returns>非 NaN、非无穷且大于零时返回 true。</returns>
        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
    }
}
