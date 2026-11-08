using System;

namespace SuitMerge.Core.Deck
{
    /// <summary>
    /// Bộ sinh số ngẫu nhiên có seed (Mulberry32). Cùng seed → cùng dãy số trên mọi máy.
    /// Không dùng UnityEngine.Random để deck tái lập được và test được (spec 5.1).
    /// </summary>
    public sealed class SeededRng
    {
        /// <summary>Trạng thái nội bộ — lưu/khôi phục được (dùng cho Undo).</summary>
        public uint State { get; set; }

        public SeededRng(uint seed)
        {
            State = seed;
        }

        public uint NextUInt()
        {
            unchecked
            {
                State += 0x6D2B79F5u;
                uint z = State;
                z = (z ^ (z >> 15)) * (z | 1u);
                z ^= z + (z ^ (z >> 7)) * (z | 61u);
                return z ^ (z >> 14);
            }
        }

        /// <summary>Số nguyên trong [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentException("maxExclusive phải lớn hơn minInclusive.");
            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)((NextUInt() * range) >> 32));
        }

        /// <summary>Số thực trong [0, 1).</summary>
        public double NextDouble() => NextUInt() / 4294967296.0;

        public SeededRng Clone() => new SeededRng(0) { State = State };
    }
}
