using System;
using System.Collections.Generic;
using System.Linq;
namespace Grimward.Domain.Map
{
    public sealed class MapData
    {
        private readonly Dictionary<int, NodeData> nodesById;
        private readonly Dictionary<int, EdgeData> edgesById;
        public int Radius { get; }
        public int StartNodeId { get; }
        public IReadOnlyList<NodeData> Nodes { get; }
        public IReadOnlyList<EdgeData> Edges { get; }
        public MapData(int radius, IEnumerable<NodeData> nodes, IEnumerable<EdgeData> edges, int startNodeId)
        {
            Radius = radius; StartNodeId = startNodeId;
            Nodes = Array.AsReadOnly(nodes.ToArray());
            Edges = Array.AsReadOnly(edges.ToArray());
            // 중복 ID는 자료구조 생성 단계에서 거부한다. 나머지 제약은 MapValidator 담당.
            nodesById = Nodes.ToDictionary(n => n.Id);
            edgesById = Edges.ToDictionary(e => e.Id);
            if (Nodes.Any(n => n.EdgeIds.Count != 0))
                throw new ArgumentException("MapData에는 새 노드 인스턴스를 전달해야 합니다.");
            foreach (var edge in Edges)
            {
                if (nodesById.TryGetValue(edge.FromNodeId, out var from)) from.AttachEdge(edge.Id);
                if (nodesById.TryGetValue(edge.ToNodeId, out var to)) to.AttachEdge(edge.Id);
            }
        }
        public NodeData Node(int id) => nodesById[id];
        public EdgeData Edge(int id) => edgesById[id];
        public bool ContainsNode(int id) => nodesById.ContainsKey(id);
        public EdgeData Connection(int from, int to) =>
            !ContainsNode(from) ? null : Node(from).EdgeIds.Select(Edge).FirstOrDefault(e => e.Other(from) == to);
    }
}
