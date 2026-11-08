using System;
using System.Collections;
using SuitMerge.View.Animation;
using SuitMerge.View.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Thắng level (Figma popup-level-complete 178:444): LEVEL N · HOÀN THÀNH! · ĐIỂM · coin thưởng · TIẾP TỤC.
    /// Nút "NHẬN ×2" (quảng cáo) chưa có tới M7. Coin chỉ HIỂN THỊ, chưa cộng ví (M7).
    /// Animation phần 4: hiện xong → ĐIỂM đếm từ 0 lên (0,6s) → coin đếm +0 → +N rồi nảy → nút TIẾP TỤC nhún nhẹ mời bấm
    /// (dừng khi người chơi nhấn). Chạy theo giờ thực.
    /// </summary>
    public class LevelCompletePopup : PopupView
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private Button continueButton;

        public event Action ContinueClicked;

        private const float ScoreCountDuration = 0.6f;
        private const float CoinCountDuration = 0.4f;
        private const float InvitePeriod = 1.2f;
        private const float InviteScale = 1.06f;

        private int _score, _coins;
        private Coroutine _count, _invite;
        private ButtonPress _press;

        private void Awake()
        {
            continueButton.onClick.AddListener(() => ContinueClicked?.Invoke());
            _press = continueButton.GetComponent<ButtonPress>();
            if (_press != null) _press.Pressed += StopInvite;
        }

        private void OnDestroy()
        {
            if (_press != null) _press.Pressed -= StopInvite;
        }

        public void Show(int levelId, int score, int coins)
        {
            _score = score;
            _coins = coins;
            titleText.text = Loc.Format("complete.title", levelId);
            scoreText.text = Loc.Number(0);
            rewardText.text = Loc.Format("complete.reward", 0);
            StopInvite();
            if (_count != null) StopCoroutine(_count);
            _count = null;
            Open();
        }

        protected override void OnShown() => _count = StartCoroutine(CountUp());

        private IEnumerator CountUp()
        {
            yield return Tween.Run(ScoreCountDuration, k => scoreText.text = Loc.Number(Mathf.RoundToInt(_score * k)), Ease.OutCubic, unscaled: true);
            scoreText.text = Loc.Number(_score);
            yield return Tween.Run(CoinCountDuration, k => rewardText.text = Loc.Format("complete.reward", Mathf.RoundToInt(_coins * k)), Ease.OutQuad, unscaled: true);
            rewardText.text = Loc.Format("complete.reward", _coins);
            Transform coinRow = rewardText.transform.parent != null ? rewardText.transform.parent : rewardText.transform;
            yield return Tween.Punch(coinRow, Vector3.one, 0.25f, 0.3f, unscaled: true);
            coinRow.localScale = Vector3.one;
            _count = null;
            _invite = StartCoroutine(Invite());
        }

        /// <summary>Nút TIẾP TỤC phập phồng nhẹ mời bấm.</summary>
        private IEnumerator Invite()
        {
            Transform t = continueButton.transform;
            float time = 0f;
            while (true)
            {
                float p = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * time / InvitePeriod);
                t.localScale = Vector3.one * Mathf.Lerp(1f, InviteScale, p);
                time += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void StopInvite()
        {
            if (_invite != null) StopCoroutine(_invite);
            _invite = null;
            if (continueButton != null) continueButton.transform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            _count = null;
            _invite = null;
            if (continueButton != null) continueButton.transform.localScale = Vector3.one;
            // Đóng giữa lúc đang đếm → lần sau mở vẫn đúng số
            if (scoreText != null) scoreText.text = Loc.Number(_score);
            if (rewardText != null) rewardText.text = Loc.Format("complete.reward", _coins);
        }
    }
}
