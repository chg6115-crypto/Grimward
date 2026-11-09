using System;
using System.Collections.Generic;
using Grimward.Domain.Config;
using Grimward.Domain.Core;
using Grimward.Domain.Map;
namespace Grimward.Domain.Exploration
{
    public sealed class ExplorationState
    {
        private readonly BalanceConfig config;
        private readonly ITravelTimeCalculator travelTime;
        private readonly FogOfWar fog;
        private readonly PathEventResolver pathEvents;
        private readonly ReturnPathFinder returnPaths;
        public MapData Map { get; }
        public GameTime Time { get; }
        public int CurrentNodeId { get; private set; }
        public bool IsDay => Time.IsDay(config);
        public bool IsAtStart => CurrentNodeId == Map.StartNodeId;
        public event Action<string> Logged;
        public event Action Changed;

        public ExplorationState(MapData map, BalanceConfig config, ITravelTimeCalculator travelTime,
            FogOfWar fog, PathEventResolver pathEvents, ReturnPathFinder returnPaths)
        {
            config.Validate();
            Map = map; this.config = config; this.travelTime = travelTime;
            this.fog = fog; this.pathEvents = pathEvents; this.returnPaths = returnPaths;
            Time = new GameTime(config.GameStartDay, config.GameStartMinute);
            CurrentNodeId = map.StartNodeId;
            fog.RevealFrom(map, CurrentNodeId);
            map.Node(CurrentNodeId).CompleteEvent();
        }

        public bool CanMoveTo(int nodeId) => Map.Connection(CurrentNodeId, nodeId) != null;

        public bool TryMove(int nodeId)
        {
            var edge = Map.Connection(CurrentNodeId, nodeId);
            if (edge == null) { Logged?.Invoke("연결된 경로가 없어 이동할 수 없습니다."); return false; }
            int minutes = travelTime.CalculateMinutes(edge);
            if (minutes <= 0) throw new InvalidOperationException("이동 시간은 양수여야 합니다.");
            // 출발 시각·이동 전 통과 여부로 딱 한 번 판정한다.
            bool ambush = pathEvents.Roll(IsDay, edge.IsTraversed);
            Time.AddMinutes(minutes);
            edge.IsTraversed = true;
            CurrentNodeId = nodeId;
            fog.RevealFrom(Map, nodeId);
            var node = Map.Node(nodeId);
            if (node.EventType == NodeEventType.Start || node.EventType == NodeEventType.Empty)
                node.CompleteEvent();
            Logged?.Invoke($"{nodeId:00}번 노드 도착 (+{minutes}분) · {Time}");
            if (ambush) Logged?.Invoke("매복 발생 — M2에서 구현");
            if (!node.IsEventCompleted)
            {
                if (node.EventType == NodeEventType.Battle) Logged?.Invoke("전투 발생 — M2에서 구현");
                if (node.EventType == NodeEventType.Treasure) Logged?.Invoke("보물 발견 — M3에서 구현");
            }
            Changed?.Invoke();
            return true;
        }

        public IReadOnlyList<int> FindReturnPath() => returnPaths.Find(Map, CurrentNodeId);

        public bool TryAutoReturn()
        {
            var path = FindReturnPath();
            if (path == null)
            {
                Logged?.Invoke("자동 귀환 불가: 통과한 경로와 완료 노드로 이어지는 길이 없습니다.");
                return false;
            }
            for (int i = 1; i < path.Count; i++)
                if (!TryMove(path[i])) return false;
            return TryManualReturn();
        }

        public bool TryManualReturn()
        {
            if (!IsAtStart) { Logged?.Invoke("시작 노드에서만 수동 귀환할 수 있습니다."); return false; }
            Logged?.Invoke("마을로 귀환 — M4에서 구현");
            return true;
        }
    }
}
