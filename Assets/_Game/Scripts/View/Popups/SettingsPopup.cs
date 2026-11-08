using System;
using SuitMerge.Core.Localization;
using SuitMerge.View.Animation;
using SuitMerge.View.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Cài đặt TẠM (bước 4.5, M7 làm Cài đặt thật): chỉ chọn ngôn ngữ Tiếng Việt / English.
    /// Nút đang chọn màu teal, nút còn lại màu tím. Ngôn ngữ được lưu khi tắt game.
    /// Animation phần 1: thêm hàng HIỆU ỨNG — ĐẦY ĐỦ / GIẢM BỚT (<see cref="FxSettings"/>), cùng kiểu 2 nút. Bỏ trống = không có hàng này.
    /// Animation phần 5: thêm hàng ÂM THANH và NHẠC — BẬT / TẮT (<see cref="AudioService"/>). Bỏ trống = không có hàng.
    /// </summary>
    public class SettingsPopup : PopupView
    {
        [SerializeField] private UiTheme theme;
        [SerializeField] private Button vietnameseButton;
        [SerializeField] private Button englishButton;
        [SerializeField] private Button closeButton;
        [Header("Animation phần 1")]
        [SerializeField] private Button fxFullButton;
        [SerializeField] private Button fxReducedButton;
        [Header("Animation phần 5")]
        [SerializeField] private Button sfxOnButton;
        [SerializeField] private Button sfxOffButton;
        [SerializeField] private Button musicOnButton;
        [SerializeField] private Button musicOffButton;

        public event Action Closed;

        private void Awake()
        {
            vietnameseButton.onClick.AddListener(() => Pick(StringTable.Vietnamese));
            englishButton.onClick.AddListener(() => Pick(StringTable.English));
            if (fxFullButton != null) fxFullButton.onClick.AddListener(() => PickFx(false));
            if (fxReducedButton != null) fxReducedButton.onClick.AddListener(() => PickFx(true));
            if (sfxOnButton != null) sfxOnButton.onClick.AddListener(() => AudioService.SetSfxOn(true));
            if (sfxOffButton != null) sfxOffButton.onClick.AddListener(() => AudioService.SetSfxOn(false));
            if (musicOnButton != null) musicOnButton.onClick.AddListener(() => AudioService.SetMusicOn(true));
            if (musicOffButton != null) musicOffButton.onClick.AddListener(() => AudioService.SetMusicOn(false));
            closeButton.onClick.AddListener(() =>
            {
                Hide();
                Closed?.Invoke();
            });
            AudioService.Changed += Refresh;
        }

        private void OnDestroy() => AudioService.Changed -= Refresh;

        public void Show()
        {
            Refresh();
            Open();
        }

        private void Pick(string language)
        {
            Loc.SetLanguage(language);
            Refresh();
        }

        private void PickFx(bool reduced)
        {
            FxSettings.SetReduced(reduced);
            Refresh();
        }

        private void Refresh()
        {
            if (theme == null) return;
            bool en = Loc.IsEnglish;
            vietnameseButton.image.sprite = en ? theme.buttonPurple : theme.buttonTeal;
            englishButton.image.sprite = en ? theme.buttonTeal : theme.buttonPurple;
            bool reduced = FxSettings.Reduced;
            if (fxFullButton != null) fxFullButton.image.sprite = reduced ? theme.buttonPurple : theme.buttonTeal;
            if (fxReducedButton != null) fxReducedButton.image.sprite = reduced ? theme.buttonTeal : theme.buttonPurple;
            Pair(sfxOnButton, sfxOffButton, AudioService.SfxOn);
            Pair(musicOnButton, musicOffButton, AudioService.MusicOn);
        }

        /// <summary>Cặp nút BẬT / TẮT: nút đang chọn màu teal, nút còn lại màu tím.</summary>
        private void Pair(Button on, Button off, bool isOn)
        {
            if (on != null) on.image.sprite = isOn ? theme.buttonTeal : theme.buttonPurple;
            if (off != null) off.image.sprite = isOn ? theme.buttonPurple : theme.buttonTeal;
        }
    }
}
