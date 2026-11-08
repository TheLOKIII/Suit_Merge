using SuitMerge.Core.Model;
using UnityEngine;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Bộ hiệu ứng chất (bước 1.4b): ảnh FX trắng (Unity tô màu), màu từng chất, âm thanh.
    /// Đổi màu / thay ảnh / gắn âm thanh chỉ cần sửa asset Data/Theme/FxTheme.asset.
    /// Ô âm thanh để trống = im lặng.
    /// </summary>
    [CreateAssetMenu(menuName = "Suit Merge/Fx Theme", fileName = "FxTheme")]
    public class FxTheme : ScriptableObject
    {
        [Header("Ảnh FX (trắng, nền trong suốt)")]
        public Sprite ring;
        public Sprite glow;
        public Sprite spark;
        [Tooltip("Chữ nhật bo góc 9-slice — chớp màu khung bàn.")]
        public Sprite flash;
        [Tooltip("Animation phần 3: 12 tia sáng toả từ tâm (màn Ace, popup MỞ KHÓA).")]
        public Sprite rays;
        [Tooltip("Animation phần 3: viền phát sáng phía trong khung bo góc, 9-slice (cảnh báo bàn sắp đầy).")]
        public Sprite edgeGlow;

        [Header("Overlay ô khi kéo (Figma cell-overlay 177:410, bước 4.5)")]
        [Tooltip("Ô đang trỏ tới, thả được.")]
        public Sprite overlayValid;
        [Tooltip("Lá sẽ merge với lá đang kéo.")]
        public Sprite overlayMerge;
        [Tooltip("Vùng ♠ sẽ bị dọn.")]
        public Sprite overlaySpade;
        [Tooltip("Ô ♣ sẽ sinh lá.")]
        public Sprite overlayClub;
        [Tooltip("Ô gợi ý của lượt tutorial.")]
        public Sprite overlayHint;
        [Tooltip("Ô đang trỏ tới, KHÔNG thả được.")]
        public Sprite overlayInvalid;

        [Header("Nền badge (Figma fx-badge 177:716, 9-slice)")]
        public Sprite badgeDiamond;
        public Sprite badgeHeart;
        public Sprite badgeClub;
        public Sprite badgeSpade;

        [Header("Khối đôi (Figma hand-pair 177:354, rotate-arrow-glass 180:616 — M4.5)")]
        [Tooltip("Khung tay khối ngang 252×132.")]
        public Sprite handPairH;
        [Tooltip("Khung tay khối dọc 132×252.")]
        public Sprite handPairV;
        [Tooltip("Mũi tên cong xoay 160×40 (chỉ sang phải).")]
        public Sprite rotateArrow;

        [Header("Màu từng chất")]
        public Color diamondColor = new Color32(0xF6, 0xD4, 0x77, 0xFF);
        public Color heartColor = new Color32(0xFF, 0x6B, 0x9A, 0xFF);
        public Color clubColor = new Color32(0x4F, 0xA3, 0xF7, 0xFF);
        public Color spadeColor = new Color32(0xD8, 0x48, 0x3F, 0xFF);

        [Header("Âm thanh (để trống = im lặng)")]
        [Tooltip("Lúc icon chất to đóng dấu (mọi chất).")]
        public AudioClip stamp;
        public AudioClip diamond;
        [Tooltip("Mỗi nấc nhảy rank của ♥ (cao dần theo nấc).")]
        public AudioClip heartStep;
        public AudioClip club;
        public AudioClip spade;
        [Range(0f, 1f)] public float volume = 0.8f;

        public Color GetColor(Suit suit)
        {
            switch (suit)
            {
                case Suit.Diamond: return diamondColor;
                case Suit.Heart: return heartColor;
                case Suit.Club: return clubColor;
                case Suit.Spade: return spadeColor;
                default: return Color.white;
            }
        }

        public AudioClip GetClip(Suit suit)
        {
            switch (suit)
            {
                case Suit.Diamond: return diamond;
                case Suit.Club: return club;
                case Suit.Spade: return spade;
                default: return null; // ♥ phát theo từng nấc (heartStep)
            }
        }
    }
}
