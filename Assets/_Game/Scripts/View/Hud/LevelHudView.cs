using System.Collections;
using System.Collections.Generic;
using SuitMerge.Core.Goals;
using SuitMerge.Core.Level;
using SuitMerge.View.Animation;
using SuitMerge.View.Localization;
using SuitMerge.View.Popups;
using TMPro;
using UnityEngine;

namespace SuitMerge.View.Hud
{
    /// <summary>
    /// HUD chế độ Level (bước 4.5, Figma Level 13 178:539): pill "LEVEL N" (level-title 178:661),
    /// khung MỤC TIÊU (objective-panel 178:663) và ô BỘ BÀI (deck-counter 178:674).
    /// Chỉ hiển thị — số liệu lấy từ GameSession.
    /// </summary>
    public class LevelHudView : MonoBehaviour
    {
        [SerializeField] private UiTheme theme;
        [SerializeField] private TextMeshProUGUI levelText;
        [Tooltip("Hàng chứa các mục tiêu (HorizontalLayoutGroup) bên trong khung MỤC TIÊU.")]
        [SerializeField] private RectTransform goalsRoot;
        [SerializeField] private TextMeshProUGUI deckText;

        private readonly List<GoalItemView> _goals = new List<GoalItemView>();
        private int _levelId;
        private int _deck;
        private Coroutine _deckBump;

        // Animation phần 3
        /// <summary>BỘ BÀI còn ≤ số này → số chuyển đỏ, đập nhịp tim.</summary>
        private const int DeckWarnAt = 3;
        private const float HeartbeatPeriod = 0.8f;
        private const float HeartbeatScale = 1.2f;
        private static readonly Color DeckWarnColor = new Color32(0xFF, 0x6B, 0x5E, 0xFF);
        private Color _deckColor;
        private bool _deckColorSaved;
        private Coroutine _heartbeat;
        /// <summary>Mục tiêu đang có bản sao bay tới — Sync (vẽ lại từ Core) chưa được đổi số, để số đổi đúng lúc bản sao tới nơi.</summary>
        private readonly HashSet<int> _held = new HashSet<int>();

        public int Deck => _deck;

        /// <summary>Animation phần 4: khung "LEVEL N" (bay từ giữa màn lên chỗ cũ trong màn vào level).</summary>
        public RectTransform TitlePanel => levelText != null ? levelText.transform.parent as RectTransform : null;

        /// <summary>Animation phần 4: khung MỤC TIÊU (bay từ giữa màn lên chỗ cũ trong màn vào level).</summary>
        public RectTransform ObjectivePanel => goalsRoot != null ? goalsRoot.parent as RectTransform : null;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        /// <summary>Dựng lại HUD cho level mới (mục tiêu theo thứ tự file level).</summary>
        public void Show(int levelId, IReadOnlyList<GoalProgress> goals, int deck)
        {
            SetVisible(true);
            _levelId = levelId;
            _held.Clear();
            foreach (GoalItemView g in _goals)
                if (g != null) Destroy(g.gameObject);
            _goals.Clear();
            foreach (GoalProgress g in goals)
            {
                GoalItemView item = GoalItemView.Create(goalsRoot, theme, g.Goal, GoalItemView.Style.Hud);
                item.SetProgress(g.Current, animate: false);
                _goals.Add(item);
            }
            RefreshLanguage();
            SetDeck(deck, animate: false);
        }

        /// <summary>Cập nhật toàn bộ tiến độ từ Core (sau mỗi lượt / Undo) — không animation.</summary>
        public void Sync(IReadOnlyList<GoalProgress> goals, int deck)
        {
            for (int i = 0; i < _goals.Count && i < goals.Count; i++)
                if (!_held.Contains(i)) _goals[i].SetProgress(goals[i].Current, animate: false);
            SetDeck(deck, animate: false);
        }

        public void SetGoalProgress(int index, int value, bool animate)
        {
            _held.Remove(index);
            if (index >= 0 && index < _goals.Count) _goals[index].SetProgress(value, animate);
        }

        /// <summary>Animation phần 3: giữ nguyên số của mục tiêu <paramref name="index"/> tới lần <see cref="SetGoalProgress"/> kế tiếp.</summary>
        public void HoldGoal(int index) => _held.Add(index);

        /// <summary>Thả mọi mục tiêu đang giữ (huỷ animation giữa chừng) — lần Sync sau vẽ đúng số từ Core.</summary>
        public void ReleaseGoals() => _held.Clear();

        /// <summary>Mục tiêu thứ <paramref name="index"/> (null nếu không có).</summary>
        public GoalData GoalAt(int index) => index >= 0 && index < _goals.Count && _goals[index] != null ? _goals[index].Goal : null;

        /// <summary>Tâm icon mục tiêu thứ <paramref name="index"/> — đích bay của bản sao từ bàn (null nếu không có).</summary>
        public Vector3? GoalIconWorld(int index) =>
            index >= 0 && index < _goals.Count && _goals[index] != null ? _goals[index].IconWorld : (Vector3?)null;

        /// <summary>Mục tiêu vừa hoàn thành: dấu ✓ đóng dấu.</summary>
        public void StampGoal(int index)
        {
            if (index >= 0 && index < _goals.Count && _goals[index] != null) _goals[index].StampCheck();
        }

        public void SetDeck(int deck, bool animate)
        {
            _deck = Mathf.Max(0, deck);
            if (deckText != null) deckText.text = _deck.ToString();
            UpdateDeckWarning();
            if (!animate || deckText == null || !isActiveAndEnabled) return;
            if (_deckBump != null) StopCoroutine(_deckBump);
            _deckBump = StartCoroutine(Bump(deckText.transform));
        }

        /// <summary>Lá UP NEXT mới vừa được rút: BỘ BÀI giảm đúng số lá của Piece đó.</summary>
        public void TakeFromDeck(int cards)
        {
            if (cards > 0) SetDeck(_deck - cards, animate: true);
        }

        /// <summary>Tâm ô mục tiêu điểm (đích bay của chữ điểm ♦); null nếu level không có mục tiêu điểm.</summary>
        public Vector3? ScoreGoalWorld
        {
            get
            {
                foreach (GoalItemView g in _goals)
                    if (g != null && g.Goal.Type == GoalType.Score)
                        return g.RectTransform.TransformPoint(g.RectTransform.rect.center);
                return null;
            }
        }

        /// <summary>Viết lại chữ phụ thuộc ngôn ngữ (LEVEL N, số có dấu phân cách).</summary>
        public void RefreshLanguage()
        {
            if (levelText != null) levelText.text = Loc.Format("hud.level", _levelId);
        }

        /// <summary>
        /// BỘ BÀI ≤ <see cref="DeckWarnAt"/>: số đỏ + đập nhịp tim 1 ↔ 1,2 mỗi 0,8s (Cài đặt GIẢM BỚT: chỉ đỏ, đứng yên).
        /// </summary>
        private void UpdateDeckWarning()
        {
            if (deckText == null) return;
            if (!_deckColorSaved)
            {
                _deckColor = deckText.color;
                _deckColorSaved = true;
            }
            bool warn = _deck <= DeckWarnAt;
            deckText.color = warn ? DeckWarnColor : _deckColor;
            bool beat = warn && !FxSettings.Reduced && isActiveAndEnabled;
            if (beat && _heartbeat == null) _heartbeat = StartCoroutine(Heartbeat());
            else if (!beat && _heartbeat != null)
            {
                StopCoroutine(_heartbeat);
                _heartbeat = null;
                if (_deckBump == null) deckText.transform.localScale = Vector3.one;
            }
        }

        private IEnumerator Heartbeat()
        {
            float t = 0f;
            while (true)
            {
                // Nhịp tim: 2 nhịp đập nhanh rồi nghỉ trong mỗi chu kỳ
                float k = (t % HeartbeatPeriod) / HeartbeatPeriod;
                float pulse = k < 0.15f ? Mathf.Sin(k / 0.15f * Mathf.PI)
                            : k < 0.35f ? 0.6f * Mathf.Sin((k - 0.15f) / 0.2f * Mathf.PI)
                            : 0f;
                if (_deckBump == null) deckText.transform.localScale = Vector3.one * Mathf.Lerp(1f, HeartbeatScale, pulse);
                t += Time.deltaTime;
                yield return null;
            }
        }

        private void OnDisable()
        {
            _heartbeat = null; // coroutine tự dừng khi tắt; bật lại thì SetDeck / Sync khởi động lại
            if (deckText != null) deckText.transform.localScale = Vector3.one;
        }

        private void OnEnable() => UpdateDeckWarning();

        private IEnumerator Bump(Transform t)
        {
            yield return Tween.Run(0.3f, k =>
            {
                float s = k < 0.3f ? Mathf.Lerp(1f, 1.35f, Ease.OutCubic(k / 0.3f)) : Mathf.Lerp(1.35f, 1f, Ease.OutCubic((k - 0.3f) / 0.7f));
                t.localScale = Vector3.one * s;
            });
            t.localScale = Vector3.one;
            _deckBump = null;
        }
    }
}
