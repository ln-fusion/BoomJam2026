namespace Game.Gameplay.Rewind
{
    /// <summary>
    /// 可回溯目标的状态读写端口; 由承载刚体的表现层组件实现。
    /// </summary>
    /// <remarks>
    /// C20 仅定义草案。设计文档 6.8 要求的回溯能力先读取本接口的当前状态存入
    /// <see cref="RewindHistory"/>, 需要回溯时再把历史快照写回。
    /// <para>
    /// 实现方必须保证读写不触发物理查询或碰撞回调, 避免回溯过程中产生新的
    /// 进入/离开事件: 位置与速度直接写入刚体, 不使用 <c>Transform</c> 与
    /// <c>Rigidbody2D.MovePosition</c> 这类会与物理步进耦合的 API。
    /// </para>
    /// <para>
    /// 真实物理对象由 C40/C41 接入; C20 只提供接口与可在 EditMode 验证的
    /// 纯数据实现, 用于确认对象状态模型不需要推翻。
    /// </para>
    /// </remarks>
    public interface IRewindableState
    {
        /// <summary>读取当前状态并生成快照。</summary>
        /// <param name="tick">当前模拟 Tick。</param>
        /// <returns>当前状态的快照。</returns>
        RewindSnapshot Capture(long tick);

        /// <summary>把快照写回对象状态。</summary>
        /// <param name="snapshot">待恢复的快照。</param>
        void Restore(in RewindSnapshot snapshot);
    }
}
