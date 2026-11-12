using Grimward.Domain.Core;
using Grimward.Domain.Data;

namespace Grimward.Domain.Map
{
    /// <summary>출정할 맵을 내준다. 튜토리얼은 고정 맵, 얕은 숲은 구조 + 이벤트 배치(M2-4).</summary>
    public interface IMapProvider
    {
        MapDefinition GetMap(string mapId, IRandomSource random);
    }

    /// <summary>길 하나를 지나는 데 걸리는 시간(분). v0.1은 처음 길·지나온 길 두 가지, 이후 지형·특성 보정.</summary>
    public interface ITravelTimeCalculator
    {
        int Minutes(MapEdgeState edge);
    }

    /// <summary>데이터에 정의된 맵을 그대로 쓴다 (튜토리얼 등 고정 맵).</summary>
    public sealed class FixedMapProvider : IMapProvider
    {
        private readonly GameDatabase db;
        public FixedMapProvider(GameDatabase db) { this.db = db; }
        public MapDefinition GetMap(string mapId, IRandomSource random) => db.GetMap(mapId);
    }

    /// <summary>처음 지나는 길 travelFirstMinutes, 지나온 길 travelRepeatMinutes.</summary>
    public sealed class DefaultTravelTimeCalculator : ITravelTimeCalculator
    {
        private readonly BalanceConfig c;
        public DefaultTravelTimeCalculator(BalanceConfig config) { c = config; }
        public int Minutes(MapEdgeState edge) => edge.Traveled ? c.travelRepeatMinutes : c.travelFirstMinutes;
    }
}
