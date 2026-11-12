using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Data;

namespace Grimward.Domain.Map
{
    /// <summary>
    /// 맵 정의 검증 (기획안 5장 · 엑셀 20_맵_이벤트).
    /// id·좌표 중복, 반경 안, 길은 이웃 칸끼리만, 시작에서 모든 노드 도달, 시작 노드는 비움,
    /// 막다른 노드(연결 1개, 시작 제외)는 빈 노드 금지, 한 노드 이벤트 최대 2개, 이벤트 참조 존재.
    /// </summary>
    public static class MapValidator
    {
        public const int MaxEventsPerNode = 2;

        public static List<string> Validate(MapDefinition m, GameDatabase db)
        {
            var e = new List<string>();
            string p = $"맵 {m.id}";
            var nodes = m.nodes ?? new MapNodeDefinition[0];
            var edges = m.edges ?? new MapEdgeDefinition[0];
            if (nodes.Length == 0) { e.Add($"{p}: 노드 없음"); return e; }

            var byId = new Dictionary<string, MapNodeDefinition>();
            var coords = new HashSet<HexCoord>();
            foreach (var n in nodes)
            {
                if (string.IsNullOrEmpty(n.id) || byId.ContainsKey(n.id)) { e.Add($"{p}: 노드 id 비었거나 중복 '{n.id}'"); continue; }
                byId[n.id] = n;
                var c = new HexCoord(n.q, n.r);
                if (!coords.Add(c)) e.Add($"{p}: 좌표 중복 {c}");
                if (m.radius > 0 && c.DistanceFromOrigin > m.radius) e.Add($"{p}: 노드 {n.id} 좌표 {c}가 반경 {m.radius} 밖");
            }
            if (string.IsNullOrEmpty(m.startNode) || !byId.ContainsKey(m.startNode)) { e.Add($"{p}: 시작 노드 '{m.startNode}' 없음"); return e; }

            var adj = byId.Keys.ToDictionary(k => k, k => new HashSet<string>());
            foreach (var ed in edges)
            {
                if (ed.a == null || ed.b == null || !byId.ContainsKey(ed.a) || !byId.ContainsKey(ed.b) || ed.a == ed.b)
                { e.Add($"{p}: 잘못된 길 {ed.a}–{ed.b}"); continue; }
                var ca = new HexCoord(byId[ed.a].q, byId[ed.a].r);
                var cb = new HexCoord(byId[ed.b].q, byId[ed.b].r);
                if (ca.DistanceTo(cb) != 1) e.Add($"{p}: 길 {ed.a}–{ed.b}이 이웃 칸이 아님");
                if (!adj[ed.a].Add(ed.b)) e.Add($"{p}: 길 중복 {ed.a}–{ed.b}");
                adj[ed.b].Add(ed.a);
            }

            // 시작에서 도달
            var seen = new HashSet<string> { m.startNode };
            var q = new Queue<string>(); q.Enqueue(m.startNode);
            while (q.Count > 0)
                foreach (var nb in adj[q.Dequeue()])
                    if (seen.Add(nb)) q.Enqueue(nb);
            foreach (var id in byId.Keys.Where(id => !seen.Contains(id))) e.Add($"{p}: 노드 {id}에 갈 수 없음");

            foreach (var n in nodes.Where(n => byId.ContainsKey(n.id ?? "")))
            {
                var evs = n.events ?? new NodeEventDefinition[0];
                if (n.id == m.startNode && evs.Length > 0) e.Add($"{p}: 시작 노드에는 이벤트를 두지 않음");
                if (n.id != m.startNode && adj[n.id].Count == 1 && evs.Length == 0) e.Add($"{p}: 막다른 노드 {n.id}가 빈 노드");
                if (evs.Length > MaxEventsPerNode) e.Add($"{p}: 노드 {n.id} 이벤트 {evs.Length}개 (최대 {MaxEventsPerNode})");
                foreach (var ev in evs) CheckEvent(e, $"{p} 노드 {n.id}", ev, db);
            }
            return e;
        }

        private static void CheckEvent(List<string> e, string p, NodeEventDefinition ev, GameDatabase db)
        {
            switch (ev.kind)
            {
                case DataKeys.NodeEvent.Battle:
                case DataKeys.NodeEvent.Boss:
                    if (!db.HasEncounter(ev.encounter)) e.Add($"{p}: 편성 '{ev.encounter}' 없음");
                    break;
                case DataKeys.NodeEvent.Gather:
                    if (!db.HasItem(ev.item)) e.Add($"{p}: 아이템 '{ev.item}' 없음");
                    if (ev.amount <= 0) e.Add($"{p}: 채집 수량 0");
                    break;
                case DataKeys.NodeEvent.Investigate:
                    if (!db.HasInvestigation(ev.objectId)) e.Add($"{p}: 조사 물건 '{ev.objectId}' 없음");
                    break;
                case DataKeys.NodeEvent.Treasure:
                    break;
                default:
                    e.Add($"{p}: 이벤트 종류 '{ev.kind}'");
                    break;
            }
        }
    }
}
