using System.Collections.Generic;
namespace Grimward.Domain.Map
{
    public enum NodeEventType { Start, Battle, Treasure, Empty }
    public sealed class NodeData
    {
        private readonly List<int> edgeIds = new List<int>();
        public int Id { get; }
        public HexCoord Coord { get; }
        public NodeEventType EventType { get; }
        public bool IsDiscovered { get; internal set; }
        public bool IsVisited { get; internal set; }
        public bool IsEventCompleted { get; private set; }
        public IReadOnlyList<int> EdgeIds => edgeIds.AsReadOnly();
        public NodeData(int id, HexCoord coord, NodeEventType eventType)
        { Id = id; Coord = coord; EventType = eventType; }
        internal void AttachEdge(int edgeId) { edgeIds.Add(edgeId); }
        public void CompleteEvent()
        {
            if (!IsVisited) throw new System.InvalidOperationException("방문한 노드만 완료할 수 있습니다.");
            IsEventCompleted = true;
        }
    }
}
