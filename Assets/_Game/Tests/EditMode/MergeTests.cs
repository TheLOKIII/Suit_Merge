using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Config;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Tests
{
    /// <summary>
    /// Merge cơ bản + chain + điểm + Ace (spec 3.1–3.4, 4.1, 4.6). Chưa có hiệu ứng chất.
    /// </summary>
    public class MergeTests
    {
        private TurnResolver _turn;

        [SetUp]
        public void SetUp()
        {
            _turn = new TurnResolver(GameConfig.Default);
        }

        private static GameState State(string boardText) => new GameState(Board.FromText(boardText));

        private static string CardAt(GameState s, int r, int c) => s.Board.GetCard(new CellPos(r, c))?.ToString() ?? ".";

        private TurnResult Place(GameState s, string card, int r, int c) =>
            _turn.PlaceCard(s, new CellPos(r, c), Card.Parse(card));

        // ---------- Spec T1 ----------
        [Test]
        public void T1_ThreeTwos_MergeIntoThreeOfAnchorSuit()
        {
            GameState s = State(@"
                2H 2C .  .
                .  .  .  .
                .  .  3D .
                .  .  .  .");
            TurnResult r = Place(s, "2S", 0, 2);

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual("3S", CardAt(s, 0, 2), "Lá kết quả: rank+1, chất lá neo (♠)");
            Assert.AreEqual(".", CardAt(s, 0, 0));
            Assert.AreEqual(".", CardAt(s, 0, 1));
            Assert.AreEqual(90, s.Score);
            Assert.AreEqual(1, r.MergeCount);
        }

        // ---------- Spec T2 (Level 2) ----------
        [Test]
        public void T2_Level2_MergeThenChain()
        {
            GameState s = State(@"
                .  2H .  .
                2C .  3D .
                .  3S .  .
                .  .  4H 4C");
            TurnResult r = Place(s, "2D", 1, 1);

            Assert.AreEqual("4D", CardAt(s, 1, 1));
            Assert.AreEqual(270, s.Score, "90 (bước 1) + 180 (bước 2, ×1.5)");
            Assert.AreEqual(2, r.MergeCount);
            foreach (var (row, col) in new[] { (0, 1), (1, 0), (1, 2), (2, 1) })
                Assert.AreEqual(".", CardAt(s, row, col), $"Ô ({row},{col}) phải trống");
            Assert.AreEqual("4H", CardAt(s, 3, 2), "Cặp 4♥4♣ không bị đụng tới");
            Assert.AreEqual("4C", CardAt(s, 3, 3));

            var created = r.Events.OfType<CardCreatedEvent>().ToList();
            Assert.AreEqual(2, created.Count);
            Assert.AreEqual("3D", created[0].Card.ToString());
            Assert.AreEqual(90, created[0].ScoreGained);
            Assert.AreEqual("4D", created[1].Card.ToString());
            Assert.AreEqual(180, created[1].ScoreGained);
        }

        [Test]
        public void T2_EventOrder()
        {
            GameState s = State(@"
                .  2H .  .
                2C .  3D .
                .  3S .  .
                .  .  4H 4C");
            TurnResult r = Place(s, "2D", 1, 1);

            string[] types = r.Events.Select(e => e.GetType().Name).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                nameof(CardPlacedEvent),
                nameof(MergeStartedEvent), nameof(CardCreatedEvent), nameof(ScoreChangedEvent),
                nameof(MergeStartedEvent), nameof(ChainStepEvent), nameof(CardCreatedEvent), nameof(ScoreChangedEvent)
            }, types);
            Assert.AreEqual(2, r.Events.OfType<ChainStepEvent>().Single().Step);
        }

        // ---------- Level 1 script (spec 12.2) ----------
        [Test]
        public void Level1_Script_TwoMergesCreateFour()
        {
            GameState s = State(@"
                2H 2C .  .
                .  .  .  .
                .  .  3D .
                .  .  .  .");
            Place(s, "2S", 0, 2); // → 3♠ tại (0,2)
            Place(s, "3H", 1, 2); // cột 3♠ 3♥ 3♦ → 4♥
            Assert.AreEqual("4H", CardAt(s, 1, 2));
            Assert.AreEqual(".", CardAt(s, 0, 2));
            Assert.AreEqual(".", CardAt(s, 2, 2));
            Assert.AreEqual(90 + 120, s.Score);
            Assert.AreEqual(2, s.Turn);
        }

        // ---------- Chain 3 bước ----------
        [Test]
        public void Chain_ThreeSteps_MultipliersOneOneHalfTwo()
        {
            GameState s = State(@"
                .  2H .  .  .
                2C .  3S 3D .
                .  4H .  .  .
                .  4D .  .  .
                .  .  .  .  .");
            TurnResult r = Place(s, "2S", 1, 1);

            Assert.AreEqual("5S", CardAt(s, 1, 1));
            Assert.AreEqual(3, r.MergeCount);
            int[] gains = r.Events.OfType<CardCreatedEvent>().Select(e => e.ScoreGained).ToArray();
            CollectionAssert.AreEqual(new[] { 90, 180, 300 }, gains, "3×3×10×1 · 4×3×10×1.5 · 5×3×10×2");
            Assert.AreEqual(570, s.Score);
        }

        [Test]
        public void GroupOfFour_UsesNEqualsFour()
        {
            GameState s = State(@"
                .  2H .
                2C .  2D
                .  .  .");
            Place(s, "2S", 1, 1);
            Assert.AreEqual("3S", CardAt(s, 1, 1));
            Assert.AreEqual(120, s.Score, "3 × 4 × 10");
        }

        [Test]
        public void NoMerge_CardStays_NoScore()
        {
            GameState s = State("2H . .");
            TurnResult r = Place(s, "2S", 0, 1);
            Assert.IsTrue(r.Accepted);
            Assert.AreEqual("2S", CardAt(s, 0, 1));
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(0, r.MergeCount);
            Assert.AreEqual(1, r.Events.Count);
        }

        // ---------- Ace (spec T8, 4.6) ----------
        [Test]
        public void T8_ThreeKings_CreateAce_CellBecomesEmpty()
        {
            GameState s = State("KH KC .");
            TurnResult r = Place(s, "KS", 0, 2);

            Assert.AreEqual(".", CardAt(s, 0, 0));
            Assert.AreEqual(".", CardAt(s, 0, 1));
            Assert.AreEqual(".", CardAt(s, 0, 2), "Ace biến mất ngay");
            Assert.AreEqual(100000, s.Score);
            Assert.AreEqual(1, r.AcesCreated);
            Assert.AreEqual(1, r.Events.OfType<AceCreatedEvent>().Count());
            Assert.AreEqual(0, r.Events.OfType<CardCreatedEvent>().Count());
        }

        [Test]
        public void Ace_InChain_IsFlatBonus_NoChainMultiplier()
        {
            GameState s = State(@"
                .  QH .
                QC .  KD
                .  KS .");
            TurnResult r = Place(s, "QD", 1, 1);

            Assert.AreEqual(390 + 100000, s.Score, "K: 13×3×10 = 390; Ace +100,000 không nhân ×1.5");
            Assert.AreEqual(1, r.AcesCreated);
            Assert.AreEqual(".", CardAt(s, 1, 1));
        }

        // ---------- Từ chối nước đi không hợp lệ ----------
        [Test]
        public void Reject_OccupiedLockedOrOutOfBoard_StateUnchanged()
        {
            GameState s = State("2H # .");
            string before = s.Board.ToText();

            Assert.AreEqual(PlaceRejectReason.CellOccupied, Place(s, "2S", 0, 0).RejectReason);
            Assert.AreEqual(PlaceRejectReason.CellNotExist, Place(s, "2S", 0, 1).RejectReason);
            Assert.AreEqual(PlaceRejectReason.OutOfBoard, Place(s, "2S", 3, 3).RejectReason);

            Assert.AreEqual(before, s.Board.ToText());
            Assert.AreEqual(0, s.Turn);
            Assert.AreEqual(0, s.Score);
        }

        [Test]
        public void Reject_WhenGameNotPlaying()
        {
            GameState s = State(". .");
            s.Status = GameStatus.Lost;
            TurnResult r = Place(s, "2S", 0, 0);
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(PlaceRejectReason.GameNotPlaying, r.RejectReason);
        }

        [Test]
        public void PlacedCard_IsCopy_CallerCardNotStoredOnBoard()
        {
            GameState s = State(". .");
            Card hand = Card.Parse("5H");
            _turn.PlaceCard(s, new CellPos(0, 0), hand);
            hand.IceLayers = 2;
            Assert.AreEqual(0, s.Board.GetCard(new CellPos(0, 0)).IceLayers);
        }

        // ---------- Bảng tra nhanh spec 4.1 ----------
        [TestCase("2", 90)]
        [TestCase("5", 180)]
        [TestCase("9", 300)]
        [TestCase("Q", 390)]
        public void ScoreTable_ThreeCardMerge(string rank, int expected)
        {
            GameState s = State($"{rank}H {rank}C .");
            Place(s, $"{rank}S", 0, 2);
            Assert.AreEqual(expected, s.Score);
        }
    }
}
