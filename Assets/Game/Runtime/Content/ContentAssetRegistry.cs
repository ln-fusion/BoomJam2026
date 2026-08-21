using System.Collections.Generic;
using Game.Foundation;
using Game.Contracts.Content;
using UnityEngine;

namespace Game.Content
{
    [System.Serializable]
    public sealed class PrefabAssetEntry
    {
        public string Id;
        public GameObject Asset;
    }

    [System.Serializable]
    public sealed class SpriteAssetEntry
    {
        public string Id;
        public Sprite Asset;
    }

    [System.Serializable]
    public sealed class AudioAssetEntry
    {
        public string Id;
        public AudioClip Asset;
    }

    [CreateAssetMenu(fileName = "ContentAssetRegistry", menuName = "Game/Content/Asset Registry")]
    public sealed class ContentAssetRegistry : ScriptableObject
    {
        [SerializeField] private List<PrefabAssetEntry> prefabs = new List<PrefabAssetEntry>();
        [SerializeField] private List<SpriteAssetEntry> sprites = new List<SpriteAssetEntry>();
        [SerializeField] private List<AudioAssetEntry> audioClips = new List<AudioAssetEntry>();

        public IReadOnlyList<PrefabAssetEntry> Prefabs => prefabs;
        public IReadOnlyList<SpriteAssetEntry> Sprites => sprites;
        public IReadOnlyList<AudioAssetEntry> AudioClips => audioClips;
    }

    public sealed class OfficialAssetResolver : IAssetResolver
    {
        private readonly Dictionary<string, GameObject> _prefabs =
            new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Sprite> _sprites =
            new Dictionary<string, Sprite>();
        private readonly Dictionary<string, AudioClip> _audio =
            new Dictionary<string, AudioClip>();

        public OfficialAssetResolver(ContentAssetRegistry registry)
        {
            if (registry == null)
                return;

            foreach (PrefabAssetEntry entry in registry.Prefabs)
                AddUnique(_prefabs, entry.Id, entry.Asset);
            foreach (SpriteAssetEntry entry in registry.Sprites)
                AddUnique(_sprites, entry.Id, entry.Asset);
            foreach (AudioAssetEntry entry in registry.AudioClips)
                AddUnique(_audio, entry.Id, entry.Asset);
        }

        public GameObject GetPrefab(PrefabId id)
        {
            return _prefabs.TryGetValue(id.Value, out GameObject asset) ? asset : null;
        }

        public Sprite GetSprite(SpriteId id)
        {
            return _sprites.TryGetValue(id.Value, out Sprite asset) ? asset : null;
        }

        public AudioClip GetAudio(AudioId id)
        {
            return _audio.TryGetValue(id.Value, out AudioClip asset) ? asset : null;
        }

        private static void AddUnique<T>(Dictionary<string, T> target, string id, T asset)
        {
            if (string.IsNullOrWhiteSpace(id) || target.ContainsKey(id))
                return;

            target.Add(id, asset);
        }
    }
}
