using Grimward.Domain.Map;
namespace Grimward.Domain.Exploration
{
    public sealed class FogOfWar
    {
        public void RevealFrom(MapData map, int nodeId)
        {
            var node = map.Node(nodeId);
            node.IsDiscovered = true;

            foreach (int edgeId in node.EdgeIds)
            {
                var edge = map.Edge(edgeId);
                edge.IsDiscovered = true;
                map.Node(edge.Other(nodeId)).IsDiscovered = true;
            }
        }
    }
}
