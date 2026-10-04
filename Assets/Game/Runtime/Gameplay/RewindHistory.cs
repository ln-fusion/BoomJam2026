using System;
using Game.Contracts.Gameplay;

namespace Game.Gameplay
{
    /// <summary>单对象定长环形历史；满后覆盖最早记录，查询从目标 Tick 向现在寻找安全状态。</summary>
    public sealed class RewindHistory
    {
        private readonly RewindBodyState[] _states;
        private int _next;
        /// <summary>有效记录数。</summary>
        public int Count { get; private set; }
        /// <summary>固定容量，不随运行时长增长。</summary>
        public int Capacity => _states.Length;

        /// <summary>分配固定容量；至少保留一条历史记录。</summary>
        /// <param name="capacity">快照槽位数。</param>
        /// <exception cref="ArgumentOutOfRangeException">容量不为正数。</exception>
        public RewindHistory(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _states = new RewindBodyState[capacity];
        }

        /// <summary>按严格递增 Tick 追加状态，容量耗尽后覆盖最早记录。</summary>
        /// <param name="state">新状态。</param>
        /// <exception cref="ArgumentException">Tick 未严格递增。</exception>
        public void Record(in RewindBodyState state)
        {
            if (Count > 0 && state.Tick <= At(Count - 1).Tick)
                throw new ArgumentException("History ticks must increase.", nameof(state));
            _states[_next] = state;
            _next = (_next + 1) % Capacity;
            Count = Math.Min(Count + 1, Capacity);
        }

        /// <summary>从目标 Tick 向现在扫描；历史不足取最早记录，不足一条或全部阻挡时失败。</summary>
        /// <param name="targetTick">期望恢复的 Tick。</param>
        /// <param name="isAvailable">在当前世界验证快照落点的函数。</param>
        /// <param name="state">成功时为首个安全状态，失败时为默认值。</param>
        /// <returns>是否找到可用历史。</returns>
        public bool TryFind(long targetTick, Func<RewindBodyState, bool> isAvailable, out RewindBodyState state)
        {
            if (isAvailable == null) throw new ArgumentNullException(nameof(isAvailable));
            for (int index = 0; index < Count; index++)
            {
                RewindBodyState candidate = At(index);
                if (candidate.Tick < targetTick || !isAvailable(candidate)) continue;
                state = candidate;
                return true;
            }
            state = default;
            return false;
        }

        /// <summary>按时间升序读取已存在的槽位。</summary>
        /// <param name="index">从零开始的有效记录序号。</param>
        /// <returns>对应快照。</returns>
        private RewindBodyState At(int index) => _states[(_next - Count + Capacity + index) % Capacity];
    }
}
