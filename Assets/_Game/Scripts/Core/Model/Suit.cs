using System;

namespace SuitMerge.Core.Model
{
    /// <summary>Chất lá bài (spec 2.2). None chỉ dùng cho Joker / ô không có ấn.</summary>
    public enum Suit
    {
        None = 0,
        Heart = 1,   // ♥ Cơ
        Diamond = 2, // ♦ Rô
        Club = 3,    // ♣ Chuồn
        Spade = 4    // ♠ Bích
    }

    public static class SuitUtil
    {
        public static bool IsRed(this Suit suit) => suit == Suit.Heart || suit == Suit.Diamond;

        public static string ToSymbol(this Suit suit)
        {
            switch (suit)
            {
                case Suit.Heart: return "♥";
                case Suit.Diamond: return "♦";
                case Suit.Club: return "♣";
                case Suit.Spade: return "♠";
                default: return "";
            }
        }

        /// <summary>Mã 1 chữ dùng trong file level: H D C S.</summary>
        public static char ToCode(this Suit suit)
        {
            switch (suit)
            {
                case Suit.Heart: return 'H';
                case Suit.Diamond: return 'D';
                case Suit.Club: return 'C';
                case Suit.Spade: return 'S';
                default: throw new ArgumentException("Suit.None không có mã.");
            }
        }

        public static bool TryParseCode(char code, out Suit suit)
        {
            switch (code)
            {
                case 'H': suit = Suit.Heart; return true;
                case 'D': suit = Suit.Diamond; return true;
                case 'C': suit = Suit.Club; return true;
                case 'S': suit = Suit.Spade; return true;
                default: suit = Suit.None; return false;
            }
        }
    }
}
