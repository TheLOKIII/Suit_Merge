using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Config;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Tests
{
    /// <summary>
    /// Hiệu ứng 4 chất (spec 3.3, 4.2–4.7): T3–T7, T11, script L3 / L6 / L8 / L9, gating, trường hợp biên, preview.
    /// </summary>
    public class SuitEffectTests
    {
        private static readonly Suit[] DCS = { Suit.Diamond, Suit.Club, Suit.Spade };
        private static readonly Suit[] All = { Suit.Heart, Suit.Diamond, Suit.Club, Suit.Spade };

        private static GameState State(string boardText, params Suit[] enabled)
        {
            var s = new GameState(Board.FromText(boardText));
            foreach (Suit suit in enabled) s.EnabledSuitEffects.Add(suit);
            return s;
        }

        private static string CardAt(GameState s, int r, int c) => s.Board.GetCard(new CellPos(r, c))?.ToString() ?? ".";

        private static TurnResult Place(GameState s, string card, int r, int c, GameConfig config = null) =>
            new TurnResolver(config ?? GameConfig.Default).PlaceCard(s, new CellPos(r, c), Card.Parse(card));

        private static T Single<T>(TurnResult r) where T : GameEvent => r.Events.OfType<T>().Single();

        private const string Level3 = @"
            3C 3S .  .  .
            .  .  .  .  .
            .  4D .  4D .
            .  .  .  .  .
            .  .  .  2H 2D";

        private const string Level6 = @"
            .  .  .  .  .
            .  5C 5C .  .
            .  .  .  .  .
            .  .  .  .  .
            .  .  .  .  .";

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

        // ================= ♦ Rô =================

        [Test]
        public void T3_Level3_Diamond_TripleScore()
        {
            GameState s = State(Level3, Suit.Diamond);
            TurnResult r = Place(s, "4D", 2, 2);

            Assert.AreEqual("5D", CardAt(s, 2, 2));
            Assert.AreEqual(450, s.Score, "5 × 3 × 10 × 3");
            Assert.AreEqual(1, r.SuitActivations(Suit.Diamond));
            Assert.AreEqual(3, Single<DiamondBonusEvent>(r).Multiplier);
        }

        [Test]
        public void Diamond_NotEnabled_NoEffect()
        {
            GameState s = State(Level3);
            TurnResult r = Place(s, "4D", 2, 2);

            Assert.AreEqual(150, s.Score, "Chưa mở ♦ → chất chỉ là hình ảnh");
            Assert.AreEqual(0, r.SuitActivations(Suit.Diamond));
            Assert.IsEmpty(r.Events.OfType<DiamondBonusEvent>());
        }

        [Test]
        public void Diamond_OnlyMultipliesItsOwnChainStep_First()
        {
            // Bước 1: 2♦×3 (×3) → 3♦ · Bước 2: 3♦ 3H 3S lẫn chất (không ×3)
            GameState s = State(@"
                2D 2D .
                .  3H 3S", All);
            TurnResult r = Place(s, "2D", 0, 2);

            Assert.AreEqual("4D", CardAt(s, 0, 2));
            Assert.AreEqual(270 + 180, s.Score, "3×3×10×3 = 270; 4×3×10×1.5 = 180");
            Assert.AreEqual(1, r.SuitActivations(Suit.Diamond));
        }

        [Test]
        public void Diamond_OnlyMultipliesItsOwnChainStep_Second()
        {
            // Bước 1: lẫn chất → 3♦ · Bước 2: 3♦ 3♦ 3♦ đồng chất → ×3 × chain 1.5
            GameState s = State(@"
                2H 2C .
                .  3D 3D", All);
            TurnResult r = Place(s, "2D", 0, 2);

            Assert.AreEqual("4D", CardAt(s, 0, 2));
            Assert.AreEqual(90 + 540, s.Score, "90; 4×3×10×3×1.5 = 540");
            Assert.AreEqual(1, r.SuitActivations(Suit.Diamond));
        }

        [Test]
        public void Diamond_Kings_MakeAce_NoTriple()
        {
            GameState s = State("KD KD .", All);
            TurnResult r = Place(s, "KD", 0, 2);

            Assert.AreEqual(100000, s.Score, "Ace không nhân ×3");
            Assert.AreEqual(".", CardAt(s, 0, 2));
            Assert.AreEqual(0, r.SuitActivations(Suit.Diamond));
            Assert.IsEmpty(r.Events.OfType<DiamondBonusEvent>());
        }

        [Test]
        public void MixedSuits_NoEffect_EvenWhenAllEnabled()
        {
            GameState s = State("5H 5D .", All);
            TurnResult r = Place(s, "5H", 0, 2);

            Assert.AreEqual("6H", CardAt(s, 0, 2));
            Assert.AreEqual(180, s.Score);
            foreach (Suit suit in All) Assert.AreEqual(0, r.SuitActivations(suit));
        }

        [Test]
        public void EventOrder_CardCreated_ThenEffect_ThenScore()
        {
            GameState s = State(Level3, Suit.Diamond);
            TurnResult r = Place(s, "4D", 2, 2);

            var kinds = r.Events.Select(e => e.GetType()).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                typeof(CardPlacedEvent), typeof(MergeStartedEvent), typeof(CardCreatedEvent),
                typeof(DiamondBonusEvent), typeof(ScoreChangedEvent)
            }, kinds);
        }

        // ================= ♣ Chuồn =================

        [Test]
        public void T4_Level6_Club_SpawnsAbove()
        {
            GameState s = State(Level6, Suit.Diamond, Suit.Club);
            TurnResult r = Place(s, "5C", 1, 3);

            Assert.AreEqual("6C", CardAt(s, 1, 3));
            Assert.AreEqual("6C", CardAt(s, 0, 3), "Spawn khoảng cách 1, 'Trên' thắng (1,4) (2,3) (1,2)");
            Assert.AreEqual(".", CardAt(s, 1, 2));
            Assert.AreEqual(180, s.Score);
            Assert.AreEqual(1, r.SuitActivations(Suit.Club));

            ClubSpawnEvent e = Single<ClubSpawnEvent>(r);
            Assert.AreEqual(new CellPos(1, 3), e.Origin);
            Assert.AreEqual(new CellPos(0, 3), e.SpawnCell);
            Assert.AreEqual("6C", e.Card.ToString());
            Assert.AreEqual(0, e.FallbackBonus);
        }

        [Test]
        public void Level6_Script_TwoTurns_MakeSeven()
        {
            GameState s = State(Level6, Suit.Diamond, Suit.Club);
            Place(s, "5C", 1, 3);
            TurnResult r = Place(s, "6H", 2, 3);

            Assert.AreEqual("7H", CardAt(s, 2, 3), "6♣ 6♣ 6♥ cột → 7 (lẫn chất)");
            Assert.AreEqual(".", CardAt(s, 0, 3));
            Assert.AreEqual(".", CardAt(s, 1, 3));
            Assert.AreEqual(180 + 210, s.Score);
            Assert.AreEqual(0, r.SuitActivations(Suit.Club));
        }

        [Test]
        public void T11_Level9_RouteB_ClubSpawnJoinsChain()
        {
            GameState s = State(Level9, DCS);
            TurnResult r = Place(s, "6C", 2, 2);

            Assert.AreEqual("8C", CardAt(s, 2, 2));
            Assert.AreEqual(".", CardAt(s, 1, 2), "Lá 7♣ spawn đã bị chain cuốn đi");
            Assert.AreEqual(".", CardAt(s, 2, 1));
            Assert.AreEqual(".", CardAt(s, 2, 3));
            Assert.AreEqual(690, s.Score, "210 + 8×4×10×1.5 = 480");
            Assert.AreEqual(2, r.MergeCount);
            Assert.AreEqual(new CellPos(1, 2), Single<ClubSpawnEvent>(r).SpawnCell);
        }

        [Test]
        public void Level9_RouteB_Full_MakesNine_1050()
        {
            GameState s = State(Level9, DCS);
            Place(s, "6C", 2, 2);
            TurnResult r = Place(s, "8S", 1, 2);

            Assert.AreEqual("9S", CardAt(s, 1, 2), "8♥ 8♠ 8♣ 8♦ = nhóm 4 lá → 9");
            Assert.AreEqual(360, r.ScoreGained);
            Assert.AreEqual(1050, s.Score, "210 + 480 + 360");
        }

        [Test]
        public void Club_JoinsChainFalse_SpawnStays()
        {
            var config = new GameConfig { clubSpawnJoinsChain = false };
            GameState s = State(Level9, DCS);
            Place(s, "6C", 2, 2, config);

            Assert.AreEqual("8C", CardAt(s, 2, 2), "Chain chỉ còn 7♣ + 7♠ + 7♠ (N=3)");
            Assert.AreEqual("7C", CardAt(s, 1, 2), "Lá spawn nằm lại");
            Assert.AreEqual(210 + 360, s.Score);
        }

        [Test]
        public void Club_NotEnabled_NoSpawn()
        {
            GameState s = State(Level6, Suit.Diamond);
            TurnResult r = Place(s, "5C", 1, 3);

            Assert.AreEqual(".", CardAt(s, 0, 3));
            Assert.IsEmpty(r.Events.OfType<ClubSpawnEvent>());
        }

        [Test]
        public void Club_SpawnUsesFreedCell()
        {
            // Bàn kín, chỉ còn 2 ô vừa được nhóm giải phóng (Trái, Phải) → spawn vào ô Phải
            GameState s = State(@"
                9H 9S 9H
                4C .  4C
                9H 9S 9H", Suit.Club);
            TurnResult r = Place(s, "4C", 1, 1);

            Assert.AreEqual("5C", CardAt(s, 1, 1));
            Assert.AreEqual("5C", CardAt(s, 1, 2), "Ô vừa giải phóng được tính là trống; Phải trước Trái");
            Assert.AreEqual(".", CardAt(s, 1, 0));
            Assert.AreEqual(150, s.Score);
            Assert.AreEqual(0, Single<ClubSpawnEvent>(r).FallbackBonus);
        }

        // ================= ♠ Bích =================

        [Test]
        public void T5_Level8_Spade_Clears3x3()
        {
            GameState s = State(Level8, DCS);
            TurnResult r = Place(s, "6S", 2, 2);

            Assert.AreEqual("7S", CardAt(s, 2, 2));
            foreach (CellPos p in new[] { new CellPos(1, 1), new CellPos(1, 2), new CellPos(1, 3),
                                          new CellPos(3, 1), new CellPos(3, 2), new CellPos(3, 3) })
                Assert.AreEqual(".", CardAt(s, p.Row, p.Col), p.ToString());
            Assert.AreEqual(210 + 190, s.Score, "210 + (3+4+2+5+2+3)×10");

            SpadeClearEvent e = Single<SpadeClearEvent>(r);
            Assert.AreEqual(190, e.Bonus);
            Assert.AreEqual(6, e.ClearedCells.Count);
            Assert.AreEqual(8, e.Area.Count);
            Assert.AreEqual(1, r.SuitActivations(Suit.Spade));

            // Ngoài vùng 3×3 giữ nguyên
            Assert.AreEqual("2H", CardAt(s, 0, 0));
            Assert.AreEqual("6C", CardAt(s, 1, 0));
            Assert.AreEqual("4S", CardAt(s, 0, 4));
            Assert.AreEqual("2H", CardAt(s, 4, 4));
        }

        [Test]
        public void Spade_NotEnabled_NoClear()
        {
            GameState s = State(Level8, Suit.Diamond, Suit.Club);
            Place(s, "6S", 2, 2);

            Assert.AreEqual(210, s.Score);
            Assert.AreEqual("3H", CardAt(s, 1, 1));
        }

        [Test]
        public void Spade_SkipsLockedCells()
        {
            GameState s = State(@"
                #  2H 4D
                3S .  3S
                5C #  2C", Suit.Spade);
            TurnResult r = Place(s, "3S", 1, 1);

            Assert.AreEqual("4S", CardAt(s, 1, 1));
            SpadeClearEvent e = Single<SpadeClearEvent>(r);
            Assert.AreEqual(6, e.Area.Count, "8 ô quanh tâm trừ 2 ô khóa");
            Assert.AreEqual(130, e.Bonus, "(2+4+5+2)×10");
            Assert.AreEqual(120 + 130, s.Score);
            Assert.IsFalse(s.Board[0, 0].Exists);
            Assert.IsFalse(s.Board[2, 1].Exists);
        }

        [Test]
        public void Spade_ClearBonus_NotMultipliedByChain()
        {
            // Bước 1 lẫn chất → 3♠ · Bước 2: 3♠ 3♠ 3♠ → 4♠ (×1.5) + dọn 7♦ 9♥ (không ×1.5)
            GameState s = State(@"
                2H 2C .  7D
                .  3S 3S 9H", All);
            Place(s, "2S", 0, 2);

            Assert.AreEqual("4S", CardAt(s, 0, 2));
            Assert.AreEqual(".", CardAt(s, 0, 3));
            Assert.AreEqual(".", CardAt(s, 1, 3));
            Assert.AreEqual(90 + 180 + 160, s.Score);
        }

        // ================= ♥ Cơ =================

        [Test]
        public void T6_Heart_ThreeFives_MakeEight()
        {
            GameState s = State("5H 5H .", Suit.Heart);
            TurnResult r = Place(s, "5H", 0, 2);

            Assert.AreEqual("8H", CardAt(s, 0, 2), "5 + 3, không phải 6");
            Assert.AreEqual(240, s.Score, "8 × 3 × 10");
            HeartLeapEvent e = Single<HeartLeapEvent>(r);
            Assert.AreEqual(3, e.Leap);
            Assert.AreEqual(0, e.OverflowBonus);
            Assert.AreEqual(1, r.SuitActivations(Suit.Heart));
        }

        [Test]
        public void Heart_FourFives_MakeNine()
        {
            GameState s = State(@"
                5H 5H .
                .  .  5H", Suit.Heart);
            Place(s, "5H", 0, 2);

            Assert.AreEqual("9H", CardAt(s, 0, 2));
            Assert.AreEqual(360, s.Score);
        }

        [Test]
        public void T7_Heart_Jacks_CapAtKing_Overflow500()
        {
            GameState s = State("JH JH .", Suit.Heart);
            TurnResult r = Place(s, "JH", 0, 2);

            Assert.AreEqual("KH", CardAt(s, 0, 2));
            Assert.AreEqual(890, s.Score, "390 + 500");
            HeartLeapEvent e = Single<HeartLeapEvent>(r);
            Assert.AreEqual(1, e.OverflowRanks);
            Assert.AreEqual(500, e.OverflowBonus);
        }

        [Test]
        public void Heart_Queens_CapAtKing_Overflow1000()
        {
            GameState s = State("QH QH .", Suit.Heart);
            Place(s, "QH", 0, 2);

            Assert.AreEqual("KH", CardAt(s, 0, 2));
            Assert.AreEqual(390 + 1000, s.Score);
        }

        [Test]
        public void Heart_Tens_ExactlyKing_NoOverflow()
        {
            GameState s = State("10H 10H .", Suit.Heart);
            Place(s, "10H", 0, 2);

            Assert.AreEqual("KH", CardAt(s, 0, 2));
            Assert.AreEqual(390, s.Score);
        }

        [Test]
        public void Heart_Kings_MakeAce_NoLeap()
        {
            GameState s = State("KH KH .", Suit.Heart);
            TurnResult r = Place(s, "KH", 0, 2);

            Assert.AreEqual(100000, s.Score);
            Assert.AreEqual(1, r.AcesCreated);
            Assert.IsEmpty(r.Events.OfType<HeartLeapEvent>());
            Assert.AreEqual(0, r.SuitActivations(Suit.Heart));
        }

        [Test]
        public void Heart_NotEnabled_NormalPlusOne()
        {
            GameState s = State("5H 5H .", DCS);
            Place(s, "5H", 0, 2);
            Assert.AreEqual("6H", CardAt(s, 0, 2));
        }

        // ================= Script L3 qua GameSession + TurnResult =================

        [Test]
        public void Session_Level3_Script_SuitActivationsSurvive()
        {
            var session = new GameSession();
            session.Start(Board.FromText(Level3), new ScriptedCardSource("4D", "2C", "3H"), GameMode.Level,
                new[] { Suit.Diamond });
            TurnResult r = session.PlaceCard(new CellPos(2, 2));

            Assert.AreEqual(450, session.Score);
            Assert.AreEqual(1, r.SuitActivations(Suit.Diamond), "Số liệu giữ nguyên sau khi GameSession nối event rút lá");
            Assert.IsInstanceOf<CardsDrawnEvent>(r.Events.Last());
        }

        // ================= Xem trước (preview) =================

        private static GameSession Scripted(string boardText, Suit[] suits, params string[] cards)
        {
            var session = new GameSession();
            session.Start(Board.FromText(boardText), new ScriptedCardSource(cards), GameMode.Level, suits);
            return session;
        }

        [Test]
        public void Preview_Spade_ReportsAreaWithoutChangingBoard()
        {
            GameSession s = Scripted(Level8, DCS, "6S", "2H");
            string before = s.Board.ToText();
            PlacementPreview p = s.PreviewPlacement(new CellPos(2, 2));

            Assert.AreEqual(Suit.Spade, p.FirstEffectSuit);
            Assert.AreEqual(8, p.SpadeArea.Count);
            Assert.IsNull(p.ClubSpawnCell);
            Assert.AreEqual(400, p.ScoreGain);
            Assert.AreEqual(before, s.Board.ToText(), "Preview không được đổi bàn");
            Assert.AreEqual(0, s.Score);
        }

        [Test]
        public void Preview_Club_ReportsSpawnCell()
        {
            GameSession s = Scripted(Level6, new[] { Suit.Diamond, Suit.Club }, "5C", "6H");
            PlacementPreview p = s.PreviewPlacement(new CellPos(1, 3));

            Assert.AreEqual(Suit.Club, p.FirstEffectSuit);
            Assert.AreEqual(new CellPos(0, 3), p.ClubSpawnCell);
            Assert.IsEmpty(p.SpadeArea);
            Assert.AreEqual(".", s.Board.GetCard(new CellPos(0, 3))?.ToString() ?? ".");
        }

        [Test]
        public void Preview_DiamondAndHeart_ReportSuit()
        {
            Assert.AreEqual(Suit.Diamond,
                Scripted(Level3, All, "4D", "2C").PreviewPlacement(new CellPos(2, 2)).FirstEffectSuit);
            Assert.AreEqual(Suit.Heart,
                Scripted("5H 5H .", All, "5H", "2C").PreviewPlacement(new CellPos(0, 2)).FirstEffectSuit);
        }

        [Test]
        public void Preview_MixedSuits_NoEffect()
        {
            PlacementPreview p = Scripted("5H 5D .", All, "5H", "2C").PreviewPlacement(new CellPos(0, 2));
            Assert.IsTrue(p.WillMerge);
            Assert.AreEqual(Suit.None, p.FirstEffectSuit);
            Assert.IsEmpty(p.SpadeArea);
            Assert.IsNull(p.ClubSpawnCell);
        }

        [Test]
        public void Preview_OnlyFirstStep_SpadeInChainNotShown()
        {
            // ♠ chỉ xảy ra ở bước chain 2 → preview (chỉ bước đầu) không báo ♠
            PlacementPreview p = Scripted(@"
                2H 2C .  7D
                .  3S 3S 9H", All, "2S", "2C").PreviewPlacement(new CellPos(0, 2));

            Assert.AreEqual(2, p.MergeSteps);
            Assert.AreEqual(Suit.None, p.FirstEffectSuit);
            Assert.IsEmpty(p.SpadeArea);
            Assert.AreEqual(430, p.ScoreGain, "Điểm xem trước vẫn tính đủ cả chain");
        }
    }
}
