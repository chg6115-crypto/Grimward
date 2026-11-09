using System;
namespace Grimward.Domain.Map
{
    public sealed class EdgeData
    {
        public int Id { get; }
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public bool IsDiscovered { get; internal set; }
        public bool IsTraversed { get; internal set; }
        public EdgeData(int id, int fromNodeId, int toNodeId)
        { Id = id; FromNodeId = fromNodeId; ToNodeId = toNodeId; }
        public bool Connects(int nodeId) => FromNodeId == nodeId || ToNodeId == nodeId;
        public int Other(int nodeId)
        {
            if (!Connects(nodeId)) throw new ArgumentException("경로에 속하지 않는 노드입니다.");
            return nodeId == FromNodeId ? ToNodeId : FromNodeId;
        }
    }
}
