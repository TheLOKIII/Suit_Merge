using System.Collections;
using SuitMerge.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Cards
{
    /// <summary>
    /// Hiển thị 1 lá bài vuông. Bố cục thống nhất (theo lá 9♠ đang cầm trong Figma), tính theo % cạnh lá
    /// nên dùng được ở mọi cỡ (84px trên bàn, 112px trên tay):
    /// - Icon chất: cách trái/trên 7.5%, cỡ 42.5%
    /// - Rank: cỡ chữ 64% cạnh lá, căn phải cách mép 8%, hộp chữ bắt đầu ở 18% từ trên
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardView : MonoBehaviour
    {
        public const float SuitInset = 0.075f;
        public const float SuitSize = 0.425f;
        public const float RankFontSize = 0.64f;
        public const float RankRightMargin = 0.08f;
        public const float RankBoxTop = 0.18f;
        /// <summary>Chiều cao dòng "normal" của Baloo 2 = 1.6 × cỡ chữ (giống Figma).</summary>
        public const float LineHeightEm = 1.6f;
        /// <summary>Bóng đổ: ảnh 224 cho lá 200, dịch xuống 6/112 cạnh lá.</summary>
        public const float ShadowScale = 224f / 200f;
        public const float ShadowOffset = 6f / 112f;

        [SerializeField] private CardTheme theme;
        [SerializeField] private Image shadow;
        [SerializeField] private Image face;
        [SerializeField] private Image suitIcon;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private bool showShadow;

        private Card _card;

        public Card Card => _card;
        public CardTheme Theme => theme;

        /// <summary>Hiện lá <paramref name="card"/>; null = ẩn lá.</summary>
        public void SetCard(Card card)
        {
            _card = card;
            gameObject.SetActive(card != null);
            if (card == null) return;

            suitIcon.sprite = theme.GetSuitSprite(card.Suit);
            suitIcon.enabled = suitIcon.sprite != null;
            rankText.text = card.IsJoker ? "★" : card.Rank.ToLabel();
            rankText.color = theme.GetRankColor(card.Suit);
            ApplyLayout();
        }

        /// <summary>Cho mặt lá nhận chạm/kéo (chỉ lá trên tay cần; lá trên bàn để false).</summary>
        public void SetRaycastTarget(bool value)
        {
            if (face != null) face.raycastTarget = value;
        }

        public void SetShadowVisible(bool visible)
        {
            showShadow = visible;
            if (shadow != null) shadow.gameObject.SetActive(visible);
        }

        // ---------- Nhấc lá (Animation phần 2): bóng dịch xa hơn + nhạt bớt → lá như đang bay cao khỏi bàn ----------
        /// <summary>Bóng dịch thêm tối đa 8/112 cạnh lá khi nhấc hẳn (k = 1).</summary>
        private const float LiftShadowOffset = 8f / 112f;
        private const float LiftShadowFade = 0.3f;
        private float _shadowAlpha = -1f;

        /// <summary>Độ nhấc <paramref name="k"/> ∈ [0,1]: 0 = nằm trên bàn (bóng như cũ), 1 = nhấc cao.</summary>
        public void SetLift(float k)
        {
            if (shadow == null) return;
            if (_shadowAlpha < 0f) _shadowAlpha = shadow.color.a;
            float size = ((RectTransform)transform).rect.height;
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -LiftShadowOffset * size * k);
            Color c = shadow.color;
            c.a = _shadowAlpha * (1f - LiftShadowFade * k);
            shadow.color = c;
        }

        // ---------- Icon chất sáng lên (bước 1.4: lá đang kéo sẽ kích hoạt hiệu ứng chất — spec 10.3) ----------
        private static readonly Color SuitGlowColor = new Color32(0xF6, 0xD4, 0x77, 0xFF);
        private const float SuitGlowScale = 1.5f;   // nền vàng to hơn icon 50%
        private const float SuitPulseSpeed = 8f;
        private const float SuitPulseScale = 1.15f;

        private Image _suitGlow;
        private Coroutine _suitPulse;

        /// <summary>Bật/tắt hiệu ứng "icon chất sáng lên": nền vàng nhấp nháy sau icon chất + icon phập phồng.</summary>
        public void SetSuitHighlight(bool on)
        {
            if (on && _suitGlow == null && theme != null && suitIcon != null)
            {
                // Ảnh mặt lá (kem) tô vàng → ô vàng bo góc sau icon (tint là phép nhân màu nên phải dùng ảnh sáng)
                var go = new GameObject("SuitGlow", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(suitIcon.transform.parent, false);
                rt.SetSiblingIndex(suitIcon.transform.GetSiblingIndex()); // ngay dưới icon
                _suitGlow = go.AddComponent<Image>();
                _suitGlow.sprite = theme.cardFace;
                _suitGlow.color = SuitGlowColor;
                _suitGlow.raycastTarget = false;
                LayoutSuitGlow();
            }

            if (_suitPulse != null) StopCoroutine(_suitPulse);
            _suitPulse = null;
            if (suitIcon != null) suitIcon.rectTransform.localScale = Vector3.one;
            if (_suitGlow == null) return;
            _suitGlow.gameObject.SetActive(on);
            if (on && isActiveAndEnabled) _suitPulse = StartCoroutine(PulseSuit());
        }

        private IEnumerator PulseSuit()
        {
            float t = 0f;
            while (true)
            {
                float k = (Mathf.Sin(t * SuitPulseSpeed) + 1f) / 2f;
                Color c = SuitGlowColor;
                c.a = Mathf.Lerp(0.55f, 1f, k);
                _suitGlow.color = c;
                float scale = Mathf.Lerp(1f, SuitPulseScale, k);
                suitIcon.rectTransform.localScale = Vector3.one * scale;
                _suitGlow.rectTransform.localScale = Vector3.one * scale;
                t += Time.deltaTime;
                yield return null;
            }
        }

        private void LayoutSuitGlow()
        {
            if (_suitGlow == null) return;
            float grow = SuitSize * (SuitGlowScale - 1f) / 2f;
            SetAnchors(_suitGlow.rectTransform,
                SuitInset - grow, 1f - SuitInset - SuitSize - grow,
                SuitInset + SuitSize + grow, 1f - SuitInset + grow);
        }

        private void OnDisable()
        {
            // Lá bị ẩn / tắt giữa chừng → không để icon kẹt ở cỡ phóng to
            _suitPulse = null;
            if (suitIcon != null) suitIcon.rectTransform.localScale = Vector3.one;
            if (_suitGlow != null) _suitGlow.gameObject.SetActive(false);
        }

        private void OnEnable() => ApplyLayout();

        private void OnRectTransformDimensionsChange() => ApplyLayout();

        /// <summary>
        /// Đặt bố cục bằng anchor theo tỉ lệ (tự co giãn theo cỡ lá); chỉ cỡ chữ phải tính theo cạnh lá.
        /// </summary>
        public void ApplyLayout()
        {
            if (rankText == null || suitIcon == null) return;

            if (face != null) SetAnchors(face.rectTransform, 0f, 0f, 1f, 1f);

            // Icon chất: góc trên-trái, cách 7.5%, cỡ 42.5%
            SetAnchors(suitIcon.rectTransform,
                SuitInset, 1f - SuitInset - SuitSize,
                SuitInset + SuitSize, 1f - SuitInset);
            LayoutSuitGlow();

            // Rank: hộp 1 dòng (cao 1.6 × cỡ chữ), mép trên ở 18%, mép phải ở 92%, rộng = cạnh lá
            float boxTop = 1f - RankBoxTop;
            float boxBottom = boxTop - RankFontSize * LineHeightEm;
            float boxRight = 1f - RankRightMargin;
            SetAnchors(rankText.rectTransform, boxRight - 1f, boxBottom, boxRight, boxTop);
            rankText.alignment = TextAlignmentOptions.TopRight;
            rankText.textWrappingMode = TextWrappingModes.NoWrap;
            rankText.overflowMode = TextOverflowModes.Overflow;
            rankText.margin = Vector4.zero;

            float size = ((RectTransform)transform).rect.height;
            if (size > 0f) rankText.fontSize = RankFontSize * size;

            if (shadow != null)
            {
                // Ảnh bóng to 1.12 × lá, dịch xuống 6/112 cạnh lá
                float half = (ShadowScale - 1f) / 2f;
                SetAnchors(shadow.rectTransform,
                    -half, -half - ShadowOffset,
                    1f + half, 1f + half - ShadowOffset);
                shadow.gameObject.SetActive(showShadow);
            }
        }

        private static void SetAnchors(RectTransform t, float minX, float minY, float maxX, float maxY)
        {
            t.anchorMin = new Vector2(minX, minY);
            t.anchorMax = new Vector2(maxX, maxY);
            t.offsetMin = t.offsetMax = Vector2.zero;
        }
    }
}
