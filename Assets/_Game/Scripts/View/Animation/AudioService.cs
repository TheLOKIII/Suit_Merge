using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Phát âm thanh + nhạc nền (Animation phần 5). Tự tạo 1 GameObject "Audio" (giữ qua các scene) với 8 nguồn hiệu ứng
    /// xoay vòng + 1 nguồn nhạc lặp. Cài đặt ÂM THANH / NHẠC: BẬT / TẮT lưu bằng PlayerPrefs.
    /// <see cref="Theme"/> do EventPlayer gán lúc khởi động (scene Sandbox không có → chỉ âm 4 chất của FxTheme).
    /// </summary>
    public static class AudioService
    {
        private const string SfxKey = "sfx_on";
        private const string MusicKey = "music_on";
        private const int Voices = 8;
        /// <summary>Cùng 1 file không phát lại trong khoảng này (vd 6 lá rơi cùng lúc → 1 tiếng).</summary>
        private const float SameClipGap = 0.04f;
        private const float MusicFade = 0.6f;

        private static bool? _sfxOn, _musicOn;
        private static Host _host;

        /// <summary>Bảng âm thanh của game (null = chỉ phát được clip truyền thẳng).</summary>
        public static SfxTheme Theme { get; set; }

        /// <summary>Đổi BẬT / TẮT (popup Cài đặt nghe để đổi màu nút).</summary>
        public static event Action Changed;

        public static bool SfxOn => _sfxOn ??= PlayerPrefs.GetInt(SfxKey, 1) == 1;
        public static bool MusicOn => _musicOn ??= PlayerPrefs.GetInt(MusicKey, 1) == 1;

        public static void SetSfxOn(bool on)
        {
            if (SfxOn == on) return;
            _sfxOn = on;
            PlayerPrefs.SetInt(SfxKey, on ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void SetMusicOn(bool on)
        {
            if (MusicOn == on) return;
            _musicOn = on;
            PlayerPrefs.SetInt(MusicKey, on ? 1 : 0);
            PlayerPrefs.Save();
            if (_host != null) _host.ApplyMusic();
            Changed?.Invoke();
        }

        /// <summary>Phát âm <paramref name="id"/> của bảng âm. <paramref name="pitch"/> 1 = gốc; <paramref name="vary"/> = lệch ±4% cho đỡ lặp.</summary>
        public static void Play(SfxId id, float pitch = 1f, bool vary = true, float volume = 1f)
        {
            if (Theme == null) return;
            Play(Theme.Get(id), volume * Theme.sfxVolume, pitch, vary);
        }

        /// <summary>Phát thẳng 1 file (âm 4 chất trong FxTheme). Clip null / đang TẮT âm thanh = im lặng.</summary>
        public static void Play(AudioClip clip, float volume, float pitch = 1f, bool vary = false)
        {
            if (clip == null || volume <= 0f || !SfxOn) return;
            EnsureHost().PlayOneShot(clip, volume, vary ? pitch * UnityEngine.Random.Range(0.96f, 1.04f) : pitch);
        }

        /// <summary>Bắt đầu nhạc nền của bảng âm (lặp, mờ dần lên). Đang TẮT nhạc thì chỉ ghi nhớ.</summary>
        public static void PlayMusic()
        {
            if (Theme == null || Theme.music == null) return;
            EnsureHost().SetMusic(Theme.music, Theme.musicVolume);
        }

        private static Host EnsureHost()
        {
            if (_host != null) return _host;
            var go = new GameObject("Audio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<Host>();
            // Scene chưa có Audio Listener ("tai nghe") → Unity không phát gì cả. Scene dựng bằng menu cũ thiếu cái này
            // trên Main Camera → tự thêm 1 cái để luôn có tiếng (menu Cập nhật scene Game sẽ gắn hẳn vào camera).
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
            {
                go.AddComponent<AudioListener>();
                Debug.Log("[Suit Merge] Scene chưa có Audio Listener — đã tự thêm để phát âm thanh.");
            }
            return _host;
        }

        /// <summary>Nơi chứa các AudioSource (MonoBehaviour để chạy coroutine mờ nhạc).</summary>
        private sealed class Host : MonoBehaviour
        {
            private readonly List<AudioSource> _voices = new List<AudioSource>();
            private readonly Dictionary<AudioClip, float> _lastPlayed = new Dictionary<AudioClip, float>();
            private AudioSource _music;
            private float _musicVolume;
            private int _next;
            private Coroutine _fade;

            public void PlayOneShot(AudioClip clip, float volume, float pitch)
            {
                float now = Time.unscaledTime;
                if (_lastPlayed.TryGetValue(clip, out float last) && now - last < SameClipGap) return;
                _lastPlayed[clip] = now;
                if (_voices.Count < Voices)
                {
                    var s = gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    _voices.Add(s);
                }
                AudioSource src = _voices[_next % _voices.Count];
                _next++;
                src.Stop();
                src.clip = clip;
                src.pitch = pitch;
                src.volume = volume;
                src.Play();
            }

            public void SetMusic(AudioClip clip, float volume)
            {
                if (_music == null)
                {
                    _music = gameObject.AddComponent<AudioSource>();
                    _music.playOnAwake = false;
                    _music.loop = true;
                }
                _musicVolume = volume;
                if (_music.clip != clip)
                {
                    _music.clip = clip;
                    _music.volume = 0f;
                }
                ApplyMusic();
            }

            public void ApplyMusic()
            {
                if (_music == null || _music.clip == null) return;
                if (_fade != null) StopCoroutine(_fade);
                _fade = StartCoroutine(FadeMusic(MusicOn ? _musicVolume : 0f));
            }

            private IEnumerator FadeMusic(float to)
            {
                if (to > 0f && !_music.isPlaying) _music.Play();
                float from = _music.volume;
                yield return Tween.Run(MusicFade, k => _music.volume = Mathf.Lerp(from, to, k), null, unscaled: true);
                if (to <= 0f) _music.Pause();
                _fade = null;
            }
        }
    }
}
