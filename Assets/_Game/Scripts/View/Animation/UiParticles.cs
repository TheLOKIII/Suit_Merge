using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Hạt / vòng sóng / quầng sáng vẽ bằng Image UI trên lớp Fx (bước 1.4b).
    /// Không dùng ParticleSystem vì nó không vẽ đúng thứ tự trong Canvas UI.
    /// Mọi Image sinh ra được theo dõi để <see cref="Clear"/> dọn sạch khi huỷ animation.
    /// </summary>
    public sealed class UiParticles
    {
        private readonly MonoBehaviour _host;
        private readonly RectTransform _layer;
        private readonly List<GameObject> _alive = new List<GameObject>();

        public UiParticles(MonoBehaviour host, RectTransform layer)
        {
            _host = host;
            _layer = layer;
        }

        /// <summary>Thông số một loạt hạt bung ra từ 1 điểm.</summary>
        public struct BurstSettings
        {
            public int Count;
            public float SizeMin, SizeMax;
            public float SpeedMin, SpeedMax;   // đơn vị UI / giây
            public float Gravity;              // > 0 rơi xuống, < 0 bay lên
            public float Lifetime;
            public float Spin;                 // độ / giây (ngẫu nhiên ±)
            /// <summary>Góc giữa (độ, 90 = lên trên) và độ toả (360 = mọi hướng).</summary>
            public float Direction, Spread;
            /// <summary>Animation phần 3: cao / rộng của mỗi hạt (0 = vuông) — vd mảnh pháo giấy chữ nhật.</summary>
            public float Aspect;
            /// <summary>Animation phần 3: mỗi hạt lấy ngẫu nhiên 1 màu trong bảng này (null = dùng màu chung).</summary>
            public Color[] Palette;
        }

        /// <summary>Ảnh UI mới trên lớp Fx, tâm tại <paramref name="world"/>.</summary>
        public Image Spawn(Sprite sprite, Vector3 world, float size, Color color, string name = "Fx")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = _layer.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_layer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.position = world;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            _alive.Add(go);
            return img;
        }

        public void Despawn(GameObject go)
        {
            _alive.Remove(go);
            if (go != null) Object.Destroy(go);
        }

        /// <summary>Huỷ mọi hạt còn sống (khi chơi lại giữa chừng animation).</summary>
        public void Clear()
        {
            foreach (GameObject go in _alive)
                if (go != null) Object.Destroy(go);
            _alive.Clear();
        }

        /// <summary>Bung <see cref="BurstSettings.Count"/> hạt từ <paramref name="world"/>. Không chặn (tự chạy).</summary>
        public void Burst(Sprite sprite, Vector3 world, Color color, BurstSettings s)
        {
            if (sprite == null || s.Count <= 0) return;
            _host.StartCoroutine(BurstRoutine(sprite, world, color, s));
        }

        private IEnumerator BurstRoutine(Sprite sprite, Vector3 world, Color color, BurstSettings s)
        {
            int n = s.Count;
            var images = new Image[n];
            var velocity = new Vector2[n];
            var spin = new float[n];
            var baseScale = new float[n];
            var colors = new Color[n];
            for (int i = 0; i < n; i++)
            {
                float size = Random.Range(s.SizeMin, s.SizeMax);
                colors[i] = s.Palette != null && s.Palette.Length > 0 ? s.Palette[Random.Range(0, s.Palette.Length)] : color;
                images[i] = Spawn(sprite, world, size, colors[i], "Particle");
                if (s.Aspect > 0f) images[i].rectTransform.sizeDelta = new Vector2(size, size * s.Aspect);
                images[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
                float angle = (s.Direction + Random.Range(-s.Spread, s.Spread) / 2f) * Mathf.Deg2Rad;
                velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(s.SpeedMin, s.SpeedMax);
                spin[i] = Random.Range(-s.Spin, s.Spin);
                baseScale[i] = 1f;
            }

            float t = 0f;
            while (t < s.Lifetime)
            {
                float dt = Time.deltaTime;
                t += dt;
                float k = Mathf.Clamp01(t / s.Lifetime);
                for (int i = 0; i < n; i++)
                {
                    if (images[i] == null) continue;
                    velocity[i].y -= s.Gravity * dt;
                    velocity[i] *= 1f - 1.8f * dt; // lực cản nhẹ → bung nhanh rồi chậm lại
                    RectTransform rt = images[i].rectTransform;
                    rt.localPosition += (Vector3)(velocity[i] * dt);
                    rt.localEulerAngles += new Vector3(0f, 0f, spin[i] * dt);
                    rt.localScale = Vector3.one * (baseScale[i] * (k < 0.15f ? Mathf.Lerp(0.3f, 1f, k / 0.15f) : 1f - 0.5f * k));
                    Color c = colors[i];
                    c.a = colors[i].a * (k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                    images[i].color = c;
                }
                yield return null;
            }
            foreach (Image img in images)
                if (img != null) Despawn(img.gameObject);
        }

        /// <summary>Vòng sóng nở từ <paramref name="fromSize"/> tới <paramref name="toSize"/> rồi tắt. Trả coroutine để chờ nếu cần.</summary>
        public IEnumerator Ring(Sprite sprite, Vector3 world, Color color, float fromSize, float toSize, float duration)
        {
            if (sprite == null) yield break;
            Image img = Spawn(sprite, world, fromSize, color, "Ring");
            RectTransform rt = img.rectTransform;
            yield return Tween.Run(duration, k =>
            {
                if (rt == null) return;
                float size = Mathf.Lerp(fromSize, toSize, Ease.OutCubic(k));
                rt.sizeDelta = new Vector2(size, size);
                Color c = color;
                c.a = color.a * (1f - k * k);
                img.color = c;
            });
            Despawn(img.gameObject);
        }

        /// <summary>Quầng / mảng màu hiện lên rồi mờ dần (alpha đỉnh → 0). Kích thước cố định.</summary>
        public IEnumerator FlashImage(Image img, Color color, float duration)
        {
            if (img == null) yield break;
            yield return Tween.Run(duration, k =>
            {
                if (img == null) return;
                Color c = color;
                c.a = color.a * (1f - k);
                img.color = c;
            }, Ease.OutCubic);
            Despawn(img.gameObject);
        }
    }
}
