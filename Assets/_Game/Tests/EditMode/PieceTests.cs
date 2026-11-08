using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Tests
{
    /// <summary>
    /// Bước 4.0 — lá trên tay là Piece (spec 2.3, 2.6). Bước này mọi Piece đều là lá đơn:
    /// luật / điểm / dãy lá phải y hệt trước, các lối tắt "Card" trỏ tới lá ①.
    /// </summary>
    public class PieceTests
    {
        private const string Board3x3 = @"
            .  .  .
            .  .  .
            .  .  .";

        private static GameSession Scripted(string boardText, params string[] cards)
        {
            var session = new GameSession();
            session.Start(Board.FromText(boardText), new ScriptedCardSource(cards), GameMode.Level);
            return session;
        }

        private static string Events(TurnResult r) => string.Join("\n", r.Events.Select(e => e.ToString()));

        [Test]
        public void Single_HasOneCard()
        {
            Piece p = Piece.Single(Card.Parse("7S"));
            Assert.AreEqual(1, p.Count);
            Assert.IsFalse(p.IsPair);
            Assert.AreEqual("7S", p.First.ToString());
            Assert.IsNull(p.Second);
            Assert.AreEqual(PieceOrientation.H_AB, p.Orientation);
            Assert.AreEqual("7S", p.ToString());
            Assert.Throws<System.ArgumentNullException>(() => Piece.Single(null));
        }

        [Test]
        public void Clone_IsDeep()
        {
            Piece p = Piece.Single(Card.Parse("10H"));
            Piece copy = p.Clone();
            Assert.AreNotSame(p.First, copy.First);
            Assert.AreEqual(p.ToString(), copy.ToString());

            p.First.IceLayers = 2;
            Assert.AreEqual(0, copy.First.IceLayers, "Sửa bản gốc không ảnh hưởng bản sao");
        }

        [Test]
        public void SinglePieceSource_WrapsEachCard_ThenNull()
        {
            var source = new SinglePieceSource(new ScriptedCardSource("2S", "3H"));
            Board board = Board.FromText(Board3x3);
            Assert.AreEqual("2S", source.Draw(board).ToString());
            Assert.AreEqual("3H", source.Draw(board).ToString());
            Assert.IsNull(source.Draw(board));
        }

        [Test]
        public void Session_HandIsSinglePieces_ShortcutsPointToFirstCard()
        {
            GameSession s = Scripted(Board3x3, "2S", "3H");
            Assert.IsFalse(s.CurrentPiece.IsPair);
            Assert.AreEqual("2S", s.CurrentPiece.ToString());
            Assert.AreEqual("3H", s.NextPiece.ToString());
            Assert.AreSame(s.CurrentPiece.First, s.CurrentCard);
            Assert.AreSame(s.NextPiece.First, s.NextCard);
        }

        [Test]
        public void PlacePiece_TwoCellsForSingle_Rejected_NothingChanges()
        {
            GameSession s = Scripted(Board3x3, "2S", "3H");
            string before = s.Board.ToText();

            TurnResult r = s.PlacePiece(new CellPos(0, 0), new CellPos(0, 1));
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(PlaceRejectReason.PieceShapeMismatch, r.RejectReason);
            Assert.AreEqual(before, s.Board.ToText());
            Assert.AreEqual(0, s.Turn);
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual("2S", s.CurrentPiece.ToString(), "Không mất lá khi thả sai");
            Assert.AreEqual("3H", s.NextPiece.ToString());

            Assert.IsFalse(s.PreviewPiece(new CellPos(0, 0), new CellPos(0, 1)).Valid);
        }

        [Test]
        public void PlacePiece_SameAsPlaceCard()
        {
            const string board = @"
                5H 5D .  .
                .  .  .  3C
                .  .  3S .
                8D .  .  .";
            string[] cards = { "5C", "3D", "4H", "9S", "2D" };
            GameSession byCard = Scripted(board, cards);
            GameSession byPiece = Scripted(board, cards);
            CellPos[] moves = { new CellPos(0, 2), new CellPos(1, 2), new CellPos(3, 3), new CellPos(2, 0) };

            foreach (CellPos m in moves)
            {
                PlacementPreview a = byCard.PreviewPlacement(m);
                PlacementPreview b = byPiece.PreviewPiece(m);
                Assert.AreEqual(a.ScoreGain, b.ScoreGain);
                Assert.AreEqual(a.MergeSteps, b.MergeSteps);
                Assert.AreEqual(a.FirstEffectSuit, b.FirstEffectSuit);

                Assert.AreEqual(Events(byCard.PlaceCard(m)), Events(byPiece.PlacePiece(m)));
                Assert.AreEqual(byCard.Board.ToText(), byPiece.Board.ToText());
                Assert.AreEqual(byCard.Score, byPiece.Score);
            }
        }

        [Test]
        public void CardsDrawnEvent_CarriesPieces()
        {
            GameSession s = Scripted(Board3x3, "2S", "3H", "4D");
            TurnResult r = s.PlacePiece(new CellPos(0, 0));
            CardsDrawnEvent drawn = r.Events.OfType<CardsDrawnEvent>().Single();
            Assert.AreEqual("3H", drawn.CurrentPiece.ToString());
            Assert.AreEqual("4D", drawn.NextPiece.ToString());
            Assert.AreEqual("3H", drawn.Current.ToString(), "Lối tắt Current = lá ①");
            Assert.AreNotSame(s.CurrentPiece, drawn.CurrentPiece, "Event giữ bản sao, không trỏ vào state");
        }

        [Test]
        public void OutOfCards_NextPieceNull_ThenLose()
        {
            GameSession s = Scripted(Board3x3, "2S", "3H");
            s.PlacePiece(new CellPos(0, 0));
            Assert.AreEqual("3H", s.CurrentPiece.ToString());
            Assert.IsNull(s.NextPiece);

            s.PlacePiece(new CellPos(2, 2));
            Assert.IsNull(s.CurrentPiece);
            Assert.AreEqual(GameStatus.Lost, s.Status);
            Assert.AreEqual(LoseReason.OutOfCards, s.LoseReason);
        }

        [Test]
        public void SetHandPiece_AndSnapshot_RestorePieces()
        {
            var s = new GameSession();
            s.Start(Board.FromText(Board3x3), new FixedCardSource(Card.Parse("5H")), GameMode.Level);
            GameSession.Snapshot snap = s.CaptureSnapshot();

            s.SetHand(Piece.Single(Card.Parse("9C")), Piece.Single(Card.Parse("8D")));
            Assert.AreEqual("9C", s.CurrentPiece.ToString());
            Assert.AreEqual("8D", s.NextPiece.ToString());

            s.RestoreSnapshot(snap);
            Assert.AreEqual("5H", s.CurrentPiece.ToString());
            Assert.AreEqual("5H", s.NextPiece.ToString());
        }
    }
}
