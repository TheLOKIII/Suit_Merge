using System;
using UnityEngine;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Công tắc Cài đặt → HIỆU ỨNG: ĐẦY ĐỦ / GIẢM BỚT (Animation phần 1). Lựa chọn được lưu (PlayerPrefs) khi tắt game.
    /// GIẢM BỚT: không rung màn hình, không khựng hình, chớp khung bàn còn 1/3, hạt còn một nửa.
    /// Hiệu ứng mạnh làm sau (pháo giấy, tia sáng, hạt nền) cũng phải hỏi <see cref="Reduced"/>.
    /// </summary>
    public static class FxSettings
    {
        private const string PrefKey = "fx_reduced";

        /// <summary>Hệ số chớp khung bàn khi GIẢM BỚT.</summary>
        public const float ReducedFlash = 1f / 3f;
        /// <summary>Hệ số số lượng hạt khi GIẢM BỚT.</summary>
        public const float ReducedParticles = 0.5f;

        private static bool? _reduced;

        /// <summary>Đổi công tắc (kể cả từ chỗ khác ngoài popup Cài đặt).</summary>
        public static event Action Changed;

        public static bool Reduced
        {
            get
            {
                if (_reduced == null) _reduced = PlayerPrefs.GetInt(PrefKey, 0) == 1;
                return _reduced.Value;
            }
        }

        public static void SetReduced(bool reduced)
        {
            if (Reduced == reduced) return;
            _reduced = reduced;
            PlayerPrefs.SetInt(PrefKey, reduced ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
