using System.Collections.Generic;
using NUnit.Framework;
using SuitMerge.Core.Model;
using SuitMerge.Core.Rules;

namespace SuitMerge.Tests
{
    public class GroupFinderTests
    {
        private static List<CellPos> Find(string boardText, int row, int col) =>
            GroupFinder.FindGroup(Board.FromText(boardText), new CellPos(row, col));

        [Test]
        public void TwoCards_GroupOfTwo()
        {
            List<CellPos> g = Find("2H 2C .", 0, 1);
            Assert.AreEqual(2, g.Count);
        }

        [Test]
        public void ThreeInRow_GroupOfThree_SortedRowMajor()
        {
            List<CellPos> g = Find("2H 2C 2S", 0, 2);
            CollectionAssert.AreEqual(new[] { new CellPos(0, 0), new CellPos(0, 1), new CellPos(0, 2) }, g);
        }

        [Test]
        public void LShape_GroupOfFour()
        {
            List<CellPos> g = Find(@"
                5H .  .
                5C .  .
                5D 5S .", 2, 1);
            Assert.AreEqual(4, g.Count);
        }

        [Test]
        public void Diagonal_NotConnected()
        {
            List<CellPos> g = Find(@"
                3H .  .
                .  3C .
                .  .  3D", 1, 1);
            Assert.AreEqual(1, g.Count);
        }

        [Test]
        public void DifferentRank_StopsFloodFill()
        {
            // 4 ở giữa chặn hai lá 2 bên phải
            List<CellPos> g = Find("2H 2C 4D 2S 2H", 0, 0);
            Assert.AreEqual(2, g.Count);
        }

        [Test]
        public void GroupNotContainingAnchor_IsIgnored()
        {
            // Bàn có sẵn nhóm 5-5-5 nhưng anchor là lá 2
            List<CellPos> g = Find(@"
                5H 5C 5D
                .  .  2S", 1, 2);
            Assert.AreEqual(1, g.Count);
        }

        [Test]
        public void EmptyAnchor_ReturnsEmpty()
        {
            Assert.AreEqual(0, Find("2H . 2C", 0, 1).Count);
        }

        [Test]
        public void FaceDownAndIcedCards_DoNotJoin()
        {
            Board board = Board.FromText("2H 2C 2S 2D");
            board[0, 1].Card.FaceDown = true;
            Assert.AreEqual(1, GroupFinder.FindGroup(board, new CellPos(0, 0)).Count);

            board[0, 1].Card.FaceDown = false;
            board[0, 3].Card.IceLayers = 1;
            Assert.AreEqual(3, GroupFinder.FindGroup(board, new CellPos(0, 0)).Count);
        }
    }
}
