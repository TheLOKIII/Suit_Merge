using NUnit.Framework;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Tests
{
    /// <summary>GameSession.PreviewPlacement — xem trước nhóm sẽ merge, KHÔNG đổi ván.</summary>
    public class PlacementPreviewTests
    {
        private static GameSession Scripted(string boardText, params string[] cards)
        {
            var session = new GameSession();
            session.Start(Board.FromText(boardText), new ScriptedCardSource(cards), GameMode.Level);
            return session;
        }

        private const string Level1 = @"
            2H 2C .  .
            .  .  .  .
            .  .  3D .
            .  .  .  .";

        private const string Level2 = @"
            .  2H .  .
            2C .  3D .
            .  3S .  .
            .  .  4H 4C";

        [Test]
        public void T1_Preview_FirstGroupAndScore()
        {
            GameSession s = Scripted(Level1, "2S", "9H", "9D");
            PlacementPreview p = s.PreviewPlacement(new CellPos(0, 2));

            Assert.IsTrue(p.Valid);
            Assert.IsTrue(p.WillMerge);
            CollectionAssert.AreEqual(new[] { new CellPos(0, 0), new CellPos(0, 1) }, p.FirstGroupCells,
                "Chỉ các ô đang có lá, không gồm ô thả (0,2)");
            Assert.AreEqual(1, p.MergeSteps);
            Assert.AreEqual(90, p.ScoreGain);
        }

        [Test]
        public void T2_Preview_OnlyFirstGroup_ButCountsChain()
        {
            GameSession s = Scripted(Level2, "2D", "5S", "5H");
            PlacementPreview p = s.PreviewPlacement(new CellPos(1, 1));

            CollectionAssert.AreEqual(new[] { new CellPos(0, 1), new CellPos(1, 0) }, p.FirstGroupCells,
                "Chỉ 2♥ 2♣ (nhóm đầu) — 3♦ 3♠ của bước chain không nằm trong danh sách");
            Assert.AreEqual(2, p.MergeSteps);
            Assert.AreEqual(270, p.ScoreGain);
        }

        [Test]
        public void Preview_DoesNotChangeGame()
        {
            GameSession s = Scripted(Level2, "2D", "5S", "5H");
            string boardBefore = s.Board.ToText();

            s.PreviewPlacement(new CellPos(1, 1));
            s.PreviewPlacement(new CellPos(3, 0));

            Assert.AreEqual(boardBefore, s.Board.ToText());
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(0, s.Turn);
            Assert.AreEqual("2D", s.CurrentCard.ToString());
            Assert.AreEqual("5S", s.NextCard.ToString());

            // Lượt thật sau khi xem trước vẫn cho đúng kết quả
            TurnResult r = s.PlaceCard(new CellPos(1, 1));
            Assert.AreEqual(270, r.ScoreGained);
            Assert.AreEqual("5S", s.CurrentCard.ToString());
            Assert.AreEqual("5H", s.NextCard.ToString());
        }

        [Test]
        public void Preview_Endless_DoesNotConsumeRandomCards()
        {
            var a = new GameSession();
            var b = new GameSession();
            a.StartEndless(2468);
            b.StartEndless(2468);

            for (int turn = 0; turn < 12 && a.Status == GameStatus.Playing; turn++)
            {
                // Chỉ phiên a xem trước nhiều ô
                foreach (CellPos p in a.Board.AllPositions()) a.PreviewPlacement(p);

                CellPos target = default;
                foreach (CellPos p in a.Board.AllPositions())
                    if (a.Board.IsPlaceable(p)) { target = p; break; }
                a.PlaceCard(target);
                b.PlaceCard(target);

                Assert.AreEqual(b.Board.ToText(), a.Board.ToText(), $"Lượt {turn}");
                Assert.AreEqual(b.CurrentCard?.ToString(), a.CurrentCard?.ToString());
                Assert.AreEqual(b.NextCard?.ToString(), a.NextCard?.ToString());
                Assert.AreEqual(b.Score, a.Score);
            }
        }

        [Test]
        public void Preview_InvalidCells()
        {
            GameSession s = Scripted("2H # .", "3S", "4D");
            Assert.IsFalse(s.PreviewPlacement(new CellPos(0, 0)).Valid, "Ô có lá");
            Assert.IsFalse(s.PreviewPlacement(new CellPos(0, 1)).Valid, "Ô khoá");
            Assert.IsFalse(s.PreviewPlacement(new CellPos(5, 5)).Valid, "Ngoài bàn");
            Assert.IsTrue(s.PreviewPlacement(new CellPos(0, 2)).Valid);
        }

        [Test]
        public void Preview_NoMerge_EmptyGroup()
        {
            GameSession s = Scripted("2H . .", "5S", "4D");
            PlacementPreview p = s.PreviewPlacement(new CellPos(0, 2));
            Assert.IsTrue(p.Valid);
            Assert.IsFalse(p.WillMerge);
            Assert.AreEqual(0, p.FirstGroupCells.Count);
            Assert.AreEqual(0, p.ScoreGain);
        }

        [Test]
        public void Preview_Ace()
        {
            GameSession s = Scripted("KH KC .", "KS", "2D");
            PlacementPreview p = s.PreviewPlacement(new CellPos(0, 2));
            Assert.AreEqual(1, p.AcesCreated);
            Assert.AreEqual(100000, p.ScoreGain);
            Assert.AreEqual(2, p.FirstGroupCells.Count);
        }

        [Test]
        public void Preview_AfterGameOver_Invalid()
        {
            GameSession s = Scripted(". .", "2S");
            s.PlaceCard(new CellPos(0, 0)); // hết lá → thua OutOfCards
            Assert.AreEqual(GameStatus.Lost, s.Status);
            Assert.IsFalse(s.PreviewPlacement(new CellPos(0, 1)).Valid);
        }
    }
}
