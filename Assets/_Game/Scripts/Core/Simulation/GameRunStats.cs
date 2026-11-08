using SuitMerge.Core.Model;

namespace SuitMerge.Core.Simulation
{
    /// <summary>Số liệu của 1 ván Endless mô phỏng.</summary>
    public sealed class GameRunStats
    {
        public uint Seed { get; }
        public int Turns { get; }
        public int Score { get; }
        /// <summary>Rank cao nhất từng xuất hiện trên bàn (14 = đã tạo Ace).</summary>
        public int HighestRank { get; }
        public int Aces { get; }
        /// <summary>false = dừng vì chạm giới hạn lượt (không phải thua).</summary>
        public bool Finished { get; }
        /// <summary>Lý do thua (M4.5: tách bàn đầy / hết chỗ cho khối đôi).</summary>
        public LoseReason LoseReason { get; }

        public GameRunStats(uint seed, int turns, int score, int highestRank, int aces, bool finished,
            LoseReason loseReason = LoseReason.None)
        {
            Seed = seed;
            Turns = turns;
            Score = score;
            HighestRank = highestRank;
            Aces = aces;
            Finished = finished;
            LoseReason = loseReason;
        }
    }
}
