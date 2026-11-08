using System.Collections;
using UnityEngine;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Rung khung thiết kế (bước 1.4b): lệch ngẫu nhiên, giảm dần về 0, luôn trả đúng vị trí gốc.
    /// Gọi <see cref="Stop"/> khi huỷ animation giữa chừng. Cài đặt GIẢM BỚT (<see cref="FxSettings"/>) → không rung.
    /// </summary>
    public sealed class ScreenShake
    {
        private readonly MonoBehaviour _host;
        private readonly RectTransform _target;
        private Vector2 _origin;
        private Coroutine _routine;

        public ScreenShake(MonoBehaviour host, RectTransform target)
        {
            _host = host;
            _target = target;
        }

        /// <summary>Rung biên độ <paramref name="amplitude"/> (đơn vị UI) trong <paramref name="duration"/> giây.</summary>
        public void Shake(float amplitude, float duration)
        {
            if (_target == null || amplitude <= 0f || duration <= 0f || !_host.isActiveAndEnabled) return;
            if (FxSettings.Reduced) return; // Cài đặt HIỆU ỨNG: GIẢM BỚT → không rung (Animation phần 1)
            if (_routine == null) _origin = _target.anchoredPosition;
            else _host.StopCoroutine(_routine);
            _routine = _host.StartCoroutine(Run(amplitude, duration));
        }

        public void Stop()
        {
            if (_routine == null) return;
            _host.StopCoroutine(_routine);
            _routine = null;
            if (_target != null) _target.anchoredPosition = _origin;
        }

        private IEnumerator Run(float amplitude, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                float falloff = 1f - t / duration;
                _target.anchoredPosition = _origin + Random.insideUnitCircle * (amplitude * falloff * falloff);
                yield return null;
                t += Time.deltaTime;
            }
            _target.anchoredPosition = _origin;
            _routine = null;
        }
    }
}
