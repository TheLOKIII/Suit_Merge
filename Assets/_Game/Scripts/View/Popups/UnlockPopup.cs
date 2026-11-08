using System;
using System.Collections;
using SuitMerge.Core.Model;
using SuitMerge.View.Animation;
using SuitMerge.View.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Mở khoá hiệu ứng chất (Figma popup-unlock 178:501): MỞ KHÓA! · icon chất · "Hiệu ứng Rô" · mô tả · ĐÃ HIỂU!
    /// Booster / blocker sẽ dùng chung khung này ở M5 / M6.
    /// Animation phần 4: icon bật 0 → 1,3 → 1, tia sáng vàng (FxTheme.rays) xoay chậm phía sau icon. Chạy theo giờ thực.
    /// </summary>
    public class UnlockPopup : PopupView
    {
        [SerializeField] private UiTheme theme;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private Button okButton;
        [Tooltip("Animation phần 4: ảnh tia sáng (FxTheme.rays). Bỏ trống = không có tia sáng.")]
        [SerializeField] private FxTheme fxTheme;

        public event Action Closed;

        private static readonly Color RaysColor = new Color32(0xF6, 0xD4, 0x77, 0xFF);
        private const float RaysAlpha = 0.85f;
        private const float RaysSize = 2.8f;
        private const float RaysSpin = 25f; // độ / giây
        private Image _rays;
        private Coroutine _spin;

        private void Awake() => okButton.onClick.AddListener(() =>
        {
            Hide();
            Closed?.Invoke();
        });

        public void Show(Suit suit)
        {
            string code = suit.ToCode().ToString();
            icon.sprite = theme != null ? theme.SuitCircle(suit) : null;
            nameText.text = Loc.Get($"unlock.{code}.name");
            nameText.color = suit.IsRed() ? UiTheme.TextRed : UiTheme.TextDark;
            descText.text = Loc.Get($"unlock.{code}.desc");
            if (icon != null) icon.transform.localScale = Vector3.zero;
            Open();
        }

        protected override void OnShown()
        {
            AudioService.Play(SfxId.Unlock, vary: false); // Animation phần 5
            if (_spin != null) StopCoroutine(_spin);
            _spin = StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            Image rays = Rays();
            Transform t = icon != null ? icon.transform : null;
            float time = 0f;
            while (true)
            {
                if (t != null)
                {
                    float k = Mathf.Clamp01(time / 0.4f);
                    t.localScale = Vector3.one * (k < 0.55f ? Mathf.Lerp(0f, 1.3f, Ease.OutCubic(k / 0.55f))
                                                            : Mathf.Lerp(1.3f, 1f, Ease.OutBack((k - 0.55f) / 0.45f)));
                }
                if (rays != null)
                {
                    // bám theo icon (khung xếp dọc có thể dời icon), hiện dần, xoay chậm
                    rays.rectTransform.position = icon.rectTransform.TransformPoint(icon.rectTransform.rect.center);
                    rays.rectTransform.sizeDelta = icon.rectTransform.rect.size * RaysSize;
                    rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -RaysSpin * time);
                    Color c = RaysColor;
                    c.a = RaysAlpha * Mathf.Clamp01(time / 0.3f);
                    rays.color = c;
                }
                time += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Ảnh tia sáng nằm ngay dưới icon, không tham gia xếp hàng của khung.</summary>
        private Image Rays()
        {
            if (fxTheme == null || fxTheme.rays == null || icon == null) return null;
            if (_rays != null) return _rays;
            var go = new GameObject("Rays", typeof(RectTransform));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(icon.transform.parent, false);
            rt.SetSiblingIndex(icon.transform.GetSiblingIndex());
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            _rays = go.AddComponent<Image>();
            _rays.sprite = fxTheme.rays;
            _rays.raycastTarget = false;
            _rays.color = new Color(1f, 1f, 1f, 0f);
            return _rays;
        }

        private void OnDisable()
        {
            _spin = null;
            if (icon != null) icon.transform.localScale = Vector3.one;
            if (_rays != null) _rays.color = new Color(1f, 1f, 1f, 0f);
        }
    }
}
