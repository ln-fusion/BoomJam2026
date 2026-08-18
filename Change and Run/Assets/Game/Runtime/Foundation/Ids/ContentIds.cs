using System;

namespace Game.Foundation.Ids
{
    [Serializable]
    public readonly struct MapId : IEquatable<MapId>
    {
        public string Value { get; }
        public MapId(string value) => Value = StableIdRules.Require(value, nameof(value));
        public bool Equals(MapId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MapId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(MapId left, MapId right) => left.Equals(right);
        public static bool operator !=(MapId left, MapId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct PrefabId : IEquatable<PrefabId>
    {
        public string Value { get; }
        public PrefabId(string value) => Value = StableIdRules.Require(value, nameof(value));
        public bool Equals(PrefabId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PrefabId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(PrefabId left, PrefabId right) => left.Equals(right);
        public static bool operator !=(PrefabId left, PrefabId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct SpriteId : IEquatable<SpriteId>
    {
        public string Value { get; }
        public SpriteId(string value) => Value = StableIdRules.Require(value, nameof(value));
        public bool Equals(SpriteId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SpriteId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SpriteId left, SpriteId right) => left.Equals(right);
        public static bool operator !=(SpriteId left, SpriteId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct AudioId : IEquatable<AudioId>
    {
        public string Value { get; }
        public AudioId(string value) => Value = StableIdRules.Require(value, nameof(value));
        public bool Equals(AudioId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AudioId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AudioId left, AudioId right) => left.Equals(right);
        public static bool operator !=(AudioId left, AudioId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct LevelId : IEquatable<LevelId>
    {
        public string Value { get; }
        public LevelId(string value) => Value = StableIdRules.Require(value, nameof(value));
        public bool Equals(LevelId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LevelId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(LevelId left, LevelId right) => left.Equals(right);
        public static bool operator !=(LevelId left, LevelId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct StoryId : IEquatable<StoryId>
    {
        public string Value { get; }
        public StoryId(string value) => Value = StableIdRules.Require(value, nameof(value));
        public bool Equals(StoryId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is StoryId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(StoryId left, StoryId right) => left.Equals(right);
        public static bool operator !=(StoryId left, StoryId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct CharacterId : IEquatable<CharacterId>
    {
        public string Value { get; }
        public CharacterId(string value) => Value = StableIdRules.Require(value, nameof(value));
        public bool Equals(CharacterId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CharacterId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(CharacterId left, CharacterId right) => left.Equals(right);
        public static bool operator !=(CharacterId left, CharacterId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct ArchiveEntryId : IEquatable<ArchiveEntryId>
    {
        public string Value { get; }
        public ArchiveEntryId(string value) => Value = StableIdRules.Require(value, nameof(value));
        public bool Equals(ArchiveEntryId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ArchiveEntryId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ArchiveEntryId left, ArchiveEntryId right) => left.Equals(right);
        public static bool operator !=(ArchiveEntryId left, ArchiveEntryId right) => !left.Equals(right);
    }
}
