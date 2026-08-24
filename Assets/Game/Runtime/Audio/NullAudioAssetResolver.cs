#nullable enable
using Game.Contracts;
using Game.Foundation;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// 空音频资源解析器：占位期返回 null（C04 未注册稳定音频资源）.
    /// </summary>
    public sealed class NullAudioAssetResolver : IAudioAssetResolver
    {
        public AudioClip? GetClip(AudioId audioId) => null;
    }
}
