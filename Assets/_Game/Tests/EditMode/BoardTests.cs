using NUnit.Framework;
using SuitMerge.Core.Model;

namespace SuitMerge.Tests
{
    public class BoardTests
    {
        [Test]
        public void FromText_ParsesSizeCardsAndLockedCells()
        {
            Board board = Board.FromText(@"
                2H 2C .
                #  .  10S
            ");
            Assert.AreEqual(2, board.Rows);
            Assert.AreEqual(3, board.Cols);
            Assert.AreEqual("2H", board.GetCard(new CellPos(0, 0)).ToString());
            Assert.AreEqual("10S", board.GetCard(new CellPos(1, 2)).ToString());
            Assert.IsFalse(board[1, 0].Exists);
            Assert.IsTrue(board.IsPlaceable(new CellPos(0, 2)));
            Assert.IsFalse(board.IsPlaceable(new CellPos(1, 0)), "Ô # không đặt được");
            Assert.IsFalse(board.IsPlaceable(new CellPos(0, 0)), "Ô có lá không đặt được");
            Assert.IsFalse(board.IsPlaceable(new CellPos(5, 5)), "Ngoài bàn không đặt được");
        }

        [Test]
        public void ToText_RoundTrip()
        {
            const string text = "2H 2C .\n#  .  10S";
            Board board = Board.FromText(text);
            Board again = Board.FromText(board.ToText());
            Assert.AreEqual(board.ToText(), again.ToText());
        }

        [Test]
        public void Clone_IsDeep()
        {
            Board board = Board.FromText("2H .");
            Board copy = board.Clone();
            copy[0, 0].Card = null;
            copy[0, 1].Card = Card.Parse("3S");
            Assert.AreEqual("2H", board.GetCard(new CellPos(0, 0)).ToString());
            Assert.IsNull(board.GetCard(new CellPos(0, 1)));
        }

        [Test]
        public void HasPlaceableCell()
        {
            Assert.IsTrue(Board.FromText("2H .").HasPlaceableCell());
            Assert.IsFalse(Board.FromText("2H #").HasPlaceableCell());
        }
    }
}
