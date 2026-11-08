using System;

namespace SuitMerge.Core.Model
{
    /// <summary>
    /// Rank lá bài. Giá trị số = RankValue trong công thức điểm (spec 2.1).
    /// Ace (14) chỉ là kết quả của merge K — không bao giờ nằm trên bàn.
    /// </summary>
    public enum Rank
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }

    public static class RankUtil
    {
        public const Rank Lowest = Rank.Two;
        public const Rank HighestOnBoard = Rank.King;

        public static int Value(this Rank rank) => (int)rank;

        public static Rank Next(this Rank rank)
        {
            if (rank == Rank.Ace) throw new InvalidOperationException("Ace là rank cao nhất.");
            return (Rank)((int)rank + 1);
        }

        public static string ToLabel(this Rank rank)
        {
            switch (rank)
            {
                case Rank.Jack: return "J";
                case Rank.Queen: return "Q";
                case Rank.King: return "K";
                case Rank.Ace: return "A";
                default: return ((int)rank).ToString();
            }
        }

        public static bool TryParse(string label, out Rank rank)
        {
            rank = default;
            switch (label)
            {
                case "J": rank = Rank.Jack; return true;
                case "Q": rank = Rank.Queen; return true;
                case "K": rank = Rank.King; return true;
                case "A": rank = Rank.Ace; return true;
            }
            if (int.TryParse(label, out int n) && n >= 2 && n <= 10 && label == n.ToString())
            {
                rank = (Rank)n;
                return true;
            }
            return false;
        }

        public static Rank Parse(string label)
        {
            if (!TryParse(label, out Rank rank))
                throw new FormatException($"Rank không hợp lệ: '{label}'");
            return rank;
        }
    }
}
