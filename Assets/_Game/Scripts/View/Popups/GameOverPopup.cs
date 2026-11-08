using System;
using SuitMerge.View.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Popup thua (bàn đầy): NO MORE SPACE! · SCORE · BEST · New best! · nút PLAY AGAIN.
    /// Nền tối mờ phủ toàn màn hình và chặn chạm xuống màn chơi.
    /// Khi hiện: nền mờ dần lên, khung bật nhẹ (0.85 → 1).
    /// Bước 4.5: khung Figma "HẾT CHỖ!" · ĐIỂM · KỶ LỤC · Kỷ lục mới! · CHƠI LẠI · VỀ MÀN CHÍNH (nút này không bắt buộc).
    /// Animation phần 1: dùng chung <see cref="PopupView"/> (hiện / đóng có animation) thay cho code hiện riêng trước đây.
    /// </summary>
    public class GameOverPopup : PopupView
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI scoreShadowText;
        [SerializeField] private TextMeshProUGUI bestText;
        [SerializeField] private GameObject newBestLabel;
        [SerializeField] private Button playAgainButton;
        [Header("Bước 4.5")]
        [SerializeField] private Button homeButton;

        /// <summary>Người chơi bấm PLAY AGAIN.</summary>
        public event Action PlayAgainClicked;
        /// <summary>Người chơi bấm VỀ MÀN CHÍNH.</summary>
        public event Action HomeClicked;

        /// <summary>Animation phần 4: popup thua trượt từ dưới lên.</summary>
        protected override bool SlideIn => true;

        private void Awake()
        {
            playAgainButton.onClick.AddListener(() => PlayAgainClicked?.Invoke());
            if (homeButton != null) homeButton.onClick.AddListener(() => HomeClicked?.Invoke());
        }

        public void Show(int score, int best, bool isNewBest)
        {
            string s = Loc.Number(score);
            scoreText.text = s;
            if (scoreShadowText != null) scoreShadowText.text = s;
            bestText.text = Loc.Number(best);
            newBestLabel.SetActive(isNewBest);
            Open();
        }
    }
}
