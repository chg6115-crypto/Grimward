using System;
namespace Grimward.Domain.Core
{
    public sealed class SeededRandom : IRandomSource
    {
        private readonly Random random;
        public SeededRandom(int seed) { random = new Random(seed); }
        public double NextDouble() => random.NextDouble();
    }
}
