using System.Collections.Generic;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Rules
{
    /// <summary>
    /// Tìm nhóm lá cùng rank kề cạnh 4 hướng chứa ô neo (spec 3.2).
    /// Lá úp / còn băng không tham gia. Joker sẽ hỗ trợ ở M5 (hiện tại Joker không khớp lá nào).
    /// </summary>
    public static class GroupFinder
    {
        /// <summary>
        /// Trả về các ô trong nhóm (gồm cả anchor), sắp xếp row-major.
        /// Danh sách rỗng nếu anchor không có lá hợp lệ.
        /// <paramref name="excluded"/>: các ô coi như không khớp (vd lá ♣ vừa sinh khi cờ clubSpawnJoinsChain = false).
        /// </summary>
        public static List<CellPos> FindGroup(Board board, CellPos anchor, ICollection<CellPos> excluded = null)
        {
            var result = new List<CellPos>();
            Card anchorCard = board.GetCard(anchor);
            if (!CanMatch(anchorCard)) return result;

            var visited = new HashSet<CellPos> { anchor };
            var frontier = new Queue<CellPos>();
            frontier.Enqueue(anchor);

            while (frontier.Count > 0)
            {
                CellPos p = frontier.Dequeue();
                result.Add(p);
                foreach (CellPos n in p.Neighbors4())
                {
                    if (visited.Contains(n)) continue;
                    if (excluded != null && excluded.Contains(n)) continue;
                    Card card = board.GetCard(n);
                    if (!CanMatch(card) || card.Rank != anchorCard.Rank) continue;
                    visited.Add(n);
                    frontier.Enqueue(n);
                }
            }

            result.Sort();
            return result;
        }

        private static bool CanMatch(Card card) => card != null && !card.IsJoker && card.CanJoinGroup;
    }
}
