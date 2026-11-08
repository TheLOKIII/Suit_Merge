using SuitMerge.Core.Model;
using SuitMerge.View.Board;
using SuitMerge.View.Hand;
using SuitMerge.View.Hud;
using UnityEngine;

namespace SuitMerge.App
{
    /// <summary>
    /// Công cụ dev: đổ dữ liệu giống hệt mockup Figma "Main Play – Endless" vào màn hình để so sánh bố cục.
    /// Bỏ tick component này trong Inspector để tắt. Bước 2.4 sẽ tắt nó khi chạy ván thật.
    /// </summary>
    public class MockupPreview : MonoBehaviour
    {
        // Bàn trong mockup (Docs/Ref/mockup-endless.png)
        private const string MockupBoard = @"
            8S  .   10H .   9C
            .   8D  JD  .   .
            9S  .   .   7H  8C
            .   10C 9H  .   .
            7D  .   8S  .   QH";

        [SerializeField] private BoardView boardView;
        [SerializeField] private HudView hudView;
        [SerializeField] private HandView handView;

        private void Start()
        {
            boardView.Render(Board.FromText(MockupBoard));
            hudView.SetScore(9720);
            hudView.SetCoins(180);
            hudView.SetNext(Card.Parse("9S"));
            handView.SetCurrent(Card.Parse("9S"));
        }
    }
}
