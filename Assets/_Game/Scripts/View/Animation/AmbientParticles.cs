using SuitMerge.Core.Model;
using SuitMerge.View.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Hạt nền (Animation phần 4): ~14 icon ♠♥♦♣ trắng mờ trôi lên rất chậm phía sau bàn, lắc nhẹ, xoay nhẹ;
    /// sinh dưới đáy, mờ dần ở đỉnh. Cài đặt GIẢM BỚT → tắt hết. Đặt trên 1 RectTransform phủ khung thiết kế,
    /// nằm dưới bàn / tay / HUD (menu Cập nhật scene Game dựng sẵn "Ambient").
    /// </summary>
    public class AmbientParticles : MonoBehaviour
    {
        [SerializeField] private CardTheme cardTheme;
        [SerializeField, Range(0, 40)] private int count = 14;
        [SerializeField, Range(0f, 1f)] private float alpha = 0.1f;
        [SerializeField] private Vector2 sizeRange = new Vector2(18f, 34f);
        [Tooltip("Tốc độ trôi lên (đơn vị UI / giây).")]
        [SerializeField] private Vector2 speedRange = new Vector2(14f, 30f);

        private static readonly Suit[] Suits = { Suit.Spade, Suit.Heart, Suit.Diamond, Suit.Club };

        private struct Particle
        {
            public RectTransform Rt;
            public Image Img;
            public float X, Y, Speed, Sway, SwayFreq, Phase, Spin;
        }

        private Particle[] _particles;
        private RectTransform _rt;

        private void OnEnable()
        {
            FxSettings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => FxSettings.Changed -= Refresh;

        private void Refresh()
        {
            bool on = !FxSettings.Reduced && cardTheme != null && count > 0;
            if (on && _particles == null) Build();
            if (_particles == null) return;
            foreach (Particle p in _particles)
                if (p.Rt != null) p.Rt.gameObject.SetActive(on);
        }

        private void Build()
        {
            _rt = (RectTransform)transform;
            _particles = new Particle[count];
            Rect r = _rt.rect;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Suit", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(_rt, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                float size = Random.Range(sizeRange.x, sizeRange.y);
                rt.sizeDelta = new Vector2(size, size);
                var img = go.AddComponent<Image>();
                img.sprite = cardTheme.GetSuitSprite(Suits[i % Suits.Length]);
                img.raycastTarget = false;
                img.preserveAspect = true;
                _particles[i] = new Particle
                {
                    Rt = rt, Img = img,
                    X = Random.Range(r.xMin, r.xMax),
                    Y = Random.Range(r.yMin, r.yMax), // lần đầu rải khắp màn, sau đó sinh lại dưới đáy
                    Speed = Random.Range(speedRange.x, speedRange.y),
                    Sway = Random.Range(6f, 16f),
                    SwayFreq = Random.Range(0.25f, 0.6f),
                    Phase = Random.Range(0f, 10f),
                    Spin = Random.Range(-15f, 15f)
                };
            }
        }

        private void Update()
        {
            if (_particles == null || FxSettings.Reduced) return;
            Rect r = _rt.rect;
            float dt = Time.deltaTime;
            for (int i = 0; i < _particles.Length; i++)
            {
                ref Particle p = ref _particles[i];
                if (p.Rt == null) continue;
                p.Y += p.Speed * dt;
                p.Phase += dt;
                if (p.Y > r.yMax + 20f)
                {
                    p.Y = r.yMin - 20f;
                    p.X = Random.Range(r.xMin, r.xMax);
                }
                p.Rt.anchoredPosition = new Vector2(p.X + Mathf.Sin(p.Phase * p.SwayFreq * 2f * Mathf.PI) * p.Sway, p.Y);
                p.Rt.localRotation = Quaternion.Euler(0f, 0f, p.Phase * p.Spin);
                // Hiện dần ở 10% dưới đáy, mờ dần ở 15% trên đỉnh
                float h = Mathf.InverseLerp(r.yMin, r.yMax, p.Y);
                float fade = Mathf.Clamp01(h / 0.1f) * Mathf.Clamp01((1f - h) / 0.15f);
                p.Img.color = new Color(1f, 1f, 1f, alpha * fade);
            }
        }
    }
}
