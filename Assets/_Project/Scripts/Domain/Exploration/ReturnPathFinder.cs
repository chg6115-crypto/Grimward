using System.Collections.Generic;
using Grimward.Domain.Config;
using Grimward.Domain.Map;
namespace Grimward.Domain.Exploration
{
    public sealed class ReturnPathFinder
    {
        private readonly BalanceConfig config;
        public ReturnPathFinder(BalanceConfig config) { this.config = config; }
        // 경로는 현재 위치를 포함한다. 현재 위치가 미완료여도 떠날 수 있으나 중간 노드로 통과할 수 없다.
        public IReadOnlyList<int> Find(MapData map, int currentNodeId)
        {
            if (!map.ContainsNode(currentNodeId)) return null;
            var previous = new Dictionary<int, int> { [currentNodeId] = currentNodeId };
            var queue = new Queue<int>();
            queue.Enqueue(currentNodeId);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == map.StartNodeId)
                {
                    var path = new List<int> { current };
                    while (current != currentNodeId) { current = previous[current]; path.Add(current); }
                    path.Reverse();
                    return path.AsReadOnly();
                }
                foreach (int edgeId in map.Node(current).EdgeIds)
                {
                    var edge = map.Edge(edgeId);
                    if (!edge.IsTraversed) continue;
                    int next = edge.Other(current);
                    var node = map.Node(next);
                    bool safeException = config.AllowIncompleteSafeNodesOnReturn &&
                        node.IsVisited && node.EventType == NodeEventType.Treasure;
                    if (previous.ContainsKey(next) || (!node.IsEventCompleted && !safeException)) continue;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }
            return null;
        }
    }
}
