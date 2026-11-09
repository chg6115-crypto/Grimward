using Grimward.Domain.Config;
using Grimward.Domain.Core;
namespace Grimward.Domain.Exploration
{
    public sealed class PathEventResolver
    {
        private readonly BalanceConfig config;
        private readonly IRandomSource random;
        public PathEventResolver(BalanceConfig config, IRandomSource random)
        { this.config = config; this.random = random; }
        public double SelectChance(bool isDay, bool wasTraversed) =>
            isDay
                ? (wasTraversed ? config.PathEventChanceDayRevisit : config.PathEventChanceDayFirst)
                : (wasTraversed ? config.PathEventChanceNightRevisit : config.PathEventChanceNightFirst);
        public bool Roll(bool isDay, bool wasTraversed) => random.NextDouble() < SelectChance(isDay, wasTraversed);
    }
}
