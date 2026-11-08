using SuitMerge.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Cards
{
    /// <summary>
    /// Lá nhỏ kiểu UP NEXT: nền xám 40.8×49.3 (Figma up-next 105:76),
    /// icon chất nhỏ ở góc trên-trái, rank ở góc dưới-phải (theo ảnh mẫu bạn gửi, bản sửa 1).
    /// Bố cục tính theo % kích thước nền nên co giãn được.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MiniCardView : MonoBehaviour
    {
        // Số đo đơn vị px trên nền 40.8×49.3
        private const float BaseWidth = 40.8f;
        private const float BaseHeight = 49.3f;
        private const float SuitLeft = 1.5f;       // mép trái ảnh chất (ảnh có lề trong 10%)
        private const float SuitTop = 4f;          // mép trên ảnh chất
        private const float SuitSize = 17f;        // phần hình thấy được ≈ 13.6px
        private const float RankTop = 14f;         // mép trên hộp chữ → chân số ở ≈ 42px
        private const float RankRight = 34.9f;     // mép phải hộp chữ → số cách mép phải lá ≈ 7px
        private const float RankFont = 25.5f;      // cỡ chữ (nhỏ hơn Figma 28.9 cho giống ảnh mẫu)

        [SerializeField] private CardTheme theme;
        [SerializeField] private Image background;
        [SerializeField] private Image suitIcon;
        [SerializeField] private TextMeshProUGUI rankText;

        /// <summary>Hiện lá; null = ẩn (hết lá).</summary>
        public void SetCard(Card card)
        {
            gameObject.SetActive(card != null);
            if (card == null) return;

            suitIcon.sprite = theme.GetSuitSprite(card.Suit);
            suitIcon.enabled = suitIcon.sprite != null;
            rankText.text = card.IsJoker ? "★" : card.Rank.ToLabel();
            rankText.color = theme.GetRankColor(card.Suit);
            ApplyLayout();
        }

        private void OnEnable() => ApplyLayout();

        private void OnRectTransformDimensionsChange() => ApplyLayout();

        /// <summary>Bố cục bằng anchor theo tỉ lệ; chỉ cỡ chữ tính theo chiều cao nền.</summary>
        public void ApplyLayout()
        {
            if (suitIcon == null || rankText == null) return;

            if (background != null) SetAnchors(background.rectTransform, 0f, 0f, 1f, 1f);

            // Icon chất: góc trên-trái
            SetAnchors(suitIcon.rectTransform,
                SuitLeft / BaseWidth, 1f - (SuitTop + SuitSize) / BaseHeight,
                (SuitLeft + SuitSize) / BaseWidth, 1f - SuitTop / BaseHeight);

            // Rank: hộp 1 dòng, mép trên ở 13.6px, căn phải tại 35.1px (hộp rộng = nền để "10" không bị cắt)
            float boxTop = 1f - RankTop / BaseHeight;
            float boxBottom = boxTop - RankFont * CardView.LineHeightEm / BaseHeight;
            float boxRight = RankRight / BaseWidth;
            SetAnchors(rankText.rectTransform, boxRight - 1f, boxBottom, boxRight, boxTop);
            rankText.alignment = TextAlignmentOptions.TopRight;
            rankText.textWrappingMode = TextWrappingModes.NoWrap;
            rankText.overflowMode = TextOverflowModes.Overflow;
            rankText.margin = Vector4.zero;

            float height = ((RectTransform)transform).rect.height;
            if (height > 0f) rankText.fontSize = RankFont / BaseHeight * height;
        }

        private static void SetAnchors(RectTransform t, float minX, float minY, float maxX, float maxY)
        {
            t.anchorMin = new Vector2(minX, minY);
            t.anchorMax = new Vector2(maxX, maxY);
            t.offsetMin = t.offsetMax = Vector2.zero;
        }
    }
}
