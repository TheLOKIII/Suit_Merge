using System;
using System.Collections;
using UnityEngine;

namespace SuitMerge.View.Animation
{
    /// <summary>Hàm easing: nhận t ∈ [0,1], trả tiến độ (OutBack / OutElastic / InBack có thể vượt khỏi [0,1] một chút để tạo độ nảy).</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float InCubic(float t) => t * t * t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

        public static float OutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Lùi nhẹ (âm) rồi lao tới 1 — dùng cho vật "lấy đà" trước khi bay đi (Animation phần 1).</summary>
        public static float InBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        /// <summary>Vượt quá 1 rồi dao động tắt dần quanh 1 — kiểu "lò xo" (Animation phần 1).</summary>
        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }
    }

    /// <summary>
    /// Tween tự viết bằng coroutine (không dùng thư viện ngoài).
    /// <c>unscaled = true</c>: chạy theo giờ thực, không bị khựng hình / tua nhanh (Time.timeScale) ảnh hưởng — dùng cho nút, popup.
    /// </summary>
    public static class Tween
    {
        /// <summary>Chạy <paramref name="step"/> từ 0 → 1 trong <paramref name="duration"/> giây; luôn kết thúc đúng ở 1.</summary>
        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null, bool unscaled = false)
        {
            ease ??= Ease.Linear;
            if (duration <= 0f)
            {
                step(ease(1f));
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                step(ease(t / duration));
                yield return null;
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            step(ease(1f));
        }

        public static IEnumerator Move(Transform target, Vector3 fromWorld, Vector3 toWorld, float duration,
            Func<float, float> ease = null) =>
            Run(duration, k => { if (target != null) target.position = Vector3.LerpUnclamped(fromWorld, toWorld, k); }, ease);

        public static IEnumerator Scale(Transform target, Vector3 from, Vector3 to, float duration,
            Func<float, float> ease = null, bool unscaled = false) =>
            Run(duration, k => { if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, k); }, ease, unscaled);

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration,
            Func<float, float> ease = null, bool unscaled = false) =>
            Run(duration, k => { if (group != null) group.alpha = Mathf.LerpUnclamped(from, to, k); }, ease, unscaled);

        /// <summary>
        /// Bay theo đường cong Bézier bậc 2 từ <paramref name="fromWorld"/> qua điểm kéo <paramref name="controlWorld"/>
        /// tới <paramref name="toWorld"/> (vật không chạm điểm kéo, chỉ bị "hút" về phía nó).
        /// </summary>
        public static IEnumerator Curve(Transform target, Vector3 fromWorld, Vector3 controlWorld, Vector3 toWorld, float duration,
            Func<float, float> ease = null) =>
            Run(duration, k => { if (target != null) target.position = Bezier(fromWorld, controlWorld, toWorld, k); }, ease);

        /// <summary>Điểm trên đường cong Bézier bậc 2 ở tiến độ <paramref name="t"/>.</summary>
        public static Vector3 Bezier(Vector3 a, Vector3 control, Vector3 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }

        /// <summary>
        /// Nảy: phóng to thêm <paramref name="amount"/> (vd 0.2 = +20%) rồi dao động tắt dần về đúng cỡ <paramref name="baseScale"/>.
        /// </summary>
        public static IEnumerator Punch(Transform target, Vector3 baseScale, float amount, float duration,
            int vibrato = 2, bool unscaled = false) =>
            Run(duration, k =>
            {
                if (target == null) return;
                float wave = Mathf.Sin(k * Mathf.PI * (vibrato * 2 - 1) * 0.5f) * (1f - k);
                target.localScale = baseScale * (1f + amount * wave);
            }, null, unscaled);

        /// <summary>
        /// Lắc ngang quanh vị trí <paramref name="baseAnchored"/> (kiểu lắc đầu "không"), biên độ giảm dần, kết thúc đúng chỗ cũ.
        /// </summary>
        public static IEnumerator Shake(RectTransform target, Vector2 baseAnchored, float amplitude, float duration,
            int swings = 3, bool unscaled = false) =>
            Run(duration, k =>
            {
                if (target == null) return;
                float x = Mathf.Sin(k * Mathf.PI * 2f * swings) * amplitude * (1f - k);
                target.anchoredPosition = baseAnchored + new Vector2(x, 0f);
            }, null, unscaled);

        public static IEnumerator Delay(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
        }
    }
}
