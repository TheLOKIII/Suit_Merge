using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Config;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;
using SuitMerge.Core.Simulation;

namespace SuitMerge.Tests
{
    /// <summary>Bước 4.2 — deck theo level (spec 5.1): script → random có seed, đếm lá, ô gợi ý, Undo.</summary>
    public class LevelDeckTests
    {
        /// <summary>L1 theo spec 12.1.</summary>
        private const string L1 = @"{
  'id': 1, 'goals': [{ 'type': 'CREATE_RANK', 'rank': '4', 'count': 2 }],
  'board': [['2H','2C','.','.'], ['.','.','.','.'], ['.','.','3D','.'], ['.','.','.','.']],
  'deck': { 'size': 12,
    'script': [ { 'card': '2S', 'hint': [0,2] }, { 'card': '3H', 'hint': [1,2] }, '2D','2C','3S','2H','3C','2D','3H','2S' ],
    'random': { 'rankRange': ['2','3'], 'seed': 1001 } }
}";

        private static LevelData Load(string json)
        {
            LevelLoadResult r = LevelLoader.Load(json.Replace('\'', '"'));
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            return r.Level;
        }

        /// <summary>Level 5×5 trống, deck chỉ có phần random.</summary>
        private static LevelData RandomLevel(int size, uint seed, string random = "'rankRange':['2','5']")
        {
            const string row = "['.','.','.','.','.']";
            return Load($"{{'id':20,'goals':[{{'type':'SCORE','score':100}}],'board':[{row},{row},{row},{row},{row}]," +
                        $"'deck':{{'size':{size},'random':{{{random},'seed':{seed}}}}}}}");
        }

        private static GameSession Start(LevelData level, GameConfig config = null)
        {
            var s = new GameSession(config);
            s.StartLevel(level);
            return s;
        }

        private static List<string> Sequence(DeckData deck) => LevelDeck.Preview(deck).Select(p => p.ToString()).ToList();

        // ------------------------------------------------------------------ Thứ tự + đếm lá

        [Test]
        public void L1_StartsWithScriptAndHint_CounterExcludesHand()
        {
            GameSession s = Start(Load(L1));
            Assert.AreEqual(GameMode.Level, s.Mode);
            Assert.AreEqual("2S", s.CurrentPiece.ToString());
            Assert.AreEqual(new CellPos(0, 2), s.CurrentPiece.Hint.Single());
            Assert.AreEqual("3H", s.NextPiece.ToString());
            Assert.AreEqual(new CellPos(1, 2), s.NextPiece.Hint.Single());
            Assert.AreEqual(10, s.CardsLeftInDeck, "12 lá − lá trên tay − lá UP NEXT");
            Assert.IsNotNull(s.CurrentLevel);
            Assert.AreEqual("2H", s.Board.GetCard(new CellPos(0, 0)).ToString());
        }

        [Test]
        public void ScriptInOrder_ThenRandom_ThenOutOfCards()
        {
            // Bước 4.3: L1 thật sẽ THẮNG sớm (đủ 2 lá 4) → thay mục tiêu bằng điểm không thể đạt để chơi tới lá cuối
            LevelData level = Load(L1.Replace("{ 'type': 'CREATE_RANK', 'rank': '4', 'count': 2 }", "{ 'type': 'SCORE', 'score': 999999 }"));
            List<string> seq = Sequence(level.Deck);
            Assert.AreEqual(12, seq.Count, "Đúng size lá");
            CollectionAssert.AreEqual(new[] { "2S", "3H", "2D", "2C", "3S", "2H", "3C", "2D", "3H", "2S" }, seq.Take(10));
            Assert.IsTrue(seq.Skip(10).All(c => c.StartsWith("2") || c.StartsWith("3")), "Random trong rankRange 2–3");

            GameSession s = Start(level);
            s.PlacePiece(new CellPos(0, 2));   // 2S → merge 2H 2C 2S → 3S
            Assert.AreEqual(9, s.CardsLeftInDeck);
            Assert.AreEqual("3H", s.CurrentPiece.ToString());

            var drawn = new List<string> { "2S", "3H", s.NextPiece.ToString() };
            int guard = 0;
            while (s.Status == GameStatus.Playing && guard++ < 50)
            {
                CellPos target = s.CurrentPiece.HasHint
                    ? s.CurrentPiece.Hint[0]
                    : s.Board.AllPositions().First(p => s.Board.IsPlaceable(p));
                TurnResult r = s.PlacePiece(target);
                Assert.IsTrue(r.Accepted, r.RejectReason.ToString());
                if (s.NextPiece != null) drawn.Add(s.NextPiece.ToString());
            }
            CollectionAssert.AreEqual(seq, drawn, "Rút đúng dãy đã xem trước");
            Assert.AreEqual(0, s.CardsLeftInDeck);
            Assert.AreEqual(LoseReason.OutOfCards, s.LoseReason);
        }

        [Test]
        public void ScriptOnly_NoRandom()
        {
            LevelData l = Load("{'id':2,'goals':[{'type':'SCORE','score':10}],'board':[['.','.'],['.','.']],'deck':{'size':2,'script':['2S','3H']}}");
            CollectionAssert.AreEqual(new[] { "2S", "3H" }, Sequence(l.Deck));
            GameSession s = Start(l);
            Assert.AreEqual(0, s.CardsLeftInDeck);
            Assert.AreEqual("3H", s.NextPiece.ToString());
        }

        [Test]
        public void Endless_HasNoDeckCounter()
        {
            var s = new GameSession();
            s.StartEndless(1);
            Assert.IsNull(s.CardsLeftInDeck);
            Assert.IsNull(s.CurrentLevel);
        }

        // ------------------------------------------------------------------ Seed

        [Test]
        public void SameSeed_SameSequence_DifferentSeed_Differs()
        {
            List<string> a = Sequence(RandomLevel(40, 7).Deck);
            List<string> b = Sequence(RandomLevel(40, 7).Deck);
            List<string> c = Sequence(RandomLevel(40, 8).Deck);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreNotEqual(a, c);
        }

        [Test]
        public void RandomPart_DoesNotDependOnBoard()
        {
            LevelData level = RandomLevel(12, 99);
            GameSession s1 = Start(level);
            GameSession s2 = Start(level);
            var seq1 = new List<string>();
            var seq2 = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                seq1.Add(s1.CurrentPiece.ToString());
                seq2.Add(s2.CurrentPiece.ToString());
                // Hai cách chơi khác nhau: s1 đặt từ góc trên-trái, s2 từ góc dưới-phải
                s1.PlacePiece(s1.Board.AllPositions().First(p => s1.Board.IsPlaceable(p)));
                s2.PlacePiece(s2.Board.AllPositions().Last(p => s2.Board.IsPlaceable(p)));
            }
            CollectionAssert.AreEqual(seq1, seq2);
        }

        [Test]
        public void Restart_GivesSameSequenceAndFreshBoard()
        {
            LevelData level = Load(L1);
            GameSession s = Start(level);
            s.PlacePiece(new CellPos(0, 2));
            s.StartLevel(level);
            Assert.AreEqual("2S", s.CurrentPiece.ToString());
            Assert.AreEqual(10, s.CardsLeftInDeck);
            Assert.AreEqual(0, s.Turn);
            Assert.IsNull(s.Board.GetCard(new CellPos(0, 2)), "Bàn dựng lại từ file");
        }

        // ------------------------------------------------------------------ Trọng số

        [Test]
        public void Weights_FrequenciesMatch()
        {
            // Kiểu L7: rank 4–7 (7 chỉ 10%), ♣ 35%, ♦ 30%
            RandomDeckData random = RandomLevel(1, 77, "'rankRange':['4','7'],'rankWeights':{'7':0.1},'suitWeights':{'C':0.35,'D':0.3}").Deck.Random;
            var rng = new SeededRng(random.Seed);
            const int n = 20000;
            var ranks = new Dictionary<Rank, int>();
            var suits = new Dictionary<Suit, int>();
            for (int i = 0; i < n; i++)
            {
                Card c = LevelDeck.DrawRandomCard(random, rng);
                ranks[c.Rank] = ranks.TryGetValue(c.Rank, out int r) ? r + 1 : 1;
                suits[c.Suit] = suits.TryGetValue(c.Suit, out int k) ? k + 1 : 1;
            }
            Assert.AreEqual(4, ranks.Count, "Chỉ rank 4..7");
            Assert.IsTrue(ranks.Keys.All(x => x >= Rank.Four && x <= Rank.Seven));
            Assert.AreEqual(0.10, ranks[Rank.Seven] / (double)n, 0.015);
            Assert.AreEqual(0.30, ranks[Rank.Four] / (double)n, 0.015);
            Assert.AreEqual(0.35, suits[Suit.Club] / (double)n, 0.015);
            Assert.AreEqual(0.30, suits[Suit.Diamond] / (double)n, 0.015);
            Assert.AreEqual(0.175, suits[Suit.Heart] / (double)n, 0.015);
            Assert.AreEqual(0.175, suits[Suit.Spade] / (double)n, 0.015);
        }

        [Test]
        public void ZeroWeight_NeverDrawn()
        {
            RandomDeckData random = RandomLevel(1, 5, "'rankRange':['2','4'],'suitWeights':{'H':0,'S':0}").Deck.Random;
            var rng = new SeededRng(5);
            for (int i = 0; i < 5000; i++)
            {
                Suit s = LevelDeck.DrawRandomCard(random, rng).Suit;
                Assert.IsTrue(s == Suit.Diamond || s == Suit.Club, "Chất trọng số 0 không bao giờ ra");
            }
        }

        // ------------------------------------------------------------------ Ô gợi ý

        [Test]
        public void Hint_LocksDrop_UntilPlacedOnHint()
        {
            GameSession s = Start(Load(L1));
            string before = s.Board.ToText();

            TurnResult wrong = s.PlacePiece(new CellPos(3, 3));
            Assert.IsFalse(wrong.Accepted);
            Assert.AreEqual(PlaceRejectReason.NotHintCell, wrong.RejectReason);
            Assert.AreEqual(before, s.Board.ToText());
            Assert.AreEqual("2S", s.CurrentPiece.ToString(), "Lá về tay, không mất lượt");
            Assert.AreEqual(0, s.Turn);
            Assert.IsFalse(s.PreviewPiece(new CellPos(3, 3)).Valid);
            Assert.IsTrue(s.PreviewPiece(new CellPos(0, 2)).WillMerge);

            Assert.IsTrue(s.PlacePiece(new CellPos(0, 2)).Accepted);
        }

        [Test]
        public void Hint_FreeDrop_WhenFlagOff()
        {
            GameSession s = Start(Load(L1), new GameConfig { tutorialHintLocksDrop = false });
            Assert.IsTrue(s.PlacePiece(new CellPos(3, 3)).Accepted);
        }

        [Test]
        public void Hint_NotUsedForRandomCards()
        {
            GameSession s = Start(RandomLevel(5, 3));
            Assert.IsFalse(s.CurrentPiece.HasHint);
            Assert.IsTrue(s.PlacePiece(new CellPos(4, 4)).Accepted);
        }

        // ------------------------------------------------------------------ Undo

        [Test]
        public void Undo_RestoresDeckPosition()
        {
            GameSession s = Start(RandomLevel(20, 42));
            s.PlacePiece(new CellPos(0, 0));
            GameSession.Snapshot snap = s.CaptureSnapshot();
            int left = s.CardsLeftInDeck.Value;

            s.PlacePiece(new CellPos(4, 4));
            string drawnAfter = s.NextPiece.ToString();
            s.PlacePiece(new CellPos(4, 0));

            s.RestoreSnapshot(snap);
            Assert.AreEqual(left, s.CardsLeftInDeck);
            s.PlacePiece(new CellPos(4, 4));
            Assert.AreEqual(drawnAfter, s.NextPiece.ToString(), "Undo xong đặt lại → rút đúng lá cũ");
        }

        [Test]
        public void PairInScript_PlayableSinceM45()
        {
            // Bước 4.2 test này khẳng định "khối đôi chưa chơi được (M4.5)" — từ M4.5 phần A khối đôi chơi được
            LevelData l = Load("{'id':13,'goals':[{'type':'SCORE','score':10}],'board':[['.','.'],['.','.']]," +
                               "'deck':{'size':2,'script':[{'pair':['9H','10D']}]}}");
            var s = new GameSession();
            s.StartLevel(l);
            Assert.AreEqual("[9H 10D]", s.CurrentPiece.ToString());
            Assert.IsNull(s.NextPiece);
            Assert.AreEqual(0, s.CardsLeftInDeck, "Khối đôi tiêu 2 lá");
        }

        [Test]
        public void DescribeSequence_ShowsHintsAndRandomPart()
        {
            string text = LevelDeck.DescribeSequence(Load(L1).Deck);
            StringAssert.StartsWith("Dãy lá (seed 1001): 2S→(0,2) 3H→(1,2) 2D 2C 3S 2H 3C 2D 3H 2S | random: ", text);
            Assert.AreEqual(2, text.Split(new[] { "| random:" }, StringSplitOptions.None)[1].Trim().Split(' ').Length);
        }

        // ------------------------------------------------------------------ Bước 4.6: Sandbox "Seed khác" + xem trước lá

        [Test]
        public void SandboxSeed_KeepsScript_OriginalUnchanged()
        {
            LevelData original = Load(L1);
            LevelData other = LevelSimulator.WithDeckSeed(original, 777);
            Assert.AreEqual(1001u, original.Deck.Random.Seed, "Bản gốc giữ seed trong file");
            Assert.AreEqual(777u, other.Deck.Random.Seed);
            Assert.AreEqual(original.Deck.Size, other.Deck.Size);
            CollectionAssert.AreEqual(Sequence(original.Deck).Take(10), Sequence(other.Deck).Take(10), "Đoạn script giữ nguyên");
        }

        [Test]
        public void SandboxSeed_SameSeedSameCards_DifferentSeedDifferentCards()
        {
            LevelData level = RandomLevel(20, 5);
            List<string> Drawn(uint? seed)
            {
                var s = new GameSession();
                s.StartLevel(seed.HasValue ? LevelSimulator.WithDeckSeed(level, seed.Value) : level);
                return new[] { s.CurrentPiece, s.NextPiece }.Concat(s.PeekUpcoming(100)).Select(p => p.ToString()).ToList();
            }

            CollectionAssert.AreEqual(Drawn(null), Drawn(5), "Seed = seed trong file → như không truyền seed");
            CollectionAssert.AreEqual(Drawn(42), Drawn(42), "Cùng seed → cùng dãy lá");
            CollectionAssert.AreNotEqual(Drawn(42), Drawn(43), "Seed khác → dãy lá khác");
            Assert.AreEqual(20, Drawn(42).Count, "Vẫn đúng size");
            Assert.AreEqual(5u, level.Deck.Random.Seed, "Level gốc không bị đổi seed");
        }

        [Test]
        public void PeekUpcoming_ShowsCardsAfterUpNext_WithoutDrawing()
        {
            LevelData level = Load(L1.Replace("{ 'type': 'CREATE_RANK', 'rank': '4', 'count': 2 }", "{ 'type': 'SCORE', 'score': 999999 }"));
            List<string> seq = Sequence(level.Deck);
            GameSession s = Start(level);

            CollectionAssert.AreEqual(seq.Skip(2).Take(5), s.PeekUpcoming(5).Select(p => p.ToString()));
            Assert.AreEqual(10, s.CardsLeftInDeck, "Xem trước không rút lá");
            Assert.AreEqual(10, s.PeekUpcoming(50).Count, "Chỉ còn 10 lá chưa rút");

            s.PlacePiece(new CellPos(0, 2));
            Assert.AreEqual(seq[2], s.NextPiece.ToString(), "Lượt sau vẫn rút đúng lá đã xem trước");
            CollectionAssert.AreEqual(seq.Skip(3).Take(5), s.PeekUpcoming(5).Select(p => p.ToString()));
        }
    }
}
