using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Portals
{
    /// <summary>
    /// 一对相互连接的传送门; 由稳定 PortalId 成对配置。
    /// </summary>
    /// <remarks>
    /// 入口与出口各自是一次传送的两个端点。传送时在出口侧重算位置与速度,
    /// 并按出口朝向旋转速度向量。设计文档 6.8 要求同类效果按稳定 ID 决议,
    /// 因此 PortalId 参与排序, 不依赖构建顺序。
    /// </remarks>
    public sealed class PortalPair
    {
        /// <summary>创建传送门对。</summary>
        /// <param name="portalId">传送门对稳定标识。</param>
        /// <param name="entrancePosition">入口世界坐标。</param>
        /// <param name="entranceForwardDegrees">入口朝向（度）。</param>
        /// <param name="exitPosition">出口世界坐标。</param>
        /// <param name="exitForwardDegrees">出口朝向（度）。</param>
        public PortalPair(
            string portalId,
            Vector2 entrancePosition,
            float entranceForwardDegrees,
            Vector2 exitPosition,
            float exitForwardDegrees
        )
        {
            PortalId = portalId;
            EntrancePosition = entrancePosition;
            EntranceForwardDegrees = entranceForwardDegrees;
            ExitPosition = exitPosition;
            ExitForwardDegrees = exitForwardDegrees;
        }

        /// <summary>传送门对稳定标识。</summary>
        public string PortalId { get; }

        /// <summary>入口世界坐标。</summary>
        public Vector2 EntrancePosition { get; }

        /// <summary>入口朝向（度）。</summary>
        public float EntranceForwardDegrees { get; }

        /// <summary>出口世界坐标。</summary>
        public Vector2 ExitPosition { get; }

        /// <summary>出口朝向（度）。</summary>
        public float ExitForwardDegrees { get; }

        /// <summary>入口到出口的朝向差; 用于旋转速度向量。</summary>
        public float RotationDeltaDegrees => ExitForwardDegrees - EntranceForwardDegrees;
    }

    /// <summary>
    /// 传送结果; 失败时携带原因以便上层决定是否回退。
    /// </summary>
    public enum PortalTeleportResult
    {
        /// <summary>传送成功。</summary>
        Teleported,

        /// <summary>冷却中, 本次不再传送。</summary>
        InCooldown,

        /// <summary>出口被占用或目标位置不可用, 放弃传送。</summary>
        Blocked,

        /// <summary>没有可用的传送门对。</summary>
        NoPortal,
    }

    /// <summary>
    /// 传送门运行时草案; 处理位置/速度映射、冷却与重复触发保护。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 设计文档 6.8 与 C39 要求解决三类边界问题, 本类型各自给出对策:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <b>乒乓触发</b>: 传送后对象通常会与出口门重叠, 下一 Tick 立即再次触发反向传送,
    /// 形成无限循环。对策是传送后给该对象设置冷却 Tick, 冷却期内不再接收触发。
    /// </item>
    /// <item>
    /// <b>嵌入</b>: 出口位置若正好落在静态碰撞体内, 对象会被挤出并产生非预期速度。
    /// 对策是通过 <see cref="IPortalExitProbe"/> 询问出口是否可用, 不可用则放弃本次传送
    /// （<see cref="PortalTeleportResult.Blocked"/>）, 保持对象在原位, 由玩家重新调整布局。
    /// </item>
    /// <item>
    /// <b>重叠门/连续门</b>: 同一 Tick 内多个门命中时, 按 PortalId 升序只取第一个,
    /// 保证结果与遍历顺序无关。
    /// </item>
    /// </list>
    /// </remarks>
    public sealed class PortalRuntime
    {
        private readonly Dictionary<string, PortalPair> _pairs = new Dictionary<string, PortalPair>(
            System.StringComparer.Ordinal
        );
        private readonly Dictionary<string, long> _cooldownUntilTick = new Dictionary<string, long>(
            System.StringComparer.Ordinal
        );
        private readonly IPortalExitProbe _exitProbe;
        private readonly int _cooldownTicks;

        /// <summary>创建传送门运行时。</summary>
        /// <param name="exitProbe">出口可用性探测器; 为 null 时视为出口总是可用。</param>
        /// <param name="cooldownTicks">同一对象传送后的冷却 Tick 数; 必须非负。</param>
        public PortalRuntime(IPortalExitProbe exitProbe = null, int cooldownTicks = 2)
        {
            if (cooldownTicks < 0)
                throw new System.ArgumentOutOfRangeException(nameof(cooldownTicks), "Cooldown cannot be negative.");
            _exitProbe = exitProbe;
            _cooldownTicks = cooldownTicks;
        }

        /// <summary>已注册的传送门对数量。</summary>
        public int PortalCount => _pairs.Count;

        /// <summary>注册一对传送门; 重复 PortalId 会被拒绝。</summary>
        /// <param name="pair">传送门对。</param>
        /// <returns>注册成功返回 true; PortalId 为空或重复时返回 false。</returns>
        public bool TryRegister(PortalPair pair)
        {
            if (pair == null || string.IsNullOrWhiteSpace(pair.PortalId))
                return false;
            // 不用 Dictionary.TryAdd: Unity 2022.3 的 .NET Standard 2.1 不提供该重载。
            if (_pairs.ContainsKey(pair.PortalId))
                return false;
            _pairs.Add(pair.PortalId, pair);
            return true;
        }

        /// <summary>按 PortalId 查询传送门对。</summary>
        /// <param name="portalId">传送门对稳定标识。</param>
        /// <param name="pair">查找到的传送门对; 未注册时为 null。</param>
        /// <returns>已注册返回 true。</returns>
        public bool TryGetPair(string portalId, out PortalPair pair)
        {
            pair = null;
            return !string.IsNullOrWhiteSpace(portalId) && _pairs.TryGetValue(portalId, out pair);
        }

        /// <summary>判断对象在指定 Tick 是否处于传送冷却中。</summary>
        /// <param name="entityId">实体稳定标识。</param>
        /// <param name="tick">当前模拟 Tick。</param>
        /// <returns>冷却中返回 true。</returns>
        public bool IsInCooldown(string entityId, long tick)
        {
            return !string.IsNullOrWhiteSpace(entityId)
                && _cooldownUntilTick.TryGetValue(entityId, out long until)
                && tick < until;
        }

        /// <summary>把对象从入口传送到出口, 并按出口朝向旋转速度。</summary>
        /// <param name="entityId">实体稳定标识; 用于冷却记录。</param>
        /// <param name="portalId">触发对象所在的传送门对稳定标识。</param>
        /// <param name="position">对象当前世界坐标; 传送成功后更新为出口坐标。</param>
        /// <param name="velocity">对象当前速度; 传送成功后按朝向差旋转。</param>
        /// <param name="tick">当前模拟 Tick。</param>
        /// <returns>传送结果。</returns>
        public PortalTeleportResult TryTeleport(
            string entityId,
            string portalId,
            ref Vector2 position,
            ref Vector2 velocity,
            long tick
        )
        {
            if (!TryGetPair(portalId, out PortalPair pair))
                return PortalTeleportResult.NoPortal;
            if (IsInCooldown(entityId, tick))
                return PortalTeleportResult.InCooldown;
            if (_exitProbe != null && !_exitProbe.IsExitAvailable(pair))
                return PortalTeleportResult.Blocked;

            // 出口位置偏移: 把对象放在出口门中心, 冷却负责阻止下一 Tick 的乒乓回传。
            position = pair.ExitPosition;
            float radians = pair.RotationDeltaDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            velocity = new Vector2(velocity.x * cos - velocity.y * sin, velocity.x * sin + velocity.y * cos);
            if (_cooldownTicks > 0)
                _cooldownUntilTick[entityId ?? string.Empty] = tick + _cooldownTicks;
            return PortalTeleportResult.Teleported;
        }

        /// <summary>清空全部冷却记录; 新一次模拟运行开始前调用。</summary>
        public void ResetCoolDowns() => _cooldownUntilTick.Clear();
    }

    /// <summary>
    /// 传送出口可用性探测端口; 由物理层实现。
    /// </summary>
    /// <remarks>
    /// C20 只定义草案。真实实现应使用 <c>PhysicsScene2D.OverlapBox</c> 在出口位置
    /// 查询是否与静态碰撞体重叠, 并排除触发对象自身的碰撞体。返回 false 时
    /// 传送被放弃而不是强行放置, 避免对象嵌进墙体后被物理引擎弹飞。
    /// </remarks>
    public interface IPortalExitProbe
    {
        /// <summary>判断指定传送门对的出口位置是否可安全放置对象。</summary>
        /// <param name="pair">待检查的传送门对。</param>
        /// <returns>出口可用返回 true。</returns>
        bool IsExitAvailable(PortalPair pair);
    }
}
