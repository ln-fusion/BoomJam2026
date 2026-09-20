namespace Game.Foundation
{
    /// <summary>
    /// 关卡运行期实体稳定标识; 标识场景中被物理与能力系统跟踪的一个对象。
    /// </summary>
    /// <remarks>
    /// 实体标识只在一次模拟运行内有效, 由世界构建阶段按稳定创建顺序分配,
    /// 因此同一关卡、同一部署方案下的实体 ID 在重跑时保持一致。所有影响
    /// 结果的集合遍历都必须按本 ID 排序, 不能依赖物理回调或查询的原始顺序。
    /// </remarks>
    [System.Serializable]
    public sealed class EntityId : StrongId<EntityId>
    {
        /// <summary>创建实体稳定标识。</summary>
        /// <param name="value">稳定 ID（如 entity.cart 或 entity.object_0003）。</param>
        public EntityId(string value)
            : base(value) { }
    }
}
