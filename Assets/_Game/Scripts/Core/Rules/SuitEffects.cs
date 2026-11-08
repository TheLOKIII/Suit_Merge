using System.Collections.Generic;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Rules
{
    /// <summary>
    /// Các phép tính riêng của hiệu ứng chất (spec 3.3 bước 1–3, 4.2–4.5).
    /// MergeResolver gọi các hàm này theo đúng thứ tự trong một lần merge.
    /// </summary>
    public static class SuitEffects
    {
        /// <summary>
        /// Chất hiệu ứng của nhóm: mọi lá không-Joker cùng 1 chất X và X đã mở → X; ngược lại Suit.None.
        /// Gọi TRƯỚC khi xóa nhóm. Ô ấn (sigil) sẽ thêm ở M6.
        /// </summary>
        public static Suit DetermineEffectSuit(Board board, IReadOnlyList<CellPos> group, ICollection<Suit> enabledSuits)
        {
            Suit suit = Suit.None;
            foreach (CellPos p in group)
            {
                Card card = board.GetCard(p);
                if (card == null || card.IsJoker) continue; // Joker là chất trung tính
                if (suit == Suit.None) suit = card.Suit;
                else if (card.Suit != suit) return Suit.None;
            }
            return suit != Suit.None && enabledSuits != null && enabledSuits.Contains(suit) ? suit : Suit.None;
        }

        /// <summary>
        /// ♥ (spec 4.5): newRank = min(R + N, K). <paramref name="overflowRanks"/> = số rank vượt K (≥ 0).
        /// Không dùng cho R = K (nhóm K luôn ra Ace).
        /// </summary>
        public static Rank HeartRank(Rank groupRank, int groupSize, out int overflowRanks)
        {
            int target = groupRank.Value() + groupSize;
            int king = Rank.King.Value();
            overflowRanks = target > king ? target - king : 0;
            return (Rank)(target > king ? king : target);
        }

        /// <summary>
        /// ♠ (spec 4.4): các ô tồn tại trong vùng 3×3 quanh <paramref name="center"/>, trừ chính ô đó.
        /// Thứ tự row-major. Ô "#" (không tồn tại) bị bỏ qua.
        /// </summary>
        public static List<CellPos> SpadeArea(Board board, CellPos center)
        {
            var area = new List<CellPos>(8);
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    var p = new CellPos(center.Row + dr, center.Col + dc);
                    if (board.InBounds(p) && board[p].Exists) area.Add(p);
                }
            return area;
        }

        /// <summary>
        /// Điểm ♠ cho 1 lá bị xóa: RankValue × spadeClearPerRankValue.
        /// Joker chưa có trên bàn (M5) — tạm tính 0, sẽ hỏi chốt ở M5.
        /// </summary>
        public static int SpadeClearScore(Card card, int perRankValue) =>
            card == null || card.IsJoker ? 0 : card.Rank.Value() * perRankValue;
    }
}
