using UnityEngine;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Phát âm thanh hiệu ứng (bước 1.4b). Clip null = im lặng (chưa có file âm thanh).
    /// Animation phần 5: phát qua <see cref="AudioService"/> → tôn trọng Cài đặt ÂM THANH: TẮT và giới hạn số âm cùng lúc.
    /// </summary>
    public sealed class SfxPlayer
    {
        public SfxPlayer(GameObject owner)
        {
        }

        public void Play(AudioClip clip, float volume, float pitch = 1f) => AudioService.Play(clip, volume, pitch);
    }
}
