using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Tests
{
    /// <summary>Một ván chơi qua GameSession (spec 3.1 bước 6–7, spec 5.2, spec 7).</summary>
    public class GameSessionTests
    {
        private static GameSession Scripted(string boardText, params string[] cards)
        {
            var session = new GameSession();
            session.Start(Board.FromText(boardText), new ScriptedCardSource(cards), GameMode.Level);
            return session;
        }

        private static string CardAt(GameSession s, int r, int c) => s.Board.GetCard(new CellPos(r, c))?.ToString() ?? ".";

        /// <summary>
        /// Bàn 5×5 đầy 24 lá không có cặp kề nào cùng rank (rank = 2 + (2r + c) mod 5),
        /// chỉ trống ô (4,4). Hàng xóm của (4,4): (3,4) = 2, (4,3) = 3.
        /// </summary>
        private static string AlmostFullBoard(string at34 = null, string at43 = null)
        {
            var sb = new StringBuilder();
            string[] suits = { "H", "D", "C", "S" };
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    string token;
                    if (r == 4 && c == 4) token = ".";
                    else if (r == 3 && c == 4 && at34 != null) token = at34;
                    else if (r == 4 && c == 3 && at43 != null) token = at43;
                    else token = (2 + (2 * r + c) % 5) + suits[(r + c) % 4];
                    sb.Append(token).Append(' ');
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        // ---------- Bắt đầu ván ----------
        [Test]
        public void StartEndless_InitialState()
        {
            var s = new GameSession();
            s.StartEndless(123);

            Assert.AreEqual(GameMode.Endless, s.Mode);
            Assert.AreEqual(5, s.Board.Rows);
            Assert.AreEqual(5, s.Board.Cols);
            Assert.AreEqual(25, s.Board.AllPositions().Count(p => s.Board.IsPlaceable(p)));
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(0, s.Turn);
            Assert.AreEqual(GameStatus.Playing, s.Status);
            Assert.IsNotNull(s.CurrentCard);
            Assert.IsNotNull(s.NextCard);
            Assert.AreEqual(123u, s.Seed);
            Assert.AreEqual(4, s.EnabledSuitEffects.Count, "Endless mở cả 4 chất (spec 4.7)");
        }

        [Test]
        public void StartEndless_SameSeed_SameGame()
        {
            var a = new GameSession();
            var b = new GameSession();
            a.StartEndless(555);
            b.StartEndless(555);
            for (int i = 0; i < 15 && a.Status == GameStatus.Playing; i++)
            {
                CellPos target = a.Board.AllPositions().First(p => a.Board.IsPlaceable(p));
                a.PlaceCard(target);
                b.PlaceCard(target);
                Assert.AreEqual(a.Board.ToText(), b.Board.ToText(), $"Lượt {i}");
                Assert.AreEqual(a.Score, b.Score);
                Assert.AreEqual(a.CurrentCard?.ToString(), b.CurrentCard?.ToString());
                Assert.AreEqual(a.NextCard?.ToString(), b.NextCard?.ToString());
            }
        }

        [Test]
        public void StartEndless_Again_ResetsEverything()
        {
            var s = new GameSession();
            s.StartEndless(1);
            s.PlaceCard(new CellPos(0, 0));
            s.PlaceCard(new CellPos(2, 2));

            s.StartEndless(2);
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(0, s.Turn);
            Assert.AreEqual(GameStatus.Playing, s.Status);
            Assert.AreEqual(LoseReason.None, s.LoseReason);
            Assert.AreEqual(25, s.Board.AllPositions().Count(p => s.Board.IsPlaceable(p)));
            Assert.AreEqual(2u, s.Seed);
        }

        [Test]
        public void NotStarted_Throws()
        {
            var s = new GameSession();
            Assert.IsFalse(s.IsStarted);
            Assert.Throws<InvalidOperationException>(() => s.PlaceCard(new CellPos(0, 0)));
            Assert.Throws<InvalidOperationException>(() => { int _ = s.Score; });
        }

        // ---------- Lượt chơi & rút lá ----------
        [Test]
        public void PlaceCard_NextBecomesCurrent_TurnIncreases()
        {
            GameSession s = Scripted(". . .", "2S", "3H", "4D");
            Assert.AreEqual("2S", s.CurrentCard.ToString());
            Assert.AreEqual("3H", s.NextCard.ToString());

            TurnResult r = s.PlaceCard(new CellPos(0, 0));

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual("2S", CardAt(s, 0, 0));
            Assert.AreEqual("3H", s.CurrentCard.ToString());
            Assert.AreEqual("4D", s.NextCard.ToString());
            Assert.AreEqual(1, s.Turn);
        }

        [Test]
        public void RejectedPlacement_KeepsCards_StateUnchanged()
        {
            GameSession s = Scripted("2H # .", "3S", "4D", "5C");
            string boardBefore = s.Board.ToText();

            Assert.AreEqual(PlaceRejectReason.CellOccupied, s.PlaceCard(new CellPos(0, 0)).RejectReason);
            Assert.AreEqual(PlaceRejectReason.CellNotExist, s.PlaceCard(new CellPos(0, 1)).RejectReason);
            Assert.AreEqual(PlaceRejectReason.OutOfBoard, s.PlaceCard(new CellPos(9, 9)).RejectReason);

            Assert.AreEqual("3S", s.CurrentCard.ToString(), "Không mất lá khi thả sai");
            Assert.AreEqual("4D", s.NextCard.ToString());
            Assert.AreEqual(0, s.Turn);
            Assert.AreEqual(boardBefore, s.Board.ToText());
        }

        [Test]
        public void T1_ThroughSession()
        {
            GameSession s = Scripted(@"
                2H 2C .  .
                .  .  .  .
                .  .  3D .
                .  .  .  .", "2S", "9H", "9D");
            TurnResult r = s.PlaceCard(new CellPos(0, 2));

            Assert.AreEqual("3S", CardAt(s, 0, 2));
            Assert.AreEqual(90, s.Score);
            Assert.AreEqual(90, r.ScoreGained);
            Assert.AreEqual(1, r.MergeCount);
            Assert.AreEqual("9H", s.CurrentCard.ToString());
        }

        [Test]
        public void T2_ThroughSession()
        {
            GameSession s = Scripted(@"
                .  2H .  .
                2C .  3D .
                .  3S .  .
                .  .  4H 4C", "2D", "5S", "5H");
            TurnResult r = s.PlaceCard(new CellPos(1, 1));

            Assert.AreEqual("4D", CardAt(s, 1, 1));
            Assert.AreEqual(270, s.Score);
            Assert.AreEqual(2, r.MergeCount);
        }

        [Test]
        public void EventOrder_EndsWithCardsDrawn()
        {
            GameSession s = Scripted("2H 2C .", "2S", "9H", "9D");
            TurnResult r = s.PlaceCard(new CellPos(0, 2));

            string[] types = r.Events.Select(e => e.GetType().Name).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                nameof(CardPlacedEvent), nameof(MergeStartedEvent), nameof(CardCreatedEvent),
                nameof(ScoreChangedEvent), nameof(CardsDrawnEvent)
            }, types);

            var drawn = (CardsDrawnEvent)r.Events.Last();
            Assert.AreEqual("9H", drawn.Current.ToString());
            Assert.AreEqual("9D", drawn.Next.ToString());
        }

        // ---------- Thua ----------
        [Test]
        public void BoardFull_AfterPlacement_Loses()
        {
            GameSession s = Scripted(AlmostFullBoard(), "9S", "9H");
            Assert.AreEqual(GameStatus.Playing, s.Status);

            TurnResult r = s.PlaceCard(new CellPos(4, 4));

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(GameStatus.Lost, s.Status);
            Assert.AreEqual(LoseReason.BoardFull, s.LoseReason);
            var over = r.Events.Last() as GameOverEvent;
            Assert.IsNotNull(over, "Sự kiện cuối phải là GameOver");
            Assert.AreEqual(LoseReason.BoardFull, over.Reason);
        }

        [Test]
        public void BoardFull_ButMergeFreesCells_KeepsPlaying()
        {
            // (3,4) và (4,3) là 9 → đặt 9 vào (4,4) tạo nhóm 3 lá, giải phóng 2 ô
            GameSession s = Scripted(AlmostFullBoard("9H", "9C"), "9S", "2H", "3H");
            TurnResult r = s.PlaceCard(new CellPos(4, 4));

            Assert.AreEqual(1, r.MergeCount);
            Assert.AreEqual("10S", CardAt(s, 4, 4));
            Assert.AreEqual(GameStatus.Playing, s.Status);
            Assert.AreEqual(0, r.Events.OfType<GameOverEvent>().Count());
        }

        [Test]
        public void AfterLose_CannotPlace()
        {
            GameSession s = Scripted(AlmostFullBoard(), "9S", "9H");
            s.PlaceCard(new CellPos(4, 4));
            TurnResult r = s.PlaceCard(new CellPos(0, 0));
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(PlaceRejectReason.GameNotPlaying, r.RejectReason);
        }

        [Test]
        public void OutOfCards_Loses()
        {
            GameSession s = Scripted(". . . .", "2S", "3H");
            s.PlaceCard(new CellPos(0, 0));
            Assert.AreEqual("3H", s.CurrentCard.ToString());
            Assert.IsNull(s.NextCard, "Hết lá → không có Next");
            Assert.AreEqual(GameStatus.Playing, s.Status, "Vẫn còn lá hiện tại để đặt");

            TurnResult r = s.PlaceCard(new CellPos(0, 3));
            Assert.AreEqual(GameStatus.Lost, s.Status);
            Assert.AreEqual(LoseReason.OutOfCards, s.LoseReason);
            Assert.IsInstanceOf<GameOverEvent>(r.Events.Last());
        }

        [Test]
        public void ScriptedCardSource_ReturnsNullWhenEmpty()
        {
            var src = new ScriptedCardSource("2S");
            Assert.AreEqual(1, src.Remaining);
            Assert.AreEqual("2S", src.Draw(null).ToString());
            Assert.IsNull(src.Draw(null));
            Assert.AreEqual(0, src.Remaining);
        }

        // ---------- Endless chơi tự động tới khi thua ----------
        [Test]
        public void Endless_AutoPlay_EventuallyLoses_ByBoardFull()
        {
            var s = new GameSession();
            s.StartEndless(42);
            int guard = 0;
            while (s.Status == GameStatus.Playing && guard++ < 10000)
            {
                CellPos target = s.Board.AllPositions().First(p => s.Board.IsPlaceable(p));
                Assert.IsTrue(s.PlaceCard(target).Accepted);
            }
            Assert.AreEqual(GameStatus.Lost, s.Status);
            Assert.AreEqual(LoseReason.BoardFull, s.LoseReason);
            Assert.Greater(s.Turn, 25, "Có merge nên phải chơi được hơn 25 lượt");
        }
    }
}
