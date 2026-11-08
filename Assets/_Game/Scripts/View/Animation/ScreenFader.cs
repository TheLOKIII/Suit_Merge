using System;
using System.Collections;
using UnityEngine;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Chuyển màn (Animation phần 4): màn đen phủ toàn Canvas mờ dần lên (0,2s) → đổi màn → sáng lại (0,25s).
    /// Nằm trên mọi thứ (kể cả popup) và chặn chạm trong lúc chuyển. Chạy theo giờ thực.
    /// Cần CanvasGroup + Image đen trên cùng GameObject (menu Cập nhật scene Game dựng sẵn).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private float fadeInDuration = 0.25f;

        private CanvasGroup _group;
        private Coroutine _routine;

        public bool IsRunning => _routine != null;

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        /// <summary>Tối dần → gọi <paramref name="middle"/> (đổi màn) → sáng lại. Đang chuyển thì bỏ qua lần gọi mới.</summary>
        public void Run(Action middle, MonoBehaviour host)
        {
            if (_routine != null) return;
            _routine = host.StartCoroutine(RunRoutine(middle));
        }

        private IEnumerator RunRoutine(Action middle)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // trên cả popup
            CanvasGroup group = Group;
            group.blocksRaycasts = true;
            yield return Tween.Fade(group, 0f, 1f, fadeOutDuration, Ease.OutQuad, unscaled: true);
            try
            {
                middle?.Invoke();
            }
            finally
            {
                transform.SetAsLastSibling(); // popup vừa mở trong middle() vẫn nằm dưới màn đen
            }
            yield return null; // đợi 1 khung cho bố cục mới dựng xong rồi mới sáng lại
            yield return Tween.Fade(group, 1f, 0f, fadeInDuration, Ease.OutQuad, unscaled: true);
            group.blocksRaycasts = false;
            gameObject.SetActive(false);
            _routine = null;
        }

        /// <summary>Huỷ giữa chừng (tắt scene): ẩn ngay.</summary>
        public void Cancel()
        {
            _routine = null;
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
    }
}
