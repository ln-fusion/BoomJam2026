using System;
using UnityEngine;

namespace Game.Contracts.Gameplay
{
    /// <summary>C20 回溯可恢复的刚体状态；其他组件状态需要后续扩展独立适配器。</summary>
    [Flags]
    public enum RewindStateFields
    {
        /// <summary>位置；安全落点查询以此字段始终启用为前提。</summary>
        Position = 1,
        /// <summary>平面旋转角。</summary>
        Rotation = 2,
        /// <summary>线速度。</summary>
        Velocity = 4,
        /// <summary>角速度。</summary>
        AngularVelocity = 8,
        /// <summary>C20 默认恢复全部刚体运动状态。</summary>
        Motion = Position | Rotation | Velocity | AngularVelocity
    }

    /// <summary>固定 Tick 采集的单对象运动快照；不包含世界、关节和其他对象。</summary>
    public readonly struct RewindBodyState
    {
        /// <summary>采样 Tick。</summary>
        public readonly long Tick;
        /// <summary>世界位置。</summary>
        public readonly Vector2 Position;
        /// <summary>旋转角，单位为度。</summary>
        public readonly float Rotation;
        /// <summary>线速度。</summary>
        public readonly Vector2 Velocity;
        /// <summary>角速度，单位为度每秒。</summary>
        public readonly float AngularVelocity;

        /// <summary>创建只保存值类型的运动快照。</summary>
        /// <param name="tick">采样 Tick。</param>
        /// <param name="position">世界位置。</param>
        /// <param name="rotation">旋转角。</param>
        /// <param name="velocity">线速度。</param>
        /// <param name="angularVelocity">角速度。</param>
        public RewindBodyState(long tick, Vector2 position, float rotation, Vector2 velocity, float angularVelocity)
        {
            Tick = tick;
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
        }
    }

    /// <summary>C20 单对象回溯端口草案；只恢复配置状态，不回滚世界或永久进度。</summary>
    public interface IRewindableState
    {
        /// <summary>采集当前状态。</summary>
        /// <param name="tick">当前固定 Tick。</param>
        /// <returns>当前对象运动快照。</returns>
        RewindBodyState CaptureState(long tick);

        /// <summary>在规则阶段应用已验证的状态。</summary>
        /// <param name="state">安全落点对应的历史状态。</param>
        /// <param name="fields">允许恢复的状态字段。</param>
        void RestoreState(in RewindBodyState state, RewindStateFields fields);
    }

    /// <summary>能力框及场景传送组件共用的目标端口草案。</summary>
    public interface ITeleportable
    {
        /// <summary>在规则阶段移动至出口，保持旋转、线速度和角速度。</summary>
        /// <param name="destination">出口世界坐标。</param>
        void TeleportTo(Vector2 destination);
    }
}
