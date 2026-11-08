using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Level
{
    /// <summary>Kết quả đọc 1 file level: dữ liệu (null nếu có lỗi) + TẤT CẢ lỗi / cảnh báo kèm vị trí.</summary>
    public sealed class LevelLoadResult
    {
        public LevelData Level { get; }
        /// <summary>Lỗi: file không dùng được cho tới khi sửa.</summary>
        public IReadOnlyList<string> Errors { get; }
        /// <summary>Cảnh báo: file vẫn dùng được, nhưng có thể viết nhầm.</summary>
        public IReadOnlyList<string> Warnings { get; }
        public bool Ok => Errors.Count == 0;

        internal LevelLoadResult(LevelData level, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            Level = level;
            Errors = errors;
            Warnings = warnings;
        }
    }

    /// <summary>
    /// Đọc file level JSON (spec 12.1) thành <see cref="LevelData"/> và kiểm tra lỗi.
    /// Gom mọi lỗi trong 1 lần đọc (không dừng ở lỗi đầu tiên). Trường bắt đầu bằng "_" được bỏ qua (dùng để ghi chú).
    /// </summary>
    public static class LevelLoader
    {
        /// <summary>Level mở Khối đôi (spec 4.9).</summary>
        public const int PairUnlockLevel = 13;

        private const double Epsilon = 1e-6;

        private static readonly string[] RootKeys =
            { "id", "phase", "type", "difficulty", "goals", "enabledSuitEffects", "unlockedBoosters", "board", "deck", "tutorial", "reward" };

        public static LevelLoadResult Load(string json, string fileName = null)
        {
            var ctx = new Context();
            LevelData level = null;
            try
            {
                level = Read(json, fileName, ctx);
            }
            catch (JsonParseException e)
            {
                ctx.Error(e.Message);
            }
            return new LevelLoadResult(ctx.Errors.Count == 0 ? level : null, ctx.Errors, ctx.Warnings);
        }

        // ------------------------------------------------------------------ Đọc từng phần

        private static LevelData Read(string json, string fileName, Context ctx)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root))
            {
                ctx.Error("file level phải là 1 object { ... }");
                return null;
            }
            ctx.CheckKeys(root, "", RootKeys);

            var level = new LevelData();
            level.Id = ctx.Int(root, "id", "", required: true, min: 1) ?? 0;
            CheckIdMatchesFile(level.Id, fileName, ctx);
            level.Phase = ctx.Int(root, "phase", "", required: false, min: 0) ?? 0;
            level.Type = ctx.String(root, "type", "", required: false) ?? "";
            level.Difficulty = ctx.Int(root, "difficulty", "", required: false, min: 0) ?? 0;

            level.EnabledSuitEffects = ReadSuitList(root, ctx);
            level.UnlockedBoosters = ReadBoosters(root, ctx);

            Board board = ReadBoard(root, ctx, out IReadOnlyList<IReadOnlyList<string>> tokens);
            level.BoardTokens = tokens;

            level.Goals = ReadGoals(root, level, ctx);
            level.Deck = ReadDeck(root, board, ctx);
            level.Tutorial = ReadTutorial(root, board, ctx);
            level.Reward = ReadReward(root, ctx);

            CrossCheck(level, ctx);
            return level;
        }

        private static void CheckIdMatchesFile(int id, string fileName, Context ctx)
        {
            if (id <= 0 || string.IsNullOrEmpty(fileName)) return;
            string digits = new string(fileName.Where(char.IsDigit).ToArray());
            if (digits.Length > 0 && int.TryParse(digits, out int fromName) && fromName != id)
                ctx.Warning($"id: file tên \"{fileName}\" nhưng ghi \"id\": {id} — có nhầm file?");
        }

        private static IReadOnlyList<Suit> ReadSuitList(Dictionary<string, object> root, Context ctx)
        {
            var result = new List<Suit>();
            List<object> list = ctx.Array(root, "enabledSuitEffects", "", required: false);
            if (list == null) return result;
            for (int i = 0; i < list.Count; i++)
            {
                string path = $"enabledSuitEffects[{i}]";
                if (!TryParseSuit(list[i], out Suit suit))
                {
                    ctx.Error($"{path}: chất {Show(list[i])} không hợp lệ — dùng \"H\" (Cơ), \"D\" (Rô), \"C\" (Chuồn), \"S\" (Bích)");
                    continue;
                }
                if (result.Contains(suit)) ctx.Warning($"{path}: chất \"{suit.ToCode()}\" ghi 2 lần");
                else result.Add(suit);
            }
            return result;
        }

        private static IReadOnlyList<BoosterType> ReadBoosters(Dictionary<string, object> root, Context ctx)
        {
            var result = new List<BoosterType>();
            List<object> list = ctx.Array(root, "unlockedBoosters", "", required: false);
            if (list == null) return result;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is string s && Enum.TryParse(s, true, out BoosterType booster) && !int.TryParse(s, out _))
                {
                    if (!result.Contains(booster)) result.Add(booster);
                }
                else ctx.Error($"unlockedBoosters[{i}]: booster {Show(list[i])} không hợp lệ — dùng \"UNDO\", \"SWAP\", \"HAMMER\", \"JOKER\"");
            }
            return result;
        }

        /// <summary>Đọc + kiểm tra bàn. Trả bàn đã dựng (null nếu bàn lỗi) để kiểm tra hint / ô trống.</summary>
        private static Board ReadBoard(Dictionary<string, object> root, Context ctx, out IReadOnlyList<IReadOnlyList<string>> tokens)
        {
            var rows = new List<IReadOnlyList<string>>();
            tokens = rows;
            List<object> list = ctx.Array(root, "board", "", required: true);
            if (list == null) return null;
            if (list.Count == 0)
            {
                ctx.Error("board: bàn không có hàng nào");
                return null;
            }

            int errorsBefore = ctx.Errors.Count;
            int cols = -1;
            for (int r = 0; r < list.Count; r++)
            {
                if (!(list[r] is List<object> row))
                {
                    ctx.Error($"board hàng {r}: mỗi hàng phải là 1 mảng, vd [\"2H\", \".\", \"#\"]");
                    continue;
                }
                if (cols < 0) cols = row.Count;
                else if (row.Count != cols)
                    ctx.Error($"board hàng {r}: có {row.Count} ô, hàng 0 có {cols} ô — các hàng phải dài bằng nhau");

                var tokenRow = new List<string>();
                for (int c = 0; c < row.Count; c++)
                {
                    string token = row[c] as string;
                    if (token == null)
                    {
                        ctx.Error($"board ô ({r},{c}): {Show(row[c])} phải là chuỗi, vd \".\" hoặc \"2H\"");
                        tokenRow.Add(".");
                        continue;
                    }
                    CheckBoardToken(token, r, c, ctx);
                    tokenRow.Add(token);
                }
                rows.Add(tokenRow);
            }
            if (cols == 0) ctx.Error("board: hàng không có ô nào");
            if (ctx.Errors.Count > errorsBefore) return null;

            Board board = Board.FromTokens(rows.Select(r => r.ToArray()).ToArray());
            if (!board.HasPlaceableCell()) ctx.Error("board: bàn không có ô trống nào — vào level là thua ngay");
            return board;
        }

        private static void CheckBoardToken(string token, int r, int c, Context ctx)
        {
            if (token == "." || token == "#" || Card.TryParse(token, out _)) return;
            if (IsBlockerToken(token))
                ctx.Error($"board ô ({r},{c}): \"{token}\" là chướng ngại — chưa hỗ trợ (sẽ có ở M6)");
            else
                ctx.Error($"board ô ({r},{c}): \"{token}\" không hợp lệ — dùng \".\" (trống), \"#\" (khóa) hoặc lá như \"2H\", \"10S\", \"QD\"");
        }

        private static bool IsBlockerToken(string t) =>
            t.Contains("@") || t.StartsWith("FD:") || t.StartsWith("ICE") || t.StartsWith("BOMB") ||
            (t.Length == 3 && t.StartsWith("ST") && char.IsDigit(t[2]));

        private static IReadOnlyList<GoalData> ReadGoals(Dictionary<string, object> root, LevelData level, Context ctx)
        {
            var goals = new List<GoalData>();
            List<object> list = ctx.Array(root, "goals", "", required: true);
            if (list == null) return goals;
            if (list.Count == 0) ctx.Error("goals: level phải có ít nhất 1 mục tiêu");

            for (int i = 0; i < list.Count; i++)
            {
                string path = $"goals[{i}]";
                if (!(list[i] is Dictionary<string, object> obj))
                {
                    ctx.Error($"{path}: mỗi mục tiêu phải là 1 object, vd {{ \"type\": \"SCORE\", \"score\": 1200 }}");
                    continue;
                }
                string type = ctx.String(obj, "type", path, required: true);
                if (type == null) continue;

                var goal = new GoalData();
                switch (type)
                {
                    case "CREATE_RANK":
                        goal.Type = GoalType.CreateRank;
                        ctx.CheckKeys(obj, path, "type", "rank", "count");
                        goal.Rank = ReadGoalRank(obj, path, ctx);
                        goal.Count = ctx.Int(obj, "count", path, required: true, min: 1) ?? 0;
                        break;
                    case "SCORE":
                        goal.Type = GoalType.Score;
                        ctx.CheckKeys(obj, path, "type", "score");
                        goal.Score = ctx.Int(obj, "score", path, required: true, min: 1) ?? 0;
                        break;
                    case "ACE":
                        goal.Type = GoalType.Ace;
                        ctx.CheckKeys(obj, path, "type", "count");
                        goal.Count = ctx.Int(obj, "count", path, required: true, min: 1) ?? 0;
                        break;
                    case "SUIT_TRIGGER":
                        goal.Type = GoalType.SuitTrigger;
                        ctx.CheckKeys(obj, path, "type", "suit", "count");
                        goal.Suit = ReadGoalSuit(obj, path, ctx);
                        goal.Count = ctx.Int(obj, "count", path, required: true, min: 1) ?? 0;
                        if (goal.Suit != Suit.None && !level.EnabledSuitEffects.Contains(goal.Suit))
                            ctx.Warning($"{path}: cần kích hoạt {SuitName(goal.Suit)} nhưng \"enabledSuitEffects\" chưa mở chất này — mục tiêu không thể hoàn thành");
                        break;
                    case "SIGIL_TRIGGER":
                        goal.Type = GoalType.SigilTrigger;
                        ctx.CheckKeys(obj, path, "type", "count");
                        goal.Count = ctx.Int(obj, "count", path, required: true, min: 1) ?? 0;
                        ctx.Warning($"{path}: mục tiêu Ô Ấn chưa chơi được (sẽ có ở M6)");
                        break;
                    case "CLEAR_BLOCKER":
                        goal.Type = GoalType.ClearBlocker;
                        ctx.CheckKeys(obj, path, "type", "blocker", "count");
                        goal.Blocker = ReadBlocker(obj, path, ctx);
                        goal.Count = ctx.Int(obj, "count", path, required: true, min: 1) ?? 0;
                        ctx.Warning($"{path}: mục tiêu phá chướng ngại chưa chơi được (sẽ có ở M6)");
                        break;
                    case "COMBO":
                        ctx.Error($"{path}: không cần loại \"COMBO\" — ghi nhiều mục tiêu trong \"goals\" là thành COMBO");
                        continue;
                    default:
                        ctx.Error($"{path}.type: \"{type}\" không hợp lệ — dùng CREATE_RANK, SCORE, ACE, SUIT_TRIGGER, SIGIL_TRIGGER, CLEAR_BLOCKER");
                        continue;
                }
                goals.Add(goal);
            }
            return goals;
        }

        private static Rank ReadGoalRank(Dictionary<string, object> obj, string path, Context ctx)
        {
            if (!obj.TryGetValue("rank", out object raw)) { ctx.Error($"{path}: thiếu \"rank\""); return default; }
            if (!TryParseRank(raw, out Rank rank))
            {
                ctx.Error($"{path}.rank: {Show(raw)} không hợp lệ — dùng \"3\"..\"10\", \"J\", \"Q\", \"K\"");
                return default;
            }
            if (rank == Rank.Ace) ctx.Error($"{path}.rank: tạo Ace hãy dùng mục tiêu {{ \"type\": \"ACE\", \"count\": N }}");
            else if (rank == Rank.Two) ctx.Error($"{path}.rank: lá 2 không thể được tạo bởi merge (merge nhỏ nhất ra lá 3)");
            return rank;
        }

        private static Suit ReadGoalSuit(Dictionary<string, object> obj, string path, Context ctx)
        {
            if (!obj.TryGetValue("suit", out object raw)) { ctx.Error($"{path}: thiếu \"suit\""); return Suit.None; }
            if (TryParseSuit(raw, out Suit suit)) return suit;
            ctx.Error($"{path}.suit: {Show(raw)} không hợp lệ — dùng \"H\", \"D\", \"C\", \"S\"");
            return Suit.None;
        }

        private static BlockerType ReadBlocker(Dictionary<string, object> obj, string path, Context ctx)
        {
            string s = ctx.String(obj, "blocker", path, required: true);
            switch (s)
            {
                case null: return BlockerType.None;
                case "STONE": return BlockerType.Stone;
                case "FACE_DOWN": return BlockerType.FaceDown;
                case "ICE": return BlockerType.Ice;
                case "BOMB": return BlockerType.Bomb;
            }
            ctx.Error($"{path}.blocker: \"{s}\" không hợp lệ — dùng STONE, FACE_DOWN, ICE, BOMB");
            return BlockerType.None;
        }

        private static DeckData ReadDeck(Dictionary<string, object> root, Board board, Context ctx)
        {
            var deck = new DeckData();
            Dictionary<string, object> obj = ctx.Object(root, "deck", "", required: true);
            if (obj == null) return deck;
            ctx.CheckKeys(obj, "deck", "size", "script", "random");

            deck.Size = ctx.Int(obj, "size", "deck", required: true, min: 1) ?? 0;
            deck.Script = ReadScript(obj, board, ctx);
            if (obj.TryGetValue("random", out object raw) && raw != null)
            {
                if (raw is Dictionary<string, object> random) deck.Random = ReadRandom(random, ctx);
                else ctx.Error("deck.random: phải là 1 object (hoặc null nếu không có phần ngẫu nhiên)");
            }

            int scriptCards = deck.ScriptCardCount;
            if (deck.Size > 0 && scriptCards > deck.Size)
                ctx.Error($"deck: \"size\" = {deck.Size} nhưng \"script\" đã có {scriptCards} lá — size phải ≥ số lá trong script");
            else if (deck.RandomCardCount > 0 && deck.Random == null && !ctx.HasErrorAt("deck.random"))
                ctx.Error($"deck: còn {deck.RandomCardCount} lá phải rút ngẫu nhiên (size {deck.Size} − script {scriptCards}) nhưng thiếu \"random\"");
            else if (deck.Size > 0 && deck.RandomCardCount == 0 && deck.Random != null)
                ctx.Warning("deck.random: script đã đủ \"size\" lá nên phần random không bao giờ được dùng");
            return deck;
        }

        private static IReadOnlyList<DeckScriptEntry> ReadScript(Dictionary<string, object> deckObj, Board board, Context ctx)
        {
            var entries = new List<DeckScriptEntry>();
            List<object> list = ctx.Array(deckObj, "script", "deck", required: false);
            if (list == null) return entries;

            for (int i = 0; i < list.Count; i++)
            {
                string path = $"deck.script[{i}]";
                object item = list[i];
                if (item is string token)
                {
                    if (TryParseScriptCard(token, path, ctx, out Card card))
                        entries.Add(new DeckScriptEntry { Cards = new[] { card } });
                    continue;
                }
                if (!(item is Dictionary<string, object> obj))
                {
                    ctx.Error($"{path}: phải là lá \"2S\" hoặc object {{ \"card\": \"2S\", \"hint\": [0,2] }}");
                    continue;
                }

                if (obj.ContainsKey("pair"))
                {
                    DeckScriptEntry pair = ReadPairEntry(obj, path, board, ctx);
                    if (pair != null) entries.Add(pair);
                    continue;
                }

                ctx.CheckKeys(obj, path, "card", "hint");
                string cardText = ctx.String(obj, "card", path, required: true);
                if (cardText == null || !TryParseScriptCard(cardText, path + ".card", ctx, out Card single)) continue;
                var entry = new DeckScriptEntry { Cards = new[] { single } };
                if (obj.TryGetValue("hint", out object hintRaw) && hintRaw != null)
                {
                    if (TryReadCell(hintRaw, path + ".hint", board, ctx, out CellPos hint))
                    {
                        WarnIfOccupied(board, hint, path + ".hint", ctx);
                        entry.Hint = new[] { hint };
                    }
                }
                entries.Add(entry);
            }
            return entries;
        }

        private static DeckScriptEntry ReadPairEntry(Dictionary<string, object> obj, string path, Board board, Context ctx)
        {
            ctx.CheckKeys(obj, path, "pair", "orientation", "hint");
            if (!(obj["pair"] is List<object> pair) || pair.Count != 2)
            {
                ctx.Error($"{path}.pair: khối đôi phải có đúng 2 lá, vd [\"9H\", \"10D\"]");
                return null;
            }
            var cards = new Card[2];
            for (int k = 0; k < 2; k++)
            {
                if (!(pair[k] is string t) || !TryParseScriptCard(t, $"{path}.pair[{k}]", ctx, out cards[k]))
                {
                    if (!(pair[k] is string)) ctx.Error($"{path}.pair[{k}]: {Show(pair[k])} phải là lá dạng chuỗi, vd \"9H\"");
                    return null;
                }
            }

            var orientation = PieceOrientation.H_AB;
            string o = ctx.String(obj, "orientation", path, required: false);
            if (o != null && (int.TryParse(o, out _) || !Enum.TryParse(o, false, out orientation)))
            {
                ctx.Error($"{path}.orientation: \"{o}\" không hợp lệ — dùng H_AB, V_AB, H_BA, V_BA");
                return null;
            }

            var entry = new DeckScriptEntry { Cards = cards, Orientation = orientation };
            if (obj.TryGetValue("hint", out object hintRaw) && hintRaw != null)
            {
                if (!(hintRaw is List<object> hints) || hints.Count != 2)
                {
                    ctx.Error($"{path}.hint: khối đôi cần 2 ô (ô lá ①, ô lá ②), vd [[2,1],[2,2]]");
                    return entry;
                }
                bool ok = TryReadCell(hints[0], path + ".hint[0]", board, ctx, out CellPos h1)
                          & TryReadCell(hints[1], path + ".hint[1]", board, ctx, out CellPos h2);
                if (!ok) return entry;
                bool vertical = orientation == PieceOrientation.V_AB || orientation == PieceOrientation.V_BA;
                CellPos expected = vertical ? h1.Down : h1.Right;
                if (h2 != expected)
                {
                    ctx.Error($"{path}.hint: khối {(vertical ? "dọc" : "ngang")} ({orientation}) thì ô lá ② phải ở {(vertical ? "ngay dưới" : "ngay phải")} ô lá ① — cần {expected}, đang ghi {h2}");
                    return entry;
                }
                WarnIfOccupied(board, h1, path + ".hint[0]", ctx);
                WarnIfOccupied(board, h2, path + ".hint[1]", ctx);
                entry.Hint = new[] { h1, h2 };
            }
            return entry;
        }

        private static bool TryParseScriptCard(string token, string path, Context ctx, out Card card)
        {
            if (Card.TryParse(token, out card)) return true;
            ctx.Error($"{path}: lá \"{token}\" không hợp lệ — dùng rank 2..10, J, Q, K + chất H, D, C, S, vd \"2S\", \"10H\", \"QD\"");
            return false;
        }

        private static void WarnIfOccupied(Board board, CellPos cell, string path, Context ctx)
        {
            if (board != null && board.GetCard(cell) != null)
                ctx.Warning($"{path}: ô {cell} đang có lá lúc bắt đầu level — chỉ đúng nếu lá đó đã bị merge đi trước lượt này");
        }

        private static RandomDeckData ReadRandom(Dictionary<string, object> obj, Context ctx)
        {
            const string path = "deck.random";
            ctx.CheckKeys(obj, path, "rankRange", "rankWeights", "suitWeights", "pairRatio", "seed");
            var random = new RandomDeckData();

            bool rangeOk = false;
            if (!obj.TryGetValue("rankRange", out object rangeRaw)) ctx.Error($"{path}: thiếu \"rankRange\", vd [\"2\", \"5\"]");
            else if (!(rangeRaw is List<object> range) || range.Count != 2)
                ctx.Error($"{path}.rankRange: phải là [thấp, cao], vd [\"2\", \"5\"]");
            else if (!TryParseRank(range[0], out Rank lo) || !TryParseRank(range[1], out Rank hi) || lo > RankUtil.HighestOnBoard || hi > RankUtil.HighestOnBoard)
                ctx.Error($"{path}.rankRange: {Show(range[0])}, {Show(range[1])} không hợp lệ — rank từ \"2\" tới \"K\"");
            else if (lo > hi)
                ctx.Error($"{path}.rankRange: rank thấp \"{lo.ToLabel()}\" lớn hơn rank cao \"{hi.ToLabel()}\"");
            else
            {
                random.MinRank = lo;
                random.MaxRank = hi;
                rangeOk = true;
            }

            if (rangeOk)
                random.RankWeights = ReadWeights(obj, "rankWeights", random.Ranks().ToList(),
                    (object key, out Rank r) => TryParseRank(key, out r) && r >= random.MinRank && r <= random.MaxRank,
                    r => r.ToLabel(), "rank", $"rank trong rankRange {random.MinRank.ToLabel()}..{random.MaxRank.ToLabel()}", ctx);
            random.SuitWeights = ReadWeights(obj, "suitWeights", RandomDeckData.AllSuits,
                (object key, out Suit s) => TryParseSuit(key, out s),
                s => s.ToCode().ToString(), "chất", "\"H\", \"D\", \"C\", \"S\"", ctx);

            if (obj.TryGetValue("pairRatio", out object ratioRaw) && ratioRaw != null)
            {
                if (ratioRaw is double ratio && ratio >= 0 && ratio <= 1) random.PairRatio = ratio;
                else ctx.Error($"{path}.pairRatio: {Show(ratioRaw)} phải là số từ 0 tới 1 (vd 0.2 = 20%)");
            }

            if (!obj.TryGetValue("seed", out object seedRaw) || seedRaw == null)
                ctx.Error($"{path}: thiếu \"seed\" — mỗi level cần seed cố định để mọi người chơi cùng dãy lá");
            else if (seedRaw is double seed && seed >= 0 && seed <= uint.MaxValue && Math.Floor(seed) == seed)
                random.Seed = (uint)seed;
            else
                ctx.Error($"{path}.seed: {Show(seedRaw)} phải là số nguyên ≥ 0");
            return random;
        }

        private delegate bool KeyParser<T>(object key, out T value);

        /// <summary>
        /// Trọng số { "D": 0.5 }: mỗi giá trị 0..1, tổng ≤ 1. Mục không ghi chia đều phần còn lại.
        /// Ghi đủ mọi mục thì tổng phải = 1.
        /// </summary>
        private static IReadOnlyDictionary<T, double> ReadWeights<T>(Dictionary<string, object> obj, string key, IList<T> all,
            KeyParser<T> parse, Func<T, string> label, string noun, string allowed, Context ctx)
        {
            var result = new Dictionary<T, double>();
            if (!obj.TryGetValue(key, out object raw) || raw == null) return result;
            string path = "deck.random." + key;
            if (!(raw is Dictionary<string, object> weights))
            {
                ctx.Error($"{path}: phải là object, vd {{ \"D\": 0.5 }} (hoặc null = chia đều)");
                return result;
            }

            int errorsBefore = ctx.Errors.Count;
            foreach (KeyValuePair<string, object> kv in weights)
            {
                if (kv.Key.StartsWith("_")) continue;
                if (!parse(kv.Key, out T item))
                {
                    ctx.Error($"{path}: \"{kv.Key}\" không hợp lệ — chỉ ghi {allowed}");
                    continue;
                }
                if (result.ContainsKey(item))
                {
                    ctx.Error($"{path}: {noun} \"{label(item)}\" ghi 2 lần");
                    continue;
                }
                if (!(kv.Value is double w) || w < 0 || w > 1)
                {
                    ctx.Error($"{path}.{kv.Key}: {Show(kv.Value)} phải là số từ 0 tới 1 (vd 0.3 = 30%)");
                    continue;
                }
                result[item] = w;
            }
            if (ctx.Errors.Count > errorsBefore) return result;

            double sum = result.Values.Sum();
            List<T> unlisted = all.Where(x => !result.ContainsKey(x)).ToList();
            if (sum > 1 + Epsilon)
                ctx.Error($"{path}: tổng = {sum.ToString("0.###", CultureInfo.InvariantCulture)}, phải ≤ 1");
            else if (unlisted.Count == 0 && Math.Abs(sum - 1) > Epsilon)
                ctx.Error($"{path}: đã ghi đủ mọi {noun} nên tổng phải = 1 (đang = {sum.ToString("0.###", CultureInfo.InvariantCulture)})");
            else if (unlisted.Count > 0 && sum >= 1 - Epsilon)
                ctx.Warning($"{path}: tổng đã = 1 nên {noun} {string.Join(", ", unlisted.Select(label))} sẽ không bao giờ ra");
            return result;
        }

        private static IReadOnlyList<TutorialStep> ReadTutorial(Dictionary<string, object> root, Board board, Context ctx)
        {
            var steps = new List<TutorialStep>();
            List<object> list = ctx.Array(root, "tutorial", "", required: false);
            if (list == null) return steps;
            for (int i = 0; i < list.Count; i++)
            {
                string path = $"tutorial[{i}]";
                if (!(list[i] is Dictionary<string, object> obj))
                {
                    ctx.Error($"{path}: phải là object {{ \"turn\": 1, \"text\": {{ \"vi\": \"...\", \"en\": \"...\" }} }}");
                    continue;
                }
                ctx.CheckKeys(obj, path, "turn", "text", "handPointer");
                var step = new TutorialStep { Turn = ctx.Int(obj, "turn", path, required: true, min: 1) ?? 0 };
                step.Text = ReadText(obj, path, ctx);
                if (obj.TryGetValue("handPointer", out object hp) && hp != null &&
                    TryReadCell(hp, path + ".handPointer", board, ctx, out CellPos cell))
                    step.HandPointer = cell;
                steps.Add(step);
            }
            return steps;
        }

        private static LocalizedText ReadText(Dictionary<string, object> obj, string path, Context ctx)
        {
            if (!obj.TryGetValue("text", out object raw) || raw == null)
            {
                ctx.Error($"{path}: thiếu \"text\"");
                return new LocalizedText { Vi = "" };
            }
            if (raw is string plain)
            {
                ctx.Warning($"{path}.text: chỉ có 1 ngôn ngữ — nên ghi {{ \"vi\": \"...\", \"en\": \"...\" }} (tạm dùng chữ này cho cả 2)");
                return new LocalizedText { Vi = plain };
            }
            if (!(raw is Dictionary<string, object> texts))
            {
                ctx.Error($"{path}.text: phải là {{ \"vi\": \"...\", \"en\": \"...\" }}");
                return new LocalizedText { Vi = "" };
            }
            ctx.CheckKeys(texts, path + ".text", "vi", "en");
            var text = new LocalizedText
            {
                Vi = ctx.String(texts, "vi", path + ".text", required: true) ?? "",
                En = ctx.String(texts, "en", path + ".text", required: false)
            };
            if (string.IsNullOrEmpty(text.En))
                ctx.Warning($"{path}.text: thiếu \"en\" — bản tiếng Anh sẽ hiện chữ tiếng Việt");
            return text;
        }

        private static RewardData ReadReward(Dictionary<string, object> root, Context ctx)
        {
            var reward = new RewardData();
            Dictionary<string, object> obj = ctx.Object(root, "reward", "", required: false);
            if (obj == null) return reward;
            ctx.CheckKeys(obj, "reward", "coins", "chest");
            reward.Coins = ctx.Int(obj, "coins", "reward", required: false, min: 0) ?? 0;
            if (obj.TryGetValue("chest", out object chest) && chest != null)
            {
                if (chest is bool b) reward.Chest = b;
                else ctx.Error($"reward.chest: {Show(chest)} phải là true hoặc false");
            }
            return reward;
        }

        private static void CrossCheck(LevelData level, Context ctx)
        {
            if (level.Id <= 0 || level.Id >= PairUnlockLevel) return;
            if (level.Deck.Random != null && level.Deck.Random.PairRatio > 0)
                ctx.Warning($"deck.random.pairRatio: Khối đôi mở từ L{PairUnlockLevel} — level {level.Id} nên để 0");
            if (level.Deck.Script.Any(e => e.IsPair))
                ctx.Warning($"deck.script: có khối đôi nhưng Khối đôi mở từ L{PairUnlockLevel}");
            foreach (TutorialStep step in level.Tutorial)
                if (step.Turn > level.Deck.Size && level.Deck.Size > 0)
                    ctx.Warning($"tutorial: lượt {step.Turn} lớn hơn số lá của deck ({level.Deck.Size}) — gợi ý này không bao giờ hiện");
        }

        // ------------------------------------------------------------------ Tiện ích đọc giá trị

        private static bool TryReadCell(object raw, string path, Board board, Context ctx, out CellPos cell)
        {
            cell = default;
            if (!(raw is List<object> pair) || pair.Count != 2 || !IsInt(pair[0]) || !IsInt(pair[1]))
            {
                ctx.Error($"{path}: ô phải viết [hàng, cột] (đếm từ 0), vd [0, 2]");
                return false;
            }
            cell = new CellPos((int)(double)pair[0], (int)(double)pair[1]);
            if (board == null) return true; // bàn lỗi → đã báo ở board
            if (!board.InBounds(cell))
            {
                ctx.Error($"{path}: ô {cell} nằm ngoài bàn {board.Rows}×{board.Cols}");
                return false;
            }
            if (!board[cell].Exists)
            {
                ctx.Error($"{path}: ô {cell} là ô khóa \"#\"");
                return false;
            }
            return true;
        }

        private static bool IsInt(object v) => v is double d && Math.Floor(d) == d && d >= int.MinValue && d <= int.MaxValue;

        private static bool TryParseRank(object raw, out Rank rank)
        {
            rank = default;
            if (raw is string s) return RankUtil.TryParse(s, out rank);
            if (raw is double d && IsInt(d) && d >= (int)RankUtil.Lowest && d <= (int)Rank.Ace)
            {
                rank = (Rank)(int)d;
                return true;
            }
            return false;
        }

        private static bool TryParseSuit(object raw, out Suit suit)
        {
            suit = Suit.None;
            if (!(raw is string s)) return false;
            switch (s)
            {
                case "♥": suit = Suit.Heart; return true;
                case "♦": suit = Suit.Diamond; return true;
                case "♣": suit = Suit.Club; return true;
                case "♠": suit = Suit.Spade; return true;
            }
            return s.Length == 1 && SuitUtil.TryParseCode(s[0], out suit);
        }

        private static string Show(object v)
        {
            switch (v)
            {
                case null: return "null";
                case string s: return $"\"{s}\"";
                case double d: return d.ToString(CultureInfo.InvariantCulture);
                case bool b: return b ? "true" : "false";
                case List<object> _: return "[...]";
                default: return "{...}";
            }
        }

        // ------------------------------------------------------------------ Mô tả (cho menu Kiểm tra file level)

        /// <summary>Tóm tắt 1 dòng: "bàn 4×4, deck 12 (script 10 + random 2), mục tiêu: Tạo 2 lá 4, chất mở: chưa mở".</summary>
        public static string Summary(LevelData level)
        {
            string goals = string.Join(" + ", level.Goals.Select(Describe));
            string suits = level.EnabledSuitEffects.Count == 0 ? "chưa mở" : string.Join(", ", level.EnabledSuitEffects.Select(SuitName));
            return $"bàn {level.Rows}×{level.Cols}, deck {level.Deck.Size} (script {level.Deck.ScriptCardCount} + random {level.Deck.RandomCardCount}), " +
                   $"mục tiêu: {goals}, chất mở: {suits}";
        }

        /// <summary>Mô tả mục tiêu bằng tiếng Việt (vd "Tạo 2 lá 6", "Đạt 1,200 điểm").</summary>
        public static string Describe(GoalData goal)
        {
            switch (goal.Type)
            {
                case GoalType.CreateRank: return $"Tạo {goal.Count} lá {goal.Rank.ToLabel()}";
                case GoalType.Score: return $"Đạt {goal.Score.ToString("N0", CultureInfo.InvariantCulture)} điểm";
                case GoalType.Ace: return $"Tạo {goal.Count} Ace";
                case GoalType.SuitTrigger: return $"Kích hoạt {SuitName(goal.Suit)} {goal.Count} lần";
                case GoalType.SigilTrigger: return $"Kích hoạt {goal.Count} Ô Ấn";
                case GoalType.ClearBlocker: return $"Phá {goal.Count} {BlockerName(goal.Blocker)}";
                default: return goal.Type.ToString();
            }
        }

        private static string SuitName(Suit suit)
        {
            switch (suit)
            {
                case Suit.Heart: return "Cơ";
                case Suit.Diamond: return "Rô";
                case Suit.Club: return "Chuồn";
                case Suit.Spade: return "Bích";
                default: return "?";
            }
        }

        private static string BlockerName(BlockerType blocker)
        {
            switch (blocker)
            {
                case BlockerType.Stone: return "Đá";
                case BlockerType.FaceDown: return "Lá úp";
                case BlockerType.Ice: return "Băng";
                case BlockerType.Bomb: return "Bom";
                default: return "?";
            }
        }

        // ------------------------------------------------------------------ Gom lỗi

        private sealed class Context
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();

            public void Error(string message) => Errors.Add(message);
            public void Warning(string message) => Warnings.Add(message);

            public bool HasErrorAt(string path) => Errors.Any(e => e.StartsWith(path));

            private static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;

            /// <summary>Trường lạ (có thể viết sai tên) → cảnh báo. Trường bắt đầu bằng "_" là ghi chú, bỏ qua.</summary>
            public void CheckKeys(Dictionary<string, object> obj, string path, params string[] known)
            {
                foreach (string key in obj.Keys)
                    if (!key.StartsWith("_") && !known.Contains(key))
                        Warning($"{Join(path, key)}: trường lạ \"{key}\" sẽ bị bỏ qua — có viết sai tên? (các trường hợp lệ: {string.Join(", ", known)})");
            }

            public int? Int(Dictionary<string, object> obj, string key, string path, bool required, int min)
            {
                if (!obj.TryGetValue(key, out object raw) || raw == null)
                {
                    if (required) Error($"{(path.Length == 0 ? "" : path + ": ")}thiếu \"{key}\"");
                    return null;
                }
                if (!IsInt(raw))
                {
                    Error($"{Join(path, key)}: {Show(raw)} phải là số nguyên");
                    return null;
                }
                int value = (int)(double)raw;
                if (value < min)
                {
                    Error($"{Join(path, key)}: {value} phải ≥ {min}");
                    return null;
                }
                return value;
            }

            public string String(Dictionary<string, object> obj, string key, string path, bool required)
            {
                if (!obj.TryGetValue(key, out object raw) || raw == null)
                {
                    if (required) Error($"{(path.Length == 0 ? "" : path + ": ")}thiếu \"{key}\"");
                    return null;
                }
                if (raw is string s) return s;
                Error($"{Join(path, key)}: {Show(raw)} phải là chuỗi trong ngoặc kép");
                return null;
            }

            public List<object> Array(Dictionary<string, object> obj, string key, string path, bool required)
            {
                if (!obj.TryGetValue(key, out object raw) || raw == null)
                {
                    if (required) Error($"{(path.Length == 0 ? "" : path + ": ")}thiếu \"{key}\"");
                    return null;
                }
                if (raw is List<object> list) return list;
                Error($"{Join(path, key)}: phải là mảng [ ... ]");
                return null;
            }

            public Dictionary<string, object> Object(Dictionary<string, object> obj, string key, string path, bool required)
            {
                if (!obj.TryGetValue(key, out object raw) || raw == null)
                {
                    if (required) Error($"{(path.Length == 0 ? "" : path + ": ")}thiếu \"{key}\"");
                    return null;
                }
                if (raw is Dictionary<string, object> d) return d;
                Error($"{Join(path, key)}: phải là object {{ ... }}");
                return null;
            }
        }
    }
}
