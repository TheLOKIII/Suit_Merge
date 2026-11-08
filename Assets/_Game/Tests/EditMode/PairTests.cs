using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Config;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Tests
{
    /// <summary>
    /// M4.5 phần A — Khối đôi (spec 4.9, test P1–P6 ở 13.3): xoay, đặt 2 ô, resolve ① rồi ②, ♠ miễn lá chờ,
    /// chain đếm chung, hết chỗ cho khối đôi, deck đếm theo lá.
    /// </summary>
    public class PairTests
    {
        /// <summary>Nguồn Piece theo danh sách cho sẵn, hết thì null.</summary>
        private sealed class QueuePieceSource : IPieceSource
        {
            private readonly Queue<Piece> _pieces;
            public QueuePieceSource(params Piece[] pieces) => _pieces = new Queue<Piece>(pieces);
            public Piece Draw(Board board) => _pieces.Count > 0 ? _pieces.Dequeue() : null;
        }

        private static Piece Pair(string a, string b, PieceOrientation o = PieceOrientation.H_AB) =>
            Piece.Pair(Card.Parse(a), Card.Parse(b), o);

        private static Piece Single(string card) => Piece.Single(Card.Parse(card));

        private static GameSession Start(string board, IEnumerable<Suit> suits, params Piece[] pieces)
        {
            var s = new GameSession();
            s.Start(Board.FromText(board), new QueuePieceSource(pieces), GameMode.Level, suits);
            return s;
        }

        private static string At(GameSession s, int r, int c) => s.Board.GetCard(new CellPos(r, c))?.ToString() ?? ".";

        private static LevelData Load(string json)
        {
            LevelLoadResult r = LevelLoader.Load(json.Replace('\'', '"'));
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            return r.Level;
        }

        // ------------------------------------------------------------------ Hình dạng + xoay

        [Test]
        public void P1_Rotate4Times_CyclesOrientationAndFirstCard()
        {
            Piece p = Pair("9H", "10D");
            var orientations = new List<PieceOrientation>();
            var firsts = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                orientations.Add(p.Orientation);
                firsts.Add(p.First.ToString());
                p = p.Rotated();
            }
            CollectionAssert.AreEqual(new[] { PieceOrientation.H_AB, PieceOrientation.V_AB, PieceOrientation.H_BA, PieceOrientation.V_BA, PieceOrientation.H_AB }, orientations);
            CollectionAssert.AreEqual(new[] { "9H", "9H", "10D", "10D", "9H" }, firsts);
            Assert.AreEqual("[9H 10D]", Pair("9H", "10D").ToString());
            Assert.AreEqual("[10D/9H]", Pair("9H", "10D", PieceOrientation.V_BA).ToString());
        }

        [Test]
        public void SecondCell_RightWhenHorizontal_BelowWhenVertical()
        {
            var at = new CellPos(1, 1);
            Assert.AreEqual(new CellPos(1, 2), Pair("2H", "3H").SecondCell(at));
            Assert.AreEqual(new CellPos(2, 1), Pair("2H", "3H", PieceOrientation.V_AB).SecondCell(at));
            Assert.AreEqual(new CellPos(1, 2), Pair("2H", "3H", PieceOrientation.H_BA).SecondCell(at));
            Assert.AreEqual(new CellPos(2, 1), Pair("2H", "3H", PieceOrientation.V_BA).SecondCell(at));
            Piece single = Single("2H");
            Assert.AreSame(single, single.Rotated(), "Lá đơn xoay = chính nó");
        }

        [Test]
        public void RotateCurrentPiece_DoesNotUseTurn_SingleIgnored()
        {
            GameSession s = Start(". . .\n. . .", null, Pair("2H", "3D"), Single("4S"));
            PieceRotatedEvent e = s.RotateCurrentPiece();
            Assert.IsNotNull(e);
            Assert.AreEqual(PieceOrientation.V_AB, s.CurrentPiece.Orientation);
            Assert.AreEqual(PieceOrientation.V_AB, e.Piece.Orientation);
            Assert.AreEqual(0, s.Turn, "Xoay không tốn lượt");

            s.PlacePiece(new CellPos(0, 0)); // dọc: (0,0) + (1,0)
            Assert.AreEqual("2H", At(s, 0, 0));
            Assert.AreEqual("3D", At(s, 1, 0));
            Assert.IsNull(s.RotateCurrentPiece(), "Lá đơn không xoay");
        }

        // ------------------------------------------------------------------ Đặt xuống

        [Test]
        public void P2_SecondCellOccupiedOrMissing_Rejected_PieceStaysInHand()
        {
            GameSession s = Start(". . 5S\n. # .", null, Pair("2H", "3D"), Single("4S"));
            Assert.AreEqual(PlaceRejectReason.CellOccupied, s.PlacePiece(new CellPos(0, 1)).RejectReason, "Ô ② có lá");
            Assert.AreEqual(PlaceRejectReason.OutOfBoard, s.PlacePiece(new CellPos(1, 2)).RejectReason, "Ô ② ngoài bàn");
            Assert.AreEqual(PlaceRejectReason.CellNotExist, s.PlacePiece(new CellPos(1, 0)).RejectReason, "Ô ② bị khóa");
            Assert.AreEqual(PlaceRejectReason.PieceShapeMismatch,
                s.PlacePiece(new CellPos(0, 0), new CellPos(1, 0)).RejectReason, "Ô ② không khớp hướng ngang");
            Assert.AreEqual(0, s.Turn);
            Assert.AreEqual("[2H 3D]", s.CurrentPiece.ToString(), "Khối vẫn trên tay");
            Assert.IsFalse(s.PreviewPiece(new CellPos(0, 1)).Valid);
            Assert.IsTrue(s.PreviewPiece(new CellPos(0, 0)).Valid);
        }

        [Test]
        public void PlacePair_BothCardsLanded_ThenSecondResolves()
        {
            GameSession s = Start(". . .\n. . .", null, Pair("2H", "3D"), Single("4S"));
            TurnResult r = s.PlacePiece(new CellPos(1, 0));
            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(1, s.Turn, "Khối đôi = 1 lượt");
            Assert.AreEqual("2H", At(s, 1, 0));
            Assert.AreEqual("3D", At(s, 1, 1));
            var placed = r.Events.OfType<CardPlacedEvent>().ToList();
            Assert.AreEqual(2, placed.Count, "Đặt cả 2 lá trước khi resolve");
            Assert.AreEqual(new CellPos(1, 1), r.Events.OfType<PairSecondResolveEvent>().Single().Cell);
            Assert.AreEqual("4S", s.CurrentPiece.ToString());
        }

        // ------------------------------------------------------------------ Resolve ① rồi ②

        [Test]
        public void P3_SpadeOfFirst_DoesNotClearPendingSecond()
        {
            GameSession s = Start(@"
                6S 6S .  .
                .  .  .  2H", new[] { Suit.Spade }, Pair("6S", "8D"), Single("4C"));
            TurnResult r = s.PlacePiece(new CellPos(0, 2));
            Assert.IsTrue(r.Accepted);
            Assert.AreEqual("7S", At(s, 0, 2), "Nhóm 6♠ → 7♠ tại ô ①");
            Assert.AreEqual("8D", At(s, 0, 3), "Lá ② đang chờ → ♠ không xóa");
            Assert.AreEqual(".", At(s, 1, 3), "Lá thường trong vùng 3×3 vẫn bị xóa");
            SpadeClearEvent spade = r.Events.OfType<SpadeClearEvent>().Single();
            CollectionAssert.DoesNotContain(spade.Area, new CellPos(0, 3), "Vùng ♠ bỏ qua ô lá chờ");
            Assert.AreEqual(1, r.Events.OfType<PairSecondResolveEvent>().Count(), "Sau đó lá ② resolve bình thường");
        }

        [Test]
        public void P4_ChainStepSharedAcrossBothCards()
        {
            GameSession s = Start(@"
                2H 2C .  .
                .  .  .  5C
                .  .  .  5H", null, Pair("2D", "5D"), Single("4C"));
            TurnResult r = s.PlacePiece(new CellPos(0, 2));
            List<MergeStartedEvent> merges = r.Events.OfType<MergeStartedEvent>().ToList();
            Assert.AreEqual(2, merges.Count);
            Assert.AreEqual(new CellPos(0, 2), merges[0].Anchor);
            Assert.AreEqual(1, merges[0].ChainStep);
            Assert.AreEqual(new CellPos(0, 3), merges[1].Anchor, "Merge của lá ②");
            Assert.AreEqual(2, merges[1].ChainStep, "chainStep đếm tiếp từ lá ①");
            Assert.AreEqual(2, r.Events.OfType<ChainStepEvent>().Single().Step);
            // ①: 3 × 3 lá × 10 = 90 · ②: 6 × 3 × 10 × 1.5 = 270
            CollectionAssert.AreEqual(new[] { 90, 270 }, r.Events.OfType<CardCreatedEvent>().Select(e => e.ScoreGained));
            Assert.AreEqual(360, s.Score);
            Assert.AreEqual("3D", At(s, 0, 2));
            Assert.AreEqual("6D", At(s, 0, 3));
        }

        [Test]
        public void ChainOfFirst_SwallowsSecond_SecondSkipped()
        {
            GameSession s = Start(@"
                2H 2C .  .
                .  .  .  3C
                .  .  .  3H", null, Pair("2D", "3D"), Single("4C"));
            TurnResult r = s.PlacePiece(new CellPos(0, 2));
            // ① 2D → 3D tại (0,2); chain: 3D + 3D(②) + 3C + 3H → 4D tại (0,2)
            List<MergeStartedEvent> merges = r.Events.OfType<MergeStartedEvent>().ToList();
            Assert.AreEqual(2, merges.Count);
            CollectionAssert.Contains(merges[1].Group, new CellPos(0, 3), "Lá ② bị chain của ① lấy mất");
            Assert.AreEqual("4D", At(s, 0, 2));
            Assert.AreEqual(".", At(s, 0, 3));
            Assert.IsEmpty(r.Events.OfType<PairSecondResolveEvent>(), "Ô ② không còn lá gốc → bỏ qua");
        }

        [Test]
        public void FirstAndSecondInSameGroup_OneMerge_AnchorFirst()
        {
            GameSession s = Start("5S . . .", null, Pair("5H", "5D"), Single("4C"));
            TurnResult r = s.PlacePiece(new CellPos(0, 1));
            MergeStartedEvent merge = r.Events.OfType<MergeStartedEvent>().Single();
            Assert.AreEqual(new CellPos(0, 1), merge.Anchor, "Lá neo = ①");
            Assert.AreEqual(3, merge.Group.Count);
            Assert.AreEqual("6H", At(s, 0, 1), "Chất lá kết quả = chất lá neo ①");
            Assert.AreEqual(".", At(s, 0, 2));
            Assert.IsEmpty(r.Events.OfType<PairSecondResolveEvent>());
        }

        [Test]
        public void ClubSpawnOfFirst_NeverLandsOnSecondCell()
        {
            // Ô ② nằm bên PHẢI ô ① — thứ tự ưu tiên (Trên → Phải → Dưới → Trái) sẽ chọn ô ② nếu nó trống
            GameSession s = Start("2C 2C . .", new[] { Suit.Club }, Pair("2C", "9H"), Single("4S"));
            TurnResult r = s.PlacePiece(new CellPos(0, 2));
            ClubSpawnEvent club = r.Events.OfType<ClubSpawnEvent>().Single();
            Assert.AreEqual(new CellPos(0, 1), club.SpawnCell, "Ô ② có lá → sinh sang ô trái");
            Assert.AreEqual("9H", At(s, 0, 3));
        }

        [Test]
        public void PreviewPair_FirstGroupExcludesBothPlacedCells()
        {
            GameSession s = Start("5S . . .", null, Pair("5H", "5D"), Single("4C"));
            PlacementPreview p = s.PreviewPiece(new CellPos(0, 1));
            Assert.IsTrue(p.Valid);
            CollectionAssert.AreEqual(new[] { new CellPos(0, 0) }, p.FirstGroupCells);
            Assert.AreEqual(".", At(s, 0, 1), "Xem trước không đổi bàn");
            Assert.AreEqual(0, s.Turn);
        }

        [Test]
        public void UndoAfterPairTurn_RestoresBoardAndHand()
        {
            GameSession s = Start("5S . . .", null, Pair("5H", "5D"), Single("4C"));
            s.RotateCurrentPiece();
            s.RotateCurrentPiece(); // [5D 5H]
            GameSession.Snapshot snap = s.CaptureSnapshot();
            s.PlacePiece(new CellPos(0, 1));
            s.RestoreSnapshot(snap);
            Assert.AreEqual(Board.FromText("5S . . .").ToText(), s.Board.ToText());
            Assert.AreEqual("[5D 5H]", s.CurrentPiece.ToString());
            Assert.AreEqual(0, s.Score);
        }

        // ------------------------------------------------------------------ Hết chỗ cho khối đôi

        [Test]
        public void FindPairSlots_OnlyAdjacentEmptyExistingCells()
        {
            Board b = Board.FromText(@"
                .  .  #
                2H .  .");
            CollectionAssert.AreEqual(
                new[] { (new CellPos(0, 0), new CellPos(0, 1)), (new CellPos(0, 1), new CellPos(1, 1)), (new CellPos(1, 1), new CellPos(1, 2)) },
                b.FindPairSlots());
            Assert.IsTrue(b.HasPairSlot());
            Assert.IsFalse(Board.FromText(". 2H .\n3H . 4H").HasPairSlot());
        }

        [Test]
        public void P5_NoAdjacentEmptyPair_LoseNoSpaceForPair()
        {
            const string checker = @"
                .  2H .
                3H .  4H
                .  5H .";
            GameSession single = Start(checker, null, Single("9C"), Pair("2D", "3D"));
            Assert.AreEqual(GameStatus.Playing, single.Status, "Lá đơn vẫn đặt được");

            GameSession s = Start(checker, null, Pair("2D", "3D"), Single("9C"));
            Assert.AreEqual(GameStatus.Lost, s.Status, "Còn 5 ô trống nhưng không có 2 ô kề nhau");
            Assert.AreEqual(LoseReason.NoSpaceForPair, s.LoseReason);

            // Sau 1 lượt: Piece hiện tại mới là khối đôi mà hết cặp ô → THUA (cứu bằng Swap: M5)
            GameSession t = Start(@"
                .  .  2H
                3H 6H 4H", null, Single("9C"), Pair("2D", "3D"));
            TurnResult r = t.PlacePiece(new CellPos(0, 1));
            Assert.AreEqual(LoseReason.NoSpaceForPair, r.Events.OfType<GameOverEvent>().Single().Reason);
            Assert.AreEqual(GameStatus.Lost, t.Status);
        }

        // ------------------------------------------------------------------ Deck

        [Test]
        public void P6_LastCardIsAlwaysSingle_DeckCountsCards()
        {
            LevelData level = Load("{'id':13,'goals':[{'type':'SCORE','score':999999}],'board':[['.','.','.'],['.','.','.'],['.','.','.']]," +
                                   "'deck':{'size':5,'random':{'rankRange':['2','5'],'pairRatio':1,'seed':7}}}");
            List<Piece> seq = LevelDeck.Preview(level.Deck).ToList();
            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, seq.Select(p => p.Count), "5 lá = đôi + đôi + đơn (còn 1 lá → lá đơn)");

            var s = new GameSession();
            s.StartLevel(level);
            Assert.IsTrue(s.CurrentPiece.IsPair);
            Assert.AreEqual(1, s.CardsLeftInDeck, "5 − 2 (trên tay) − 2 (UP NEXT)");
        }

        [Test]
        public void PairRatioZero_DrawsNoExtraRandom_SameSequenceAsBefore()
        {
            string Json(string ratio) => "{'id':20,'goals':[{'type':'SCORE','score':10}],'board':[['.','.'],['.','.']]," +
                                         "'deck':{'size':12,'random':{'rankRange':['2','5']" + ratio + ",'seed':99}}}";
            List<string> none = LevelDeck.Preview(Load(Json("")).Deck).Select(p => p.ToString()).ToList();
            List<string> zero = LevelDeck.Preview(Load(Json(",'pairRatio':0")).Deck).Select(p => p.ToString()).ToList();
            CollectionAssert.AreEqual(none, zero);
            Assert.IsTrue(zero.All(p => !p.StartsWith("[")));
        }

        [Test]
        public void ScriptPair_PlayableWithHint_OrientationMustMatch()
        {
            LevelData level = Load("{'id':13,'goals':[{'type':'SCORE','score':999999}],'board':[['.','.','.'],['.','.','.']]," +
                                   "'deck':{'size':3,'script':[{'pair':['9H','10D'],'orientation':'H_AB','hint':[[1,0],[1,1]]},'2S']}}");
            var s = new GameSession();
            s.StartLevel(level);
            Assert.AreEqual("[9H 10D]", s.CurrentPiece.ToString());
            CollectionAssert.AreEqual(new[] { new CellPos(1, 0), new CellPos(1, 1) }, s.CurrentPiece.Hint);
            Assert.AreEqual("2S", s.NextPiece.ToString());
            Assert.AreEqual(0, s.CardsLeftInDeck, "Deck 3 lá − khối đôi trên tay (2) − lá UP NEXT (1)");

            Assert.AreEqual(PlaceRejectReason.NotHintCell, s.PlacePiece(new CellPos(0, 0)).RejectReason);
            s.RotateCurrentPiece();
            s.RotateCurrentPiece(); // [10D 9H] — cùng 2 ô nhưng ngược lá
            Assert.AreEqual(PlaceRejectReason.NotHintCell, s.PlacePiece(new CellPos(1, 0)).RejectReason, "Sai hướng gợi ý");
            s.RotateCurrentPiece();
            s.RotateCurrentPiece(); // về [9H 10D]
            Assert.IsTrue(s.PlacePiece(new CellPos(1, 0)).Accepted);
            Assert.AreEqual("9H", At(s, 1, 0));
            Assert.AreEqual("10D", At(s, 1, 1));
        }

        // ------------------------------------------------------------------ Endless

        [Test]
        public void Endless_PairRatio_ProducesPairs_ZeroKeepsSingles()
        {
            var cfg = new GameConfig { endlessPairRatio = 1f };
            var s = new GameSession(cfg);
            s.StartEndless(123);
            Assert.IsTrue(s.CurrentPiece.IsPair);
            Assert.IsTrue(s.NextPiece.IsPair);

            var plain = new GameSession();
            plain.StartEndless(123);
            Assert.IsFalse(plain.CurrentPiece.IsPair, "Mặc định endlessPairRatio = 0 (phần A)");
            Assert.AreEqual(plain.CurrentPiece.First.ToString(), s.CurrentPiece.First.ToString(),
                "Lá đầu tiên vẫn lấy từ cùng dãy sinh lá");
        }

        [Test]
        public void UnsupportedPairResolveOrder_FailsFast()
        {
            Assert.Throws<System.NotSupportedException>(() => new GameSession(new GameConfig { pairResolveOrder = "BIGGER_GROUP_FIRST" }));
        }
    }
}
