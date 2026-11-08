using System.Collections.Generic;
using NUnit.Framework;
using SuitMerge.Core.Config;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Model;

namespace SuitMerge.Tests
{
    /// <summary>Bộ sinh lá Endless (spec 5.2).</summary>
    public class EndlessCardGeneratorTests
    {
        private static Board Empty5x5() => new Board(5, 5);

        /// <summary>Rút nhiều lá, trả về tập rank đã gặp; mọi lá phải nằm trong [min, max].</summary>
        private static HashSet<Rank> DrawRanks(Board board, int count, uint seed, Rank min, Rank max)
        {
            var gen = new EndlessCardGenerator(seed, GameConfig.Default);
            var seen = new HashSet<Rank>();
            for (int i = 0; i < count; i++)
            {
                Card c = gen.Draw(board);
                Assert.GreaterOrEqual(c.Rank, min, $"Lá {c} thấp hơn {min}");
                Assert.LessOrEqual(c.Rank, max, $"Lá {c} cao hơn {max}");
                seen.Add(c.Rank);
            }
            return seen;
        }

        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new EndlessCardGenerator(2026, GameConfig.Default);
            var b = new EndlessCardGenerator(2026, GameConfig.Default);
            Board board = Empty5x5();
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.Draw(board).ToString(), b.Draw(board).ToString(), $"Khác ở lá thứ {i}");
        }

        [Test]
        public void DifferentSeed_DifferentSequence()
        {
            var a = new EndlessCardGenerator(1, GameConfig.Default);
            var b = new EndlessCardGenerator(2, GameConfig.Default);
            Board board = Empty5x5();
            int same = 0;
            for (int i = 0; i < 100; i++)
                if (a.Draw(board).ToString() == b.Draw(board).ToString()) same++;
            Assert.Less(same, 30);
        }

        [Test]
        public void EmptyBoard_RanksTwoToFour()
        {
            HashSet<Rank> seen = DrawRanks(Empty5x5(), 1000, 7, Rank.Two, Rank.Four);
            CollectionAssert.AreEquivalent(new[] { Rank.Two, Rank.Three, Rank.Four }, seen);
        }

        [Test]
        public void HighestEight_RanksFourToSeven()
        {
            Board board = Board.FromText("8H 2C .\n.  .  .");
            HashSet<Rank> seen = DrawRanks(board, 1000, 7, Rank.Four, Rank.Seven);
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void HighestKing_RanksNineToQueen_NeverKing()
        {
            Board board = Board.FromText("KH .\n.  .");
            HashSet<Rank> seen = DrawRanks(board, 1000, 7, Rank.Nine, Rank.Queen);
            Assert.AreEqual(4, seen.Count);
        }

        // H → [min, max]   (H = rank cao nhất trên bàn)
        [TestCase("2", Rank.Two, Rank.Four)]
        [TestCase("4", Rank.Two, Rank.Four)]
        [TestCase("5", Rank.Two, Rank.Four)]
        [TestCase("6", Rank.Two, Rank.Five)]
        [TestCase("8", Rank.Four, Rank.Seven)]
        [TestCase("10", Rank.Six, Rank.Nine)]
        [TestCase("K", Rank.Nine, Rank.Queen)]
        public void RankRange_Table(string highest, Rank expectedMin, Rank expectedMax)
        {
            Board board = Board.FromText($"{highest}S 2H .");
            (Rank min, Rank max) = EndlessCardGenerator.GetRankRange(board, GameConfig.Default);
            Assert.AreEqual(expectedMin, min);
            Assert.AreEqual(expectedMax, max);
        }

        [Test]
        public void LockedCells_DoNotAffectRange()
        {
            Board board = Board.FromText("# # .\n. # .");
            (Rank min, Rank max) = EndlessCardGenerator.GetRankRange(board, GameConfig.Default);
            Assert.AreEqual(Rank.Two, min);
            Assert.AreEqual(Rank.Four, max);
        }

        [Test]
        public void Suits_RoughlyUniform()
        {
            var gen = new EndlessCardGenerator(99, GameConfig.Default);
            Board board = Empty5x5();
            var counts = new Dictionary<Suit, int>();
            for (int i = 0; i < 1000; i++)
            {
                Suit s = gen.Draw(board).Suit;
                counts[s] = counts.TryGetValue(s, out int n) ? n + 1 : 1;
            }
            Assert.AreEqual(4, counts.Count, "Phải có đủ 4 chất");
            foreach (KeyValuePair<Suit, int> kv in counts)
                Assert.That(kv.Value, Is.InRange(200, 300), $"{kv.Key} ra {kv.Value}/1000 lần");
        }

        [Test]
        public void Config_IsRespected()
        {
            var config = new GameConfig { endlessStartHighest = 8 };
            (Rank min, Rank max) = EndlessCardGenerator.GetRankRange(Empty5x5(), config);
            Assert.AreEqual(Rank.Four, min);
            Assert.AreEqual(Rank.Seven, max);
        }
    }
}
