using System;
using System.Collections.Generic;

namespace SuitMerge.Core.Model
{
    /// <summary>
    /// Hướng của khối đôi (spec 2.3, 4.9). Chu kỳ xoay: [A B] → [A/B] → [B A] → [B/A].
    /// Lá đơn luôn là <see cref="H_AB"/> (không dùng tới).
    /// </summary>
    public enum PieceOrientation
    {
        H_AB,
        V_AB,
        H_BA,
        V_BA
    }

    /// <summary>
    /// Đơn vị người chơi nhận mỗi lượt (spec 2.3): 1 lá (lá đơn) hoặc 2 lá dính nhau (khối đôi, spec 4.9 — M4.5).
    /// Bất biến: xoay trả về Piece mới.
    /// </summary>
    public sealed class Piece
    {
        private readonly Card[] _cards;

        /// <summary>Các lá theo thứ tự gốc A, B (không phụ thuộc hướng).</summary>
        public IReadOnlyList<Card> Cards => _cards;
        public int Count => _cards.Length;
        public bool IsPair => _cards.Length == 2;
        public PieceOrientation Orientation { get; }
        /// <summary>Khối dọc (lá ② nằm dưới lá ①); khối ngang / lá đơn = false.</summary>
        public bool IsVertical => IsPair && (Orientation == PieceOrientation.V_AB || Orientation == PieceOrientation.V_BA);
        /// <summary>
        /// Ô gợi ý của lá scripted trong tutorial (spec 12.1): lá đơn 1 ô, khối đôi 2 ô (① ②). Rỗng = không gợi ý.
        /// </summary>
        public IReadOnlyList<CellPos> Hint { get; }
        public bool HasHint => Hint.Count > 0;
        /// <summary>
        /// Khối đôi có gợi ý: hướng mà 2 ô gợi ý được viết cho (hướng lúc rút ra). Xoay khác hướng này thì không thả
        /// được vào ô gợi ý (tránh đặt ngược 2 lá của lượt hướng dẫn).
        /// </summary>
        public PieceOrientation HintOrientation { get; }

        /// <summary>
        /// Lá ① (spec 4.9): lá trên (khối dọc) / trái (khối ngang) theo hướng hiện tại.
        /// Lá đơn: chính lá đó.
        /// </summary>
        public Card First => IsPair && IsReversed(Orientation) ? _cards[1] : _cards[0];

        /// <summary>Lá ② của khối đôi; null với lá đơn.</summary>
        public Card Second => !IsPair ? null : IsReversed(Orientation) ? _cards[0] : _cards[1];

        private Piece(Card[] cards, PieceOrientation orientation, CellPos[] hint, PieceOrientation hintOrientation)
        {
            _cards = cards;
            Orientation = orientation;
            Hint = hint ?? Array.Empty<CellPos>();
            HintOrientation = hintOrientation;
        }

        /// <summary>Piece 1 lá; <paramref name="hint"/> = ô gợi ý (null = không có).</summary>
        public static Piece Single(Card card, CellPos? hint = null)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            return new Piece(new[] { card }, PieceOrientation.H_AB, hint.HasValue ? new[] { hint.Value } : null,
                PieceOrientation.H_AB);
        }

        /// <summary>
        /// Khối đôi [A B] theo <paramref name="orientation"/>. <paramref name="hint"/> = null hoặc đúng 2 ô (ô lá ①, ô lá ②)
        /// viết cho chính hướng này.
        /// </summary>
        public static Piece Pair(Card a, Card b, PieceOrientation orientation = PieceOrientation.H_AB, IReadOnlyList<CellPos> hint = null)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (hint != null && hint.Count != 0 && hint.Count != 2)
                throw new ArgumentException("Khối đôi cần 0 hoặc 2 ô gợi ý.", nameof(hint));
            CellPos[] cells = hint != null && hint.Count == 2 ? new[] { hint[0], hint[1] } : null;
            return new Piece(new[] { a, b }, orientation, cells, orientation);
        }

        /// <summary>
        /// Xoay 90° theo chu kỳ [A B] → [A/B] → [B A] → [B/A] → [A B] (spec 4.9). Lá đơn trả về chính nó.
        /// </summary>
        public Piece Rotated()
        {
            if (!IsPair) return this;
            var next = (PieceOrientation)(((int)Orientation + 1) % 4);
            return new Piece(_cards, next, Hint.Count > 0 ? new List<CellPos>(Hint).ToArray() : null, HintOrientation);
        }

        /// <summary>Ô của lá ② khi lá ① nằm ở <paramref name="first"/>: khối ngang → ô bên phải, khối dọc → ô bên dưới.</summary>
        public CellPos SecondCell(CellPos first) => IsVertical ? first.Down : first.Right;

        public Piece Clone()
        {
            var cards = new Card[_cards.Length];
            for (int i = 0; i < cards.Length; i++) cards[i] = _cards[i].Clone();
            return new Piece(cards, Orientation, Hint.Count > 0 ? new List<CellPos>(Hint).ToArray() : null, HintOrientation);
        }

        /// <summary>Lá đơn: "7S". Khối đôi: "[9H 10D]" (ngang) / "[9H/10D]" (dọc), theo thứ tự ① ②.</summary>
        public override string ToString()
        {
            if (!IsPair) return _cards[0].ToString();
            bool vertical = Orientation == PieceOrientation.V_AB || Orientation == PieceOrientation.V_BA;
            return $"[{First}{(vertical ? "/" : " ")}{Second}]";
        }

        private static bool IsReversed(PieceOrientation o) => o == PieceOrientation.H_BA || o == PieceOrientation.V_BA;
    }
}
