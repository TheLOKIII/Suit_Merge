using System.Collections;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.View.Animation;
using SuitMerge.View.Localization;
using SuitMerge.View.Popups;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Hud
{
    /// <summary>
    /// 1 mục tiêu (Figma goal-item 177:869): icon + tiến độ "0/2"; xong → chữ teal + dấu ✓.
    /// Mục tiêu điểm trên HUD hiện chữ "ĐIỂM 0/1.500" (Figma objective-panel 178:663).
    /// Dựng bằng code từ <see cref="UiTheme"/> — dùng cho khung MỤC TIÊU và popup Hết lá.
    /// </summary>
    public class GoalItemView : MonoBehaviour
    {
        public enum Style
        {
            /// <summary>Khung MỤC TIÊU trên HUD: icon 40, chữ trắng 20.</summary>
            Hud,
            /// <summary>Popup Hết lá: icon 51, chữ đậm 34.</summary>
            Popup
        }

        private GoalData _goal;
        private Style _style;
        private TextMeshProUGUI _value;
        private TextMeshProUGUI _label;
        private Image _check;
        private RectTransform _icon;
        private int _current;
        private Coroutine _bump;
        private Coroutine _stamp;

        public GoalData Goal => _goal;
        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>Đã đạt mục tiêu (tiến độ ≥ mức cần).</summary>
        public bool IsDone => _goal != null && _current >= _goal.Target;

        /// <summary>Animation phần 3: tâm icon mục tiêu (mục tiêu ĐIỂM: tâm con số) — đích bay của bản sao từ bàn.</summary>
        public Vector3 IconWorld
        {
            get
            {
                RectTransform rt = _icon != null ? _icon : _value != null ? _value.rectTransform : RectTransform;
                return rt.TransformPoint(rt.rect.center);
            }
        }

        /// <summary>
        /// Animation phần 3: mục tiêu vừa hoàn thành → dấu ✓ đóng dấu 0 → 1,4 → 1 (mục tiêu ĐIỂM không có dấu ✓: con số nảy).
        /// </summary>
        public void StampCheck()
        {
            if (!isActiveAndEnabled) return;
            Transform t = _check != null && _check.gameObject.activeSelf ? _check.transform : _value != null ? _value.transform : null;
            if (t == null) return;
            if (_stamp != null) StopCoroutine(_stamp);
            _stamp = StartCoroutine(Stamp(t));
        }

        private IEnumerator Stamp(Transform t)
        {
            yield return Tween.Run(0.35f, k =>
            {
                float s = k < 0.45f ? Mathf.Lerp(0f, 1.4f, Ease.OutCubic(k / 0.45f)) : Mathf.Lerp(1.4f, 1f, Ease.OutBack((k - 0.45f) / 0.55f));
                t.localScale = Vector3.one * s;
            });
            t.localScale = Vector3.one;
            _stamp = null;
        }

        public static GoalItemView Create(Transform parent, UiTheme theme, GoalData goal, Style style)
        {
            var go = new GameObject("Goal " + goal.Type, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<GoalItemView>();
            view.Build(theme, goal, style);
            return view;
        }

        private void Build(UiTheme theme, GoalData goal, Style style)
        {
            _goal = goal;
            _style = style;
            bool hud = style == Style.Hud;
            float icon = hud ? 40f : 51.2f;

            var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = hud ? (goal.Type == GoalType.Score ? 6f : 8f) : 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            // Cỡ do hàng chứa (HorizontalLayoutGroup của khung MỤC TIÊU / popup) quyết định theo cỡ ưu tiên

            if (goal.Type == GoalType.Score && hud)
            {
                // Figma: "ĐIỂM" vàng 16 + "0/1.500" trắng 18
                _label = AddText(theme, Loc.Get("goal.score"), 16f, UiTheme.TextGold);
                _value = AddText(theme, "0", 18f, Color.white);
            }
            else
            {
                // "TẠO LÁ" cho icon lá số (icon không có chất nên cần chữ để người chơi hiểu).
                // HUD: đứng trước icon. Popup: nằm trên icon, vì đứng ngang thì L9 (2 mục tiêu) tràn khung 380.
                Transform iconParent = transform;
                if (goal.Type == GoalType.CreateRank)
                {
                    if (hud)
                    {
                        _label = AddText(theme, Loc.Get("goal.rank"), 16f, UiTheme.TextGold);
                    }
                    else
                    {
                        RectTransform block = NewChild("RankBlock", transform);
                        var v = block.gameObject.AddComponent<VerticalLayoutGroup>();
                        v.childAlignment = TextAnchor.MiddleCenter;
                        v.childControlWidth = v.childControlHeight = true;
                        v.childForceExpandWidth = v.childForceExpandHeight = false;
                        _label = NewText("Label", block, theme, Loc.Get("goal.rank"), 16f, UiTheme.TextMuted);
                        _label.alignment = TextAlignmentOptions.Center;
                        iconParent = block;
                    }
                }

                RectTransform iconRoot = NewChild("Icon", iconParent);
                _icon = iconRoot;
                var le = iconRoot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = icon;
                BuildIcon(iconRoot, theme, goal, icon);

                _check = NewImage("Check", iconRoot, theme.goalCheck);
                RectTransform c = _check.rectTransform;
                c.anchorMin = c.anchorMax = new Vector2(0f, 1f);
                c.pivot = new Vector2(0f, 1f);
                float cs = icon * 0.6f;
                c.sizeDelta = new Vector2(cs, cs);
                c.anchoredPosition = new Vector2(icon * 0.55f, -icon * 0.55f); // Figma (22, 22) trên icon 40
                _check.gameObject.SetActive(false);

                _value = AddText(theme, "0", hud ? 20f : 34f, hud ? Color.white : UiTheme.TextDark);
            }
        }

        /// <summary>Icon theo loại mục tiêu: lá trống chỉ ghi rank X · Ace · vòng tròn chất · ngôi sao (điểm / M6).</summary>
        private static void BuildIcon(RectTransform root, UiTheme theme, GoalData goal, float size)
        {
            float inner = size * 60f / 64f; // Figma: lá 60 trong ô 64
            switch (goal.Type)
            {
                case GoalType.CreateRank when theme.cardBlank != null && goal.Rank >= RankUtil.Lowest && goal.Rank <= RankUtil.HighestOnBoard:
                {
                    // Mục tiêu tính lá rank X thuộc chất nào cũng được → không vẽ chất để khỏi hiểu nhầm (Figma vẽ 9♠)
                    Image bg = NewImage("Card", root, theme.cardBlank);
                    Center(bg.rectTransform, inner);
                    string label = goal.Rank.ToLabel();
                    TextMeshProUGUI r = NewText("Rank", root, theme, label, inner * (label.Length > 1 ? 0.6f : 0.7f), UiTheme.TextDark);
                    Center(r.rectTransform, inner);
                    r.alignment = TextAlignmentOptions.Center;
                    break;
                }
                case GoalType.Ace:
                {
                    Image bg = NewImage("Card", root, theme.cardBlank);
                    Center(bg.rectTransform, inner);
                    TextMeshProUGUI a = NewText("A", root, theme, "A", inner * 0.75f, UiTheme.TextAcePurple);
                    Center(a.rectTransform, inner);
                    a.alignment = TextAlignmentOptions.Center;
                    break;
                }
                case GoalType.SuitTrigger:
                    Center(NewImage("Suit", root, theme.SuitCircle(goal.Suit)).rectTransform, size);
                    break;
                default:
                    Center(NewImage("Star", root, theme.goalScore).rectTransform, inner);
                    break;
            }
        }

        /// <summary>Tiến độ (cắt ở mức cần đạt khi hiển thị). <paramref name="animate"/> = nảy khi tăng.</summary>
        public void SetProgress(int current, bool animate)
        {
            bool increased = current > _current;
            _current = current;
            int target = _goal.Target;
            bool done = current >= target;
            int shown = Mathf.Min(current, target);
            _value.text = Loc.Number(shown) + "/" + Loc.Number(target);
            _value.color = done ? UiTheme.TextTeal : (_style == Style.Hud ? Color.white : UiTheme.TextDark);
            if (_check != null) _check.gameObject.SetActive(done);
            if (_label != null) _label.text = Loc.Get(_goal.Type == GoalType.CreateRank ? "goal.rank" : "goal.score");
            if (animate && increased && isActiveAndEnabled)
            {
                if (_bump != null) StopCoroutine(_bump);
                _bump = StartCoroutine(Bump(done ? 1.45f : 1.3f));
            }
        }

        private IEnumerator Bump(float peak)
        {
            Transform t = transform;
            yield return Tween.Run(0.35f, k =>
            {
                float s = k < 0.3f ? Mathf.Lerp(1f, peak, Ease.OutCubic(k / 0.3f)) : Mathf.Lerp(peak, 1f, Ease.OutCubic((k - 0.3f) / 0.7f));
                t.localScale = Vector3.one * s;
            });
            t.localScale = Vector3.one;
            _bump = null;
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
            if (_check != null) _check.transform.localScale = Vector3.one;
            if (_value != null) _value.transform.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ Dựng UI

        private TextMeshProUGUI AddText(UiTheme theme, string text, float size, Color color) =>
            NewText("Text", transform, theme, text, size, color);

        private static RectTransform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var img = NewChild(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, UiTheme theme, string text, float size, Color color)
        {
            var t = NewChild(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (theme.font != null) t.font = theme.font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void Center(RectTransform rt, float size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
