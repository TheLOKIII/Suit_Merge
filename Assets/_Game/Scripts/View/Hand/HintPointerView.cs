using System.Collections;
using SuitMerge.View.Animation;
using SuitMerge.View.Localization;
using TMPro;
using UnityEngine;

namespace SuitMerge.View.Hand
{
    /// <summary>
    /// Tutorial (bước 4.5): bàn tay (Figma hand-pointer 177:411) lặp lại động tác "kéo lá từ tay tới ô gợi ý",
    /// và chữ tutorial thay chỗ dòng "Kéo thả vào ô trống" dưới lá trên tay.
    /// Điểm chạm = đầu ngón trỏ (51, 8) trên ảnh 120×149.
    /// </summary>
    public class HintPointerView : MonoBehaviour
    {
        private static readonly Color TutorialColor = new Color32(0xF6, 0xD4, 0x77, 0xFF);
        private static readonly Vector2 FingerTip = new Vector2(51f / 120f, 1f - 8f / 149f); // pivot của ảnh

        [SerializeField] private RectTransform hand;
        [Tooltip("Dòng chữ dưới lá trên tay (có LocalizedLabel \"hud.drag_hint\").")]
        [SerializeField] private TextMeshProUGUI caption;
        [SerializeField] private float moveDuration = 0.9f;
        [SerializeField] private float pauseDuration = 0.45f;

        private Coroutine _loop;
        private Color _captionColor = Color.white;
        private bool _captionSaved;

        private void Awake()
        {
            if (hand != null)
            {
                hand.pivot = FingerTip;
                hand.gameObject.SetActive(false);
            }
        }

        /// <summary>Bắt đầu chỉ từ <paramref name="fromWorld"/> (lá trên tay) tới <paramref name="toWorld"/> (tâm ô gợi ý).</summary>
        public void Show(Vector3 fromWorld, Vector3 toWorld)
        {
            if (hand == null) return;
            if (_loop != null) StopCoroutine(_loop);
            hand.gameObject.SetActive(true);
            hand.SetAsLastSibling();
            _loop = StartCoroutine(Loop(fromWorld, toWorld));
        }

        public void Hide()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            if (hand != null) hand.gameObject.SetActive(false);
        }

        /// <summary>Chữ tutorial (null / rỗng = trả lại dòng "Kéo thả vào ô trống").</summary>
        public void SetCaption(string text)
        {
            if (caption == null) return;
            if (!_captionSaved)
            {
                _captionColor = caption.color;
                _captionSaved = true;
            }
            var label = caption.GetComponent<LocalizedLabel>();
            bool tutorial = !string.IsNullOrEmpty(text);
            if (label != null) label.enabled = !tutorial;
            if (tutorial)
            {
                caption.text = text;
                caption.color = TutorialColor;
            }
            else
            {
                caption.color = _captionColor;
                if (label != null) label.Apply();
            }
        }

        private void OnDisable()
        {
            _loop = null;
            if (hand != null) hand.gameObject.SetActive(false);
        }

        private IEnumerator Loop(Vector3 from, Vector3 to)
        {
            var group = hand.GetComponent<CanvasGroup>();
            if (group == null) group = hand.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            while (true)
            {
                hand.position = from;
                hand.localScale = Vector3.one;
                yield return Tween.Run(0.2f, k => group.alpha = k);
                yield return Tween.Run(0.15f, k => hand.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, k)); // nhấn
                yield return Tween.Run(moveDuration, k => hand.position = Vector3.Lerp(from, to, Ease.OutCubic(k)));
                yield return Tween.Run(0.15f, k => hand.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, k));  // thả
                yield return Tween.Run(0.25f, k => group.alpha = 1f - k);
                yield return Tween.Delay(pauseDuration);
            }
        }
    }
}
