using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Lớp trong suốt phủ màn chơi, chỉ bật khi đang phát animation (Animation phần 1):
    /// chạm vào bất kỳ đâu → báo <see cref="Tapped"/> để EventPlayer tua nhanh phần còn lại.
    /// Cần 1 Image (alpha 0, raycastTarget) trên cùng GameObject để nhận chạm.
    /// </summary>
    public class FastForwardCatcher : MonoBehaviour, IPointerDownHandler
    {
        public event Action Tapped;

        public void OnPointerDown(PointerEventData eventData) => Tapped?.Invoke();

        public void SetActive(bool active)
        {
            if (gameObject.activeSelf != active) gameObject.SetActive(active);
        }
    }
}
