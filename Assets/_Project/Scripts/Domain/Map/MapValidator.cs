using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Config;
namespace Grimward.Domain.Map
{
    public sealed class MapValidator
    {
        private readonly BalanceConfig config;
        public MapValidator(BalanceConfig config) { this.config = config; }
        public IReadOnlyList<string> Validate(MapData map)
        {
            var errors = new List<string>();
            if (map.Radius != config.MapRadius) errors.Add("맵 반경 불일치");
            if (map.Nodes.Count != config.ActiveNodeCount) errors.Add("활성 노드 수 불일치");
            CheckCount(map, NodeEventType.Start, config.StartNodeCount, errors);
            CheckCount(map, NodeEventType.Battle, config.BattleNodeCount, errors);
            CheckCount(map, NodeEventType.Treasure, config.TreasureNodeCount, errors);
            CheckCount(map, NodeEventType.Empty, config.EmptyNodeCount, errors);
            if (map.Nodes.Any(n => n.Coord.DistanceFromOrigin > map.Radius)) errors.Add("반경 밖 노드");
            if (map.Nodes.Select(n => n.Coord).Distinct().Count() != map.Nodes.Count) errors.Add("중복 좌표");
            bool validStart = map.ContainsNode(map.StartNodeId);
            if (!validStart || map.Node(map.StartNodeId).EventType != NodeEventType.Start ||
                map.Node(map.StartNodeId).Coord.DistanceFromOrigin != map.Radius) errors.Add("시작 노드 가장자리 조건 위반");
            var pairs = new HashSet<string>();
            bool validEdges = true;
            foreach (var edge in map.Edges)
            {
                if (!map.ContainsNode(edge.FromNodeId) || !map.ContainsNode(edge.ToNodeId))
                { errors.Add("경로 끝 노드 없음"); validEdges = false; continue; }
                if (map.Node(edge.FromNodeId).Coord.DistanceTo(map.Node(edge.ToNodeId).Coord) != 1)
                    errors.Add("인접하지 않은 경로");
                string pair = edge.FromNodeId < edge.ToNodeId ? $"{edge.FromNodeId}:{edge.ToNodeId}" : $"{edge.ToNodeId}:{edge.FromNodeId}";
                if (!pairs.Add(pair)) errors.Add("중복 경로");
            }
            if (!validStart || !validEdges) return errors;
            var distance = new Dictionary<int, int> { [map.StartNodeId] = 0 };
            var queue = new Queue<int>();
            queue.Enqueue(map.StartNodeId);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int edgeId in map.Node(current).EdgeIds)
                {
                    int next = map.Edge(edgeId).Other(current);
                    if (distance.ContainsKey(next)) continue;
                    distance[next] = distance[current] + 1;
                    queue.Enqueue(next);
                }
            }
            if (distance.Count != map.Nodes.Count) errors.Add("연결되지 않은 노드");
            else
            {
                int cycles = map.Edges.Count - map.Nodes.Count + 1;
                if (cycles < config.MinCycleCount || cycles > config.MaxCycleCount) errors.Add("순환 경로 수 위반");
            }
            if (distance.Values.Any(d => d > config.MaxPathDistance)) errors.Add("최대 경로 거리 초과");
            return errors;
        }
        private static void CheckCount(MapData map, NodeEventType type, int count, List<string> errors)
        {
            if (map.Nodes.Count(n => n.EventType == type) != count) errors.Add($"{type} 구성 불일치");
        }
    }
}
