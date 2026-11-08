using System;
using SuitMerge.Core.Config;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Deck
{
    /// <summary>
    /// Sinh lá chế độ Endless (spec 5.2):
    /// H = rank cao nhất đang có trên bàn (bàn trống → endlessStartHighest = 4).
    /// Rank rút đều trong [max(2, H − 4), max(4, H − 1)], chất đều 25%.
    /// Cùng seed + cùng diễn biến bàn → cùng dãy lá.
    /// </summary>
    public sealed class EndlessCardGenerator : ICardSource
    {
        private readonly SeededRng _rng;
        private readonly GameConfig _config;

        public EndlessCardGenerator(uint seed, GameConfig config)
        {
            _rng = new SeededRng(seed);
            _config = config ?? GameConfig.Default;
        }

        public Card Draw(Board board)
        {
            (Rank min, Rank max) = GetRankRange(board, _config);
            var rank = (Rank)_rng.NextInt((int)min, (int)max + 1);
            var suit = (Suit)_rng.NextInt((int)Suit.Heart, (int)Suit.Spade + 1);
            return new Card(rank, suit);
        }

        /// <summary>Khoảng rank (gồm cả 2 đầu) sẽ được rút với bàn hiện tại.</summary>
        public static (Rank min, Rank max) GetRankRange(Board board, GameConfig config)
        {
            int h = HighestRankOnBoard(board) ?? config.endlessStartHighest;
            int lo = Math.Max((int)RankUtil.Lowest, h - config.endlessLowOffset);
            int hi = Math.Max(config.endlessMinHigh, h - config.endlessHighOffset);
            hi = Math.Min(hi, (int)RankUtil.HighestOnBoard);
            lo = Math.Min(lo, hi);
            return ((Rank)lo, (Rank)hi);
        }

        /// <summary>Rank cao nhất của các lá đang trên bàn; null nếu bàn không có lá.</summary>
        public static int? HighestRankOnBoard(Board board)
        {
            int? best = null;
            foreach (CellPos p in board.AllPositions())
            {
                Card card = board.GetCard(p);
                if (card == null || card.IsJoker) continue;
                if (best == null || (int)card.Rank > best.Value) best = (int)card.Rank;
            }
            return best;
        }
    }
}
