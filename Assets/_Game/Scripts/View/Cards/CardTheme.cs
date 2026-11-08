using SuitMerge.Core.Model;
using TMPro;
using UnityEngine;

namespace SuitMerge.View.Cards
{
    /// <summary>
    /// Giao diện lá bài: ảnh mặt lá, bóng, 4 icon chất, font, màu chữ rank.
    /// Đổi giao diện lá (skin) chỉ cần sửa / thay asset này.
    /// </summary>
    [CreateAssetMenu(menuName = "Suit Merge/Card Theme", fileName = "CardTheme")]
    public class CardTheme : ScriptableObject
    {
        [Header("Ảnh")]
        public Sprite cardFace;
        public Sprite cardShadow;
        public Sprite heart;
        public Sprite diamond;
        public Sprite club;
        public Sprite spade;

        [Header("Chữ")]
        public TMP_FontAsset font;
        public Color redRankColor = new Color32(0xC6, 0x2E, 0x27, 0xFF);
        public Color blackRankColor = new Color32(0x2A, 0x2A, 0x2A, 0xFF);

        public Sprite GetSuitSprite(Suit suit)
        {
            switch (suit)
            {
                case Suit.Heart: return heart;
                case Suit.Diamond: return diamond;
                case Suit.Club: return club;
                case Suit.Spade: return spade;
                default: return null;
            }
        }

        public Color GetRankColor(Suit suit) => suit.IsRed() ? redRankColor : blackRankColor;
    }
}
