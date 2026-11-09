using System;
namespace Grimward.Domain.Core
{
    public sealed class SeededRandom : IRandomSource
    {
        private readonly Random random;
        public SeededRandom(int seed) { random = new Random(seed); }
        public double NextDouble() => random.NextDouble();
        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            // inclusive 상한, int.MaxValue도 오버플로 없이 처리.
            return (int)(minInclusive + (long)(random.NextDouble() * ((long)maxInclusive - minInclusive + 1)));
        }
    }
}
