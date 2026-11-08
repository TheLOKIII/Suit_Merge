using System;
using System.Collections;
using System.Collections.Generic;
using SuitMerge.Core.Goals;
using SuitMerge.Core.Model;
using SuitMerge.View.Animation;
using SuitMerge.View.Hud;
using SuitMerge.View.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Thua level (Figma popup-game-over 178:468): HẾT LÁ! / BÀN ĐẦY! · "Còn thiếu một chút!" · tiến độ mục tiêu ·
    /// CHƠI LẠI (tạm thay chỗ "+5 LÁ" cho tới M7) · BỎ CUỘC.
    /// </summary>
    public class LevelFailPopup : PopupView
    {
        [SerializeField] private UiTheme theme;
        [SerializeField] private TextMeshProUGUI titleText;
        [Tooltip("Hàng chứa các mục tiêu chưa xong.")]
        [SerializeField] private RectTransform goalsRoot;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button giveUpButton;

        public event Action RetryClicked;
        public event Action GiveUpClicked;

        private readonly List<GoalItemView> _items = new List<GoalItemView>();

        /// <summary>Animation phần 4: popup thua trượt từ dưới lên.</summary>
        protected override bool SlideIn => true;

        /// <summary>Animation phần 4: hiện xong → các mục tiêu CHƯA đạt lắc nhẹ ("còn thiếu").</summary>
        protected override void OnShown()
        {
            foreach (GoalItemView item in _items)
                if (item != null && !item.IsDone) StartCoroutine(Shake((RectTransform)item.transform));
        }

        private static IEnumerator Shake(RectTransform rt)
        {
            Vector3 home = rt.localPosition;
            yield return Tween.Run(0.35f, k =>
            {
                if (rt != null) rt.localPosition = home + new Vector3(Mathf.Sin(k * Mathf.PI * 6f) * 6f * (1f - k), 0f, 0f);
            }, null, unscaled: true);
            if (rt != null) rt.localPosition = home;
        }

        private void Awake()
        {
            retryButton.onClick.AddListener(() => RetryClicked?.Invoke());
            giveUpButton.onClick.AddListener(() => GiveUpClicked?.Invoke());
        }

        public void Show(LoseReason reason, IReadOnlyList<GoalProgress> goals)
        {
            titleText.text = Loc.Get(reason == LoseReason.BoardFull ? "fail.board_full"
                : reason == LoseReason.NoSpaceForPair ? "fail.no_space_pair" : "fail.out_of_cards");
            foreach (GoalItemView item in _items)
                if (item != null) Destroy(item.gameObject);
            _items.Clear();
            foreach (GoalProgress g in goals)
            {
                GoalItemView item = GoalItemView.Create(goalsRoot, theme, g.Goal, GoalItemView.Style.Popup);
                item.SetProgress(g.Current, animate: false);
                _items.Add(item);
            }
            Open();
        }
    }
}
