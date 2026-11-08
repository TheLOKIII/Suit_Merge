using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SuitMerge.View.Sandbox
{
    /// <summary>Lớp trong suốt phủ bàn (chỉ bật ở chế độ Sửa ô của Sandbox): báo vị trí chạm ra ngoài.</summary>
    public class BoardTapCatcher : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Toạ độ màn hình của điểm chạm.</summary>
        public event Action<Vector2> Tapped;

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke(eventData.position);
    }
}
