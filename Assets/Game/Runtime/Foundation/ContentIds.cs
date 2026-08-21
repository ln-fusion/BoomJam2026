namespace Game.Foundation
{
    [System.Serializable]
    public sealed class MapId : StrongId<MapId>
    {
        public MapId(string value) : base(value) { }
    }

    [System.Serializable]
    public sealed class PrefabId : StrongId<PrefabId>
    {
        public PrefabId(string value) : base(value) { }
    }

    [System.Serializable]
    public sealed class SpriteId : StrongId<SpriteId>
    {
        public SpriteId(string value) : base(value) { }
    }

    [System.Serializable]
    public sealed class AudioId : StrongId<AudioId>
    {
        public AudioId(string value) : base(value) { }
    }
}
