using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;

namespace SuitMerge.Tests
{
    /// <summary>Bước 4.1 — đọc file level JSON (spec 12.1) + báo lỗi / cảnh báo cho người viết level.</summary>
    public class LevelLoaderTests
    {
        /// <summary>Ví dụ spec 12.1 (= L1), tutorial song ngữ.</summary>
        private const string SpecExample = @"{
  'id': 1,
  'phase': 1,
  'type': 'Onboarding',
  'difficulty': 1,
  'goals': [{ 'type': 'CREATE_RANK', 'rank': '4', 'count': 2 }],
  'enabledSuitEffects': [],
  'unlockedBoosters': [],
  'board': [['2H','2C','.','.'], ['.','.','.','.'], ['.','.','3D','.'], ['.','.','.','.']],
  'deck': {
    'size': 12,
    'script': [
      { 'card': '2S', 'hint': [0,2] },
      { 'card': '3H', 'hint': [1,2] },
      '2D','2C','3S','2H','3C','2D','3H','2S'
    ],
    'random': { 'rankRange': ['2','3'], 'rankWeights': null, 'suitWeights': null, 'pairRatio': 0, 'seed': 1001 }
  },
  'tutorial': [{ 'turn': 1, 'text': { 'vi': 'Ghép 3 lá giống nhau!', 'en': 'Match 3 same cards!' }, 'handPointer': [0,2] }],
  'reward': { 'coins': 20, 'chest': false }
}";

        /// <summary>Viết JSON bằng nháy đơn cho dễ đọc trong test.</summary>
        private static string J(string s) => s.Replace('\'', '"');

        /// <summary>Level hợp lệ tối thiểu; truyền phần cần thay để thử từng lỗi.</summary>
        private static string Level(string goals = "[{'type':'SCORE','score':100}]",
            string board = "[['.','.'],['.','2H']]",
            string deck = "{'size':3,'script':['2S'],'random':{'rankRange':['2','3'],'seed':1}}",
            string extra = "", int id = 1) =>
            J($"{{'id':{id},'goals':{goals},'board':{board},'deck':{deck}{extra}}}");

        private static LevelLoadResult Load(string json, string fileName = null) => LevelLoader.Load(json, fileName);

        private static LevelData LoadOk(string json)
        {
            LevelLoadResult r = Load(json);
            Assert.IsTrue(r.Ok, "Lỗi không mong đợi:\n" + string.Join("\n", r.Errors));
            return r.Level;
        }

        private static void AssertError(LevelLoadResult r, string fragment)
        {
            Assert.IsFalse(r.Ok, "Phải có lỗi chứa: " + fragment);
            Assert.IsNull(r.Level, "Có lỗi thì không trả dữ liệu");
            Assert.IsTrue(r.Errors.Any(e => e.Contains(fragment)),
                $"Không thấy lỗi chứa \"{fragment}\". Các lỗi:\n" + string.Join("\n", r.Errors));
        }

        private static void AssertWarning(LevelLoadResult r, string fragment)
        {
            Assert.IsTrue(r.Warnings.Any(w => w.Contains(fragment)),
                $"Không thấy cảnh báo chứa \"{fragment}\". Các cảnh báo:\n" + string.Join("\n", r.Warnings));
        }

        // ------------------------------------------------------------------ Đọc đúng

        [Test]
        public void SpecExample_LoadsEveryField()
        {
            LevelLoadResult r = Load(J(SpecExample), "level_001");
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            Assert.AreEqual(0, r.Warnings.Count, string.Join("\n", r.Warnings));
            LevelData l = r.Level;

            Assert.AreEqual(1, l.Id);
            Assert.AreEqual(1, l.Phase);
            Assert.AreEqual("Onboarding", l.Type);
            Assert.AreEqual(1, l.Difficulty);

            Assert.AreEqual(1, l.Goals.Count);
            Assert.AreEqual(GoalType.CreateRank, l.Goals[0].Type);
            Assert.AreEqual(Rank.Four, l.Goals[0].Rank);
            Assert.AreEqual(2, l.Goals[0].Target);
            Assert.AreEqual(0, l.EnabledSuitEffects.Count);
            Assert.AreEqual(0, l.UnlockedBoosters.Count);

            Assert.AreEqual(4, l.Rows);
            Assert.AreEqual(4, l.Cols);
            Assert.AreEqual(Board.FromText("2H 2C . .\n. . . .\n. . 3D .\n. . . .").ToText(), l.CreateBoard().ToText());

            Assert.AreEqual(12, l.Deck.Size);
            Assert.AreEqual(10, l.Deck.Script.Count);
            Assert.AreEqual(10, l.Deck.ScriptCardCount);
            Assert.AreEqual(2, l.Deck.RandomCardCount);
            Assert.AreEqual("2S", l.Deck.Script[0].Cards[0].ToString());
            Assert.AreEqual(new CellPos(0, 2), l.Deck.Script[0].Hint.Single());
            Assert.AreEqual(new CellPos(1, 2), l.Deck.Script[1].Hint.Single());
            Assert.AreEqual(0, l.Deck.Script[2].Hint.Count);
            Assert.AreEqual("2S", l.Deck.Script[9].Cards[0].ToString());
            Assert.AreEqual(Rank.Two, l.Deck.Random.MinRank);
            Assert.AreEqual(Rank.Three, l.Deck.Random.MaxRank);
            Assert.AreEqual(1001u, l.Deck.Random.Seed);
            Assert.AreEqual(0, l.Deck.Random.PairRatio);

            Assert.AreEqual(1, l.Tutorial.Count);
            Assert.AreEqual(1, l.Tutorial[0].Turn);
            Assert.AreEqual("Ghép 3 lá giống nhau!", l.Tutorial[0].Text.Get("vi"));
            Assert.AreEqual("Match 3 same cards!", l.Tutorial[0].Text.Get("en"));
            Assert.AreEqual(new CellPos(0, 2), l.Tutorial[0].HandPointer);
            Assert.AreEqual(20, l.Reward.Coins);
            Assert.IsFalse(l.Reward.Chest);

            Assert.AreEqual("bàn 4×4, deck 12 (script 10 + random 2), mục tiêu: Tạo 2 lá 4, chất mở: chưa mở",
                LevelLoader.Summary(l));
        }

        [Test]
        public void ProjectFile_Level001_IsOk()
        {
            string path = FindProjectFile("Assets/_Game/Data/Levels/level_001.json");
            if (path == null) Assert.Inconclusive("Không tìm thấy level_001.json (chạy từ thư mục project Unity).");
            LevelLoadResult r = Load(File.ReadAllText(path), "level_001");
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            Assert.AreEqual(0, r.Warnings.Count, string.Join("\n", r.Warnings));
        }

        private static string FindProjectFile(string relative)
        {
            var roots = new List<string> { Directory.GetCurrentDirectory() };
            string env = Environment.GetEnvironmentVariable("SUITMERGE_PROJECT");
            if (!string.IsNullOrEmpty(env)) roots.Add(env);
            return roots.Select(r => Path.Combine(r, relative)).FirstOrDefault(File.Exists);
        }

        [Test]
        public void CreateBoard_ReturnsNewBoardEachTime()
        {
            LevelData l = LoadOk(J(SpecExample));
            Board a = l.CreateBoard();
            Board b = l.CreateBoard();
            a[new CellPos(0, 0)].Card = null;
            Assert.AreEqual("2H", b.GetCard(new CellPos(0, 0)).ToString(), "Sửa bàn đang chơi không làm hỏng dữ liệu level");
        }

        [Test]
        public void Goals_AllTypes_AndCombo()
        {
            LevelLoadResult r = Load(Level(
                goals: "[{'type':'CREATE_RANK','rank':'J','count':1},{'type':'SCORE','score':2800},{'type':'ACE','count':1}," +
                       "{'type':'SUIT_TRIGGER','suit':'S','count':2},{'type':'SIGIL_TRIGGER','count':3}," +
                       "{'type':'CLEAR_BLOCKER','blocker':'STONE','count':4}]",
                extra: ",'enabledSuitEffects':['D','C','S']"));
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            IReadOnlyList<GoalData> g = r.Level.Goals;
            Assert.AreEqual(6, g.Count, "Nhiều mục tiêu = COMBO");
            Assert.AreEqual(Rank.Jack, g[0].Rank);
            Assert.AreEqual(2800, g[1].Target);
            Assert.AreEqual("Đạt 2,800 điểm", LevelLoader.Describe(g[1]));
            Assert.AreEqual(GoalType.Ace, g[2].Type);
            Assert.AreEqual(Suit.Spade, g[3].Suit);
            Assert.AreEqual("Kích hoạt Bích 2 lần", LevelLoader.Describe(g[3]));
            Assert.AreEqual(BlockerType.Stone, g[5].Blocker);
            Assert.AreEqual(new[] { Suit.Diamond, Suit.Club, Suit.Spade }, r.Level.EnabledSuitEffects.ToArray());
            AssertWarning(r, "Ô Ấn chưa chơi được");
            AssertWarning(r, "chướng ngại chưa chơi được");
        }

        [Test]
        public void Goal_Errors()
        {
            AssertError(Load(Level(goals: "[{'type':'CREATE_RANK','count':2}]")), "thiếu \"rank\"");
            AssertError(Load(Level(goals: "[{'type':'CREATE_RANK','rank':'2','count':2}]")), "lá 2 không thể");
            AssertError(Load(Level(goals: "[{'type':'CREATE_RANK','rank':'A','count':1}]")), "\"ACE\"");
            AssertError(Load(Level(goals: "[{'type':'SCORE','score':0}]")), "phải ≥ 1");
            AssertError(Load(Level(goals: "[{'type':'COMBO'}]")), "không cần loại \"COMBO\"");
            AssertError(Load(Level(goals: "[{'type':'MAKE_RANK'}]")), "\"MAKE_RANK\" không hợp lệ");
            AssertError(Load(Level(goals: "[]")), "ít nhất 1 mục tiêu");
            AssertWarning(Load(Level(goals: "[{'type':'SUIT_TRIGGER','suit':'S','count':2}]")), "chưa mở chất này");
        }

        [Test]
        public void Weights_UnlistedShareTheRemainder()
        {
            LevelData l = LoadOk(Level(deck: "{'size':3,'random':{'rankRange':['4','7'],'rankWeights':{'7':0.1}," +
                                             "'suitWeights':{'C':0.35,'D':0.3},'seed':7}}"));
            RandomDeckData rnd = l.Deck.Random;
            Assert.AreEqual(0.1, rnd.RankProbability(Rank.Seven), 1e-9);
            Assert.AreEqual(0.3, rnd.RankProbability(Rank.Four), 1e-9);
            Assert.AreEqual(0, rnd.RankProbability(Rank.Eight), 1e-9, "Ngoài rankRange");
            Assert.AreEqual(0.35, rnd.SuitProbability(Suit.Club), 1e-9);
            Assert.AreEqual(0.175, rnd.SuitProbability(Suit.Heart), 1e-9);
            Assert.AreEqual(0.175, rnd.SuitProbability(Suit.Spade), 1e-9);

            LevelData even = LoadOk(Level());
            Assert.AreEqual(0.25, even.Deck.Random.SuitProbability(Suit.Diamond), 1e-9, "null / không ghi = chia đều");
            Assert.AreEqual(0.5, even.Deck.Random.RankProbability(Rank.Two), 1e-9);

            // L4: ghi đủ 4 rank, tổng = 1
            LevelData l4 = LoadOk(Level(deck: "{'size':3,'random':{'rankRange':['2','5'],'rankWeights':{'2':0.3,'3':0.3,'4':0.25,'5':0.15},'seed':4}}"));
            Assert.AreEqual(0.15, l4.Deck.Random.RankProbability(Rank.Five), 1e-9);
        }

        [Test]
        public void Weights_Errors()
        {
            string D(string weights) => $"{{'size':3,'random':{{'rankRange':['2','4'],{weights},'seed':1}}}}";
            AssertError(Load(Level(deck: D("'suitWeights':{'D':0.7,'C':0.5}"))), "phải ≤ 1");
            AssertError(Load(Level(deck: D("'rankWeights':{'2':0.3,'3':0.3,'4':0.3}"))), "tổng phải = 1");
            AssertError(Load(Level(deck: D("'rankWeights':{'9':0.3}"))), "\"9\" không hợp lệ");
            AssertError(Load(Level(deck: D("'suitWeights':{'X':0.3}"))), "\"X\" không hợp lệ");
            AssertError(Load(Level(deck: D("'suitWeights':{'D':1.5}"))), "từ 0 tới 1");
            AssertWarning(Load(Level(deck: D("'suitWeights':{'D':1}"))), "sẽ không bao giờ ra");
        }

        // ------------------------------------------------------------------ JSON sai cú pháp

        [Test]
        public void Json_SyntaxError_HasLineAndColumn()
        {
            LevelLoadResult r = Load("{\n  \"id\": 1\n  \"goals\": []\n}");
            AssertError(r, "dòng 2, cột 10");
            AssertError(r, "thiếu dấu ','");

            AssertError(Load("{ \"id\": 1, }"), "dấu ',' thừa trước '}'");
            AssertError(Load("[1, 2, ]"), "dấu ',' thừa trước ']'");
            AssertError(Load("{ \"id\": 'x' }"), "ngoặc kép");
            AssertError(Load("{ \"id\": 1, \"id\": 2 }"), "viết 2 lần");
            AssertError(Load(""), "file rỗng");
            AssertError(Load("{ \"id\": 1"), "thiếu dấu '}'");
            AssertError(Load("[1, 2]"), "phải là 1 object");
        }

        [Test]
        public void MiniJson_ReadsValueTypes()
        {
            var obj = (Dictionary<string, object>)MiniJson.Parse("\uFEFF{ \"a\": [1, -2.5, true, false, null], \"s\": \"x\\\"y\\u0041\\n\", \"o\": {} }");
            var a = (List<object>)obj["a"];
            Assert.AreEqual(1.0, a[0]);
            Assert.AreEqual(-2.5, a[1]);
            Assert.AreEqual(true, a[2]);
            Assert.AreEqual(false, a[3]);
            Assert.IsNull(a[4]);
            Assert.AreEqual("x\"yA\n", obj["s"]);
            Assert.AreEqual(0, ((Dictionary<string, object>)obj["o"]).Count);
        }

        [Test]
        public void ManyErrors_ReportedTogether()
        {
            LevelLoadResult r = Load(Level(goals: "[{'type':'MAKE_RANK'}]", board: "[['.','2X'],['.','.']]", deck: "{'script':['2S']}"));
            AssertError(r, "\"MAKE_RANK\" không hợp lệ");
            AssertError(r, "\"2X\" không hợp lệ");
            AssertError(r, "thiếu \"size\"");
            Assert.GreaterOrEqual(r.Errors.Count, 3);
        }

        // ------------------------------------------------------------------ Bàn

        [Test]
        public void Board_Errors()
        {
            AssertError(Load(Level(board: "[['.','.'],['.']]")), "các hàng phải dài bằng nhau");
            AssertError(Load(Level(board: "[['.','ST2'],['.','.']]")), "chưa hỗ trợ (sẽ có ở M6)");
            AssertError(Load(Level(board: "[['.','FD:7S'],['ICE1:7D','8C@S']]")), "chướng ngại");
            AssertError(Load(Level(board: "[['2H','#'],['#','3S']]")), "không có ô trống");
            AssertError(Load(Level(board: "[]")), "không có hàng nào");
            AssertError(Load(Level(board: "[['.', 2]]")), "phải là chuỗi");
        }

        [Test]
        public void Board_LockedCells_Allowed()
        {
            LevelData l = LoadOk(Level(board: "[['#','.','#'],['.','.','.'],['#','.','#']]"));
            Assert.IsFalse(l.CreateBoard()[new CellPos(0, 0)].Exists);
            Assert.AreEqual(3, l.Cols);
        }

        // ------------------------------------------------------------------ Deck

        [Test]
        public void Deck_Errors()
        {
            AssertError(Load(Level(deck: "{'size':1,'script':['2S','3S']}")), "size phải ≥ số lá trong script");
            AssertError(Load(Level(deck: "{'size':5,'script':['2S']}")), "còn 4 lá phải rút ngẫu nhiên");
            AssertError(Load(Level(deck: "{'size':3,'random':{'rankRange':['2','3']}}")), "thiếu \"seed\"");
            AssertError(Load(Level(deck: "{'size':3,'random':{'rankRange':['5','3'],'seed':1}}")), "lớn hơn rank cao");
            AssertError(Load(Level(deck: "{'size':3,'random':{'rankRange':['2','A'],'seed':1}}")), "rank từ \"2\" tới \"K\"");
            AssertError(Load(Level(deck: "{'size':3,'random':{'rankRange':['2','3'],'seed':-1}}")), "số nguyên ≥ 0");
            AssertError(Load(Level(deck: "{'size':2,'script':['2S','1H']}")), "lá \"1H\" không hợp lệ");
            AssertWarning(Load(Level(deck: "{'size':1,'script':['2S'],'random':{'rankRange':['2','3'],'seed':1}}")),
                "không bao giờ được dùng");
        }

        [Test]
        public void Hint_Checks()
        {
            string Hint(string cell) => $"{{'size':3,'script':[{{'card':'2S','hint':{cell}}}],'random':{{'rankRange':['2','3'],'seed':1}}}}";
            AssertError(Load(Level(deck: Hint("[5,0]"))), "nằm ngoài bàn 2×2");
            AssertError(Load(Level(deck: Hint("[0]"))), "[hàng, cột]");
            AssertError(Load(Level(board: "[['#','.'],['.','.']]", deck: Hint("[0,0]"))), "ô khóa");
            AssertWarning(Load(Level(deck: Hint("[1,1]"))), "đang có lá lúc bắt đầu");
        }

        [Test]
        public void PairEntry_ParsedForLater()
        {
            LevelLoadResult r = Load(Level(id: 13,
                board: "[['.','.','.'],['.','.','.'],['.','.','.']]",
                deck: "{'size':5,'script':[{'pair':['9H','10D'],'orientation':'V_AB','hint':[[1,1],[2,1]]},'2S']," +
                      "'random':{'rankRange':['2','3'],'pairRatio':0.2,'seed':13}}"));
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            Assert.AreEqual(0, r.Warnings.Count, string.Join("\n", r.Warnings));
            DeckScriptEntry pair = r.Level.Deck.Script[0];
            Assert.IsTrue(pair.IsPair);
            Assert.AreEqual(PieceOrientation.V_AB, pair.Orientation);
            Assert.AreEqual(new[] { new CellPos(1, 1), new CellPos(2, 1) }, pair.Hint.ToArray());
            Assert.AreEqual(3, r.Level.Deck.ScriptCardCount, "Khối đôi tính 2 lá");
            Assert.AreEqual(2, r.Level.Deck.RandomCardCount);
            Assert.AreEqual(0.2, r.Level.Deck.Random.PairRatio, 1e-9);

            string Pair(string body) => $"{{'size':5,'script':[{body}],'random':{{'rankRange':['2','3'],'seed':1}}}}";
            string b3 = "[['.','.','.'],['.','.','.'],['.','.','.']]";
            AssertError(Load(Level(board: b3, deck: Pair("{'pair':['9H'],'hint':null}"))), "đúng 2 lá");
            AssertError(Load(Level(board: b3, deck: Pair("{'pair':['9H','10D'],'orientation':'DIAG'}"))), "\"DIAG\" không hợp lệ");
            AssertError(Load(Level(board: b3, deck: Pair("{'pair':['9H','10D'],'hint':[[0,0],[1,0]]}"))), "ngay phải");
        }

        [Test]
        public void PairBeforeL13_Warns()
        {
            LevelLoadResult r = Load(Level(id: 5, deck: "{'size':3,'script':[{'pair':['9H','10D'],'hint':[[0,0],[0,1]]}]," +
                                                       "'random':{'rankRange':['2','3'],'pairRatio':0.3,'seed':1}}"));
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            AssertWarning(r, "level 5 nên để 0");
            AssertWarning(r, "có khối đôi nhưng Khối đôi mở từ L13");
        }

        // ------------------------------------------------------------------ Cảnh báo khác

        [Test]
        public void Warnings_IdFileName_UnknownField_Tutorial()
        {
            AssertWarning(Load(Level(id: 8), "level_007"), "có nhầm file?");

            LevelLoadResult typo = Load(Level(goals: "[{'type':'SCORE','score':100,'scroe':5}]", extra: ",'_ghiChu':'bỏ qua','dek':1"));
            Assert.IsTrue(typo.Ok);
            AssertWarning(typo, "trường lạ \"scroe\"");
            AssertWarning(typo, "trường lạ \"dek\"");
            Assert.IsFalse(typo.Warnings.Any(w => w.Contains("_ghiChu")), "Trường bắt đầu bằng _ là ghi chú");

            LevelLoadResult plain = Load(Level(extra: ",'tutorial':[{'turn':1,'text':'Ghép 3 lá!'}]"));
            Assert.IsTrue(plain.Ok);
            AssertWarning(plain, "chỉ có 1 ngôn ngữ");
            Assert.AreEqual("Ghép 3 lá!", plain.Level.Tutorial[0].Text.Get("en"), "Thiếu tiếng Anh → dùng tiếng Việt");

            LevelLoadResult noEn = Load(Level(extra: ",'tutorial':[{'turn':9,'text':{'vi':'A'},'handPointer':[0,0]}]"));
            AssertWarning(noEn, "thiếu \"en\"");
            AssertWarning(noEn, "không bao giờ hiện");
        }

        [Test]
        public void Boosters_AndReward()
        {
            LevelData l = LoadOk(Level(extra: ",'unlockedBoosters':['UNDO','swap'],'reward':{'coins':140,'chest':true}"));
            Assert.AreEqual(new[] { BoosterType.Undo, BoosterType.Swap }, l.UnlockedBoosters.ToArray());
            Assert.AreEqual(140, l.Reward.Coins);
            Assert.IsTrue(l.Reward.Chest);
            AssertError(Load(Level(extra: ",'unlockedBoosters':['BOMB']")), "\"BOMB\" không hợp lệ");
            AssertError(Load(Level(extra: ",'reward':{'chest':'yes'}")), "true hoặc false");
        }
    }
}
