using System.Collections;
using System.Collections.Generic;
using SuitMerge.Core.Model;
using SuitMerge.View.Animation;
using SuitMerge.View.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Board
{
    /// <summary>Lớp phủ màu trên ô (bước 1.4).</summary>
    public enum CellZone
    {
        None,
        /// <summary>♠: ô trong vùng 3×3 sẽ bị dọn — đỏ.</summary>
        Danger,
        /// <summary>♣: ô sẽ sinh lá — xanh dương.</summary>
        Spawn
    }

    /// <summary>
    /// Một ô bàn: nền (trống / khóa) + 1 lá bài (có thể ẩn)
    /// + lớp sáng khi lá đang lơ lửng phía trên + quầng sáng vàng khi lá trong ô sắp merge.
    /// Bước 4.5: có FxTheme (ảnh overlay Figma 177:410) thì dùng overlay Figma — Valid / MergeGlow / SpadeClear /
    /// ClubSpawn / Hint / Invalid, nằm TRÊN lá; chưa có ảnh thì giữ cách vẽ cũ (bước 2.5 / 1.4).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CellView : MonoBehaviour
    {
        private static readonly Color HighlightColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color GlowColor = new Color32(0xF6, 0xD4, 0x77, 0xFF);
        private const float GlowScale = 224f / 200f; // ảnh bóng lá to hơn lá 12%
        private const float GlowPulseSpeed = 6f;
        private const float GlowMinAlpha = 0.45f;
        private const float GlowMaxAlpha = 0.95f;

        [SerializeField] private Image background;
        [SerializeField] private Sprite emptySprite;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private CardView cardView;

        // Bước 1.4 — lớp phủ vùng hiệu ứng chất (nằm TRÊN lá để thấy được cả khi ô có lá)
        private static readonly Color DangerColor = new Color32(0xD8, 0x48, 0x3F, 0xFF); // token đỏ (nút Trash)
        private static readonly Color SpawnColor = new Color32(0x4F, 0xA3, 0xF7, 0xFF);  // xanh dương
        private const float ZoneMinAlpha = 0.35f;
        private const float ZoneMaxAlpha = 0.7f;

        private Image _highlight;
        private Image _zone;
        private CellZone _zoneKind;
        private Coroutine _zoneRoutine;
        private Image _glow;
        private Coroutine _glowRoutine;

        // Bước 4.5 — overlay Figma
        private enum Overlay { Valid, Merge, Spade, Club, Hint, Invalid }
        private const float FigmaOverlayScale = 240f / 200f; // ảnh 240 chừa chỗ cho quầng sáng; ô = vùng 200 ở giữa
        private const float FigmaPulseMin = 0.55f;
        private FxTheme _fxTheme;
        private readonly Dictionary<Overlay, Image> _figma = new Dictionary<Overlay, Image>();
        private readonly Dictionary<Overlay, Coroutine> _figmaPulse = new Dictionary<Overlay, Coroutine>();

        public CellPos Pos { get; private set; }
        public CardView CardView => cardView;
        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>Toạ độ thế giới của tâm ô.</summary>
        public Vector3 CenterWorld => RectTransform.TransformPoint(RectTransform.rect.center);

        public void Init(CellPos pos)
        {
            Pos = pos;
            name = $"Cell {pos.Row},{pos.Col}";
        }

        /// <summary>Ảnh overlay Figma (null = dùng cách vẽ cũ).</summary>
        public void SetFxTheme(FxTheme theme) => _fxTheme = theme;

        /// <summary>Vẽ ô theo dữ liệu Core: ô không tồn tại → khóa; có lá → hiện lá.</summary>
        public void Render(Cell cell)
        {
            bool locked = cell == null || !cell.Exists;
            background.sprite = locked ? lockedSprite : emptySprite;
            ShowCard(locked ? null : cell.Card);
        }

        /// <summary>Hiện lá (null = ẩn) mà không cần dữ liệu Core — dùng khi phát animation.</summary>
        public void ShowCard(Card card)
        {
            cardView.SetCard(card);
            cardView.transform.localScale = Vector3.one;
        }

        /// <summary>Bật/tắt lớp sáng mờ (ô trống mà lá đang kéo nằm phía trên).</summary>
        public void SetHighlight(bool on)
        {
            if (!on) StopBump();
            if (SetFigma(Overlay.Valid, on, pulse: false)) return;
            // Dùng ảnh mặt lá (kem) phủ trắng 25% → ô sáng lên thật.
            // (Bản 2.4 dùng ảnh ô tím: tím × trắng vẫn là tím → gần như không thấy sáng.)
            if (on && _highlight == null)
            {
                Sprite sprite = cardView.Theme != null ? cardView.Theme.cardFace : emptySprite;
                _highlight = CreateOverlay("Highlight", sprite, HighlightColor, 1f);
            }
            if (_highlight != null) _highlight.gameObject.SetActive(on);
        }

        /// <summary>Bật/tắt quầng sáng vàng nhấp nháy quanh lá (lá sẽ merge nếu thả lá đang kéo).</summary>
        public void SetGlow(bool on)
        {
            if (SetFigma(Overlay.Merge, on, pulse: true)) return;
            if (on && _glow == null)
            {
                // Ảnh mặt lá (kem) tô vàng, to hơn lá 12% → viền vàng ấm quanh lá.
                // (Không dùng ảnh bóng: ảnh bóng màu đen, tô màu kiểu nhân thì vẫn ra đen.)
                Sprite sprite = cardView.Theme != null ? cardView.Theme.cardFace : null;
                _glow = CreateOverlay("Glow", sprite, GlowColor, GlowScale);
            }
            if (_glow == null) return;

            if (_glowRoutine != null) StopCoroutine(_glowRoutine);
            _glowRoutine = null;
            _glow.gameObject.SetActive(on);
            if (on && isActiveAndEnabled) _glowRoutine = StartCoroutine(PulseGlow());
        }

        private IEnumerator PulseGlow()
        {
            float t = 0f;
            while (true)
            {
                float k = (Mathf.Sin(t * GlowPulseSpeed) + 1f) / 2f;
                Color c = GlowColor;
                c.a = Mathf.Lerp(GlowMinAlpha, GlowMaxAlpha, k);
                _glow.color = c;
                t += Time.deltaTime;
                yield return null;
            }
        }

        public CellZone Zone => _zoneKind;

        /// <summary>
        /// Phủ màu lên ô (đè lên cả lá): Danger = đỏ, Spawn = xanh dương, None = tắt.
        /// <paramref name="pulse"/> = nhấp nháy (xem trước khi kéo); false = màu đứng (lúc phát animation).
        /// </summary>
        public void SetZone(CellZone zone, bool pulse = true)
        {
            if (FigmaSprite(Overlay.Spade) != null && FigmaSprite(Overlay.Club) != null)
            {
                _zoneKind = zone;
                SetFigma(Overlay.Spade, zone == CellZone.Danger, pulse);
                SetFigma(Overlay.Club, zone == CellZone.Spawn, pulse);
                return;
            }
            if (zone != CellZone.None && _zone == null)
            {
                Sprite sprite = cardView.Theme != null ? cardView.Theme.cardFace : emptySprite;
                _zone = CreateOverlay("Zone", sprite, Color.white, 1f);
            }
            _zoneKind = zone;
            if (_zone == null) return;

            if (_zoneRoutine != null) StopCoroutine(_zoneRoutine);
            _zoneRoutine = null;
            _zone.transform.SetAsLastSibling(); // trên lá bài
            _zone.gameObject.SetActive(zone != CellZone.None);
            if (zone == CellZone.None) return;

            Color c = zone == CellZone.Danger ? DangerColor : SpawnColor;
            c.a = ZoneMaxAlpha;
            _zone.color = c;
            if (pulse && isActiveAndEnabled) _zoneRoutine = StartCoroutine(PulseZone(c));
        }

        /// <summary>Độ đậm lớp phủ (0..1) — EventPlayer dùng để làm mờ dần.</summary>
        public void SetZoneAlpha(float alpha)
        {
            if (FigmaSprite(Overlay.Spade) != null)
            {
                foreach (Overlay o in new[] { Overlay.Spade, Overlay.Club })
                    if (_figma.TryGetValue(o, out Image img) && img.gameObject.activeSelf)
                    {
                        StopPulse(o);
                        img.color = new Color(1f, 1f, 1f, alpha);
                    }
                return;
            }
            if (_zone == null || !_zone.gameObject.activeSelf) return;
            Color c = _zone.color;
            c.a = alpha * ZoneMaxAlpha;
            _zone.color = c;
        }

        private IEnumerator PulseZone(Color baseColor)
        {
            float t = 0f;
            while (true)
            {
                float k = (Mathf.Sin(t * GlowPulseSpeed) + 1f) / 2f;
                Color c = baseColor;
                c.a = Mathf.Lerp(ZoneMinAlpha, ZoneMaxAlpha, k);
                _zone.color = c;
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Bước 4.5: viền vàng nét đứt nhấp nháy — ô gợi ý của lượt tutorial (chỉ có khi FxTheme có ảnh).</summary>
        public void SetHint(bool on) => SetFigma(Overlay.Hint, on, pulse: true);

        // Animation phần 2 — ô vừa được trỏ vào nảy 1,1 → 1
        private const float BumpScale = 1.1f;
        private const float BumpDuration = 0.16f;
        private Coroutine _bump;
        private Transform _bumped;

        /// <summary>Lớp sáng của ô (đang bật) nảy 1,1 → 1 — gọi khi lá đang kéo vừa trỏ vào ô này.</summary>
        public void Bump()
        {
            Transform target = null;
            if (_figma.TryGetValue(Overlay.Valid, out Image img) && img != null && img.gameObject.activeSelf) target = img.transform;
            else if (_highlight != null && _highlight.gameObject.activeSelf) target = _highlight.transform;
            if (target == null || !isActiveAndEnabled) return;
            StopBump();
            _bumped = target;
            _bump = StartCoroutine(BumpRoutine(target));
        }

        private IEnumerator BumpRoutine(Transform target)
        {
            yield return Tween.Run(BumpDuration, k =>
            {
                if (target != null) target.localScale = Vector3.one * Mathf.LerpUnclamped(BumpScale, 1f, Ease.OutBack(k));
            });
            _bump = null;
        }

        private void StopBump()
        {
            if (_bump != null) StopCoroutine(_bump);
            _bump = null;
            if (_bumped != null) _bumped.localScale = Vector3.one;
            _bumped = null;
        }

        /// <summary>Bước 4.5: ô đang trỏ tới nhưng KHÔNG thả được (ô có lá / ô khóa / không phải ô gợi ý).</summary>
        public void SetInvalid(bool on) => SetFigma(Overlay.Invalid, on, pulse: false);

        private Sprite FigmaSprite(Overlay o)
        {
            if (_fxTheme == null) return null;
            switch (o)
            {
                case Overlay.Valid: return _fxTheme.overlayValid;
                case Overlay.Merge: return _fxTheme.overlayMerge;
                case Overlay.Spade: return _fxTheme.overlaySpade;
                case Overlay.Club: return _fxTheme.overlayClub;
                case Overlay.Hint: return _fxTheme.overlayHint;
                default: return _fxTheme.overlayInvalid;
            }
        }

        /// <summary>Bật/tắt 1 overlay Figma. Trả false nếu chưa có ảnh (để dùng cách vẽ cũ).</summary>
        private bool SetFigma(Overlay o, bool on, bool pulse)
        {
            Sprite sprite = FigmaSprite(o);
            if (sprite == null) return false;
            _figma.TryGetValue(o, out Image img);
            if (on && img == null)
            {
                img = CreateOverlay("Overlay" + o, sprite, Color.white, FigmaOverlayScale);
                _figma[o] = img;
            }
            if (img == null) return true;

            StopPulse(o);
            img.gameObject.SetActive(on);
            if (!on) return true;
            img.transform.SetAsLastSibling(); // trên lá bài
            img.color = Color.white;
            if (pulse && isActiveAndEnabled) _figmaPulse[o] = StartCoroutine(PulseAlpha(img));
            return true;
        }

        private void StopPulse(Overlay o)
        {
            if (_figmaPulse.TryGetValue(o, out Coroutine c) && c != null) StopCoroutine(c);
            _figmaPulse.Remove(o);
        }

        private IEnumerator PulseAlpha(Image img)
        {
            float t = 0f;
            while (img != null)
            {
                float k = (Mathf.Sin(t * GlowPulseSpeed) + 1f) / 2f;
                img.color = new Color(1f, 1f, 1f, Mathf.Lerp(FigmaPulseMin, 1f, k));
                t += Time.deltaTime;
                yield return null;
            }
        }

        private void OnDisable()
        {
            // Coroutine dừng khi ô bị tắt → xoá sổ để lần bật sau tạo lại
            _figmaPulse.Clear();
        }

        /// <summary>Lớp phủ nằm trên nền ô, dưới lá bài.</summary>
        private Image CreateOverlay(string overlayName, Sprite sprite, Color color, float scale)
        {
            var go = new GameObject(overlayName, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.SetSiblingIndex(cardView.transform.GetSiblingIndex()); // ngay dưới lá bài
            float half = (scale - 1f) / 2f;
            rt.anchorMin = new Vector2(-half, -half);
            rt.anchorMax = new Vector2(1f + half, 1f + half);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
