using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Rewind
{
    /// <summary>
    /// 单对象在某一 Tick 的可回溯状态快照。
    /// </summary>
    /// <remarks>
    /// 只覆盖设计文档 6.8 要求的可序列化物理量: 位置、旋转、线速度、角速度。
    /// 不含接触对、关节或 Sleep 状态——这些由物理引擎内部持有, 回溯时按设计文档
    /// 6.5 的失败恢复策略处理（重新构建而非局部复位）。
    /// </remarks>
    public readonly struct RewindSnapshot
    {
        /// <summary>快照对应的模拟 Tick。</summary>
        public readonly long Tick;

        /// <summary>刚体世界坐标。</summary>
        public readonly Vector2 Position;

        /// <summary>刚体绕 Z 轴旋转角度（度）。</summary>
        public readonly float RotationDegrees;

        /// <summary>刚体线速度。</summary>
        public readonly Vector2 LinearVelocity;

        /// <summary>刚体角速度（度/秒）。</summary>
        public readonly float AngularVelocity;

        /// <summary>创建快照。</summary>
        /// <param name="tick">模拟 Tick。</param>
        /// <param name="position">世界坐标。</param>
        /// <param name="rotationDegrees">旋转角度（度）。</param>
        /// <param name="linearVelocity">线速度。</param>
        /// <param name="angularVelocity">角速度（度/秒）。</param>
        public RewindSnapshot(
            long tick,
            Vector2 position,
            float rotationDegrees,
            Vector2 linearVelocity,
            float angularVelocity
        )
        {
            Tick = tick;
            Position = position;
            RotationDegrees = rotationDegrees;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }
    }

    /// <summary>
    /// 单对象回溯历史; 以固定容量环形缓冲保存最近若干个 Tick 的状态快照。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 容量由回溯时长与 Tick 频率决定: <c>容量 = 回溯秒数 × Tick 频率 + 1</c>。
    /// 60 Hz 下回溯 3 秒需要 181 个快照。每个快照 1 个 long + 2 个 Vector2 +
    /// 2 个 float = 8 + 8 + 8 + 4 + 4 = 32 字节（float 精度、无对齐填充时）,
    /// 加上环缓冲数组引用开销, 单对象 3 秒历史约 6 KB。设计文档 6.8 要求的能力框
    /// 数量级（数十个）下总内存不超过 1 MB, 无需对象池或分块存储。
    /// </para>
    /// <para>
    /// 缓冲只保存数据, 不持有 Unity 对象引用; 目标对象的读写由调用方通过
    /// <see cref="IRewindableState"/> 完成, 因此本类型可在 EditMode 下直接测试。
    /// </para>
    /// </remarks>
    public sealed class RewindHistory
    {
        private readonly RewindSnapshot[] _buffer;
        private int _nextIndex;
        private int _count;
        private long _latestTick = -1;

        /// <summary>创建固定容量的回溯历史。</summary>
        /// <param name="capacity">可保存的快照数量; 必须大于 0。</param>
        /// <exception cref="System.ArgumentOutOfRangeException">容量不大于 0 时抛出。</exception>
        public RewindHistory(int capacity)
        {
            if (capacity <= 0)
                throw new System.ArgumentOutOfRangeException(
                    nameof(capacity),
                    "Rewind history capacity must be positive."
                );
            _buffer = new RewindSnapshot[capacity];
        }

        /// <summary>缓冲容量; 即最多可回看的 Tick 数。</summary>
        public int Capacity => _buffer.Length;

        /// <summary>当前已保存的快照数量; 未填满时小于 <see cref="Capacity"/>。</summary>
        public int Count => _count;

        /// <summary>是否已有至少一个快照。</summary>
        public bool HasData => _count > 0;

        /// <summary>最近一次采样的 Tick; 无数据时为 -1。</summary>
        public long LatestTick => _latestTick;

        /// <summary>按固定 Tick 频率采样一次状态; 容量满时覆盖最旧快照。</summary>
        /// <param name="snapshot">待保存的快照。</param>
        public void Record(in RewindSnapshot snapshot)
        {
            _buffer[_nextIndex] = snapshot;
            _nextIndex = (_nextIndex + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
            _latestTick = snapshot.Tick;
        }

        /// <summary>清空全部历史; 用于新一次模拟运行开始前重置。</summary>
        public void Clear()
        {
            _nextIndex = 0;
            _count = 0;
            _latestTick = -1;
        }

        /// <summary>读取指定 Tick 的快照; 该 Tick 未采样或已超出容量时返回 false。</summary>
        /// <param name="tick">目标 Tick。</param>
        /// <param name="snapshot">查找到的快照; 失败时为 default。</param>
        /// <returns>找到快照返回 true。</returns>
        public bool TryGetSnapshot(long tick, out RewindSnapshot snapshot)
        {
            for (int i = 0; i < _count; i++)
            {
                RewindSnapshot candidate = GetByAge(i);
                if (candidate.Tick == tick)
                {
                    snapshot = candidate;
                    return true;
                }
            }
            snapshot = default;
            return false;
        }

        /// <summary>
        /// 读取指定 Tick 之前（含该 Tick）最近的一个快照。
        /// </summary>
        /// <param name="tick">目标 Tick; 通常为 <c>当前 Tick - 回溯 Tick 数</c>。</param>
        /// <param name="snapshot">查找到的快照; 失败时为 default。</param>
        /// <returns>存在不晚于目标 Tick 的快照时返回 true。</returns>
        /// <remarks>
        /// 从最新一条向最旧遍历, 返回不晚于 <paramref name="tick"/> 的快照中最新的一个。
        /// 该结果依赖快照按 Tick 递增顺序写入, 与固定 Tick 采样流程一致;
        /// 目标 Tick 早于全部已保存快照时返回 false, 由调用方决定失败策略。
        /// </remarks>
        public bool TryGetSnapshotAtOrBefore(long tick, out RewindSnapshot snapshot)
        {
            for (int age = _count - 1; age >= 0; age--)
            {
                RewindSnapshot candidate = GetByAge(age);
                if (candidate.Tick <= tick)
                {
                    snapshot = candidate;
                    return true;
                }
            }
            snapshot = default;
            return false;
        }

        /// <summary>按“距今第 N 旧”读取快照; 0 表示最旧的一条。</summary>
        /// <param name="age">距今的序号; 0 为最旧。</param>
        /// <returns>对应快照。</returns>
        private RewindSnapshot GetByAge(int age)
        {
            int start = (_nextIndex - _count + _buffer.Length) % _buffer.Length;
            return _buffer[(start + age) % _buffer.Length];
        }
    }
}
