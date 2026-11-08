using SuitMerge.Core.Model;

namespace SuitMerge.Core.Rules
{
    /// <summary>
    /// Tìm ô sinh lá ♣ (spec 4.3 — <c>findSpawnCell(A)</c>):
    /// ô trống gần A nhất theo Manhattan; hòa khoảng cách → quét theo chiều kim đồng hồ bắt đầu từ 12 giờ
    /// (Trên, Trên-Phải, Phải, Dưới-Phải, Dưới, Dưới-Trái, Trái, Trên-Trái) — quyết định đã chốt.
    /// Ở khoảng cách 1 thứ tự này đúng bằng Trên → Phải → Dưới → Trái của spec.
    /// </summary>
    public static class SpawnCellFinder
    {
        /// <summary>
        /// Ô sinh lá, hoặc null nếu bàn không còn ô trống.
        /// Gọi SAU khi nhóm đã bị xóa và lá kết quả đã đặt tại A (ô vừa giải phóng tính là trống).
        /// </summary>
        public static CellPos? Find(Board board, CellPos origin)
        {
            CellPos? best = null;
            foreach (CellPos p in board.AllPositions())
            {
                if (p == origin || !board.IsPlaceable(p)) continue;
                if (best == null || Compare(origin, p, best.Value) < 0) best = p;
            }
            return best;
        }

        /// <summary>&lt; 0 nếu ô <paramref name="a"/> được ưu tiên hơn ô <paramref name="b"/> khi sinh lá từ <paramref name="origin"/>.</summary>
        public static int Compare(CellPos origin, CellPos a, CellPos b)
        {
            int byDistance = origin.ManhattanTo(a).CompareTo(origin.ManhattanTo(b));
            if (byDistance != 0) return byDistance;

            // Trục toán học: x sang phải, y lên trên
            int ax = a.Col - origin.Col, ay = origin.Row - a.Row;
            int bx = b.Col - origin.Col, by = origin.Row - b.Row;

            int halfA = Half(ax, ay), halfB = Half(bx, by);
            if (halfA != halfB) return halfA.CompareTo(halfB);

            // Cùng nửa vòng: cross < 0 nghĩa là b nằm theo chiều kim đồng hồ sau a
            long cross = (long)ax * by - (long)ay * bx;
            return cross < 0 ? -1 : cross > 0 ? 1 : 0;
        }

        /// <summary>0 = nửa vòng [12 giờ, 6 giờ) bên phải; 1 = nửa vòng [6 giờ, 12 giờ) bên trái.</summary>
        private static int Half(int x, int y) => x > 0 || (x == 0 && y > 0) ? 0 : 1;
    }
}
