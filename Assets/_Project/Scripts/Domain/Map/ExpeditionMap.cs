using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Data;

namespace Grimward.Domain.Map
{
    /// <summary>탐험 중 노드 하나의 상태. 발견 → 방문 → (이벤트를 모두 마치면) 완료.</summary>
    public sealed class MapNodeState
    {
        public MapNodeDefinition Definition { get; }
        public string Id => Definition.id;
        public HexCoord Coord { get; }
        public IReadOnlyList<NodeEventDefinition> Events { get; }
        /// <summary>이벤트별 완료 여부 (Events와 같은 순서).</summary>
        internal bool[] EventDone { get; }
        public bool Discovered { get; internal set; }
        public bool Visited { get; internal set; }
        public bool IsStart { get; }

        /// <summary>이벤트를 모두 마쳤는가. 시작·빈 노드는 방문하면 완료.</summary>
        public bool Completed => Visited && EventDone.All(d => d);
        /// <summary>처음 발견할 때 지각 탐지에 성공해 이벤트 종류를 미리 안다.</summary>
        public bool Detected { get; internal set; }
        /// <summary>이벤트 종류가 보이는가: 방문했거나 지각 탐지에 성공.</summary>
        public bool EventsRevealed => Visited || Detected;
        public bool HasEvents => Events.Count > 0;
        /// <summary>
        /// 지나갈 수 있는 노드: 방문했고 남은 전투가 없다. 채집·조사가 남아 있어도(지나친 골동품) 지나갈 수 있다.
        /// 여러 칸 이동·자동 귀환의 중간 노드 조건.
        /// </summary>
        public bool Passable => Visited && !PendingEvents().Any(p =>
            p.ev.kind == DataKeys.NodeEvent.Battle || p.ev.kind == DataKeys.NodeEvent.Boss);

        internal MapNodeState(MapNodeDefinition def, bool isStart)
        {
            Definition = def;
            Coord = new HexCoord(def.q, def.r);
            Events = def.events ?? new NodeEventDefinition[0];
            EventDone = new bool[Events.Count];
            IsStart = isStart;
        }

        public bool IsEventDone(int index) => EventDone[index];

        /// <summary>아직 마치지 않은 이벤트 (정의 순서: 전투 → 채집처럼 앞의 것부터 처리).</summary>
        public IEnumerable<(int index, NodeEventDefinition ev)> PendingEvents() =>
            Events.Select((ev, i) => (i, ev)).Where(x => !EventDone[x.i]);
    }

    /// <summary>길 하나의 상태: 발견(한쪽 끝을 방문) · 지나감(한 번이라도 통과 → 다음부터 짧은 이동 시간).</summary>
    public sealed class MapEdgeState
    {
        public string A { get; }
        public string B { get; }
        public bool Discovered { get; internal set; }
        public bool Traveled { get; internal set; }

        internal MapEdgeState(string a, string b) { A = a; B = b; }
        public bool Connects(string x, string y) => (A == x && B == y) || (A == y && B == x);
        public string Other(string id) => A == id ? B : A;
    }

    /// <summary>이동 결과.</summary>
    public sealed class MoveResult
    {
        public MapNodeState From { get; internal set; }
        public MapNodeState To { get; internal set; }
        public int Minutes { get; internal set; }
        public bool FirstTimeOnEdge { get; internal set; }
        public bool FirstVisit { get; internal set; }
    }

    /// <summary>
    /// 탐험 맵 상태 (기획안 5장). 길로 연결된 노드로만 이동, 이동 중에는 아무 일도 없다.
    /// 이동 시간은 ITravelTimeCalculator (처음 길 60분 · 지나온 길 30분).
    /// 전장의 안개: 방문한 노드에서 길 VisionRange칸 안의 노드·길이 발견된다(기본 1, 특성·조사 물건으로 늘어남).
    /// 이벤트 종류는 방문하거나, 노드를 처음 발견할 때 지각 탐지(Detector)에 성공해야 보인다.
    /// </summary>
    public sealed class ExpeditionMap
    {
        private readonly Dictionary<string, MapNodeState> nodes = new Dictionary<string, MapNodeState>();
        private readonly List<MapEdgeState> edges = new List<MapEdgeState>();

        private readonly ITravelTimeCalculator travel;

        public MapDefinition Definition { get; }
        public MapNodeState Start { get; }
        public MapNodeState Current { get; private set; }
        /// <summary>직전에 있던 노드 (도주 시 후퇴할 곳).</summary>
        public MapNodeState Previous { get; private set; }
        /// <summary>시야: 방문한 노드에서 길 몇 칸까지 보이는가.</summary>
        public int VisionRange { get; private set; }
        /// <summary>노드를 처음 발견할 때 이벤트 종류를 알아채는지 판정 (지각 탐지). 없으면 판정 안 함.</summary>
        public Func<MapNodeState, bool> Detector { get; set; }
        public IEnumerable<MapNodeState> Nodes => Definition.nodes.Select(n => nodes[n.id]);
        public IReadOnlyList<MapEdgeState> Edges => edges;

        public ExpeditionMap(MapDefinition def, ITravelTimeCalculator travel, int visionRange = 1,
            Func<MapNodeState, bool> detector = null)
        {
            Definition = def ?? throw new ArgumentNullException(nameof(def));
            this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
            VisionRange = Math.Max(1, visionRange);
            Detector = detector;
            foreach (var n in def.nodes) nodes[n.id] = new MapNodeState(n, n.id == def.startNode);
            foreach (var e in def.edges ?? new MapEdgeDefinition[0]) edges.Add(new MapEdgeState(e.a, e.b));
            Start = Node(def.startNode);
            Current = Start;
            Visit(Start);
        }

        public MapNodeState Node(string id) =>
            nodes.TryGetValue(id, out var n) ? n : throw new KeyNotFoundException($"맵 {Definition.id}: 노드 '{id}' 없음");

        public MapEdgeState Edge(string a, string b) => edges.FirstOrDefault(e => e.Connects(a, b));

        public IEnumerable<MapNodeState> Neighbors(MapNodeState n) =>
            edges.Where(e => e.A == n.Id || e.B == n.Id).Select(e => nodes[e.Other(n.Id)]);

        public bool CanMoveTo(string id) => id != Current.Id && Edge(Current.Id, id) != null;

        public int TravelMinutes(string id)
        {
            var e = Edge(Current.Id, id) ?? throw new InvalidOperationException($"{Current.Id}에서 {id}로 가는 길이 없습니다.");
            return travel.Minutes(e);
        }

        /// <summary>이웃 노드로 이동. 시간 계산·길 통과·방문·안개 공개.</summary>
        public MoveResult MoveTo(string id)
        {
            if (!CanMoveTo(id)) throw new InvalidOperationException($"{Current.Id}에서 {id}로 갈 수 없습니다.");
            var e = Edge(Current.Id, id);
            var to = Node(id);
            var result = new MoveResult
            {
                From = Current, To = to, Minutes = TravelMinutes(id),
                FirstTimeOnEdge = !e.Traveled, FirstVisit = !to.Visited
            };
            e.Traveled = true;
            Previous = Current;
            Current = to;
            Visit(to);
            return result;
        }

        /// <summary>도주: 직전 노드로 물러난다 (시간은 도주 시간으로 따로 처리). 직전 노드가 없으면 그대로.</summary>
        public MapNodeState Retreat()
        {
            if (Previous == null) return Current;
            var back = Previous;
            Previous = Current;
            Current = back;
            return Current;
        }

        /// <summary>시야를 늘린다 (특성·조사 물건). 이미 방문한 노드 기준으로 다시 공개한다.</summary>
        public void SetVisionRange(int range)
        {
            VisionRange = Math.Max(1, range);
            foreach (var n in nodes.Values.Where(x => x.Visited).ToList()) Reveal(n);
        }

        private void Visit(MapNodeState n)
        {
            n.Visited = true;
            Discover(n);
            Reveal(n);
        }

        /// <summary>방문한 노드에서 길 VisionRange칸 안의 노드·길을 발견 상태로.</summary>
        private void Reveal(MapNodeState from)
        {
            var depth = new Dictionary<string, int> { [from.Id] = 0 };
            var q = new Queue<MapNodeState>(); q.Enqueue(from);
            while (q.Count > 0)
            {
                var n = q.Dequeue();
                int d = depth[n.Id];
                if (d >= VisionRange) continue;
                foreach (var e in edges.Where(x => x.A == n.Id || x.B == n.Id))
                {
                    e.Discovered = true;
                    var nb = nodes[e.Other(n.Id)];
                    Discover(nb);
                    if (depth.ContainsKey(nb.Id)) continue;
                    depth[nb.Id] = d + 1;
                    q.Enqueue(nb);
                }
            }
        }

        private void Discover(MapNodeState n)
        {
            if (n.Discovered) return;
            n.Discovered = true;
            if (!n.Visited && n.HasEvents && Detector != null && Detector(n)) n.Detected = true;
        }

        /// <summary>노드 이벤트 하나를 마침 (전투 승리, 채집, 조사 등).</summary>
        public void CompleteEvent(string nodeId, int index)
        {
            var n = Node(nodeId);
            if (index < 0 || index >= n.Events.Count) throw new ArgumentOutOfRangeException(nameof(index));
            n.EventDone[index] = true;
        }

        /// <summary>
        /// 여러 칸 한 번에 이동할 경로: 현재 노드에서 목표까지 이동 시간이 가장 짧은 경로.
        /// 지나가는 중간 노드는 지나갈 수 있는 노드(Passable: 방문했고 남은 전투 없음)여야 한다. 목표는 발견한 노드면 된다.
        /// 결과는 현재 다음 노드부터 목표까지. 갈 수 없으면 null.
        /// </summary>
        public List<MapNodeState> FindRoute(string targetId)
        {
            var target = Node(targetId);
            if (target == Current || !target.Discovered) return null;
            var dist = new Dictionary<string, int> { [Current.Id] = 0 };
            var prev = new Dictionary<string, string>();
            var open = new List<MapNodeState> { Current };
            while (open.Count > 0)
            {
                var n = open.OrderBy(x => dist[x.Id]).First();
                open.Remove(n);
                if (n == target) break;
                if (n != Current && !n.Passable) continue; // 전투가 남았거나 안 가 본 노드에서는 더 나아가지 않는다 (목표일 때만 도착)
                foreach (var e in edges.Where(x => (x.A == n.Id || x.B == n.Id) && x.Discovered))
                {
                    var nb = nodes[e.Other(n.Id)];
                    if (!nb.Discovered) continue;
                    int d = dist[n.Id] + travel.Minutes(e);
                    if (dist.TryGetValue(nb.Id, out int old) && old <= d) continue;
                    dist[nb.Id] = d; prev[nb.Id] = n.Id;
                    if (!open.Contains(nb)) open.Add(nb);
                }
            }
            if (!prev.ContainsKey(target.Id)) return null;
            var path = new List<MapNodeState>();
            for (string id = target.Id; id != Current.Id; id = prev[id]) path.Add(nodes[id]);
            path.Reverse();
            return path;
        }

        /// <summary>현재 노드에서 경로를 따라 걸리는 시간(분). 경로는 현재 다음 노드부터.</summary>
        public int RouteMinutes(IReadOnlyList<MapNodeState> path)
        {
            int total = 0;
            var at = Current;
            foreach (var n in path)
            {
                total += travel.Minutes(Edge(at.Id, n.Id));
                at = n;
            }
            return total;
        }

        /// <summary>
        /// 자동 귀환 경로: 현재 노드에서 시작 노드까지, 지나갈 수 있는 노드(Passable)만 지나는 최단 경로(BFS, 가중치 없음).
        /// 현재 노드는 미완료여도 된다(도주 직후 등). 경로가 없으면 null. 결과는 현재 다음 노드부터 시작 노드까지.
        /// </summary>
        public List<MapNodeState> PathToStart()
        {
            if (Current == Start) return new List<MapNodeState>();
            var prev = new Dictionary<string, string> { [Current.Id] = null };
            var q = new Queue<MapNodeState>(); q.Enqueue(Current);
            while (q.Count > 0)
            {
                var n = q.Dequeue();
                if (n == Start) break;
                foreach (var nb in Neighbors(n))
                {
                    if (prev.ContainsKey(nb.Id) || !nb.Passable) continue;
                    prev[nb.Id] = n.Id;
                    q.Enqueue(nb);
                }
            }
            if (!prev.ContainsKey(Start.Id)) return null;
            var path = new List<MapNodeState>();
            for (string id = Start.Id; id != Current.Id; id = prev[id]) path.Add(nodes[id]);
            path.Reverse();
            return path;
        }
    }
}
