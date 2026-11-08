using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Nút nhún khi bấm (Animation phần 1): nhấn xuống co còn <see cref="pressedScale"/>, thả ra bật lại cỡ gốc có nảy.
    /// Nút đang khoá (interactable = false) thì không nhún. Chạy theo giờ thực (không bị khựng hình / tua nhanh).
    /// Menu Cập nhật scene Game tự gắn vào mọi nút của scene Game.
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0.7f, 1f)] private float pressedScale = 0.92f;
        [SerializeField] private float pressDuration = 0.06f;
        [SerializeField] private float releaseDuration = 0.12f;

        private Selectable _selectable;
        private Vector3 _baseScale = Vector3.one;
        private bool _hasBase;
        private bool _pressed;
        private Coroutine _routine;

        /// <summary>Animation phần 4: vừa nhấn xuống (trước khi nút co lại) — vd dừng nhịp "mời bấm" của nút.</summary>
        public event System.Action Pressed;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            CaptureBase();
        }

        private void OnDisable()
        {
            // Popup tắt giữa lúc đang nhấn → lần sau hiện lại nút phải đúng cỡ
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _pressed = false;
            if (_hasBase) transform.localScale = _baseScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_selectable != null && !_selectable.IsInteractable()) return;
            Pressed?.Invoke();
            AudioService.Play(SfxId.Button); // Animation phần 5
            CaptureBase();
            _pressed = true;
            Animate(_baseScale * pressedScale, pressDuration, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        /// <summary>Kéo ngón tay ra khỏi nút → nút cũng bật lại (Unity sẽ không gọi onClick).</summary>
        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (!_pressed) return;
            _pressed = false;
            Animate(_baseScale, releaseDuration, Ease.OutBack);
        }

        private void CaptureBase()
        {
            if (_hasBase && (_pressed || _routine != null)) return; // đang nhún → giữ cỡ gốc cũ
            _baseScale = transform.localScale;
            _hasBase = true;
        }

        private void Animate(Vector3 to, float duration, System.Func<float, float> ease)
        {
            if (!isActiveAndEnabled) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(transform.localScale, to, duration, ease));
        }

        private IEnumerator Run(Vector3 from, Vector3 to, float duration, System.Func<float, float> ease)
        {
            yield return Tween.Scale(transform, from, to, duration, ease, unscaled: true);
            _routine = null;
        }
    }
}
