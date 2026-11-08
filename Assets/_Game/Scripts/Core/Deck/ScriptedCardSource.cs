using System.Collections.Generic;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Deck
{
    /// <summary>
    /// Trả lần lượt các lá cho trước, hết thì trả null.
    /// Dùng cho test và (sau này) đoạn script tutorial của Level.
    /// </summary>
    public sealed class ScriptedCardSource : ICardSource
    {
        private readonly Queue<Card> _cards;

        public ScriptedCardSource(IEnumerable<Card> cards)
        {
            _cards = new Queue<Card>(cards);
        }

        /// <summary>Tạo từ token: new ScriptedCardSource("2S", "3H", "10D").</summary>
        public ScriptedCardSource(params string[] tokens)
        {
            _cards = new Queue<Card>();
            foreach (string token in tokens)
                _cards.Enqueue(Card.Parse(token));
        }

        public int Remaining => _cards.Count;

        public Card Draw(Board board) => _cards.Count > 0 ? _cards.Dequeue() : null;
    }
}
