using System;
using SuitMerge.View.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Popup tạm dừng (nút MENU): PAUSED · SCORE · BEST · RESUME / RESTART.
    /// Endless không có đồng hồ → tạm dừng = popup chặn chạm xuống màn chơi.
    /// Bước 4.5 (Figma popup-pause 178:517): TẠM DỪNG · TIẾP TỤC · CHƠI LẠI · CÀI ĐẶT · VỀ MÀN CHÍNH · nút X.
    /// Ô điểm / kỷ lục và các nút mới đều không bắt buộc (bỏ trống = không có).
    /// Animation phần 1: dùng chung <see cref="PopupView"/> (hiện / đóng có animation) thay cho code hiện riêng trước đây.
    /// </summary>
    public class PausePopup : PopupView
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI scoreShadowText;
        [SerializeField] private TextMeshProUGUI bestText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [Header("Bước 4.5")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button closeButton;

        public event Action ResumeClicked;
        public event Action RestartClicked;
        public event Action SettingsClicked;
        public event Action HomeClicked;

        private void Awake()
        {
            resumeButton.onClick.AddListener(() => ResumeClicked?.Invoke());
            restartButton.onClick.AddListener(() => RestartClicked?.Invoke());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => SettingsClicked?.Invoke());
            if (homeButton != null) homeButton.onClick.AddListener(() => HomeClicked?.Invoke());
            if (closeButton != null) closeButton.onClick.AddListener(() => ResumeClicked?.Invoke());
        }

        public void Show(int score, int best)
        {
            string s = Loc.Number(score);
            if (scoreText != null) scoreText.text = s;
            if (scoreShadowText != null) scoreShadowText.text = s;
            if (bestText != null) bestText.text = Loc.Number(best);
            Open();
        }
    }
}
