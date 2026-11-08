using System.Collections.Generic;
using SuitMerge.Core.Config;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Core.Goals
{
    /// <summary>
    /// Cộng tiến độ mục tiêu sau 1 lượt (spec 6, spec 3.1 bước 7). Chỉ đọc event + bàn cuối lượt, không đổi bàn.
    ///
    /// CREATE_RANK X (quyết định 18/10/2026 — "Tính 1"): cuối lượt đếm các lá ĐƯỢC TẠO TRONG LƯỢT mà VẪN CÒN trên bàn,
    /// rank ≥ X (cờ createRankCountsHigher), gồm cả lá ♣ sinh ra (createRankCountsClubSpawn) + mỗi Ace tạo ra (+1).
    /// Chain 4 → 5 chỉ tính lá 5. Bật createRankCountsEveryChainStep = true → mỗi lá tạo ra đều tính (đúng chữ spec 6).
    /// </summary>
    public static class GoalTracker
    {
        /// <summary>
        /// Cập nhật <paramref name="goals"/> theo lượt vừa xong; trả event cho các mục tiêu đổi tiến độ.
        /// </summary>
        /// <param name="board">Bàn CUỐI lượt (sau resolve).</param>
        /// <param name="turnEvents">Event của lượt (đặt lá → merge → hiệu ứng).</param>
        public static List<GoalProgressEvent> Apply(IList<GoalProgress> goals, Board board, int score,
            IReadOnlyList<GameEvent> turnEvents, TurnResult turn, GameConfig config)
        {
            var changes = new List<GoalProgressEvent>();
            if (goals == null || goals.Count == 0) return changes;

            for (int i = 0; i < goals.Count; i++)
            {
                GoalProgress g = goals[i];
                int before = g.Current;
                switch (g.Goal.Type)
                {
                    case GoalType.CreateRank:
                        g.Current += CountCreated(g.Goal.Rank, board, turnEvents, config);
                        break;
                    case GoalType.Score:
                        g.Current = score;
                        break;
                    case GoalType.Ace:
                        g.Current += turn.AcesCreated;
                        break;
                    case GoalType.SuitTrigger:
                        g.Current += turn.SuitActivations(g.Goal.Suit);
                        break;
                    // SIGIL_TRIGGER, CLEAR_BLOCKER: M6
                }
                if (g.Current != before)
                    changes.Add(new GoalProgressEvent(i, before, g.Current, g.Target, before < g.Target && g.IsDone));
            }
            return changes;
        }

        /// <summary>Số lá rank ≥ X (hoặc = X) tạo ra trong lượt, tính theo quyết định "Tính 1" hoặc cờ mỗi bước chain.</summary>
        public static int CountCreated(Rank target, Board board, IReadOnlyList<GameEvent> turnEvents, GameConfig config)
        {
            config = config ?? GameConfig.Default;
            int count = 0;
            var createdCells = new HashSet<CellPos>();

            foreach (GameEvent e in turnEvents)
            {
                switch (e)
                {
                    case CardCreatedEvent created:
                        createdCells.Add(created.Cell);
                        if (config.createRankCountsEveryChainStep && Matches(created.Card, target, config)) count++;
                        break;
                    case ClubSpawnEvent spawn when spawn.SpawnCell.HasValue && config.createRankCountsClubSpawn:
                        createdCells.Add(spawn.SpawnCell.Value);
                        if (config.createRankCountsEveryChainStep && Matches(spawn.Card, target, config)) count++;
                        break;
                    case AceCreatedEvent _:
                        if (config.createRankCountsHigher || target == Rank.Ace) count++;
                        break;
                }
            }
            if (config.createRankCountsEveryChainStep) return count;

            // "Tính 1": chỉ lá tạo trong lượt còn nằm trên bàn lúc cuối lượt. Ô bị merge đi thì trống / đã thành lá mới
            // (lá mới cũng được tạo trong lượt, tính 1 lần) → mỗi ô tối đa 1.
            foreach (CellPos cell in createdCells)
                if (Matches(board.GetCard(cell), target, config)) count++;
            return count;
        }

        private static bool Matches(Card card, Rank target, GameConfig config)
        {
            if (card == null || card.IsJoker) return false;
            return config.createRankCountsHigher ? card.Rank >= target : card.Rank == target;
        }
    }
}
