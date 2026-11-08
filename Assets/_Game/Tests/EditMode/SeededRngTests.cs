using NUnit.Framework;
using SuitMerge.Core.Deck;

namespace SuitMerge.Tests
{
    public class SeededRngTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new SeededRng(1001);
            var b = new SeededRng(1001);
            for (int i = 0; i < 200; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"Khác nhau ở lần rút thứ {i}");
        }

        [Test]
        public void DifferentSeed_DifferentSequence()
        {
            var a = new SeededRng(1001);
            var b = new SeededRng(1002);
            int same = 0;
            for (int i = 0; i < 50; i++)
                if (a.NextUInt() == b.NextUInt()) same++;
            Assert.Less(same, 3);
        }

        [Test]
        public void NextInt_StaysInRange_AndCoversAllValues()
        {
            var rng = new SeededRng(42);
            var seen = new bool[4];
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.NextInt(2, 6);
                Assert.GreaterOrEqual(v, 2);
                Assert.Less(v, 6);
                seen[v - 2] = true;
            }
            CollectionAssert.AreEqual(new[] { true, true, true, true }, seen);
        }

        [Test]
        public void Clone_ContinuesSameSequence()
        {
            var rng = new SeededRng(7);
            rng.NextUInt();
            rng.NextUInt();
            SeededRng copy = rng.Clone();
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(rng.NextUInt(), copy.NextUInt());
        }
    }
}
