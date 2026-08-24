using System.Collections.Generic;
using Game.Foundation;
using Game.Contracts.Content;
using UnityEngine;

namespace Game.Content
{
    /// <summary>
    /// 预制体资源 ID 与 Unity 资源的映射项。
    /// </summary>
    [System.Serializable]
    public sealed class PrefabAssetEntry
    {
        /// <summary>预制体资源稳定标识。</summary>
        public string Id;
        /// <summary>对应的 Unity 预制体。</summary>
        public GameObject Asset;
    }

    /// <summary>
    /// 精灵资源 ID 与 Unity 资源的映射项。
    /// </summary>
    [System.Serializable]
    public sealed class SpriteAssetEntry
    {
        /// <summary>精灵资源稳定标识。</summary>
        public string Id;
        /// <summary>对应的 Unity 精灵。</summary>
        public Sprite Asset;
    }

    /// <summary>
    /// 音频资源 ID 与 Unity 资源的映射项。
    /// </summary>
    [System.Serializable]
    public sealed class AudioAssetEntry
    {
        /// <summary>音频资源稳定标识。</summary>
        public string Id;
        /// <summary>对应的 Unity 音频片段。</summary>
        public AudioClip Asset;
    }

    /// <summary>
    /// 官方资源 Registry，供 Unity Inspector 配置稳定 ID 到资源对象的映射。
    /// </summary>
    [CreateAssetMenu(fileName = "ContentAssetRegistry", menuName = "Game/Content/Asset Registry")]
    public sealed class ContentAssetRegistry : ScriptableObject
    {
        [SerializeField] private List<PrefabAssetEntry> prefabs = new List<PrefabAssetEntry>();
        [SerializeField] private List<SpriteAssetEntry> sprites = new List<SpriteAssetEntry>();
        [SerializeField] private List<AudioAssetEntry> audioClips = new List<AudioAssetEntry>();

        /// <summary>Inspector 配置的预制体映射项。</summary>
        public IReadOnlyList<PrefabAssetEntry> Prefabs => prefabs;
        /// <summary>Inspector 配置的精灵映射项。</summary>
        public IReadOnlyList<SpriteAssetEntry> Sprites => sprites;
        /// <summary>Inspector 配置的音频映射项。</summary>
        public IReadOnlyList<AudioAssetEntry> AudioClips => audioClips;
    }

    /// <summary>
    /// 官方资源解析器，将 Registry 构建为按稳定 ID 查询的内存索引。
    /// </summary>
    public sealed class OfficialAssetResolver : IAssetResolver
    {
        private readonly Dictionary<string, GameObject> _prefabs =
            new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Sprite> _sprites =
            new Dictionary<string, Sprite>();
        private readonly Dictionary<string, AudioClip> _audio =
            new Dictionary<string, AudioClip>();

        /// <summary>从资源 Registry 创建官方资源解析器。</summary>
        /// <param name="registry">资源 Registry；为空时创建空索引。</param>
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

        /// <summary>按稳定 ID 获取预制体。</summary>
        /// <param name="id">预制体稳定标识。</param>
        /// <returns>找到的预制体；不存在时为 null。</returns>
        public GameObject GetPrefab(PrefabId id)
        {
            return _prefabs.TryGetValue(id.Value, out GameObject asset) ? asset : null;
        }

        /// <summary>按稳定 ID 获取精灵。</summary>
        /// <param name="id">精灵稳定标识。</param>
        /// <returns>找到的精灵；不存在时为 null。</returns>
        public Sprite GetSprite(SpriteId id)
        {
            return _sprites.TryGetValue(id.Value, out Sprite asset) ? asset : null;
        }

        /// <summary>按稳定 ID 获取音频片段。</summary>
        /// <param name="id">音频稳定标识。</param>
        /// <returns>找到的音频片段；不存在时为 null。</returns>
        public AudioClip GetAudio(AudioId id)
        {
            return _audio.TryGetValue(id.Value, out AudioClip asset) ? asset : null;
        }

        /// <summary>向资源索引添加唯一的非空映射项。</summary>
        /// <typeparam name="T">Unity 资源类型。</typeparam>
        /// <param name="target">目标索引。</param>
        /// <param name="id">资源稳定标识。</param>
        /// <param name="asset">资源对象。</param>
        private static void AddUnique<T>(Dictionary<string, T> target, string id, T asset)
        {
            if (string.IsNullOrWhiteSpace(id) || target.ContainsKey(id))
                return;

            target.Add(id, asset);
        }
    }
}
