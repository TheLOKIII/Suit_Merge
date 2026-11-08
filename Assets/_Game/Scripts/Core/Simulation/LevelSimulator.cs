using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SuitMerge.Core.Config;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Core.Simulation
{
    /// <summary>Kết quả 1 ván Level do bot chơi.</summary>
    public sealed class LevelRunStats
    {
        public uint DeckSeed { get; }
        public bool Won { get; }
        public int Turns { get; }
        public int Score { get; }
        /// <summary>Số lá chưa dùng khi ván kết thúc (deck + lá trên tay + UP NEXT) — "lá dư" khi thắng.</summary>
        public int CardsLeft { get; }
        public LoseReason LoseReason { get; }

        public LevelRunStats(uint deckSeed, bool won, int turns, int score, int cardsLeft, LoseReason loseReason)
        {
            DeckSeed = deckSeed;
            Won = won;
            Turns = turns;
            Score = score;
            CardsLeft = cardsLeft;
            LoseReason = loseReason;
        }
    }

    /// <summary>Tổng hợp nhiều ván của 1 bot trên 1 level với 1 seed deck.</summary>
    public sealed class LevelSimulationResult
    {
        public LevelData Level { get; }
        public BotKind Bot { get; }
        public uint DeckSeed { get; }
        public IReadOnlyList<LevelRunStats> Runs { get; }

        public LevelSimulationResult(LevelData level, BotKind bot, uint deckSeed, IReadOnlyList<LevelRunStats> runs)
        {
            Level = level;
            Bot = bot;
            DeckSeed = deckSeed;
            Runs = runs;
        }

        public int Games => Runs.Count;
        public double WinRate => Games == 0 ? 0 : Runs.Count(r => r.Won) / (double)Games;
        public double AverageTurns => Games == 0 ? 0 : Runs.Average(r => r.Turns);
        /// <summary>Lá dư trung bình trong các ván THẮNG (NaN nếu không thắng ván nào).</summary>
        public double AverageCardsLeftWhenWon => Runs.Any(r => r.Won) ? Runs.Where(r => r.Won).Average(r => r.CardsLeft) : double.NaN;
        public double LoseRate(LoseReason reason) => Games == 0 ? 0 : Runs.Count(r => !r.Won && r.LoseReason == reason) / (double)Games;
    }

    /// <summary>
    /// Cho máy tự chơi Level (bước 4.4) để đo độ khó theo seed deck — LDD: "mỗi level playtest ≥3 seed, chọn seed có
    /// win rate gần target". Chỉ DÙNG GameSession — không sửa luật. Bot luôn tuân theo ô gợi ý (lượt tutorial).
    /// Cùng level + seed deck + seed bot → cùng kết quả.
    /// </summary>
    public static class LevelSimulator
    {
        /// <summary>Giới hạn an toàn (deck level hữu hạn nên thực tế không bao giờ chạm tới).</summary>
        public const int MaxTurns = 500;

        public static LevelSimulationResult Run(LevelData level, BotKind bot, int games, GameConfig config)
        {
            var runs = new List<LevelRunStats>(games);
            for (int i = 0; i < games; i++) runs.Add(PlayOne(level, bot, (uint)(i + 1), config));
            return new LevelSimulationResult(level, bot, DeckSeedOf(level), runs);
        }

        public static LevelRunStats PlayOne(LevelData level, BotKind bot, uint botSeed, GameConfig config)
        {
            var session = new GameSession(config);
            session.StartLevel(level);
            var rng = new SeededRng(botSeed ^ 0x9E3779B9u);

            while (session.Status == GameStatus.Playing && session.Turn < MaxTurns)
            {
                if (session.CurrentPiece.IsPair)
                {
                    List<PairBot.Move> moves = PairBot.ValidMoves(session);
                    if (moves.Count == 0) break; // không xảy ra: không còn chỗ cho khối đôi thì ván đã THUA
                    PairBot.Move move = bot == BotKind.Greedy ? PairBot.ChooseGreedy(moves, rng) : moves[rng.NextInt(0, moves.Count)];
                    TurnResult played = PairBot.Play(session, move);
                    if (!played.Accepted) throw new InvalidOperationException($"Bot đặt khối đôi vào ô không hợp lệ {move.Cell}: {played.RejectReason}");
                    continue;
                }

                List<CellPos> cells = ValidCells(session);
                if (cells.Count == 0) break; // không xảy ra: còn ô trống thì luôn có ô hợp lệ (trừ hint trỏ vào ô đã có lá)
                CellPos target = bot == BotKind.Greedy ? ChooseGreedy(session, cells, rng) : cells[rng.NextInt(0, cells.Count)];
                TurnResult result = session.PlacePiece(target);
                if (!result.Accepted) throw new InvalidOperationException($"Bot đặt vào ô không hợp lệ {target}: {result.RejectReason}");
            }

            int left = (session.CardsLeftInDeck ?? 0) + (session.CurrentPiece?.Count ?? 0) + (session.NextPiece?.Count ?? 0);
            return new LevelRunStats(DeckSeedOf(level), session.IsWon, session.Turn, session.Score, left, session.LoseReason);
        }

        /// <summary>Bản sao level với seed deck khác (để thử seed thay thế). Không sửa level gốc.</summary>
        public static LevelData WithDeckSeed(LevelData level, uint seed)
        {
            RandomDeckData r = level.Deck.Random;
            if (r == null) return level;
            var random = new RandomDeckData
            {
                MinRank = r.MinRank,
                MaxRank = r.MaxRank,
                RankWeights = r.RankWeights,
                SuitWeights = r.SuitWeights,
                PairRatio = r.PairRatio,
                Seed = seed
            };
            return new LevelData
            {
                Id = level.Id,
                Phase = level.Phase,
                Type = level.Type,
                Difficulty = level.Difficulty,
                Goals = level.Goals,
                EnabledSuitEffects = level.EnabledSuitEffects,
                UnlockedBoosters = level.UnlockedBoosters,
                BoardTokens = level.BoardTokens,
                Deck = new DeckData { Size = level.Deck.Size, Script = level.Deck.Script, Random = random },
                Tutorial = level.Tutorial,
                Reward = level.Reward
            };
        }

        private static uint DeckSeedOf(LevelData level) => level.Deck.Random?.Seed ?? 0;

        /// <summary>Ô trống mà thả vào được (tôn trọng ô gợi ý của lượt tutorial).</summary>
        private static List<CellPos> ValidCells(GameSession session)
        {
            var cells = new List<CellPos>();
            foreach (CellPos p in session.Board.AllPositions())
                if (session.Board.IsPlaceable(p) && session.PreviewPiece(p).Valid) cells.Add(p);
            return cells;
        }

        /// <summary>Giống bot Tham lam của Endless: nhiều điểm nhất, hoà thì nhiều lá kề cùng rank nhất, còn hoà thì ngẫu nhiên.</summary>
        private static CellPos ChooseGreedy(GameSession session, List<CellPos> cells, SeededRng rng)
        {
            Rank current = session.CurrentCard.Rank;
            var best = new List<CellPos>();
            int bestScore = int.MinValue;
            int bestNeighbors = int.MinValue;
            foreach (CellPos p in cells)
            {
                int score = session.PreviewPiece(p).ScoreGain;
                int neighbors = 0;
                foreach (CellPos n in p.Neighbors4())
                {
                    Card c = session.Board.GetCard(n);
                    if (c != null && c.Rank == current) neighbors++;
                }
                if (score > bestScore || (score == bestScore && neighbors > bestNeighbors))
                {
                    best.Clear();
                    bestScore = score;
                    bestNeighbors = neighbors;
                }
                if (score == bestScore && neighbors == bestNeighbors) best.Add(p);
            }
            return best[rng.NextInt(0, best.Count)];
        }

        // ------------------------------------------------------------------ Báo cáo

        /// <summary>Bảng markdown cho Docs/Playtest/level-simulation.md.</summary>
        /// <param name="main">Kết quả với seed trong file (mỗi level: Ngẫu nhiên + Tham lam).</param>
        /// <param name="alternatives">Kết quả với các seed thay thế.</param>
        public static string ToMarkdown(IReadOnlyList<LevelSimulationResult> main, IReadOnlyList<LevelSimulationResult> alternatives,
            DateTime now)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("# Mô phỏng Level — tỉ lệ thắng theo seed");
            sb.AppendLine();
            sb.AppendLine($"> Tạo lúc {now.ToString("dd/MM/yyyy HH:mm", ci)} · menu **Suit Merge → Mô phỏng Level** · file này được TẠO LẠI mỗi lần chạy.");
            sb.AppendLine();
            int randomGames = main.FirstOrDefault(r => r.Bot == BotKind.Random)?.Games ?? 0;
            int greedyGames = main.FirstOrDefault(r => r.Bot == BotKind.Greedy)?.Games ?? 0;
            sb.AppendLine($"Mỗi level: bot **Ngẫu nhiên** chơi {randomGames} ván, bot **Tham lam** chơi {greedyGames} ván (cùng dãy lá theo seed; " +
                          "các ván khác nhau ở lựa chọn của bot). Bot luôn thả đúng ô gợi ý ở lượt tutorial.");
            sb.AppendLine();
            sb.AppendLine("## Seed trong file");
            sb.AppendLine("| Lv | Mục tiêu | Seed | Ngẫu nhiên: thắng | Tham lam: thắng | Tham lam: số lượt TB | Tham lam: lá dư TB khi thắng | Tham lam thua vì bàn đầy / hết lá |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (IGrouping<int, LevelSimulationResult> lv in main.GroupBy(r => r.Level.Id).OrderBy(g => g.Key))
            {
                LevelSimulationResult rnd = lv.FirstOrDefault(r => r.Bot == BotKind.Random);
                LevelSimulationResult greedy = lv.FirstOrDefault(r => r.Bot == BotKind.Greedy);
                LevelData level = (greedy ?? rnd).Level;
                string goals = string.Join(" + ", level.Goals.Select(LevelLoader.Describe));
                string cardsLeft = greedy == null || double.IsNaN(greedy.AverageCardsLeftWhenWon) ? "—" : greedy.AverageCardsLeftWhenWon.ToString("0.0", ci);
                string loses = greedy == null ? "—" : $"{Percent(greedy.LoseRate(LoseReason.BoardFull))} / {Percent(greedy.LoseRate(LoseReason.OutOfCards))}";
                sb.AppendLine($"| {lv.Key} | {goals} | {(greedy ?? rnd).DeckSeed} | {(rnd == null ? "—" : Percent(rnd.WinRate))} | " +
                              $"{(greedy == null ? "—" : Percent(greedy.WinRate))} | {(greedy == null ? "—" : greedy.AverageTurns.ToString("0.0", ci))} | {cardsLeft} | {loses} |");
            }
            sb.AppendLine();

            if (alternatives.Count > 0)
            {
                sb.AppendLine("## Seed thay thế (LDD: thử ≥ 3 seed)");
                sb.AppendLine("| Lv | Seed | Ngẫu nhiên: thắng | Tham lam: thắng |");
                sb.AppendLine("|---|---|---|---|");
                foreach (IGrouping<(int, uint), LevelSimulationResult> g in alternatives.GroupBy(r => (r.Level.Id, r.DeckSeed)).OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2))
                {
                    LevelSimulationResult rnd = g.FirstOrDefault(r => r.Bot == BotKind.Random);
                    LevelSimulationResult greedy = g.FirstOrDefault(r => r.Bot == BotKind.Greedy);
                    sb.AppendLine($"| {g.Key.Item1} | {g.Key.Item2} | {(rnd == null ? "—" : Percent(rnd.WinRate))} | {(greedy == null ? "—" : Percent(greedy.WinRate))} |");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## Cách đọc");
            sb.AppendLine("- **Ngẫu nhiên** ≈ người chơi không suy nghĩ; **Tham lam** ≈ người chơi khá nhưng chỉ nhìn 1 nước (không biết chừa chỗ, không thấy bẫy). Người thật thường giỏi hơn bot Tham lam ở level có bẫy (L10).");
            sb.AppendLine("- **Lá dư khi thắng** = số lá chưa dùng lúc thắng — nhiều lá dư = level rộng rãi; gần 0 = sát nút.");
            sb.AppendLine("- Đổi `seed` trong `Data/Levels/level_0xx.json` → chạy lại menu để so sánh. Menu **Kiểm tra file level** in dãy lá của seed.");
            sb.AppendLine("- Spec chưa có tỉ lệ thắng mục tiêu theo độ khó → bảng chỉ báo số liệu, KHÔNG tự đổi seed / độ khó.");
            return sb.ToString();
        }

        private static string Percent(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
