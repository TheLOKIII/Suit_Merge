using System;
using System.Collections.Generic;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Level
{
    /// <summary>Loại mục tiêu (spec 6). COMBO = nhiều mục trong <see cref="LevelData.Goals"/>.</summary>
    public enum GoalType
    {
        CreateRank,
        Score,
        Ace,
        SuitTrigger,
        SigilTrigger,
        ClearBlocker
    }

    /// <summary>Loại chướng ngại cho goal CLEAR_BLOCKER (spec 8, M6).</summary>
    public enum BlockerType
    {
        None,
        Stone,
        FaceDown,
        Ice,
        Bomb
    }

    /// <summary>Booster (spec 9, M5).</summary>
    public enum BoosterType
    {
        Undo,
        Swap,
        Hammer,
        Joker
    }

    /// <summary>Một mục tiêu. Chỉ các trường đúng loại mới có nghĩa.</summary>
    public sealed class GoalData
    {
        public GoalType Type { get; set; }
        /// <summary>CREATE_RANK: rank cần tạo (3..K).</summary>
        public Rank Rank { get; set; }
        /// <summary>SUIT_TRIGGER: chất cần kích hoạt.</summary>
        public Suit Suit { get; set; }
        /// <summary>CLEAR_BLOCKER: loại chướng ngại.</summary>
        public BlockerType Blocker { get; set; }
        /// <summary>Số lần cần đạt (mọi loại trừ SCORE).</summary>
        public int Count { get; set; }
        /// <summary>SCORE: số điểm cần đạt.</summary>
        public int Score { get; set; }

        /// <summary>Con số cần đạt để hoàn thành (SCORE: điểm; loại khác: số lần).</summary>
        public int Target => Type == GoalType.Score ? Score : Count;
    }

    /// <summary>Một mục trong đoạn scripted của deck: 1 lá (có thể kèm hint) hoặc khối đôi (M4.5).</summary>
    public sealed class DeckScriptEntry
    {
        /// <summary>Lá đơn: 1 lá. Khối đôi: 2 lá theo thứ tự A, B như trong file.</summary>
        public IReadOnlyList<Card> Cards { get; set; }
        /// <summary>Hướng khối đôi (lá đơn luôn H_AB).</summary>
        public PieceOrientation Orientation { get; set; } = PieceOrientation.H_AB;
        /// <summary>Ô gợi ý: lá đơn 1 ô, khối đôi 2 ô (① ②). Rỗng = không gợi ý.</summary>
        public IReadOnlyList<CellPos> Hint { get; set; } = Array.Empty<CellPos>();

        public bool IsPair => Cards.Count == 2;
        public int CardCount => Cards.Count;
    }

    /// <summary>Phần rút ngẫu nhiên của deck (spec 5.1).</summary>
    public sealed class RandomDeckData
    {
        public Rank MinRank { get; set; }
        public Rank MaxRank { get; set; }
        /// <summary>Trọng số ghi trong file. Rank trong khoảng mà không ghi → chia đều phần còn lại.</summary>
        public IReadOnlyDictionary<Rank, double> RankWeights { get; set; } = new Dictionary<Rank, double>();
        /// <summary>Trọng số ghi trong file. Chất không ghi → chia đều phần còn lại.</summary>
        public IReadOnlyDictionary<Suit, double> SuitWeights { get; set; } = new Dictionary<Suit, double>();
        /// <summary>Tỉ lệ lượt rút ra khối đôi (0..1, dùng từ M4.5).</summary>
        public double PairRatio { get; set; }
        public uint Seed { get; set; }

        public static readonly Suit[] AllSuits = { Suit.Heart, Suit.Diamond, Suit.Club, Suit.Spade };

        /// <summary>Các rank có thể rút (MinRank..MaxRank).</summary>
        public IEnumerable<Rank> Ranks()
        {
            for (int r = (int)MinRank; r <= (int)MaxRank; r++) yield return (Rank)r;
        }

        /// <summary>Xác suất thật của rank (đã chia phần còn lại cho rank không ghi).</summary>
        public double RankProbability(Rank rank)
        {
            if (rank < MinRank || rank > MaxRank) return 0;
            return Resolve(RankWeights, Ranks(), rank);
        }

        /// <summary>Xác suất thật của chất (đã chia phần còn lại cho chất không ghi).</summary>
        public double SuitProbability(Suit suit) => Resolve(SuitWeights, AllSuits, suit);

        private static double Resolve<T>(IReadOnlyDictionary<T, double> listed, IEnumerable<T> all, T key)
        {
            if (listed.TryGetValue(key, out double w)) return w;
            double used = 0;
            int unlisted = 0;
            foreach (T k in all)
            {
                if (listed.TryGetValue(k, out double v)) used += v;
                else unlisted++;
            }
            return unlisted == 0 ? 0 : Math.Max(0, 1 - used) / unlisted;
        }
    }

    public sealed class DeckData
    {
        /// <summary>Tổng số lá của deck (gồm cả đoạn script).</summary>
        public int Size { get; set; }
        public IReadOnlyList<DeckScriptEntry> Script { get; set; } = Array.Empty<DeckScriptEntry>();
        /// <summary>null = không có phần ngẫu nhiên (deck chỉ gồm script).</summary>
        public RandomDeckData Random { get; set; }

        /// <summary>Số lá trong đoạn script (khối đôi tính 2).</summary>
        public int ScriptCardCount
        {
            get
            {
                int n = 0;
                foreach (DeckScriptEntry e in Script) n += e.CardCount;
                return n;
            }
        }

        /// <summary>Số lá rút ngẫu nhiên sau khi hết script.</summary>
        public int RandomCardCount => Math.Max(0, Size - ScriptCardCount);
    }

    /// <summary>Chữ song ngữ ghi thẳng trong file level.</summary>
    public sealed class LocalizedText
    {
        public string Vi { get; set; }
        public string En { get; set; }

        /// <summary>Chữ theo ngôn ngữ ("vi" / "en"); thiếu bản tiếng Anh thì dùng tiếng Việt.</summary>
        public string Get(string language) =>
            language == "en" && !string.IsNullOrEmpty(En) ? En : Vi;

        public override string ToString() => Vi;
    }

    public sealed class TutorialStep
    {
        /// <summary>Lượt hiện gợi ý (đếm từ 1).</summary>
        public int Turn { get; set; }
        public LocalizedText Text { get; set; }
        /// <summary>Ô bàn tay chỉ vào; null = không có bàn tay.</summary>
        public CellPos? HandPointer { get; set; }
    }

    public sealed class RewardData
    {
        public int Coins { get; set; }
        public bool Chest { get; set; }
    }

    /// <summary>
    /// Dữ liệu một level đọc từ file JSON (spec 12.1). Chỉ chứa dữ liệu — luật nằm ở GameSession / Rules.
    /// Đọc bằng <see cref="LevelLoader"/>.
    /// </summary>
    public sealed class LevelData
    {
        public int Id { get; set; }
        public int Phase { get; set; }
        /// <summary>Loại level theo LDD: Onboarding, Normal, Relief, Peak…</summary>
        public string Type { get; set; }
        public int Difficulty { get; set; }
        public IReadOnlyList<GoalData> Goals { get; set; } = Array.Empty<GoalData>();
        public IReadOnlyList<Suit> EnabledSuitEffects { get; set; } = Array.Empty<Suit>();
        public IReadOnlyList<BoosterType> UnlockedBoosters { get; set; } = Array.Empty<BoosterType>();
        /// <summary>Token bàn theo hàng (spec 12.1). Dùng <see cref="CreateBoard"/> để có bàn mới.</summary>
        public IReadOnlyList<IReadOnlyList<string>> BoardTokens { get; set; } = Array.Empty<IReadOnlyList<string>>();
        public DeckData Deck { get; set; } = new DeckData();
        public IReadOnlyList<TutorialStep> Tutorial { get; set; } = Array.Empty<TutorialStep>();
        public RewardData Reward { get; set; } = new RewardData();

        public int Rows => BoardTokens.Count;
        public int Cols => BoardTokens.Count > 0 ? BoardTokens[0].Count : 0;

        /// <summary>Dựng bàn MỚI từ token (mỗi lần gọi là 1 bàn riêng — dùng khi bắt đầu / chơi lại level).</summary>
        public Board CreateBoard()
        {
            var rows = new string[BoardTokens.Count][];
            for (int r = 0; r < rows.Length; r++)
            {
                rows[r] = new string[BoardTokens[r].Count];
                for (int c = 0; c < rows[r].Length; c++) rows[r][c] = BoardTokens[r][c];
            }
            return Board.FromTokens(rows);
        }
    }
}
