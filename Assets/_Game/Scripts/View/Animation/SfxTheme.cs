using UnityEngine;

namespace SuitMerge.View.Animation
{
    /// <summary>Tên các âm thanh của game (Animation phần 5). Âm của 4 chất vẫn nằm trong FxTheme như bước 1.4b.</summary>
    public enum SfxId
    {
        Pick, Place, Invalid, Rotate, Button,
        Merge, Create, Combo, Draw,
        Ace, GoalStep, GoalDone,
        PopupOpen, Win, Lose, Unlock
    }

    /// <summary>
    /// Bảng âm thanh (Animation phần 5): mỗi ô 1 file, ô trống = im lặng. Đổi âm chỉ cần thả file mới vào ô
    /// (Data/Theme/SfxTheme.asset) — không cần sửa code. Bản hiện tại là âm TẠM tạo bằng code (Tools/gen_audio.py).
    /// </summary>
    [CreateAssetMenu(menuName = "Suit Merge/Sfx Theme", fileName = "SfxTheme")]
    public class SfxTheme : ScriptableObject
    {
        [Header("Thao tác")]
        public AudioClip pick;
        public AudioClip place;
        [Tooltip("Thả không thành — lá bay về tay lắc 'không'.")]
        public AudioClip invalid;
        public AudioClip rotate;
        public AudioClip button;

        [Header("Lượt chơi")]
        public AudioClip merge;
        public AudioClip create;
        [Tooltip("COMBO ×k — phát cao dần theo k.")]
        public AudioClip combo;
        [Tooltip("Lá UP NEXT bay xuống tay.")]
        public AudioClip draw;

        [Header("Khoảnh khắc lớn")]
        public AudioClip ace;
        [Tooltip("Bản sao bay tới ô mục tiêu, số tăng.")]
        public AudioClip goalStep;
        public AudioClip goalDone;

        [Header("Popup / kết quả")]
        public AudioClip popupOpen;
        public AudioClip win;
        public AudioClip lose;
        public AudioClip unlock;

        [Header("Nhạc nền (lặp lại)")]
        public AudioClip music;

        [Header("Âm lượng")]
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float musicVolume = 0.35f;

        public AudioClip Get(SfxId id)
        {
            switch (id)
            {
                case SfxId.Pick: return pick;
                case SfxId.Place: return place;
                case SfxId.Invalid: return invalid;
                case SfxId.Rotate: return rotate;
                case SfxId.Button: return button;
                case SfxId.Merge: return merge;
                case SfxId.Create: return create;
                case SfxId.Combo: return combo;
                case SfxId.Draw: return draw;
                case SfxId.Ace: return ace;
                case SfxId.GoalStep: return goalStep;
                case SfxId.GoalDone: return goalDone;
                case SfxId.PopupOpen: return popupOpen;
                case SfxId.Win: return win;
                case SfxId.Lose: return lose;
                case SfxId.Unlock: return unlock;
                default: return null;
            }
        }
    }
}
