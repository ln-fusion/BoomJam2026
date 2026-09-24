using System;

namespace Game.Contracts.Gameplay
{
    /// <summary>
    /// 固定 Tick 循环的运行状态; 终止后不再步进物理。
    /// </summary>
    /// <remarks>
    /// 技术设计文档 §6.6 要求"累计落后超过安全阈值时终止本次运行", 但未定义可观测载体。
    /// 这里补一个状态属性作为 §6.6 的补充, 使驱动方能在不改动
    /// <see cref="ISimulationLoop"/> 三个既有成员（CurrentTick/TickSeconds/AdvanceFrame）的前提下
    /// 观察到终止, 并据 <see cref="SimulationLoopStatus.PerformanceTerminated"/>
    /// 保证该运行不形成失败成绩。
    /// </remarks>
    public enum SimulationLoopStatus
    {
        /// <summary>正常运行, 每次 <see cref="ISimulationLoop.AdvanceFrame"/> 都会按预算步进物理。</summary>
        Running,

        /// <summary>
        /// 待模拟累积时间超过安全阈值, 已停止步进; 该运行不应形成失败成绩（技术设计文档 §6.6）。
        /// </summary>
        PerformanceTerminated,

        /// <summary>
        /// 底层物理场景无法步进, 已停止步进。
        /// </summary>
        /// <remarks>
        /// 出现本状态说明 <c>PhysicsScene2D.Simulate</c> 返回 false（场景已失效）。
        /// 必须先终止再上报: 若忽略该返回值并继续累加 Tick, 计时用的 Tick 数会与实际发生的物理
        /// 步数脱节, 成绩随之失真。
        /// </remarks>
        PhysicsUnavailable,
    }

    /// <summary>
    /// 固定 Tick 循环的运行时参数; 由组合根注入, 不属于关卡内容。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 技术设计文档 §6.6 规定这些数值"须通过目标硬件性能与高速碰撞测试确认, 确认后作为
    /// <c>PhysicsProfile</c> 的版本化参数锁定"。当前默认值是尚未按目标硬件确认的临时值,
    /// 也是本结构体尚未进入 <c>PhysicsProfileData</c> 的原因: 在数值被实测确认之前先固化进
    /// 关卡内容, 会让每份关卡都背上一个待改的内容修订。
    /// </para>
    /// <para>
    /// 取值依据见 <see cref="Default"/>; 循环构造时会校验三个值的合法范围。
    /// </para>
    /// </remarks>
    public readonly struct SimulationLoopSettings
    {
        /// <summary>单个固定 Tick 的时长, 单位为秒; 有限正数。</summary>
        public readonly double TickSeconds;

        /// <summary>单次渲染帧最多执行的 Tick 数; 至少为 1。</summary>
        public readonly int MaxTicksPerFrame;

        /// <summary>待模拟累积时间的安全阈值, 单位为秒; 有限正数。</summary>
        public readonly double MaxPendingSeconds;

        /// <summary>首发默认参数: 60 Hz、单帧最多 8 Tick、落后 1 秒即终止。</summary>
        /// <remarks>
        /// 60 Hz 取自技术设计文档 §6.6 的推荐初始值。
        /// 单帧 8 Tick 对应 60 Hz 下约 133 ms 的可追回卡顿, 足以吸收一次 GC 或一次资源加载,
        /// 同时把单帧的物理耗时限住, 避免卡顿被放大成更长的卡顿。
        /// 1 秒阈值意味着只有持续约三秒以上无法追平（按 8 Tick/帧的追平速度反推）才判定性能不足,
        /// 让偶发卡顿不至于误杀一次运行。
        /// </remarks>
        public static SimulationLoopSettings Default => new SimulationLoopSettings(1.0 / 60.0, 8, 1.0);

        /// <summary>创建循环参数; 合法性由 <c>SimulationLoop</c> 构造时校验。</summary>
        /// <param name="tickSeconds">单个 Tick 时长, 单位为秒。</param>
        /// <param name="maxTicksPerFrame">单帧最多执行的 Tick 数。</param>
        /// <param name="maxPendingSeconds">待模拟累积时间的安全阈值, 单位为秒。</param>
        public SimulationLoopSettings(double tickSeconds, int maxTicksPerFrame, double maxPendingSeconds)
        {
            TickSeconds = tickSeconds;
            MaxTicksPerFrame = maxTicksPerFrame;
            MaxPendingSeconds = maxPendingSeconds;
        }
    }

    /// <summary>
    /// 本地物理世界的显式步进入口; 由世界自身实现, 测试用替身替代。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 技术设计文档 §6.6 规定模拟循环只能调用 Gameplay 本地 Scene 的
    /// <c>PhysicsScene2D.Simulate</c>, 绝不调用会推进默认场景的全局 <c>Physics2D.Simulate</c>。
    /// 把步进收进世界自身, 而不是由接口交出原始 <c>PhysicsScene2D</c> 句柄, 是为了让这条约束在
    /// 结构上成立: 零值 <c>PhysicsScene2D</c> 的 <c>IsValid()</c> 返回 true 且指向默认场景,
    /// 句柄一旦外流, 调用方就无法自行判断它能不能步进; 而世界持有承载 Scene,
    /// 可以用可靠的存活判据把守这一步。
    /// </para>
    /// <para>
    /// 后续能力框的空间查询（技术设计文档 §6.9）同样在本地物理上执行, 届时在本接口上扩展查询方法,
    /// 使世界始终是本地物理句柄的唯一持有者。
    /// </para>
    /// </remarks>
    public interface ISimulationPhysics
    {
        /// <summary>按固定步长推进一次本地物理。</summary>
        /// <param name="fixedDeltaSeconds">本次步长, 单位为秒; 恒等于循环的 Tick 时长。</param>
        /// <returns>
        /// 推进成功返回 true; 世界已释放、本地物理不可用时返回 false。
        /// 调用方必须处理 false 而不能忽略: 忽略会让 Tick 数与实际物理步数脱节。
        /// </returns>
        bool Simulate(double fixedDeltaSeconds);
    }

    /// <summary>
    /// 脚本驱动的固定步长模拟循环; 渲染帧只负责把时长交给它。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 定义见技术设计文档 §6.6。计时与规则不散落在 <c>Update</c>/<c>FixedUpdate</c>:
    /// 驱动方每渲染帧调用一次 <see cref="AdvanceFrame"/>, 循环内部把时长累加后执行零到若干个
    /// 固定 Tick, 因此物理结果只取决于 Tick 数, 与渲染帧率无关。
    /// </para>
    /// <para>
    /// 通关耗时定义为 <c>CurrentTick * TickSeconds</c>; 存档保存整数 Tick 与 Tick 率,
    /// 禁止用墙钟或帧数计算成绩（技术设计文档 §6.6）。
    /// </para>
    /// <para>
    /// 本接口只描述调度, 不描述单个 Tick 内的阶段顺序。技术设计文档 §6.6 的八个固定阶段中,
    /// 目前只有物理步进与 Tick 递增已实现; 命令队列、能力前/后置 Tick、接触事实收集、
    /// 效果解析、条件判定与成败决议分别在 C26/C27 接入。
    /// </para>
    /// </remarks>
    public interface ISimulationLoop
    {
        /// <summary>已完成的 Tick 数; 每次开始模拟从 0 重新计数。</summary>
        long CurrentTick { get; }

        /// <summary>单个固定 Tick 的时长, 单位为秒; 运行期间恒定。</summary>
        double TickSeconds { get; }

        /// <summary>当前运行状态; 非 <see cref="SimulationLoopStatus.Running"/> 时不再步进物理。</summary>
        SimulationLoopStatus Status { get; }

        /// <summary>
        /// 推进一个渲染帧: 把时长加入累加器, 再按预算执行零到若干个固定 Tick。
        /// </summary>
        /// <param name="unscaledFrameSeconds">
        /// 渲染帧时长, 单位为秒; 应为未缩放时长（<c>Time.unscaledDeltaTime</c>）。
        /// 非有限值或非正值会被忽略, 不抛异常也不累加。
        /// </param>
        /// <remarks>
        /// 本方法在非 <see cref="SimulationLoopStatus.Running"/> 状态下是空操作, 因此驱动方
        /// 不必先判断状态。达到单帧 Tick 预算时, 未执行完的累积时间被保留而不是丢弃,
        /// 这使性能下降表现为画面变慢, 而不是计时变快（技术设计文档 §6.6）。
        /// </remarks>
        void AdvanceFrame(double unscaledFrameSeconds);
    }
}
