using System;
using Game.Contracts.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>小车或 Box 的 C20 能力适配器；当前只支持单个根 BoxCollider2D，不处理关节和复合碰撞体。</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class TemporalBody2D : MonoBehaviour, IRewindableState, ITeleportable
    {
        [SerializeField] private string stableId;
        [SerializeField] private bool allowRewind = true;
        [SerializeField] private bool allowTeleport = true;
        /// <summary>同世界内唯一的稳定 ID，用于同 Tick 平局排序。</summary>
        public string StableId => stableId;
        /// <summary>是否接受回溯。</summary>
        public bool AllowRewind => allowRewind;
        /// <summary>是否接受传送。</summary>
        public bool AllowTeleport => allowTeleport;
        /// <summary>适配的刚体。</summary>
        public Rigidbody2D Body => GetComponent<Rigidbody2D>();
        /// <summary>安全落点检测使用的根矩形碰撞体。</summary>
        public BoxCollider2D Shape => GetComponent<BoxCollider2D>();
        /// <summary>传送后等待离开所有入口，避免出口重叠造成连锁传送。</summary>
        internal bool WaitingForPortalExit { get; set; }
        /// <summary>本对象最近一次位移能力执行 Tick。</summary>
        internal long LastDisplacementTick { get; set; } = -1;

        /// <summary>初始化对象标识与能力资格，随后由世界注册。</summary>
        /// <param name="id">非空稳定标识。</param>
        /// <param name="rewind">是否允许回溯。</param>
        /// <param name="teleport">是否允许传送。</param>
        public void Configure(string id, bool rewind = true, bool teleport = true)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Stable ID is required.", nameof(id));
            stableId = id;
            allowRewind = rewind;
            allowTeleport = teleport;
        }

        /// <inheritdoc/>
        public RewindBodyState CaptureState(long tick) =>
            new RewindBodyState(tick, Body.position, Body.rotation, Body.velocity, Body.angularVelocity);

        /// <inheritdoc/>
        public void RestoreState(in RewindBodyState state, RewindStateFields fields)
        {
            if ((fields & RewindStateFields.Position) != 0) Body.position = state.Position;
            if ((fields & RewindStateFields.Rotation) != 0) Body.rotation = state.Rotation;
            if ((fields & RewindStateFields.Velocity) != 0) Body.velocity = state.Velocity;
            if ((fields & RewindStateFields.AngularVelocity) != 0) Body.angularVelocity = state.AngularVelocity;
            Body.WakeUp();
        }

        /// <inheritdoc/>
        public void TeleportTo(Vector2 destination)
        {
            Body.position = destination;
            Body.WakeUp();
        }
    }
}
