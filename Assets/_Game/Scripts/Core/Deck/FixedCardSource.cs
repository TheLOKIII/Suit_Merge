using SuitMerge.Core.Model;

namespace SuitMerge.Core.Deck
{
    /// <summary>
    /// Nguồn lá luôn trả về cùng 1 lá (<see cref="Card"/>) — Sandbox: lá trên tay giữ nguyên tới khi người test đổi.
    /// Card = null → hết lá.
    /// </summary>
    public sealed class FixedCardSource : ICardSource
    {
        public Card Card { get; set; }

        public FixedCardSource(Card card = null)
        {
            Card = card;
        }

        public Card Draw(Board board) => Card?.Clone();
    }
}
