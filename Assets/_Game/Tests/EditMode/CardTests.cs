using System;
using NUnit.Framework;
using SuitMerge.Core.Model;

namespace SuitMerge.Tests
{
    public class CardTests
    {
        [TestCase("2H", Rank.Two, Suit.Heart)]
        [TestCase("10S", Rank.Ten, Suit.Spade)]
        [TestCase("JC", Rank.Jack, Suit.Club)]
        [TestCase("QD", Rank.Queen, Suit.Diamond)]
        [TestCase("KH", Rank.King, Suit.Heart)]
        public void Parse_ValidToken(string token, Rank rank, Suit suit)
        {
            Card card = Card.Parse(token);
            Assert.AreEqual(rank, card.Rank);
            Assert.AreEqual(suit, card.Suit);
            Assert.AreEqual(token, card.ToString(), "ToString phải trả lại đúng token");
        }

        [TestCase("")]
        [TestCase("H")]
        [TestCase("1H")]
        [TestCase("11H")]
        [TestCase("010H")]
        [TestCase("10X")]
        [TestCase("AH")] // Ace không tồn tại dưới dạng lá
        [TestCase("2h")]
        public void Parse_InvalidToken_Throws(string token)
        {
            Assert.Throws<FormatException>(() => Card.Parse(token));
        }

        [Test]
        public void Constructor_RejectsAceAndNoSuit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Card(Rank.Ace, Suit.Heart));
            Assert.Throws<ArgumentException>(() => new Card(Rank.Five, Suit.None));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var card = new Card(Rank.Seven, Suit.Club) { IceLayers = 2 };
            Card copy = card.Clone();
            copy.IceLayers = 0;
            Assert.AreEqual(2, card.IceLayers);
            Assert.AreEqual("7C", copy.ToString());
        }

        [Test]
        public void RankLabelsAndValues()
        {
            Assert.AreEqual("10", Rank.Ten.ToLabel());
            Assert.AreEqual("J", Rank.Jack.ToLabel());
            Assert.AreEqual("A", Rank.Ace.ToLabel());
            Assert.AreEqual(13, Rank.King.Value());
            Assert.AreEqual(Rank.Ace, Rank.King.Next());
        }

        [Test]
        public void SuitColors()
        {
            Assert.IsTrue(Suit.Heart.IsRed());
            Assert.IsTrue(Suit.Diamond.IsRed());
            Assert.IsFalse(Suit.Club.IsRed());
            Assert.IsFalse(Suit.Spade.IsRed());
        }
    }
}
