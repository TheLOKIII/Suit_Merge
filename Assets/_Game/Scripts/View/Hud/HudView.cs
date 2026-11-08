using System.Collections;
using SuitMerge.Core.Model;
using SuitMerge.View.Animation;
using SuitMerge.View.Cards;
using SuitMerge.View.Localization;
using TMPro;
using UnityEngine;

namespace SuitMerge.View.Hud
{
    /// <summary>
    /// HUD màn chơi: kỷ lục, điểm (chữ + bóng chữ), coin, lá UP NEXT.
    /// Bước 4.5: chế độ Level ẩn điểm / kỷ lục / coin (HUD Level riêng: LevelHudView); dòng kỷ lục lấy chữ từ bảng chữ.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI scoreShadowText;
        [SerializeField] private TextMeshProUGUI bestText;
        [SerializeField] private TextMeshProUGUI coinText;
        [SerializeField] private MiniCardView nextCard;

        private const float PairNextGap = 4f; // Figma up-next-pair: 2 lá 50, khe 4
        private MiniCardView _nextSecond;
        private Vector2 _nextHome;

        private Coroutine _countRoutine;
        private int _countTarget;
        private string _bestLabel; // khác null → dòng BEST hiện chữ này (scene Sandbox: "SANDBOX")
        private int _lastBest;
        private bool _endlessVisible = true;

        /// <summary>Lá UP NEXT (để animation biết vị trí / kích thước).</summary>
        public RectTransform NextCardRect => (RectTransform)nextCard.transform;

        /// <summary>Đặt điểm ngay. Nếu đang đếm tăng tới đúng số này thì để nó đếm nốt.</summary>
        public void SetScore(int score)
        {
            if (_countRoutine != null && _countTarget == score) return;
            StopCount();
            WriteScore(score);
        }

        /// <summary>Dòng "BEST …" phía trên điểm. Chưa gán ô chữ thì bỏ qua. Đang đếm điểm thì để đếm cập nhật.</summary>
        public void SetBest(int best)
        {
            if (_countRoutine != null) return;
            WriteBest(best);
        }

        /// <summary>Điểm đếm tăng dần từ <paramref name="from"/> tới <paramref name="to"/>; BEST = max(kỷ lục đã lưu, điểm đang hiện).</summary>
        public void AnimateScore(int from, int to, float duration, int savedBest)
        {
            StopCount();
            if (!isActiveAndEnabled || duration <= 0f)
            {
                WriteScore(to);
                WriteBest(Mathf.Max(savedBest, to));
                return;
            }
            _countTarget = to;
            _countRoutine = StartCoroutine(CountRoutine(from, to, duration, savedBest));
        }

        public void SetCoins(int coins) => coinText.text = coins.ToString();

        /// <summary>Bước 4.5: hiện (Endless) / ẩn (Level) điểm, dòng kỷ lục và coin.</summary>
        public void SetEndlessElementsVisible(bool visible)
        {
            _endlessVisible = visible;
            SetActive(scoreText != null ? scoreText.transform.parent : null, visible);
            SetActive(bestText != null ? bestText.transform : null, visible);
            SetActive(coinText != null ? coinText.transform.parent : null, visible);
        }

        /// <summary>Ô điểm đang hiện (Endless) — chữ điểm ♦ chỉ bay vào khi ô điểm hiện.</summary>
        public bool ScoreVisible => _endlessVisible && scoreText != null && scoreText.gameObject.activeInHierarchy;

        /// <summary>Viết lại các chữ phụ thuộc ngôn ngữ (dòng kỷ lục).</summary>
        public void RefreshLanguage() => WriteBest(_lastBest);

        private static void SetActive(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        /// <summary>Thay dòng "BEST n" bằng chữ cố định (null = trở lại hiện BEST).</summary>
        public void SetBestLabel(string label)
        {
            _bestLabel = label;
            if (label != null && bestText != null) bestText.text = label;
        }

        // ---------- Bước 1.4b: chữ điểm ♦ bay vào ô điểm ----------

        private Coroutine _bumpRoutine;

        /// <summary>Tâm ô điểm (toạ độ thế giới) — đích bay của chữ điểm ♦.</summary>
        public Vector3 ScoreWorldCenter
        {
            get
            {
                RectTransform rt = scoreText.rectTransform;
                return rt.TransformPoint(rt.rect.center);
            }
        }

        /// <summary>Ô điểm nảy lên (phóng to rồi về cỡ cũ).</summary>
        public void BumpScore(float scale, float duration)
        {
            if (!isActiveAndEnabled) return;
            if (_bumpRoutine != null) StopCoroutine(_bumpRoutine);
            _bumpRoutine = StartCoroutine(BumpRoutine(scale, duration));
        }

        private IEnumerator BumpRoutine(float scale, float duration)
        {
            Transform a = scoreText.transform;
            Transform b = scoreShadowText != null ? scoreShadowText.transform : null;
            yield return Tween.Run(duration, k =>
            {
                // 0 → đỉnh ở 30% → về 1
                float s = k < 0.3f ? Mathf.Lerp(1f, scale, Ease.OutCubic(k / 0.3f)) : Mathf.Lerp(scale, 1f, Ease.OutCubic((k - 0.3f) / 0.7f));
                a.localScale = Vector3.one * s;
                if (b != null) b.localScale = Vector3.one * s;
            });
            _bumpRoutine = null;
        }

        /// <summary>
        /// Piece UP NEXT; null = hết lá (ẩn). M4.5: khối đôi hiện 2 lá nhỏ đúng hướng (Figma up-next-pair 177:375):
        /// ngang = 2 lá cạnh nhau căn giữa chỗ lá đơn, dọc = lá ② nằm dưới lá ①; khe 4.
        /// </summary>
        public void SetNext(Piece piece)
        {
            EnsureSecondNext();
            var first = (RectTransform)nextCard.transform;
            var second = (RectTransform)_nextSecond.transform;
            nextCard.SetCard(piece?.First);
            _nextSecond.SetCard(piece != null && piece.IsPair ? piece.Second : null);
            first.localScale = second.localScale = Vector3.one;
            first.anchoredPosition = _nextHome;
            if (piece == null || !piece.IsPair) return;

            Vector2 size = first.rect.size;
            if (piece.IsVertical)
                second.anchoredPosition = _nextHome + new Vector2(0f, -(size.y + PairNextGap));
            else
            {
                float half = size.x / 2f + PairNextGap / 2f;
                first.anchoredPosition = _nextHome + new Vector2(-half, 0f);
                second.anchoredPosition = _nextHome + new Vector2(half, 0f);
            }
        }

        /// <summary>Lối tắt: lá UP NEXT (thành Piece lá đơn); null = hết lá (ẩn).</summary>
        public void SetNext(Card card) => SetNext(card != null ? Piece.Single(card) : null);

        /// <summary>Tâm lá UP NEXT thứ <paramref name="slot"/> (0 = lá ①, 1 = lá ② của khối đôi) — gốc lá bay xuống tay.</summary>
        public Vector3 NextCardCenterWorld(int slot)
        {
            EnsureSecondNext();
            var rt = (RectTransform)(slot == 1 ? _nextSecond.transform : nextCard.transform);
            return rt.TransformPoint(rt.rect.center);
        }

        /// <summary>Piece UP NEXT mới bật ra (0 → 1, có nảy).</summary>
        public IEnumerator PopNext(Piece piece, float duration)
        {
            SetNext(piece);
            if (piece == null) yield break;
            if (piece.IsPair) StartCoroutine(Tween.Scale(_nextSecond.transform, Vector3.zero, Vector3.one, duration, Ease.OutBack));
            yield return Tween.Scale(nextCard.transform, Vector3.zero, Vector3.one, duration, Ease.OutBack);
        }

        /// <summary>Animation phần 4 (màn vào level): lá UP NEXT hiện dần — 0 = ẩn, 1 = đúng cỡ (vượt 1 = nảy).</summary>
        public void SetNextAppear(float k)
        {
            EnsureSecondNext();
            Vector3 s = Vector3.one * Mathf.Max(0f, k);
            nextCard.transform.localScale = s;
            _nextSecond.transform.localScale = s;
        }

        /// <summary>Lá nhỏ thứ 2 cho khối đôi: nhân bản lá UP NEXT có sẵn (scene cũ không cần dựng lại).</summary>
        private void EnsureSecondNext()
        {
            if (_nextSecond != null) return;
            _nextHome = ((RectTransform)nextCard.transform).anchoredPosition;
            _nextSecond = Instantiate(nextCard, nextCard.transform.parent);
            _nextSecond.name = "NextCard2";
            _nextSecond.SetCard(null);
        }

        /// <summary>Lối tắt: lá UP NEXT mới bật ra.</summary>
        public IEnumerator PopNext(Card card, float duration) => PopNext(card != null ? Piece.Single(card) : null, duration);

        private IEnumerator CountRoutine(int from, int to, float duration, int savedBest)
        {
            yield return Tween.Run(duration, k =>
            {
                int value = Mathf.RoundToInt(Mathf.Lerp(from, to, k));
                WriteScore(value);
                WriteBest(Mathf.Max(savedBest, value));
            }, Ease.OutCubic);
            _countRoutine = null;
        }

        private void StopCount()
        {
            if (_countRoutine != null) StopCoroutine(_countRoutine);
            _countRoutine = null;
        }

        private void WriteScore(int score)
        {
            string s = score.ToString();
            scoreText.text = s;
            if (scoreShadowText != null) scoreShadowText.text = s;
        }

        private void WriteBest(int best)
        {
            _lastBest = best;
            if (bestText != null) bestText.text = _bestLabel ?? Loc.Format("hud.best", best);
        }
    }
}
