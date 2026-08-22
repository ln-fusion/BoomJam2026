namespace Game.Foundation
{
    /// <summary>地图内容的稳定标识。</summary>
    [System.Serializable]
    public sealed class MapId : StrongId<MapId>
    {
        /// <summary>创建地图稳定标识。</summary>
        /// <param name="value">原始 ID 字符串。</param>
        public MapId(string value) : base(value) { }
    }

    /// <summary>预制体资源的稳定标识。</summary>
    [System.Serializable]
    public sealed class PrefabId : StrongId<PrefabId>
    {
        /// <summary>创建预制体资源稳定标识。</summary>
        /// <param name="value">原始 ID 字符串。</param>
        public PrefabId(string value) : base(value) { }
    }

    /// <summary>精灵资源的稳定标识。</summary>
    [System.Serializable]
    public sealed class SpriteId : StrongId<SpriteId>
    {
        /// <summary>创建精灵资源稳定标识。</summary>
        /// <param name="value">原始 ID 字符串。</param>
        public SpriteId(string value) : base(value) { }
    }

    /// <summary>音频资源的稳定标识。</summary>
    [System.Serializable]
    public sealed class AudioId : StrongId<AudioId>
    {
        /// <summary>创建音频资源稳定标识。</summary>
        /// <param name="value">原始 ID 字符串。</param>
        public AudioId(string value) : base(value) { }
    }
}
