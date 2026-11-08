using System.Collections.Generic;
using SuitMerge.Core.Config;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Core.Rules
{
    /// <summary>
    /// Bước RESOLVE của một lượt (spec 3.1 bước 4 + 3.3).
    /// Có: merge, chain, điểm, Ace, hiệu ứng 4 chất (1.4). Blocker / ô ấn (M6) sẽ cắm vào đây.
    /// </summary>
    public sealed class MergeResolver
    {
        private readonly GameConfig _config;

        public MergeResolver(GameConfig config)
        {
            _config = config;
        }

        public sealed class Result
        {
            private readonly int[] _suitActivations = new int[5]; // chỉ số = (int)Suit

            public int MergeCount { get; internal set; }
            public int ScoreGained { get; internal set; }
            public int AcesCreated { get; internal set; }

            /// <summary>Số lần hiệu ứng chất <paramref name="suit"/> được kích hoạt trong lượt.</summary>
            public int SuitActivations(Suit suit) => _suitActivations[(int)suit];

            internal void AddSuitActivation(Suit suit) => _suitActivations[(int)suit]++;

            internal int[] CopySuitActivations() => (int[])_suitActivations.Clone();
        }

        /// <summary>Xử lý toàn bộ merge/chain bắt đầu từ ô vừa đặt. Ghi event vào <paramref name="events"/>.</summary>
        public Result Resolve(GameState state, CellPos placed, List<GameEvent> events)
        {
            var result = new Result();
            // Lá ♣ sinh trong lượt — chỉ bị loại khỏi nhóm khi cờ clubSpawnJoinsChain = false (spec 4.3)
            HashSet<CellPos> excluded = _config.clubSpawnJoinsChain ? null : new HashSet<CellPos>();
            int chainStep = 0;
            RunQueue(state, placed, ref chainStep, result, excluded, null, events);
            return result;
        }

        /// <summary>
        /// Khối đôi (spec 3.1 bước 4, 4.9 — thứ tự ① trước ②): resolve hết lá ① (gồm chain) rồi mới tới lá ②.
        /// Trong lúc ① resolve, ô ② "chờ" — ♠ bỏ qua ô đó. Lá ② bị merge của ① lấy mất (ô trống / đã thành lá mới)
        /// thì bỏ qua. chainStep đếm chung cả lượt.
        /// </summary>
        /// <param name="secondCard">Đúng object lá ② đã đặt xuống bàn (để nhận ra lá gốc còn hay không).</param>
        public Result ResolvePair(GameState state, CellPos first, CellPos second, Card secondCard, List<GameEvent> events)
        {
            var result = new Result();
            HashSet<CellPos> excluded = _config.clubSpawnJoinsChain ? null : new HashSet<CellPos>();
            var pending = new HashSet<CellPos> { second };
            int chainStep = 0;
            RunQueue(state, first, ref chainStep, result, excluded, pending, events);

            pending.Clear();
            if (!ReferenceEquals(state.Board.GetCard(second), secondCard)) return result;
            events.Add(new PairSecondResolveEvent(second));
            RunQueue(state, second, ref chainStep, result, excluded, null, events);
            return result;
        }

        /// <summary>
        /// Hàng đợi ô neo bắt đầu từ <paramref name="start"/>. Chain tại anchor được đẩy lên ĐẦU hàng đợi (spec 3.1).
        /// <paramref name="pending"/> = ô lá cùng khối chưa tới lượt resolve (♠ không xóa).
        /// </summary>
        private void RunQueue(GameState state, CellPos start, ref int chainStep, Result result,
            HashSet<CellPos> excluded, HashSet<CellPos> pending, List<GameEvent> events)
        {
            Board board = state.Board;
            var queue = new LinkedList<CellPos>();
            queue.AddLast(start);

            while (queue.Count > 0)
            {
                CellPos anchor = queue.First.Value;
                queue.RemoveFirst();

                List<CellPos> group = GroupFinder.FindGroup(board, anchor, excluded);
                if (group.Count < 3) continue;

                chainStep++;
                Card anchorCard = board.GetCard(anchor);
                Rank groupRank = anchorCard.Rank;
                Suit anchorSuit = anchorCard.Suit; // quyết định #2: chất lá kết quả = chất lá neo

                // Merge ra Ace bỏ qua mọi hiệu ứng chất (spec 3.3 bước 3, 4.6)
                Suit effect = groupRank == Rank.King
                    ? Suit.None
                    : SuitEffects.DetermineEffectSuit(board, group, state.EnabledSuitEffects);

                events.Add(new MergeStartedEvent(group, anchor, chainStep));
                if (chainStep >= 2) events.Add(new ChainStepEvent(chainStep));

                foreach (CellPos p in group)
                    board[p].Card = null;

                int gained;
                int bonus = 0;
                if (groupRank == Rank.King)
                {
                    // Ace: biến mất ngay, ô trống, không chain tiếp (spec 4.6)
                    gained = ScoreCalculator.AceScore(_config);
                    result.AcesCreated++;
                    events.Add(new AceCreatedEvent(anchor, gained));
                }
                else
                {
                    int overflowRanks = 0;
                    Rank newRank = effect == Suit.Heart
                        ? SuitEffects.HeartRank(groupRank, group.Count, out overflowRanks)
                        : groupRank.Next();
                    int suitMultiplier = effect == Suit.Diamond ? _config.diamondMultiplier : 1;

                    var newCard = new Card(newRank, anchorSuit);
                    board[anchor].Card = newCard;
                    gained = ScoreCalculator.MergeScore(newRank, group.Count, suitMultiplier, chainStep, _config);
                    events.Add(new CardCreatedEvent(anchor, newCard.Clone(), gained));

                    bonus = ApplyEffect(effect, board, anchor, groupRank, newRank, overflowRanks, gained, excluded, pending, events);
                    if (effect != Suit.None) result.AddSuitActivation(effect);

                    queue.AddFirst(anchor);
                }

                int oldScore = state.Score;
                state.Score += gained + bonus;
                result.ScoreGained += gained + bonus;
                result.MergeCount++;
                events.Add(new ScoreChangedEvent(oldScore, state.Score));
            }
        }

        /// <summary>
        /// Áp hiệu ứng chất sau khi lá kết quả đã nằm ở anchor (spec 3.3 bước 6). Trả về điểm cộng thêm
        /// ngoài điểm merge (♥ vượt K, ♣ bàn đầy, ♠ dọn 3×3). ♦ đã nằm trong điểm merge.
        /// </summary>
        private int ApplyEffect(Suit effect, Board board, CellPos anchor, Rank groupRank, Rank newRank,
            int overflowRanks, int mergeScore, HashSet<CellPos> excluded, HashSet<CellPos> pending, List<GameEvent> events)
        {
            switch (effect)
            {
                case Suit.Diamond:
                    events.Add(new DiamondBonusEvent(anchor, _config.diamondMultiplier));
                    return 0;

                case Suit.Heart:
                {
                    int bonus = overflowRanks * _config.heartOverflowPerRank; // cộng thẳng, không nhân
                    events.Add(new HeartLeapEvent(anchor, groupRank, newRank, overflowRanks, bonus));
                    return bonus;
                }

                case Suit.Club:
                {
                    CellPos? spawn = SpawnCellFinder.Find(board, anchor);
                    if (spawn == null)
                    {
                        // Bàn đầy: nhận thêm 1 lần điểm merge (spec 4.1)
                        events.Add(new ClubSpawnEvent(anchor, null, null, mergeScore));
                        return mergeScore;
                    }
                    var spawned = new Card(newRank, Suit.Club);
                    board[spawn.Value].Card = spawned;
                    excluded?.Add(spawn.Value);
                    events.Add(new ClubSpawnEvent(anchor, spawn.Value, spawned.Clone(), 0));
                    return 0;
                }

                case Suit.Spade:
                {
                    List<CellPos> area = SuitEffects.SpadeArea(board, anchor);
                    if (pending != null) area.RemoveAll(pending.Contains); // lá cùng khối chưa resolve: miễn ♠ (spec 4.9)
                    var clearedCells = new List<CellPos>();
                    var clearedCards = new List<Card>();
                    int bonus = 0;
                    foreach (CellPos p in area)
                    {
                        Card card = board[p].Card;
                        if (card == null) continue;
                        bonus += SuitEffects.SpadeClearScore(card, _config.spadeClearPerRankValue);
                        clearedCells.Add(p);
                        clearedCards.Add(card.Clone());
                        board[p].Card = null;
                        excluded?.Remove(p);
                    }
                    events.Add(new SpadeClearEvent(anchor, area, clearedCells, clearedCards, bonus));
                    return bonus;
                }

                default:
                    return 0;
            }
        }
    }
}
