using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Config;
using SuitMerge.Core.Goals;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Tests
{
    /// <summary>
    /// Bước 4.3 — mục tiêu + thắng/thua Level (spec 6, 7, 3.1 bước 7).
    /// CREATE_RANK theo quyết định "Tính 1": chỉ lá tạo trong lượt còn trên bàn cuối lượt; Ace có tính.
    /// </summary>
    public class GoalTests
    {
        private const string L1 = @"{
  'id': 1, 'goals': [{ 'type': 'CREATE_RANK', 'rank': '4', 'count': 2 }],
  'board': [['2H','2C','.','.'], ['.','.','.','.'], ['.','.','3D','.'], ['.','.','.','.']],
  'deck': { 'size': 12,
    'script': [ { 'card': '2S', 'hint': [0,2] }, { 'card': '3H', 'hint': [1,2] }, '2D','2C','3S','2H','3C','2D','3H','2S' ],
    'random': { 'rankRange': ['2','3'], 'seed': 1003 } }
}";

        /// <summary>Level chỉ có deck script (size = số lá script), không gợi ý.</summary>
        private static LevelData Level(string goals, string board, string cards, string suits = "")
        {
            int size = cards.Split(',').Length;
            string json = $"{{'id':30,'goals':{goals},'board':{board},'enabledSuitEffects':[{suits}]," +
                          $"'deck':{{'size':{size},'script':[{cards}]}}}}";
            LevelLoadResult r = LevelLoader.Load(json.Replace('\'', '"'));
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            return r.Level;
        }

        private static LevelData LoadL1()
        {
            LevelLoadResult r = LevelLoader.Load(L1.Replace('\'', '"'));
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            return r.Level;
        }

        private static GameSession Start(LevelData level, GameConfig config = null)
        {
            var s = new GameSession(config);
            s.StartLevel(level);
            return s;
        }

        private static TurnResult Place(GameSession s, int r, int c)
        {
            TurnResult t = s.PlacePiece(new CellPos(r, c));
            Assert.IsTrue(t.Accepted, $"Đặt ({r},{c}) bị từ chối: {t.RejectReason}");
            return t;
        }

        // ------------------------------------------------------------------ L1 theo lời giải spec 12.2

        [Test]
        public void L1_SpecSolution_ThenSecondFour_Wins()
        {
            GameSession s = Start(LoadL1());
            Assert.AreEqual(1, s.Goals.Count);
            Assert.AreEqual(0, s.Goals[0].Current);

            TurnResult t1 = Place(s, 0, 2);                    // 2♥2♣2♠ → 3♠
            Assert.AreEqual(0, s.Goals[0].Current, "Lá 3 < 4: chưa tính");
            Assert.IsFalse(t1.Events.OfType<GoalProgressEvent>().Any(), "Không đổi tiến độ → không có event");

            TurnResult t2 = Place(s, 1, 2);                    // 3♠3♥3♦ cột → lá 4
            Assert.AreEqual(1, s.Goals[0].Current);
            GoalProgressEvent ev = t2.Events.OfType<GoalProgressEvent>().Single();
            Assert.AreEqual(0, ev.OldValue);
            Assert.AreEqual(1, ev.NewValue);
            Assert.AreEqual(2, ev.Target);
            Assert.IsFalse(ev.JustCompleted);
            Assert.AreEqual(GameStatus.Playing, s.Status);

            Place(s, 3, 0);                                    // 2D
            Place(s, 3, 1);                                    // 2C
            Place(s, 2, 3);                                    // 3S
            Place(s, 3, 2);                                    // 2H → 2D 2C 2H → 3H tại (3,2)
            TurnResult win = Place(s, 3, 3);                   // 3C + 3H + 3S → lá 4 thứ hai
            Assert.AreEqual(2, s.Goals[0].Current);
            Assert.IsTrue(s.Goals[0].IsDone);
            Assert.IsTrue(win.Events.OfType<GoalProgressEvent>().Single().JustCompleted);
            Assert.AreEqual(GameStatus.Won, s.Status);
            Assert.IsTrue(s.IsWon);

            // Thứ tự event cuối lượt: CardsDrawn → GoalProgress → LevelWon
            var tail = win.Events.Skip(win.Events.Count - 3).ToList();
            Assert.IsInstanceOf<CardsDrawnEvent>(tail[0]);
            Assert.IsInstanceOf<GoalProgressEvent>(tail[1]);
            Assert.IsInstanceOf<LevelWonEvent>(tail[2]);
            Assert.AreEqual(s.Score, ((LevelWonEvent)tail[2]).FinalScore);
            Assert.IsFalse(win.Events.OfType<GameOverEvent>().Any());

            TurnResult after = s.PlacePiece(new CellPos(0, 0));
            Assert.IsFalse(after.Accepted, "Thắng rồi không đặt thêm được");
            Assert.AreEqual(PlaceRejectReason.GameNotPlaying, after.RejectReason);
        }

        // ------------------------------------------------------------------ CREATE_RANK

        private const string ChainBoard = "[['3H','3D','.'],['.','4S','4C']]";

        [Test]
        public void CreateRank_ChainCountsOnlyFinalCard()
        {
            // 3C vào (0,2): 3 lá 3 → lá 4 tại (0,2) → chain với 4S 4C → lá 5. Lá 4 đã bị nuốt → chỉ +1
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'4','count':2}]", ChainBoard, "'3C','2S'"));
            TurnResult t = Place(s, 0, 2);
            Assert.AreEqual(2, t.MergeCount, "Có chain 2 bước");
            Assert.AreEqual(Rank.Five, s.Board.GetCard(new CellPos(0, 2)).Rank);
            Assert.AreEqual(1, s.Goals[0].Current);
        }

        [Test]
        public void CreateRank_EveryChainStep_WhenFlagOn()
        {
            var config = new GameConfig { createRankCountsEveryChainStep = true };
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'4','count':2}]", ChainBoard, "'3C','2S'"), config);
            Place(s, 0, 2);
            Assert.AreEqual(2, s.Goals[0].Current, "Cờ bật: lá 4 và lá 5 đều tính");
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void CreateRank_ExactRankOnly_WhenHigherFlagOff()
        {
            var config = new GameConfig { createRankCountsHigher = false };
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'4','count':1}]", ChainBoard, "'3C','2S'"), config);
            Place(s, 0, 2);
            Assert.AreEqual(0, s.Goals[0].Current, "Chỉ lá 5 còn lại, không bằng 4");
        }

        private const string ClubBoard = "[['.','.','.','.','.'],['.','5C','5C','.','.'],['.','.','.','.','.']]";

        [Test]
        public void CreateRank_ClubSpawnCounts()
        {
            // L6: 5C vào (1,3) → 6♣ tại (1,3) + 6♣ sinh ra; cả 2 còn trên bàn → +2
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'6','count':2}]", ClubBoard, "'5C','2S'", "'C'"));
            TurnResult t = Place(s, 1, 3);
            Assert.AreEqual(1, t.SuitActivations(Suit.Club));
            Assert.AreEqual(2, s.Goals[0].Current);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void CreateRank_ClubSpawnNotCounted_WhenFlagOff()
        {
            var config = new GameConfig { createRankCountsClubSpawn = false };
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'6','count':2}]", ClubBoard, "'5C','2S'", "'C'"), config);
            Place(s, 1, 3);
            Assert.AreEqual(1, s.Goals[0].Current);
        }

        [Test]
        public void CreateRank_HeartLeapOverTarget_Counts()
        {
            // 3 lá 3♥ đồng chất ♥ → rank 3 + 3 = 6 (nhảy qua 4, 5)
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'5','count':1}]", "[['3H','3H','.'],['.','.','.']]", "'3H','2S'", "'H'"));
            Place(s, 0, 2);
            Assert.AreEqual(Rank.Six, s.Board.GetCard(new CellPos(0, 2)).Rank);
            Assert.AreEqual(1, s.Goals[0].Current);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void CreateRank_AceCounts()
        {
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'J','count':1}]", "[['KH','KD','.'],['.','.','.']]", "'KC','2S'"));
            TurnResult t = Place(s, 0, 2);
            Assert.AreEqual(1, t.AcesCreated);
            Assert.AreEqual(1, s.Goals[0].Current, "Ace ≥ J → có tính");
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        // ------------------------------------------------------------------ ACE / SCORE / SUIT_TRIGGER / COMBO

        [Test]
        public void Ace_Goal()
        {
            GameSession s = Start(Level("[{'type':'ACE','count':1}]", "[['KH','KD','.'],['.','.','.']]", "'KC','2S'"));
            Place(s, 0, 2);
            Assert.AreEqual(1, s.Goals[0].Current);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void Score_Goal_TracksScore()
        {
            GameSession s = Start(Level("[{'type':'SCORE','score':1}]", "[['2H','2D','.'],['.','.','.']]", "'2C','5S'"));
            Place(s, 0, 2);
            Assert.Greater(s.Score, 0);
            Assert.AreEqual(s.Score, s.Goals[0].Current);
            Assert.AreEqual(GameStatus.Won, s.Status);

            GameSession far = Start(Level("[{'type':'SCORE','score':999999}]", "[['2H','2D','.'],['.','.','.']]", "'2C','5S'"));
            Place(far, 0, 2);
            Assert.AreEqual(far.Score, far.Goals[0].Current);
            Assert.AreEqual(GameStatus.Playing, far.Status);
        }

        [Test]
        public void SuitTrigger_SpadeTwice_Wins()
        {
            const string board = "[['4S','4S','.','.','.'],['.','.','.','.','.'],['.','.','.','7S','7S']]";
            GameSession s = Start(Level("[{'type':'SUIT_TRIGGER','suit':'S','count':2}]", board, "'4S','7S','2H'", "'S'"));
            Place(s, 0, 2);
            Assert.AreEqual(1, s.Goals[0].Current);
            Assert.AreEqual(GameStatus.Playing, s.Status);
            Place(s, 2, 2);
            Assert.AreEqual(2, s.Goals[0].Current);
            Assert.AreEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void SuitTrigger_DisabledSuit_DoesNotCount()
        {
            const string board = "[['4S','4S','.'],['.','.','.']]";
            LevelLoadResult r = LevelLoader.Load(("{'id':30,'goals':[{'type':'SUIT_TRIGGER','suit':'S','count':1}],'board':" + board +
                                                  ",'deck':{'size':2,'script':['4S','2H']}}").Replace('\'', '"'));
            GameSession s = Start(r.Level);
            Place(s, 0, 2);
            Assert.AreEqual(0, s.Goals[0].Current, "♠ chưa mở → hiệu ứng không kích hoạt");
        }

        [Test]
        public void Combo_NeedsAllGoals()
        {
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'3','count':1},{'type':'SCORE','score':999999}]",
                "[['2H','2D','.'],['.','.','.']]", "'2C','5S','6S'"));
            Place(s, 0, 2);
            Assert.IsTrue(s.Goals[0].IsDone);
            Assert.IsFalse(s.Goals[1].IsDone);
            Assert.AreEqual(GameStatus.Playing, s.Status, "Xong 1/2 mục tiêu chưa thắng");
        }

        // ------------------------------------------------------------------ Thắng / thua

        [Test]
        public void LastCard_CompletesGoal_WinsNotOutOfCards()
        {
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'3','count':1}]", "[['2H','2D','.'],['.','.','.']]", "'2C'"));
            Assert.IsNull(s.NextPiece);
            TurnResult t = Place(s, 0, 2);
            Assert.AreEqual(GameStatus.Won, s.Status, "Thắng ưu tiên hơn hết lá");
            Assert.AreEqual(LoseReason.None, s.LoseReason);
            Assert.IsFalse(t.Events.OfType<GameOverEvent>().Any());
        }

        [Test]
        public void OutOfCards_GoalNotDone_Loses()
        {
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'4','count':1}]", "[['2H','2D','.'],['.','.','.']]", "'2C'"));
            TurnResult t = Place(s, 0, 2);
            Assert.AreEqual(GameStatus.Lost, s.Status);
            Assert.AreEqual(LoseReason.OutOfCards, s.LoseReason);
            Assert.IsFalse(t.Events.OfType<LevelWonEvent>().Any());
        }

        [Test]
        public void BoardFull_GoalNotDone_Loses()
        {
            GameSession s = Start(Level("[{'type':'SCORE','score':100}]", "[['2H','3D'],['4C','.']]", "'5S','6S'"));
            Place(s, 1, 1);
            Assert.AreEqual(GameStatus.Lost, s.Status);
            Assert.AreEqual(LoseReason.BoardFull, s.LoseReason);
        }

        // ------------------------------------------------------------------ Undo / Endless

        [Test]
        public void Undo_RestoresGoalProgressAndStatus()
        {
            GameSession s = Start(Level("[{'type':'CREATE_RANK','rank':'3','count':1}]", "[['2H','2D','.'],['.','.','.']]", "'2C','5S','6S'"));
            GameSession.Snapshot snap = s.CaptureSnapshot();
            Place(s, 0, 2);
            Assert.AreEqual(GameStatus.Won, s.Status);

            s.RestoreSnapshot(snap);
            Assert.AreEqual(0, s.Goals[0].Current);
            Assert.AreEqual(GameStatus.Playing, s.Status);
            Assert.AreEqual("2C", s.CurrentPiece.ToString());
        }

        [Test]
        public void Endless_HasNoGoals_NeverWins()
        {
            var s = new GameSession();
            s.StartEndless(3);
            Assert.AreEqual(0, s.Goals.Count);
            for (int i = 0; i < 40 && s.Status == GameStatus.Playing; i++)
            {
                TurnResult t = s.PlacePiece(s.Board.AllPositions().First(p => s.Board.IsPlaceable(p)));
                Assert.IsFalse(t.Events.OfType<GoalProgressEvent>().Any());
                Assert.IsFalse(t.Events.OfType<LevelWonEvent>().Any());
            }
            Assert.AreNotEqual(GameStatus.Won, s.Status);
        }

        [Test]
        public void Restart_ResetsGoals()
        {
            LevelData level = Level("[{'type':'CREATE_RANK','rank':'3','count':1}]", "[['2H','2D','.'],['.','.','.']]", "'2C','5S'");
            GameSession s = Start(level);
            Place(s, 0, 2);
            s.StartLevel(level);
            Assert.AreEqual(0, s.Goals[0].Current);
            Assert.AreEqual(GameStatus.Playing, s.Status);
        }
    }
}
