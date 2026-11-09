using Grimward.Domain.Config;
using Grimward.Domain.Map;
namespace Grimward.Domain.Exploration
{
    public sealed class TravelTimeCalculator : ITravelTimeCalculator
    {
        private readonly BalanceConfig config;
        public TravelTimeCalculator(BalanceConfig config) { this.config = config; }
        public int CalculateMinutes(EdgeData edge) =>
            edge.IsTraversed ? config.RevisitTraversalMinutes : config.FirstTraversalMinutes;
    }
}
