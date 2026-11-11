using System;

namespace Grimward.Domain.Core
{
    /// <summary>
    /// 플랫폼과 무관하게 같은 시드 → 같은 수열을 내는 난수 (SplitMix64).
    /// State를 저장·복원하면 이어서 같은 수열을 얻는다.
    /// </summary>
    public sealed class SeededRandom : IRandomSource
    {
        public ulong State { get; set; }

        public SeededRandom(int seed)
        {
            State = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL);
        }

        private ulong NextULong()
        {
            unchecked
            {
                State += 0x9E3779B97F4A7C15UL;
                ulong z = State;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            ulong range = (ulong)((long)maxInclusive - minInclusive + 1);
            return (int)(minInclusive + (long)(NextULong() % range));
        }
    }
}
