using System.Collections.Generic;
using NUnit.Framework;
using SuitMerge.Core.Model;
using SuitMerge.Core.Rules;

namespace SuitMerge.Tests
{
    /// <summary>
    /// findSpawnCell (spec 4.3, 13.3): ô trống gần nhất theo Manhattan; hòa → quét kim đồng hồ từ 12 giờ.
    /// </summary>
    public class SpawnCellFinderTests
    {
        private static readonly CellPos Center = new CellPos(2, 2);

        /// <summary>Bàn 5×5 đầy lá "2H", chỉ các ô <paramref name="empty"/> để trống.</summary>
        private static Board FullExcept(params CellPos[] empty)
        {
            var board = new Board(5, 5);
            foreach (CellPos p in board.AllPositions()) board[p].Card = Card.Parse("2H");
            foreach (CellPos p in empty) board[p].Card = null;
            return board;
        }

        private static CellPos P(int r, int c) => new CellPos(r, c);

        [Test]
        public void Distance1_Order_Up_Right_Down_Left()
        {
            var expected = new[] { P(1, 2), P(2, 3), P(3, 2), P(2, 1) };
            var empty = new List<CellPos>(expected);
            foreach (CellPos want in expected)
            {
                Board board = FullExcept(empty.ToArray());
                Assert.AreEqual(want, SpawnCellFinder.Find(board, Center));
                empty.Remove(want); // lấp ô vừa chọn → ô kế tiếp theo thứ tự
            }
        }

        [Test]
        public void Distance2_ClockwiseSweepFrom12()
        {
            // 12h, 1h30, 3h, 4h30, 6h, 7h30, 9h, 10h30
            var expected = new[] { P(0, 2), P(1, 3), P(2, 4), P(3, 3), P(4, 2), P(3, 1), P(2, 0), P(1, 1) };
            var empty = new List<CellPos>(expected);
            foreach (CellPos want in expected)
            {
                Board board = FullExcept(empty.ToArray());
                Assert.AreEqual(want, SpawnCellFinder.Find(board, Center), $"Còn {empty.Count} ô");
                empty.Remove(want);
            }
        }

        [Test]
        public void Distance3_ClockwiseSweepFrom12()
        {
            var board = new Board(7, 7);
            var origin = P(3, 3);
            board[origin].Card = Card.Parse("2H");
            var cells = new List<CellPos>();
            foreach (CellPos p in board.AllPositions())
                if (origin.ManhattanTo(p) == 3) cells.Add(p);
            cells.Sort((a, b) => SpawnCellFinder.Compare(origin, a, b));

            var expected = new[]
            {
                P(0, 3), P(1, 4), P(2, 5), P(3, 6), P(4, 5), P(5, 4),
                P(6, 3), P(5, 2), P(4, 1), P(3, 0), P(2, 1), P(1, 2)
            };
            CollectionAssert.AreEqual(expected, cells);
        }

        [Test]
        public void NearerCell_BeatsDirectionOrder()
        {
            // Trái (khoảng cách 1) thắng Trên xa (khoảng cách 2)
            Board board = FullExcept(P(0, 2), P(2, 1));
            Assert.AreEqual(P(2, 1), SpawnCellFinder.Find(board, Center));
        }

        [Test]
        public void SkipsLockedCells()
        {
            Board board = Board.FromText(@"
                2H #  2H
                .  2H .
                2H .  2H");
            // Trên là ô khóa → Phải
            Assert.AreEqual(P(1, 2), SpawnCellFinder.Find(board, P(1, 1)));
        }

        [Test]
        public void CornerOrigin_IgnoresOutOfBoard()
        {
            Board board = Board.FromText(@"
                2H .
                .  .");
            Assert.AreEqual(P(0, 1), SpawnCellFinder.Find(board, P(0, 0)), "Không có ô Trên → Phải");
        }

        [Test]
        public void FullBoard_ReturnsNull()
        {
            Assert.IsNull(SpawnCellFinder.Find(FullExcept(), Center));
        }
    }
}
