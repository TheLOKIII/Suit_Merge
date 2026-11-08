using SuitMerge.Core.Model;
using SuitMerge.View.Cards;
using TMPro;
using UnityEngine;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Bộ giao diện bước 4.5 (Data/Theme/UiTheme.asset): font, prefab lá, icon mục tiêu, nút Figma (btn 178:385).
    /// HUD Level và các popup dùng chung — đổi ảnh / màu ở 1 chỗ.
    /// </summary>
    [CreateAssetMenu(menuName = "Suit Merge/Ui Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        [Header("Chung")]
        public TMP_FontAsset font;
        [Tooltip("Lá bài dùng làm icon mục tiêu \"Tạo lá X\".")]
        public CardView cardPrefab;
        [Tooltip("Mặt lá trơn — nền icon mục tiêu Ace.")]
        public Sprite cardBlank;

        [Header("Icon mục tiêu (goal-icon 177:846)")]
        public Sprite goalScore;
        public Sprite goalCheck;
        public Sprite goalDiamond;
        public Sprite goalClub;
        public Sprite goalSpade;
        public Sprite goalHeart;

        [Header("Nút (9-slice)")]
        public Sprite buttonTeal;
        public Sprite buttonPink;
        public Sprite buttonGold;
        public Sprite buttonPurple;

        // Màu chữ theo token Figma
        public static readonly Color TextDark = new Color32(0x2A, 0x2A, 0x2A, 0xFF);
        public static readonly Color TextMuted = new Color32(0x8A, 0x6A, 0x9C, 0xFF);
        public static readonly Color TextTeal = new Color32(0x37, 0xB9, 0xA4, 0xFF);
        public static readonly Color TextPink = new Color32(0xED, 0x4C, 0x8C, 0xFF);
        public static readonly Color TextGold = new Color32(0xFA, 0xCC, 0x4D, 0xFF);
        public static readonly Color TextCoin = new Color32(0xB8, 0x86, 0x2F, 0xFF);
        public static readonly Color TextRed = new Color32(0xC6, 0x2E, 0x27, 0xFF);
        public static readonly Color TextAcePurple = new Color32(0x6B, 0x1F, 0x94, 0xFF);
        public static readonly Color TextWhite70 = new Color(1f, 1f, 1f, 0.7f);
        public static readonly Color HudPanel = new Color32(0x4A, 0x1C, 0x66, 0xFF);
        public static readonly Color LevelPill = new Color32(0xED, 0x4D, 0x8C, 0xFF);

        public Sprite SuitCircle(Suit suit)
        {
            switch (suit)
            {
                case Suit.Diamond: return goalDiamond;
                case Suit.Club: return goalClub;
                case Suit.Spade: return goalSpade;
                case Suit.Heart: return goalHeart;
                default: return goalScore;
            }
        }
    }
}
