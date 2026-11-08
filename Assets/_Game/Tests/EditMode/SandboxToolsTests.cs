using NUnit.Framework;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Tests
{
    /// <summary>Công cụ Sandbox của GameSession: chụp / khôi phục (Undo), sửa ô, đổi lá trên tay, bật tắt chất.</summary>
    public class SandboxToolsTests
    {
        private static readonly Suit[] DCS = { Suit.Diamond, Suit.Club, Suit.Spade };

        private const string Level8 = @"
            2H 5D 3C 7H 4S
            6C 3H 4D 2C .
            .  6S .  6S .
            .  5H 2D 3S 7D
            4C .  .  5S 2H";

        private const string Level9 = @"
            .  .  8H .  .
            .  6C 6C .  5D
            .  7S .  7S .
            5H .  8D .  .
            .  .  .  4C .";

        private static string Text(string board) => Board.FromText(board).ToText();

        private static GameSession Fixed(string board, string hand, params Suit[] suits)
        {
            var session = new GameSession();
            session.Start(Board.FromText(board), new FixedCardSource(Card.Parse(hand)), GameMode.Level, suits);
            return session;
        }

        [Test]
        public void FixedSource_HandStaysSame_AfterEachTurn()
        {
            GameSession s = Fixed(".  .  .  .", "5H");
            s.PlaceCard(new CellPos(0, 0));
            Assert.AreEqual("5H", s.CurrentCard.ToString());
            Assert.AreEqual("5H", s.NextCard.ToString());
        }

        [Test]
        public void Undo_AfterSpadeClear_RestoresEverything()
        {
            GameSession s = Fixed(Level8, "6S", DCS);
            string board = s.Board.ToText();
            GameSession.Snapshot snap = s.CaptureSnapshot();

            s.PlaceCard(new CellPos(2, 2));
            Assert.AreEqual(400, s.Score);

            s.RestoreSnapshot(snap);
            Assert.AreEqual(board, s.Board.ToText());
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(0, s.Turn);
            Assert.AreEqual("6S", s.CurrentCard.ToString());
            Assert.AreEqual(GameStatus.Playing, s.Status);
        }

        [Test]
        public void Undo_AfterClubChain_RestoresEverything_AndReplaysSame()
        {
            GameSession s = Fixed(Level9, "6C", DCS);
            string board = s.Board.ToText();
            GameSession.Snapshot snap = s.CaptureSnapshot();

            TurnResult first = s.PlaceCard(new CellPos(2, 2));
            string after = s.Board.ToText();
            s.RestoreSnapshot(snap);
            Assert.AreEqual(board, s.Board.ToText());

            // Chơi lại đúng nước đó → ra đúng kết quả cũ (xem lại hiệu ứng nhiều lần)
            TurnResult again = s.PlaceCard(new CellPos(2, 2));
            Assert.AreEqual(after, s.Board.ToText());
            Assert.AreEqual(first.ScoreGained, again.ScoreGained);
            Assert.AreEqual(690, s.Score);
        }

        [Test]
        public void Snapshot_CanBeRestoredManyTimes_MultiStepUndo()
        {
            GameSession s = Fixed(".  .  .  .  .", "2H");
            GameSession.Snapshot s0 = s.CaptureSnapshot();
            s.PlaceCard(new CellPos(0, 0));
            GameSession.Snapshot s1 = s.CaptureSnapshot();
            s.PlaceCard(new CellPos(0, 1));
            s.PlaceCard(new CellPos(0, 2)); // 2H×3 → 3H

            s.RestoreSnapshot(s1);
            Assert.AreEqual(Text("2H . . . ."), s.Board.ToText());
            s.RestoreSnapshot(s0);
            Assert.AreEqual(Text(". . . . ."), s.Board.ToText());
            s.RestoreSnapshot(s0); // dùng lại lần 2
            Assert.AreEqual(0, s.Turn);
        }

        [Test]
        public void Snapshot_IsNotAffectedByLaterTurns()
        {
            GameSession s = Fixed(Level8, "6S", DCS);
            GameSession.Snapshot snap = s.CaptureSnapshot();
            s.RestoreSnapshot(snap);
            s.PlaceCard(new CellPos(2, 2)); // sửa bàn sau khi khôi phục không được làm hỏng ảnh chụp
            s.RestoreSnapshot(snap);
            Assert.AreEqual("3H", s.Board.GetCard(new CellPos(1, 1)).ToString());
        }

        [Test]
        public void SetCell_PlaceClearLock()
        {
            GameSession s = Fixed(".  .  .", "2H");
            s.SetCell(new CellPos(0, 0), Card.Parse("KS"));
            s.SetCell(new CellPos(0, 1), null, exists: false);
            Assert.AreEqual(Text("KS # ."), s.Board.ToText());

            s.SetCell(new CellPos(0, 1), null); // mở khóa
            s.SetCell(new CellPos(0, 0), null); // xóa lá
            Assert.AreEqual(Text(". . ."), s.Board.ToText());
        }

        [Test]
        public void SetCell_FillBoard_Loses_ThenFreeCell_PlaysAgain()
        {
            GameSession s = Fixed("2H .", "5S");
            s.SetCell(new CellPos(0, 1), Card.Parse("3D"));
            Assert.AreEqual(GameStatus.Lost, s.Status);
            Assert.AreEqual(LoseReason.BoardFull, s.LoseReason);

            s.SetCell(new CellPos(0, 1), null);
            Assert.AreEqual(GameStatus.Playing, s.Status);
            Assert.AreEqual(LoseReason.None, s.LoseReason);
        }

        [Test]
        public void SetHand_ChangesCurrentAndNext()
        {
            GameSession s = Fixed(".  .", "2H");
            s.SetHand(Card.Parse("QD"), Card.Parse("3C"));
            Assert.AreEqual("QD", s.CurrentCard.ToString());
            Assert.AreEqual("3C", s.NextCard.ToString());
        }

        [Test]
        public void SetSuitEnabled_TogglesEffect()
        {
            GameSession s = Fixed("4D 4D .", "4D");
            GameSession.Snapshot snap = s.CaptureSnapshot();

            s.PlaceCard(new CellPos(0, 2));
            Assert.AreEqual(150, s.Score, "Chưa mở ♦");

            s.RestoreSnapshot(snap);
            s.SetSuitEnabled(Suit.Diamond, true);
            CollectionAssert.Contains(s.EnabledSuitEffects, Suit.Diamond);
            s.PlaceCard(new CellPos(0, 2));
            Assert.AreEqual(450, s.Score, "Đã mở ♦");

            s.SetSuitEnabled(Suit.Diamond, false);
            CollectionAssert.DoesNotContain(s.EnabledSuitEffects, Suit.Diamond);
        }
    }
}
