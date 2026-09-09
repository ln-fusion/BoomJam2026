namespace Game.Foundation
{
    /// <summary>
    /// 能力类型稳定 ID，对应能力框能力种类的标识。
    /// </summary>
    [System.Serializable]
    public sealed class AbilityTypeId : StrongId<AbilityTypeId>
    {
        /// <summary>创建能力类型稳定标识。</summary>
        /// <param name="value">稳定 ID（如 official.ability.speed）。</param>
        public AbilityTypeId(string value)
            : base(value) { }
    }

    /// <summary>
    /// 能力尺寸选项稳定 ID，对应关卡白名单中某能力的一个尺寸配置。
    /// </summary>
    [System.Serializable]
    public sealed class AbilitySizeId : StrongId<AbilitySizeId>
    {
        /// <summary>创建能力尺寸选项稳定标识。</summary>
        /// <param name="value">稳定 ID（如 speed.small）。</param>
        public AbilitySizeId(string value)
            : base(value) { }
    }

    /// <summary>
    /// 部署位置稳定 ID，标识玩家在某次会话中放置的一个能力框实例。
    /// </summary>
    [System.Serializable]
    public sealed class PlacementId : StrongId<PlacementId>
    {
        /// <summary>创建部署位置稳定标识。</summary>
        /// <param name="value">稳定 ID（如 placement_0001）。</param>
        public PlacementId(string value)
            : base(value) { }
    }
}
