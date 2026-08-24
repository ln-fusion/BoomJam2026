using System.Collections.Generic;
using UnityEngine;

namespace SoundManager
{
    /// <summary>
    /// 声音类型：音乐或音效。
    /// </summary>
    public enum SoundType
    {
        /// <summary>背景音乐。</summary>
        Music,
        /// <summary>一次性音效。</summary>
        SFX,
    }

    /// <summary>
    /// 单个声音条目，供 SoundManager 在 Inspector 中配置。
    /// </summary>
    [System.Serializable]
    public class SoundItem
    {
        /// <summary>声音名称，调用时用该名称查找。</summary>
        public string name;

        /// <summary>声音类型。</summary>
        public SoundType type;

        /// <summary>音频资源。</summary>
        public AudioClip clip;

        /// <summary>独立音量。</summary>
        [Range(0f, 1f)]
        public float volume = 1f;

        /// <summary>音调偏移。</summary>
        [Range(-3f, 3f)]
        public float pitch = 1f;

        /// <summary>是否循环播放（仅对 SFX 生效，Music 始终循环）。</summary>
        public bool loop;
    }

    /// <summary>
    /// 简单的全局声音管理器，负责背景音乐和音效播放。
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        /// <summary>全局单例实例。</summary>
        public static SoundManager Instance { get; private set; }

        private AudioSource musicSource;
        private AudioSource sfxSource;

        /// <summary>主音量。</summary>
        [Header("Volume")]
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        /// <summary>音乐音量倍率。</summary>
        [Range(0f, 1f)]
        public float musicVolume = 1f;

        /// <summary>音效音量倍率。</summary>
        [Range(0f, 1f)]
        public float sfxVolume = 1f;

        /// <summary>声音库。</summary>
        [Header("Sound Library")]
        public List<SoundItem> sounds = new List<SoundItem>();

        private SoundItem currentMusic;

        /// <summary>初始化单例和音频源。</summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            musicSource = gameObject.AddComponent<AudioSource>();
            sfxSource = gameObject.AddComponent<AudioSource>();

            ApplyMusicVolume();
        }

        /// <summary>通过名称播放音乐或音效。</summary>
        /// <param name="name">声音名称。</param>
        public void Play(string name)
        {
            SoundItem sound = sounds.Find(s => s.name == name);
            if (sound == null || sound.clip == null)
                return;

            AudioSource source = sound.type == SoundType.Music ? musicSource : sfxSource;

            source.clip = sound.clip;
            source.pitch = sound.pitch;
            source.loop = sound.type == SoundType.Music ? true : sound.loop;

            if (sound.type == SoundType.Music)
            {
                currentMusic = sound;
                source.volume = GetEffectiveVolume(sound);
                source.Play();
            }
            else
            {
                if (sfxSource.isPlaying)
                {
                    sfxSource.Stop();
                }

                sfxSource.clip = sound.clip;
                sfxSource.pitch = sound.pitch;
                sfxSource.loop = sound.loop;
                sfxSource.volume = GetEffectiveVolume(sound);
                sfxSource.Play();
            }
        }

        /// <summary>
        /// 播放或覆盖当前正在播放的音效。
        /// </summary>
        /// <param name="name">声音名称；当当前音效为 DoorOpen 且请求并非 DoorOpen 时会忽略本次请求。</param>
        public void PlaySFXReplace(string name)
        {
            if (sfxSource.isPlaying)
            {
                if ((sfxSource.clip.name == "DoorOpen") && (name != "DoorOpen"))
                {
                    return;
                }
            }

            SoundItem sound = sounds.Find(s => s.name == name);
            if (sound == null || sound.clip == null)
                return;

            if (sound.type != SoundType.SFX)
            {
                return;
            }

            if (sfxSource.isPlaying)
            {
                sfxSource.Stop();
            }

            sfxSource.clip = sound.clip;
            sfxSource.pitch = sound.pitch;
            sfxSource.loop = sound.loop;
            sfxSource.volume = GetEffectiveVolume(sound);
            sfxSource.Play();
        }

        /// <summary>停止所有声音。</summary>
        public void StopAll()
        {
            musicSource.Stop();
            sfxSource.Stop();
        }

        /// <summary>计算最终音量。</summary>
        /// <param name="sound">声音条目。</param>
        /// <returns>独立音量、类型音量和主音量的乘积。</returns>
        private float GetEffectiveVolume(SoundItem sound)
        {
            if (sound == null)
                return 0f;

            float typeVolume = sound.type == SoundType.Music ? musicVolume : sfxVolume;
            return sound.volume * masterVolume * typeVolume;
        }

        /// <summary>刷新当前音乐的音量。</summary>
        private void ApplyMusicVolume()
        {
            if (musicSource == null || currentMusic == null)
                return;

            musicSource.volume = GetEffectiveVolume(currentMusic);
        }

        /// <summary>编辑器模式下修改参数时刷新音量。</summary>
        private void OnValidate()
        {
            ApplyMusicVolume();
        }
    }
}
