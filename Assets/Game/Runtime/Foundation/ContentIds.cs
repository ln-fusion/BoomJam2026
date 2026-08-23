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

    /// <summary>运行时 UI 预制体的稳定标识。</summary>
    [System.Serializable]
    public sealed class UiPrefabId : StrongId<UiPrefabId>
    {
        /// <summary>创建 UI 预制体稳定标识。</summary>
        /// <param name="value">原始 ID 字符串。</param>
        public UiPrefabId(string value) : base(value) { }
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

    /// <summary>本地化文本的稳定键。</summary>
    [System.Serializable]
    public sealed class LocalizationKey : StrongId<LocalizationKey>
    {
        /// <summary>创建本地化键。</summary>
        /// <param name="value">本地化键字符串。</param>
        public LocalizationKey(string value) : base(value) { }
    }

    /// <summary>背景音乐的稳定标识。</summary>
    [System.Serializable]
    public sealed class MusicId : StrongId<MusicId>
    {
        /// <summary>创建背景音乐标识。</summary>
        /// <param name="value">音乐稳定 ID 字符串。</param>
        public MusicId(string value) : base(value) { }
    }

    /// <summary>音效的稳定标识。</summary>
    [System.Serializable]
    public sealed class SfxId : StrongId<SfxId>
    {
        /// <summary>创建音效标识。</summary>
        /// <param name="value">音效稳定 ID 字符串。</param>
        public SfxId(string value) : base(value) { }
    }
}
