namespace Grimward.Domain.Map
{
    public sealed class FixedMapProvider : IMapProvider
    {
        public MapData CreateMap()
        {
            // 고정 맵의 좌표·연결은 레벨 데이터다. 설정 변경 시 검증기가 불일치를 보고한다.
            var nodes = new[]
            {
                new NodeData(0, new HexCoord(-2, 0), NodeEventType.Start),
                new NodeData(1, new HexCoord(-1, 0), NodeEventType.Empty),
                new NodeData(2, new HexCoord(-1, 1), NodeEventType.Empty),
                new NodeData(3, new HexCoord(0, 0), NodeEventType.Battle),
                new NodeData(4, new HexCoord(0, -1), NodeEventType.Battle),
                new NodeData(5, new HexCoord(1, -1), NodeEventType.Treasure),
                new NodeData(6, new HexCoord(1, 0), NodeEventType.Battle),
                new NodeData(7, new HexCoord(0, 1), NodeEventType.Battle),
                new NodeData(8, new HexCoord(-1, 2), NodeEventType.Treasure),
                new NodeData(9, new HexCoord(1, 1), NodeEventType.Battle),
                new NodeData(10, new HexCoord(2, 0), NodeEventType.Battle)
            };
            var edges = new[]
            {
                new EdgeData(0, 0, 1), new EdgeData(1, 1, 2),
                new EdgeData(2, 1, 3), new EdgeData(3, 1, 4),
                new EdgeData(4, 3, 5), new EdgeData(5, 3, 6),
                new EdgeData(6, 2, 7), new EdgeData(7, 2, 8),
                new EdgeData(8, 7, 9), new EdgeData(9, 6, 10),
                new EdgeData(10, 2, 3), new EdgeData(11, 5, 6)
            };
            return new MapData(2, nodes, edges, 0);
        }
    }
}
