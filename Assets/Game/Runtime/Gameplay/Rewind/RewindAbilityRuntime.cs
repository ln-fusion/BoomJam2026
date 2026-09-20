using Game.Foundation;

namespace Game.Gameplay.Rewind
{
    /// <summary>
    /// 单对象回溯能力的运行时草案; 负责按固定 Tick 采样历史并在请求时恢复状态。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 设计文档 6.5 规定模拟世界不是真相来源, 且失败恢复要重建物理世界而非局部复位。
    /// 回溯能力是这条规则的例外: 它只恢复**单个目标对象**的物理量, 不重建世界,
    /// 也不修改其他对象的接触对或关节。因此触发器必须在写入后要求物理引擎
    /// 重新计算接触, 且同一对象在短时间内不得连续回溯（冷却）。
    /// </para>
    /// <para>
    /// 触发语义: 进入回溯框的瞬间触发一次, 不是持续生效。这与加速等持续型能力不同,
    /// 因此本类型不实现 <c>IAbilityRuntime</c>, C41 再按能力框框架接入。
    /// </para>
    /// </remarks>
    public sealed class RewindAbilityRuntime
    {
        private readonly IRewindableState _target;
        private readonly RewindHistory _history;
        private readonly int _rewindTicks;
        private readonly int _cooldownTicks;
        private long _lastTriggerTick = long.MinValue;

        /// <summary>创建单对象回溯运行时。</summary>
        /// <param name="targetId">被回溯对象的实体稳定标识。</param>
        /// <param name="target">被回溯的目标状态端口。</param>
        /// <param name="history">历史缓冲; 容量应大于 <paramref name="rewindTicks"/>。</param>
        /// <param name="rewindTicks">回溯时长, 单位为 Tick; 必须大于 0。</param>
        /// <param name="cooldownTicks">两次回溯之间的最小 Tick 间隔; 0 表示无冷却。</param>
        public RewindAbilityRuntime(
            EntityId targetId,
            IRewindableState target,
            RewindHistory history,
            int rewindTicks,
            int cooldownTicks = 0
        )
        {
            if (rewindTicks <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(rewindTicks), "Rewind length must be positive.");
            if (cooldownTicks < 0)
                throw new System.ArgumentOutOfRangeException(nameof(cooldownTicks), "Cooldown cannot be negative.");
            TargetId = targetId ?? throw new System.ArgumentNullException(nameof(targetId));
            _target = target ?? throw new System.ArgumentNullException(nameof(target));
            _history = history ?? throw new System.ArgumentNullException(nameof(history));
            _rewindTicks = rewindTicks;
            _cooldownTicks = cooldownTicks;
        }

        /// <summary>被回溯对象的实体稳定标识。</summary>
        public EntityId TargetId { get; }

        /// <summary>回溯时长; 单位 Tick。</summary>
        public int RewindTicks => _rewindTicks;

        /// <summary>最近一次成功回溯的 Tick; 从未回溯时为 <see cref="long.MinValue"/>。</summary>
        public long LastTriggerTick => _lastTriggerTick;

        /// <summary>在当前 Tick 采样一次目标状态; 每个固定 Tick 调用一次。</summary>
        /// <param name="tick">当前模拟 Tick。</param>
        public void SampleTick(long tick) => _history.Record(_target.Capture(tick));

        /// <summary>
        /// 尝试回溯到指定 Tick 之前的状态; 冷却中或历史不足时返回失败。
        /// </summary>
        /// <param name="tick">当前模拟 Tick。</param>
        /// <param name="restoredTick">实际恢复到的 Tick; 失败时为 -1。</param>
        /// <returns>成功恢复返回 true。</returns>
        public bool TryRewind(long tick, out long restoredTick)
        {
            restoredTick = -1;
            if (_cooldownTicks > 0 && _lastTriggerTick != long.MinValue && tick - _lastTriggerTick < _cooldownTicks)
                return false;
            long targetTick = tick - _rewindTicks;
            if (!_history.TryGetSnapshotAtOrBefore(targetTick, out RewindSnapshot snapshot))
                return false;
            _target.Restore(snapshot);
            _lastTriggerTick = tick;
            restoredTick = snapshot.Tick;
            return true;
        }

        /// <summary>清空历史并重置冷却; 新一次模拟运行开始前调用。</summary>
        public void Reset()
        {
            _history.Clear();
            _lastTriggerTick = long.MinValue;
        }
    }
}
