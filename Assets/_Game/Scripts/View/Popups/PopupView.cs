using System.Collections;
using SuitMerge.View.Animation;
using UnityEngine;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Nền chung cho popup kiểu Figma (bước 4.5): nền tối phủ toàn màn hình chặn chạm + khung "Panel" bật nhẹ khi hiện.
    /// Animation phần 1: đóng cũng có animation (mờ dần + khung thu về 0.9); trong lúc đóng popup không nhận chạm
    /// và được coi là đã đóng (<see cref="IsShown"/> = false). Popup Game Over / Tạm dừng cũng dùng chung lớp này.
    /// Chạy theo giờ thực: không bị khựng hình / tua nhanh làm lệch.
    /// </summary>
    public abstract class PopupView : MonoBehaviour
    {
        private const float ShowDuration = 0.2f;
        private const float PanelStartScale = 0.85f;
        private const float HideDuration = 0.15f;
        private const float PanelEndScale = 0.9f;

        private const float SlideDistance = 320f;
        private const float SlideDuration = 0.32f;

        private Coroutine _anim;
        private bool _closing;
        private Vector2 _panelHome;
        private bool _panelHomeSaved;

        /// <summary>Animation phần 4: true = khung TRƯỢT từ dưới lên (popup thua) thay vì bật tại chỗ.</summary>
        protected virtual bool SlideIn => false;

        /// <summary>Animation phần 4: gọi khi popup đã hiện xong (vd đếm điểm, lắc mục tiêu, nút mời bấm).</summary>
        protected virtual void OnShown() { }

        public bool IsShown => gameObject.activeSelf && !_closing;

        /// <summary>Đóng có animation (mờ dần + thu nhỏ) rồi mới tắt.</summary>
        public void Hide()
        {
            if (!gameObject.activeSelf || _closing) return;
            if (!isActiveAndEnabled)
            {
                HideImmediate(); // cha đang tắt → không chạy được coroutine
                return;
            }
            StopAnim();
            _closing = true;
            Group.blocksRaycasts = false;
            _anim = StartCoroutine(AnimateOut());
        }

        /// <summary>Tắt ngay, không animation (chơi lại, đổi level).</summary>
        public void HideImmediate()
        {
            StopAnim();
            _closing = false;
            ResetVisual();
            gameObject.SetActive(false);
        }

        protected void Open()
        {
            StopAnim();
            _closing = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // trên mọi thứ khác trong Canvas
            Group.blocksRaycasts = true;
            AudioService.Play(SfxId.PopupOpen); // Animation phần 5
            _anim = StartCoroutine(AnimateIn());
        }

        private CanvasGroup Group
        {
            get
            {
                CanvasGroup group = GetComponent<CanvasGroup>();
                return group != null ? group : gameObject.AddComponent<CanvasGroup>();
            }
        }

        private Transform Panel => transform.Find("Panel");

        private void StopAnim()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = null;
        }

        private void ResetVisual()
        {
            CanvasGroup group = Group;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            Transform panel = Panel;
            if (panel != null)
            {
                panel.localScale = Vector3.one;
                if (_panelHomeSaved && panel is RectTransform rt) rt.anchoredPosition = _panelHome;
            }
        }

        private IEnumerator AnimateIn()
        {
            CanvasGroup group = Group;
            Transform panel = Panel;

            var slide = SlideIn ? panel as RectTransform : null;
            if (slide != null && !_panelHomeSaved)
            {
                _panelHome = slide.anchoredPosition;
                _panelHomeSaved = true;
            }
            if (slide != null)
            {
                // Animation phần 4: khung trượt từ dưới lên (nảy nhẹ khi tới nơi), nền tối mờ dần
                slide.localScale = Vector3.one;
                yield return Tween.Run(SlideDuration, k =>
                {
                    group.alpha = Ease.OutCubic(Mathf.Clamp01(k * 1.6f));
                    slide.anchoredPosition = _panelHome + new Vector2(0f, -SlideDistance * (1f - Ease.OutBack(k)));
                }, null, unscaled: true);
                slide.anchoredPosition = _panelHome;
            }
            else
            {
                // Mờ dần (OutCubic) và khung bật 0.85 → 1 (OutBack) trong CÙNG một tween → đóng giữa chừng dừng được cả hai
                yield return Tween.Run(ShowDuration, k =>
                {
                    group.alpha = Ease.OutCubic(k);
                    if (panel != null) panel.localScale = Vector3.one * Mathf.LerpUnclamped(PanelStartScale, 1f, Ease.OutBack(k));
                }, null, unscaled: true);
            }
            _anim = null;
            OnShown();
        }

        private IEnumerator AnimateOut()
        {
            CanvasGroup group = Group;
            Transform panel = Panel;
            float alpha = group.alpha;
            Vector3 scale = panel != null ? panel.localScale : Vector3.one;

            yield return Tween.Run(HideDuration, k =>
            {
                group.alpha = Mathf.Lerp(alpha, 0f, k);
                if (panel != null) panel.localScale = Vector3.LerpUnclamped(scale, Vector3.one * PanelEndScale, k);
            }, Ease.OutQuad, unscaled: true);

            _anim = null;
            _closing = false;
            ResetVisual();
            gameObject.SetActive(false);
        }
    }
}
