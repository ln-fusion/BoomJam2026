#nullable enable
using System;
using Game.Contracts;
using Game.Foundation;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>
    /// 基于 AudioMixer 的音频服务：线性音量转 dB，处理 0 静音值.
    /// </summary>
    /// <remarks>
    /// 设置层保存线性 [0,1] 数值（技术设计文档 §12.2），本实现负责转分贝;
    /// BGM/SFX 播放通过稳定 AudioId 经 <see cref="IAudioAssetResolver"/> 解析.
    /// </remarks>
    public sealed class UnityAudioService : IAudioService
    {
        private static readonly string MasterParam = "MasterVolume";
        private static readonly string MusicParam = "MusicVolume";
        private static readonly string SfxParam = "SfxVolume";
        private const float MinDb = -80f;
        private const float MaxDb = 0f;

        private readonly AudioMixer? _mixer;
        private readonly IAudioAssetResolver _assetResolver;
        private readonly IGameLogger _logger;

        private AudioSource? _musicSource;
        private AudioSource? _sfxSource;

        /// <summary>音频服务是否已初始化.</summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// 构造函数：绑定 AudioMixer 与资源解析器.
        /// </summary>
        /// <param name="mixer">AudioMixer 资源（可 null，仅用 AudioListener/独立源）</param>
        /// <param name="assetResolver">稳定 AudioId -> AudioClip</param>
        /// <param name="logger">日志</param>
        public UnityAudioService(AudioMixer? mixer, IAudioAssetResolver assetResolver, IGameLogger? logger = null)
        {
            _mixer = mixer;
            _assetResolver = assetResolver ?? throw new ArgumentNullException(nameof(assetResolver));
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>应用三路音量到 AudioMixer（线性转 dB）.</summary>
        public void ApplyVolumes(float master, float music, float sfx)
        {
            ApplyMixerParam(MasterParam, master);
            ApplyMixerParam(MusicParam, music);
            ApplyMixerParam(SfxParam, sfx);
        }

        /// <summary>播放音乐：使用独立 AudioSource（循环）.</summary>
        public void PlayMusic(AudioId musicId, MusicTransition transition)
        {
            AudioClip? clip = _assetResolver.GetClip(musicId);
            if (clip == null)
            {
                _logger.LogWarning(LogContext.Empty, $"[Audio] 音乐未注册:{musicId}");
                return;
            }

            if (_musicSource == null)
            {
                var host = new GameObject("[Audio] Music");
                UnityEngine.Object.DontDestroyOnLoad(host);
                _musicSource = host.AddComponent<AudioSource>();
                _musicSource.loop = true;
                _musicSource.playOnAwake = false;
            }

            if (ReferenceEquals(_musicSource.clip, clip))
                return;

            _musicSource.clip = clip;
            _musicSource.volume = 1f;
            _musicSource.Play();
        }

        /// <summary>停止音乐.</summary>
        public void StopMusic(MusicTransition transition)
        {
            _musicSource?.Stop();
        }

        /// <summary>播放一次音效（不循环）.</summary>
        public void PlaySfx(AudioId sfxId)
        {
            AudioClip? clip = _assetResolver.GetClip(sfxId);
            if (clip == null)
            {
                _logger.LogWarning(LogContext.Empty, $"[Audio] 音效未注册:{sfxId}");
                return;
            }

            if (_sfxSource == null)
            {
                var host = new GameObject("[Audio] Sfx");
                UnityEngine.Object.DontDestroyOnLoad(host);
                _sfxSource = host.AddComponent<AudioSource>();
                _sfxSource.loop = false;
                _sfxSource.playOnAwake = false;
            }

            _sfxSource.PlayOneShot(clip);
        }

        /// <summary>初始化：创建常驻音频宿主（Editor 不可用）.</summary>
        public void Initialize()
        {
            if (IsReady)
                return;

            IsReady = true;
            ApplyVolumes(1f, 1f, 1f);
        }

        private void ApplyMixerParam(string param, float linearVolume)
        {
            if (_mixer == null)
                return;

            float db = linearVolume <= 0.0001f ? MinDb : Mathf.Lerp(MinDb, MaxDb, linearVolume);
            _mixer.SetFloat(param, db);
        }
    }
}
