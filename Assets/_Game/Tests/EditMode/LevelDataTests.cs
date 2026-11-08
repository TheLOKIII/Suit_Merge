using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;
using SuitMerge.Core.Simulation;

namespace SuitMerge.Tests
{
    /// <summary>
    /// Bước 4.4 — dữ liệu L1–L10 (Data/Levels) + kiểm chứng lời giải spec 12.2 / test bắt buộc spec 13.3.
    /// Số liệu kỳ vọng tính từ công thức điểm: scoreBase 10 × rank mới × N × (1 + 0.5 × (bước chain − 1)), ♦ ×3, ♠ +rank×10 mỗi lá bị xoá.
    /// </summary>
    public class LevelDataTests
    {
        private static string LevelPath(int id)
        {
            string relative = $"Assets/_Game/Data/Levels/level_{id:000}.json";
            var roots = new List<string> { Directory.GetCurrentDirectory() };
            string env = Environment.GetEnvironmentVariable("SUITMERGE_PROJECT");
            if (!string.IsNullOrEmpty(env)) roots.Add(env);
            return roots.Select(r => Path.Combine(r, relative)).FirstOrDefault(File.Exists);
        }

        private static LevelData Load(int id)
        {
            string path = LevelPath(id);
            if (path == null) Assert.Inconclusive($"Không tìm thấy level_{id:000}.json (chạy từ thư mục project Unity).");
            LevelLoadResult r = LevelLoader.Load(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            return r.Level;
        }

        private static GameSession Start(int id)
        {
            var s = new GameSession();
            s.StartLevel(Load(id));
            return s;
        }

        private static TurnResult Place(GameSession s, int r, int c)
        {
            TurnResult t = s.PlacePiece(new CellPos(r, c));
            Assert.IsTrue(t.Accepted, $"Đặt ({r},{c}) bị từ chối: {t.RejectReason}");
            return t;
        }

        private static string At(GameSession s, int r, int c) => s.Board.GetCard(new CellPos(r, c))?.ToString() ?? ".";

        // ------------------------------------------------------------------ Cả 10 file

        [Test]
        public void AllTenLevels_LoadClean_AndMatchRoadmap()
        {
            // spec 12.3: (deck, số mục tiêu, chất mở)
            var expected = new Dictionary<int, (int deck, int goals, string suits)>
            {
                { 1, (12, 1, "") }, { 2, (14, 1, "") }, { 3, (18, 1, "D") }, { 4, (24, 1, "D") }, { 5, (22, 1, "D") },
                { 6, (20, 1, "DC") }, { 7, (30, 1, "DC") }, { 8, (20, 1, "DCS") }, { 9, (28, 2, "DCS") }, { 10, (26, 1, "DCS") }
            };
            foreach (KeyValuePair<int, (int deck, int goals, string suits)> e in expected)
            {
                string path = LevelPath(e.Key);
                if (path == null) Assert.Inconclusive("Không tìm thấy thư mục Data/Levels.");
                LevelLoadResult r = LevelLoader.Load(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
                Assert.IsTrue(r.Ok, $"L{e.Key}: " + string.Join("\n", r.Errors));
                Assert.AreEqual(0, r.Warnings.Count, $"L{e.Key}: " + string.Join("\n", r.Warnings));
                Assert.AreEqual(e.Key, r.Level.Id);
                Assert.AreEqual(e.Value.deck, r.Level.Deck.Size, $"L{e.Key} deck");
                Assert.AreEqual(e.Value.goals, r.Level.Goals.Count, $"L{e.Key} goals");
                Assert.AreEqual(e.Value.suits, string.Concat(r.Level.EnabledSuitEffects.Select(x => x.ToCode())), $"L{e.Key} suits");
                Assert.AreEqual(e.Key >= 4, r.Level.UnlockedBoosters.Contains(BoosterType.Undo), $"L{e.Key} Undo mở từ L4");
            }
            Assert.AreEqual(1001u, Load(1).Deck.Random.Seed);
            Assert.AreEqual(40, Load(10).Reward.Coins);
            Assert.IsTrue(Load(10).Reward.Chest);
        }

        // ------------------------------------------------------------------ Lời giải spec 12.2

        [Test]
        public void L1_Script_MakesFirstFour()
        {
            GameSession s = Start(1);
            Place(s, 0, 2);
            Assert.AreEqual("3S", At(s, 0, 2), "2♥2♣2♠ → 3♠");
            Place(s, 1, 2);
            Assert.AreEqual("4H", At(s, 1, 2), "3♠3♥3♦ cột → 4");
            Assert.AreEqual(".", At(s, 0, 2));
            Assert.AreEqual(".", At(s, 2, 2));
            Assert.AreEqual(1, s.Goals[0].Current);
        }

        [Test]
        public void L2_Script_ComboTwo_Makes4D()
        {
            GameSession s = Start(2);
            TurnResult t = Place(s, 1, 1);
            Assert.AreEqual(2, t.MergeCount, "Combo ×2");
            Assert.AreEqual("4D", At(s, 1, 1));
            Assert.AreEqual(90 + 180, t.ScoreGained, "3: 10×3×3 = 90 · 4: 10×4×3×1.5 = 180");
            Assert.AreEqual("4H", At(s, 3, 2), "Cặp 4♥4♣ vẫn nằm yên");
            Assert.AreEqual("4C", At(s, 3, 3));
            Assert.AreEqual(0, s.Goals[0].Current, "Lá 4 sinh ở (1,1) không kề cặp 4 → chưa có lá 5");
        }

        [Test]
        public void L3_Script_DiamondTriple_450()
        {
            GameSession s = Start(3);
            TurnResult t = Place(s, 2, 2);
            Assert.AreEqual("5D", At(s, 2, 2));
            Assert.AreEqual(450, t.ScoreGained, "spec: 450đ (10×5×3 ×3 ♦)");
            Assert.AreEqual(1, t.SuitActivations(Suit.Diamond));
            Assert.AreEqual(450, s.Goals[0].Current);
        }

        [Test]
        public void L6_Script_ClubSpawn_ThenSeven()
        {
            GameSession s = Start(6);
            TurnResult t1 = Place(s, 1, 3);
            Assert.AreEqual("6C", At(s, 1, 3));
            Assert.AreEqual("6C", At(s, 0, 3), "Lá 6♣ sinh ra ở ô (0,3) — ô * trong spec");
            Assert.AreEqual(1, t1.SuitActivations(Suit.Club));

            Place(s, 2, 3);                                    // 6♣ 6♣ 6♥ cột → 7
            Assert.AreEqual(Rank.Seven, s.Board.GetCard(new CellPos(2, 3)).Rank);
            Assert.AreEqual(".", At(s, 0, 3));
            Assert.AreEqual(".", At(s, 1, 3));
            Assert.AreEqual(1, s.Goals[0].Current, "Tạo 2 lá 7: 1/2");
            Assert.AreEqual(180 + 210, s.Score);
        }

        [Test]
        public void L8_Script_SpadeClearsSix_210Plus190()
        {
            GameSession s = Start(8);
            TurnResult t = Place(s, 2, 2);
            Assert.AreEqual("7S", At(s, 2, 2));
            SpadeClearEvent clear = t.Events.OfType<SpadeClearEvent>().Single();
            Assert.AreEqual(6, clear.ClearedCells.Count, "Xoá 6 lá quanh");
            Assert.AreEqual(190, clear.Bonus, "(3+4+2+5+2+3) × 10");
            Assert.AreEqual(210 + 190, t.ScoreGained);
            Assert.AreEqual(1, s.Goals[0].Current, "Kích hoạt ♠ 1/2");
        }

        [Test]
        public void L9_RouteB_ClubChain_ThenNine_1050()
        {
            GameSession s = Start(9);
            s.SetHand(Card.Parse("6C"), Card.Parse("8S"));

            TurnResult t1 = Place(s, 2, 2);
            Assert.AreEqual(2, t1.MergeCount);
            Assert.AreEqual("8C", At(s, 2, 2), "7♣ + 7♣ sinh ra + 7♠ 7♠ → 8♣");
            Assert.AreEqual(".", At(s, 1, 2), "Ô (1,2) trống lại (lá ♣ sinh ra bị cuốn vào chain)");
            Assert.AreEqual(210 + 480, t1.ScoreGained);

            TurnResult t2 = Place(s, 1, 2);                    // 8♥ 8 8♣ 8♦ → 9
            Assert.AreEqual(Rank.Nine, s.Board.GetCard(new CellPos(1, 2)).Rank);
            Assert.AreEqual(360, t2.ScoreGained);
            Assert.AreEqual(1050, s.Score, "spec: 210 + 480 + 360 = 1,050");
            Assert.IsTrue(s.Goals[0].IsDone, "Tạo 1 lá 9 ✓");
            Assert.IsFalse(s.Goals[1].IsDone, "Đạt 2,800 điểm chưa");
            Assert.AreEqual(GameStatus.Playing, s.Status);
        }

        [Test]
        public void L9_RouteA_SpadeClear_NoNineYet()
        {
            GameSession s = Start(9);
            s.SetHand(Card.Parse("7S"), Card.Parse("2H"));
            TurnResult t = Place(s, 2, 2);
            Assert.AreEqual("8S", At(s, 2, 2));
            Assert.AreEqual(1, t.Events.OfType<SpadeClearEvent>().Count());
            Assert.AreEqual(0, s.Goals[0].Current);
        }

        [Test]
        public void L10_Seed_FirstFourAre8899_AndSolutionMakesJack()
        {
            LevelData level = Load(10);
            List<Rank> first4 = LevelDeck.Preview(level.Deck).Take(4).Select(p => p.First.Rank).ToList();
            CollectionAssert.AreEqual(new[] { Rank.Eight, Rank.Eight, Rank.Nine, Rank.Nine }, first4,
                "Seed L10 phải cho 4 lá đầu 8, 8, 9, 9 (quyết định 18/10/2026)");

            var s = new GameSession();
            s.StartLevel(level);
            Place(s, 0, 1);
            Place(s, 0, 2);
            Assert.AreEqual(Rank.Nine, s.Board.GetCard(new CellPos(0, 2)).Rank, "8 8 + 8♦ → 9 tại (0,2)");
            Place(s, 1, 2);
            Place(s, 2, 2);                                    // 9 9 9 → 10 → chain 10♥ 10 10♠ → J
            Assert.AreEqual(Rank.Jack, s.Board.GetCard(new CellPos(2, 2)).Rank);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void L10_Trap_NineStuckInCenter()
        {
            GameSession s = Start(10);
            s.SetHand(Card.Parse("8H"), Card.Parse("8S"));
            Place(s, 2, 2);                                    // 8♦ 8 8♣ cột → 9 chiếm ô giữa
            Assert.AreEqual(Rank.Nine, s.Board.GetCard(new CellPos(2, 2)).Rank);
            Assert.AreEqual("10H", At(s, 2, 1));
            Assert.AreEqual("10S", At(s, 2, 3));
        }

        // ------------------------------------------------------------------ Mô phỏng Level

        [Test]
        public void Simulator_Deterministic_AndAlwaysFinishes()
        {
            LevelData level = Load(4);
            LevelSimulationResult a = LevelSimulator.Run(level, BotKind.Greedy, 5, null);
            LevelSimulationResult b = LevelSimulator.Run(level, BotKind.Greedy, 5, null);
            CollectionAssert.AreEqual(a.Runs.Select(r => (r.Won, r.Turns, r.Score)), b.Runs.Select(r => (r.Won, r.Turns, r.Score)));
            Assert.IsTrue(a.Runs.All(r => r.Won || r.LoseReason != LoseReason.None), "Ván nào cũng kết thúc");
        }

        [Test]
        public void Simulator_RespectsHints()
        {
            // L1 có 2 lượt gợi ý: bot thả sai sẽ bị từ chối → PlayOne ném lỗi
            LevelData level = Load(1);
            Assert.DoesNotThrow(() => LevelSimulator.Run(level, BotKind.Random, 20, null));
            Assert.DoesNotThrow(() => LevelSimulator.Run(level, BotKind.Greedy, 5, null));
        }

        [Test]
        public void Simulator_WithDeckSeed_DoesNotChangeOriginal()
        {
            LevelData level = Load(5);
            LevelData alt = LevelSimulator.WithDeckSeed(level, 12345);
            Assert.AreEqual(5001u, level.Deck.Random.Seed);
            Assert.AreEqual(12345u, alt.Deck.Random.Seed);
            CollectionAssert.AreNotEqual(LevelDeck.Preview(level.Deck).Select(p => p.ToString()),
                LevelDeck.Preview(alt.Deck).Select(p => p.ToString()));
        }
    }
}
