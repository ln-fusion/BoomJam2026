#nullable enable
using Game.Foundation;
using UnityEngine;

namespace Game.Contracts
{
    /// <summary>
    /// 音频服务：音量应用与稳定音频 ID 播放，屏蔽全局 AudioSource 细节.
    /// </summary>
    /// <remarks>
    /// 对应技术设计文档 §12.2：设置层保存线性 [0,1] 数值，实现负责转换为分贝并处理 0 的静音值.
    /// </remarks>
    public interface IAudioService
    {
        /// <summary>应用三路音量（Master/Music/SFX）到 AudioMixer.</summary>
        /// <param name="master">主音量 0..1（线性）</param>
        /// <param name="music">音乐音量 0..1（线性）</param>
        /// <param name="sfx">音效音量 0..1（线性）</param>
        void ApplyVolumes(float master, float music, float sfx);

        /// <summary>播放指定音乐（带过渡）.</summary>
        /// <param name="musicId">稳定音乐 ID</param>
        /// <param name="transition">过渡方式</param>
        void PlayMusic(AudioId musicId, MusicTransition transition);

        /// <summary>停止当前音乐（带过渡）.</summary>
        /// <param name="transition">过渡方式</param>
        void StopMusic(MusicTransition transition);

        /// <summary>播放一次音效.</summary>
        /// <param name="sfxId">稳定音效 ID</param>
        void PlaySfx(AudioId sfxId);
    }

    /// <summary>
    /// 音乐过渡方式.
    /// </summary>
    public enum MusicTransition
    {
        None = 0,
        FadeIn,
        FadeOut,
        Crossfade,
    }

    /// <summary>
    /// 音频资源解析器：稳定 AudioId 到 AudioClip 引用.
    /// </summary>
    public interface IAudioAssetResolver
    {
        /// <summary>按稳定 ID 获取 AudioClip；未注册时返回 null.</summary>
        AudioClip? GetClip(AudioId audioId);
    }
}
