using System;
using Game.Contracts;
using Game.Contracts.Content;
using Game.Foundation;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Presentation
{
    /// <summary>
    /// Unity 音频适配器：将线性设置转换为 AudioMixer 的 Master/Music/SFX 暴露参数。
    /// </summary>
    public sealed class UnityAudioService : IAudioService
    {
        private readonly AudioMixer _mixer;
        private readonly IAssetResolver _assetResolver;
        private readonly AudioSource _musicSource;
        private readonly AudioSource _sfxSource;
        private readonly string _masterParameter;
        private readonly string _musicParameter;
        private readonly string _sfxParameter;

        /// <summary>当前线性主音量。</summary>
        public float MasterVolume { get; private set; } = 1f;
        /// <summary>当前线性音乐音量。</summary>
        public float MusicVolume { get; private set; } = 1f;
        /// <summary>当前线性音效音量。</summary>
        public float SfxVolume { get; private set; } = 1f;

        /// <summary>
        /// 创建 Unity 音频服务；缺少 Mixer、Registry 或 AudioSource 时仍可安全应用设置。
        /// </summary>
        /// <param name="mixer">可选 AudioMixer。</param>
        /// <param name="assetResolver">稳定音频资源解析器。</param>
        /// <param name="musicSource">音乐 AudioSource。</param>
        /// <param name="sfxSource">音效 AudioSource。</param>
        /// <param name="masterParameter">主音量暴露参数名。</param>
        /// <param name="musicParameter">音乐音量暴露参数名。</param>
        /// <param name="sfxParameter">音效音量暴露参数名。</param>
        public UnityAudioService(AudioMixer mixer, IAssetResolver assetResolver,
            AudioSource musicSource, AudioSource sfxSource,
            string masterParameter = "MasterVolume",
            string musicParameter = "MusicVolume",
            string sfxParameter = "SfxVolume")
        {
            _mixer = mixer;
            _assetResolver = assetResolver;
            _musicSource = musicSource;
            _sfxSource = sfxSource;
            _masterParameter = string.IsNullOrWhiteSpace(masterParameter)
                ? "MasterVolume" : masterParameter;
            _musicParameter = string.IsNullOrWhiteSpace(musicParameter)
                ? "MusicVolume" : musicParameter;
            _sfxParameter = string.IsNullOrWhiteSpace(sfxParameter)
                ? "SfxVolume" : sfxParameter;
        }

        /// <summary>把线性音量写入三个 Mixer 暴露参数。</summary>
        /// <param name="master">主音量，范围为 0 到 1。</param>
        /// <param name="music">音乐音量，范围为 0 到 1。</param>
        /// <param name="sfx">音效音量，范围为 0 到 1。</param>
        public void ApplyVolumes(float master, float music, float sfx)
        {
            MasterVolume = ClampUnit(master);
            MusicVolume = ClampUnit(music);
            SfxVolume = ClampUnit(sfx);
            SetMixerVolume(_masterParameter, MasterVolume);
            SetMixerVolume(_musicParameter, MusicVolume);
            SetMixerVolume(_sfxParameter, SfxVolume);
            RefreshSourceVolumes();
        }

        /// <summary>播放稳定 ID 指向的音乐。</summary>
        /// <param name="musicId">音乐稳定 ID。</param>
        /// <param name="transition">切换方式；当前占位实现统一立即切换。</param>
        public void PlayMusic(MusicId musicId, MusicTransition transition)
        {
            if (_musicSource == null || _assetResolver == null || musicId == null)
                return;

            AudioClip clip = _assetResolver.GetAudio(new AudioId(musicId.Value));
            if (clip == null)
                return;

            _musicSource.clip = clip;
            _musicSource.loop = true;
            _musicSource.Play();
        }

        /// <summary>停止当前音乐。</summary>
        /// <param name="transition">停止方式；当前占位实现立即停止。</param>
        public void StopMusic(MusicTransition transition)
        {
            _musicSource?.Stop();
        }

        /// <summary>播放稳定 ID 指向的音效。</summary>
        /// <param name="sfxId">音效稳定 ID。</param>
        public void PlaySfx(SfxId sfxId)
        {
            if (_sfxSource == null || _assetResolver == null || sfxId == null)
                return;

            AudioClip clip = _assetResolver.GetAudio(new AudioId(sfxId.Value));
            if (clip == null)
                return;

            _sfxSource.PlayOneShot(clip);
        }

        /// <summary>把线性音量转换成 Mixer 分贝值。</summary>
        /// <param name="linear">线性音量。</param>
        /// <returns>分贝值；静音使用 -80dB。</returns>
        public static float ToDecibels(float linear)
        {
            float value = ClampUnit(linear);
            return value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
        }

        /// <summary>限制线性音量到合法范围。</summary>
        /// <param name="value">输入音量。</param>
        /// <returns>0 到 1 范围内的音量。</returns>
        private static float ClampUnit(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        }

        /// <summary>尝试写入一个 Mixer 暴露参数。</summary>
        /// <param name="parameter">参数名。</param>
        /// <param name="linear">线性值。</param>
        private void SetMixerVolume(string parameter, float linear)
        {
            if (_mixer != null)
                _mixer.SetFloat(parameter, ToDecibels(linear));
        }

        /// <summary>当没有 Mixer 时刷新 AudioSource 的可听音量。</summary>
        private void RefreshSourceVolumes()
        {
            if (_mixer != null)
                return;

            if (_musicSource != null)
                _musicSource.volume = MasterVolume * MusicVolume;
            if (_sfxSource != null)
                _sfxSource.volume = MasterVolume * SfxVolume;
        }
    }

    /// <summary>Unity 窗口设置适配器。</summary>
    public sealed class UnityWindowSettingsApplier : IWindowSettingsApplier
    {
        /// <summary>应用分辨率和全屏状态。</summary>
        /// <param name="width">窗口宽度。</param>
        /// <param name="height">窗口高度。</param>
        /// <param name="fullscreen">是否全屏。</param>
        /// <returns>应用成功或参数非法结果。</returns>
        public Result Apply(int width, int height, bool fullscreen)
        {
            if (width <= 0 || height <= 0)
                return Result.Failure(ErrorCode.SettingsInvalid, "Resolution must be positive.");

            Screen.SetResolution(width, height, fullscreen);
            return Result.Success();
        }
    }
}
