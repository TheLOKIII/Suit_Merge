using System;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Deck
{
    /// <summary>
    /// Nguồn Piece cho một ván (spec 3.1 bước 6: <c>nextPiece = deck.drawPiece()</c>).
    /// </summary>
    public interface IPieceSource
    {
        /// <summary>Rút Piece kế tiếp dựa trên bàn hiện tại. Trả null khi hết lá.</summary>
        Piece Draw(Board board);
    }

    /// <summary>Nguồn có giới hạn số lá (Level): đếm số lá CHƯA rút (không tính Piece hiện tại / Next).</summary>
    public interface ICountedPieceSource : IPieceSource
    {
        int CardsLeft { get; }
    }

    /// <summary>Nguồn chụp / khôi phục được vị trí rút (để Undo trả lại đúng lá).</summary>
    public interface IRestorablePieceSource : IPieceSource
    {
        /// <summary>Trạng thái hiện tại (bất biến — chụp bao nhiêu lần cũng được).</summary>
        object CaptureState();
        void RestoreState(object state);
    }

    /// <summary>
    /// Gói mỗi lá của một <see cref="ICardSource"/> thành Piece 1 lá.
    /// Không gọi thêm số ngẫu nhiên nào → cùng seed vẫn ra đúng dãy lá như trước.
    /// </summary>
    public sealed class SinglePieceSource : IPieceSource
    {
        public ICardSource Cards { get; }

        public SinglePieceSource(ICardSource cards)
        {
            Cards = cards ?? throw new ArgumentNullException(nameof(cards));
        }

        public Piece Draw(Board board)
        {
            Card card = Cards.Draw(board);
            return card != null ? Piece.Single(card) : null;
        }
    }

    /// <summary>
    /// Endless có khối đôi (spec 4.9, 5.2): mỗi lượt rút, xác suất <c>pairRatio</c> ra khối đôi [A B] ngang;
    /// 2 lá rút lần lượt từ cùng bộ sinh lá (cùng khoảng rank theo bàn hiện tại).
    /// Số ngẫu nhiên "ra khối đôi?" dùng dãy riêng (seed tách từ seed ván) → không xen vào dãy sinh lá.
    /// </summary>
    public sealed class EndlessPieceSource : IPieceSource
    {
        private readonly ICardSource _cards;
        private readonly double _pairRatio;
        private readonly SeededRng _pairRng;

        public EndlessPieceSource(ICardSource cards, double pairRatio, uint seed)
        {
            _cards = cards ?? throw new ArgumentNullException(nameof(cards));
            _pairRatio = pairRatio;
            _pairRng = new SeededRng(seed ^ 0x5A17D0B1u);
        }

        public Piece Draw(Board board)
        {
            bool pair = _pairRng.NextDouble() < _pairRatio;
            Card a = _cards.Draw(board);
            if (a == null) return null;
            if (!pair) return Piece.Single(a);
            Card b = _cards.Draw(board);
            return b != null ? Piece.Pair(a, b) : Piece.Single(a);
        }
    }
}
