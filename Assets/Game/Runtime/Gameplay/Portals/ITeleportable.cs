using UnityEngine;

namespace Game.Gameplay.Portals
{
    /// <summary>
    /// 可传送目标的端口; 由承载刚体的表现层组件实现。
    /// </summary>
    /// <remarks>
    /// C20 仅定义草案, C38 接入真实物理对象。
    /// <para>
    /// 实现方必须让位置与速度的写入不触发物理查询: 直接写刚体字段, 不使用
    /// <c>Rigidbody2D.MovePosition</c> 或逐帧 <c>Transform</c> 赋值。传送后由
    /// <see cref="PortalRuntime"/> 的冷却机制阻止下一 Tick 的乒乓回传。
    /// </para>
    /// <para>
    /// 实现方还需保证传送期间不产生新的进出事件: 传送前应先从所有范围框的
    /// 占用集合中移除该实体, 传送后由这些框在下一 Tick 重新检测。否则出口若
    /// 落在另一个范围框内, 该框会在同 Tick 观察到一次"进入"并重复施加效果。
    /// </para>
    /// </remarks>
    public interface ITeleportable
    {
        /// <summary>对象当前世界坐标。</summary>
        Vector2 Position { get; }

        /// <summary>对象当前速度。</summary>
        Vector2 Velocity { get; }

        /// <summary>对象当前朝向（度）; 用于判断是否从入口正面进入。</summary>
        float ForwardDegrees { get; }

        /// <summary>把对象移动到指定坐标并设置速度; 不触发物理查询。</summary>
        /// <param name="position">目标世界坐标。</param>
        /// <param name="velocity">目标速度。</param>
        void ApplyTeleport(Vector2 position, Vector2 velocity);

        /// <summary>从所有范围框的占用集合中移除本实体; 传送前调用以避免重复触发。</summary>
        void DetachFromZones();
    }
}
