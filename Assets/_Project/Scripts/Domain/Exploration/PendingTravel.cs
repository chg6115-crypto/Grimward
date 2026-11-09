using Grimward.Domain.Map;
namespace Grimward.Domain.Exploration
{
    internal sealed class PendingTravel
    {
        public int From { get; }
        public int To { get; }
        public EdgeData Edge { get; }
        public int Minutes { get; }
        public PendingTravel(int from, int to, EdgeData edge, int minutes)
        {
            From = from;
            To = to;
            Edge = edge;
            Minutes = minutes;
        }
    }
}
