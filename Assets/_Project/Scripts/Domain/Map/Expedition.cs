using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Units;

namespace Grimward.Domain.Map
{
    public enum ExpeditionPhase
    {
        /// <summary>지도에서 이동할 수 있다.</summary>
        Exploring,
        /// <summary>노드 이벤트 중 전투가 진행 중이다 (BattleState를 화면이 돌린다).</summary>
        InBattle,
        /// <summary>귀환했거나 전멸했다.</summary>
        Ended
    }

    public enum ExpeditionEnd { None, Returned, Wiped }

    /// <summary>조사 결과.</summary>
    public sealed class InvestigationOutcome
    {
        public string Grade { get; internal set; }   // good · normal · bad
        public string Text { get; internal set; }
        public bool UsedSupply { get; internal set; }
        public List<string> Gained { get; } = new List<string>();
    }

    /// <summary>
    /// 출정 한 번 (기획안 5장). 지도 이동 · 시간 · 노드 이벤트(전투·채집·조사) · 귀환.
    /// 노드에 도착하면: 전투가 남아 있으면 반드시 싸운다(도주하면 직전 노드로 후퇴). 채집·조사는 골라서 하고, 안 하면 미완료로 남는다.
    /// 시각은 이동·전투·도주로만 흐른다. 이동 중에는 아무 일도 없다.
    /// M2-3 이후: 밤 랜덤 전투, 식사·허기, 보물, 조사 나쁨 결과 효과.
    /// </summary>
    public sealed class Expedition
    {
        private readonly GameDatabase db;
        private readonly IRandomSource random;
        private readonly List<string> log = new List<string>();
        private BattleState battle;
        private NodeEventDefinition battleEvent;
        private int battleEventIndex;

        public ExpeditionMap Map { get; }
        public GameTime Time { get; }
        private readonly List<Adventurer> party;
        /// <summary>파티 순서 = 전투 시작 칸 순서 (전투 밖에서 바꿀 수 있다).</summary>
        public IReadOnlyList<Adventurer> Party => party;
        public BattleInventory Bag { get; }
        public IReadOnlyList<string> Log => log;
        public ExpeditionPhase Phase { get; private set; } = ExpeditionPhase.Exploring;
        public ExpeditionEnd End { get; private set; } = ExpeditionEnd.None;
        /// <summary>이번 출정에서 처치한 몬스터 (의뢰 집계용).</summary>
        public List<string> Killed { get; } = new List<string>();
        public bool IsNight => Time.IsNight(db.Balance);
        public BattleState CurrentBattle => battle;

        public Expedition(GameDatabase db, IRandomSource random, IReadOnlyList<Adventurer> party, BattleInventory bag,
            MapDefinition map, GameTime start, ITravelTimeCalculator travel = null)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.party = (party ?? throw new ArgumentNullException(nameof(party))).ToList();
            Bag = bag ?? new BattleInventory();
            Time = start ?? throw new ArgumentNullException(nameof(start));
            foreach (var a in Party) a.BeginExpedition();
            log.Add($"{Time} 출발 — {map.name}");
            Map = new ExpeditionMap(map, travel ?? new DefaultTravelTimeCalculator(db.Balance),
                db.Balance.mapVisionRange, DetectEvents);
        }

        public IEnumerable<Adventurer> Alive => Party.Where(a => !a.IsDead);

        // ── 전투 밖: 자리 바꾸기 · 아이템 ─────────────────────────────

        /// <summary>
        /// 전투 밖에서 살아 있는 모험가 두 명의 자리를 바꾼다 (칸 = 살아 있는 순서 1~4, 바로 옆끼리만).
        /// 다음 전투는 이 순서로 시작한다.
        /// </summary>
        public void SwapAdjacent(int rankA, int rankB)
        {
            if (Phase != ExpeditionPhase.Exploring) throw new InvalidOperationException("전투 중에는 전투 화면에서 이동한다.");
            var alive = Alive.ToList();
            if (Math.Abs(rankA - rankB) != 1 || rankA < 1 || rankB < 1 || rankA > alive.Count || rankB > alive.Count)
                throw new InvalidOperationException("바로 옆 모험가와만 자리를 바꿀 수 있다.");
            int i = party.IndexOf(alive[rankA - 1]), j = party.IndexOf(alive[rankB - 1]);
            (party[i], party[j]) = (party[j], party[i]);
            log.Add($"{Time} 자리 바꿈 — {party[j].Name} ↔ {party[i].Name}");
        }

        /// <summary>전투 밖에서 쓸 수 있는 아이템: 회복(heal) 또는 현장 치료(treatInjury). 해독·지혈은 전투 중 효과라 전투 밖에서는 쓸 데가 없다.</summary>
        public static bool IsUsableOutsideBattle(ItemDefinition item) => item.heal > 0 || item.treatInjury;

        /// <summary>
        /// 전투 밖 아이템 사용 (시간 소모 없음). 회복약 = HP 회복(최대 HP까지), 붕대 = 현장 치료(경상 제거 · 중상 → 경상).
        /// 효과가 하나도 없으면(HP 가득 + 치료할 부상 없음) 쓰지 않는다. 결과 문장을 돌려준다.
        /// </summary>
        public string UseItem(string itemId, Adventurer target)
        {
            if (Phase != ExpeditionPhase.Exploring) throw new InvalidOperationException("지금은 아이템을 쓸 수 없다.");
            if (target == null || target.IsDead || !party.Contains(target)) throw new InvalidOperationException("아이템 대상이 잘못됐다.");
            var item = db.GetItem(itemId);
            if (!IsUsableOutsideBattle(item)) throw new InvalidOperationException($"{item.name}은(는) 전투 밖에서 쓸 데가 없다.");
            if (Bag.Count(itemId) <= 0) throw new InvalidOperationException($"남은 {item.name}: 0개");

            int max = StatFormulas.MaxHp(db.Balance, target);
            int heal = item.heal > 0 ? Math.Min(item.heal, max - target.CurrentHp) : 0;
            var treatable = item.treatInjury
                ? target.Injuries.Where(i => db.GetInjury(i.Id).fieldTreatable).ToList()
                : new List<InjuryState>();
            if (heal <= 0 && treatable.Count == 0) throw new InvalidOperationException($"{target.Name}에게 {item.name}은(는) 효과가 없다.");

            Bag.TryConsume(itemId);
            var parts = new List<string>();
            if (heal > 0) { target.CurrentHp += heal; parts.Add($"HP +{heal}"); }
            foreach (var inj in treatable)
            {
                if (inj.Severe) { inj.Severe = false; parts.Add($"「{db.InjuryStageName(inj.Id, true)}」 → 「{db.InjuryStageName(inj.Id, false)}」"); }
                else { target.Injuries.Remove(inj); parts.Add($"「{db.InjuryStageName(inj.Id, false)}」 치료"); }
            }
            string text = $"{item.name} → {target.Name}: {string.Join(" · ", parts)}";
            log.Add($"{Time} {text}");
            return text;
        }

        // ── 지각 탐지 ─────────────────────────────

        /// <summary>파티 생존자 중 지각 최고 1명 기준: Clamp(기본 + 지각 × 계수, 최소, 최대).</summary>
        public double DetectChance()
        {
            var c = db.Balance;
            int best = Alive.Select(a => a.Perception).DefaultIfEmpty(0).Max();
            return Math.Max(c.perceptionDetectMin, Math.Min(c.perceptionDetectMax, c.perceptionDetectBase + best * c.perceptionDetectPerPoint));
        }

        private bool DetectEvents(MapNodeState n)
        {
            bool hit = random.NextDouble() < DetectChance();
            if (hit) log.Add($"  {NodeLabel(n)}: 지각으로 미리 알아챔 — {DescribeEvents(n)}");
            return hit;
        }

        // ── 이동 ─────────────────────────────

        /// <summary>지금 노드에 반드시 싸워야 하는 전투가 남아 있는가.</summary>
        public bool HasForcedBattle => PendingBattle().ev != null;

        public bool CanMoveTo(string nodeId) =>
            Phase == ExpeditionPhase.Exploring && !HasForcedBattle && Map.CanMoveTo(nodeId);

        /// <summary>예상 도착 시각 (미리보기용).</summary>
        public GameTime ArrivalTime(string nodeId)
        {
            var t = new GameTime(Time.Day, Time.MinuteOfDay);
            t.AddMinutes(Map.TravelMinutes(nodeId));
            return t;
        }

        /// <summary>여러 칸 한 번에 이동할 수 있는가 (중간 노드는 모두 완료, 목표는 발견한 노드).</summary>
        public bool CanTravelTo(string nodeId) =>
            Phase == ExpeditionPhase.Exploring && !HasForcedBattle && Map.FindRoute(nodeId) != null;

        /// <summary>목표까지 경로와 걸리는 시간. 갈 수 없으면 경로 null.</summary>
        public (List<MapNodeState> path, int minutes) Route(string nodeId)
        {
            var path = Map.FindRoute(nodeId);
            return path == null ? (null, 0) : (path, Map.RouteMinutes(path));
        }

        /// <summary>
        /// 목표까지 한 칸씩 이동한다 (각 칸 도착 시간 기록). 목표에서 멈춘다.
        /// (M2-3: 밤에 지나는 완료 노드에서 랜덤 전투가 나면 그 자리에서 멈춘다.)
        /// </summary>
        public List<MoveResult> TravelTo(string nodeId)
        {
            if (!CanTravelTo(nodeId)) throw new InvalidOperationException($"지금은 {nodeId}까지 갈 수 없습니다.");
            var results = new List<MoveResult>();
            foreach (var step in Map.FindRoute(nodeId))
            {
                results.Add(MoveTo(step.Id));
                if (HasForcedBattle) break;
            }
            return results;
        }

        /// <summary>자동 귀환했을 때 시작 노드 도착 시각. 완료한 노드만 지나는 경로가 없으면 null.</summary>
        public GameTime ReturnEstimate()
        {
            var path = Map.PathToStart();
            if (path == null) return null;
            var t = new GameTime(Time.Day, Time.MinuteOfDay);
            t.AddMinutes(Map.RouteMinutes(path));
            return t;
        }

        /// <summary>목표 도착 예상 시각 (여러 칸 이동 포함). 갈 수 없으면 null.</summary>
        public GameTime TravelArrival(string nodeId)
        {
            var (path, minutes) = Route(nodeId);
            if (path == null) return null;
            var t = new GameTime(Time.Day, Time.MinuteOfDay);
            t.AddMinutes(minutes);
            return t;
        }

        public MoveResult MoveTo(string nodeId)
        {
            if (!CanMoveTo(nodeId)) throw new InvalidOperationException($"지금은 {nodeId}로 이동할 수 없습니다.");
            var r = Map.MoveTo(nodeId);
            Time.AddMinutes(r.Minutes);
            log.Add($"{Time} {NodeLabel(r.To)} 도착 ({r.Minutes}분{(r.FirstTimeOnEdge ? ", 처음 길" : "")})");
            return r;
        }

        // ── 노드 이벤트 ─────────────────────────────

        private (int index, NodeEventDefinition ev) PendingBattle() =>
            Map.Current.PendingEvents().FirstOrDefault(x =>
                x.ev.kind == DataKeys.NodeEvent.Battle || x.ev.kind == DataKeys.NodeEvent.Boss);

        /// <summary>지금 노드에서 할 수 있는 다음 이벤트 (전투가 남아 있으면 전투가 먼저).</summary>
        public (int index, NodeEventDefinition ev) NextEvent()
        {
            var b = PendingBattle();
            if (b.ev != null) return b;
            return Map.Current.PendingEvents().FirstOrDefault();
        }

        /// <summary>남은 전투를 시작한다. 화면은 돌려받은 BattleState를 진행하고, 끝나면 FinishBattle을 부른다.</summary>
        public BattleState BeginBattle()
        {
            if (Phase != ExpeditionPhase.Exploring) throw new InvalidOperationException("지금은 전투를 시작할 수 없습니다.");
            var (index, ev) = PendingBattle();
            if (ev == null) throw new InvalidOperationException("이 노드에는 남은 전투가 없습니다.");
            battleEvent = ev; battleEventIndex = index;
            battle = BattleState.Create(db, random, Party.ToList(), ev.encounter, IsNight, Bag);
            battle.Start();
            Phase = ExpeditionPhase.InBattle;
            log.Add($"{Time} 전투 — {db.GetEncounter(ev.encounter).name}{(IsNight ? " (밤)" : "")}");
            return battle;
        }

        /// <summary>
        /// 전투 결과 반영. 승리: 이벤트 완료 + 전투 시간. 도주: 도주 시간 + 직전 노드로 후퇴(이벤트는 남음).
        /// 전멸: 출정 종료(전원 사망·가방 손실은 BattleState와 마을 쪽에서).
        /// </summary>
        public void FinishBattle()
        {
            if (Phase != ExpeditionPhase.InBattle || battle == null || battle.Phase != BattlePhase.Ended)
                throw new InvalidOperationException("끝난 전투가 없습니다.");
            var c = db.Balance;
            Killed.AddRange(battle.KilledMonsterIds);
            switch (battle.Outcome)
            {
                case BattleOutcome.Victory:
                    Map.CompleteEvent(Map.Current.Id, battleEventIndex);
                    int minutes = battleEvent.kind == DataKeys.NodeEvent.Boss ? c.bossBattleMinutes : c.battleMinutes;
                    Time.AddMinutes(minutes);
                    log.Add($"{Time} 승리 ({minutes}분)");
                    Phase = ExpeditionPhase.Exploring;
                    break;
                case BattleOutcome.Fled:
                    Time.AddMinutes(c.fleeMinutes);
                    var back = Map.Retreat();
                    log.Add($"{Time} 도주 — {NodeLabel(back)}(으)로 후퇴 ({c.fleeMinutes}분)");
                    Phase = ExpeditionPhase.Exploring;
                    break;
                default:
                    log.Add($"{Time} 전멸");
                    Phase = ExpeditionPhase.Ended;
                    End = ExpeditionEnd.Wiped;
                    break;
            }
            foreach (var e in battle.Log.Where(e => e.Kind == BattleEventKind.KnockedDown)) log.Add("  " + e.Message);
            battle = null; battleEvent = null;
        }

        /// <summary>채집 (약초밭 등): 아이템을 가방에 넣고 이벤트 완료. 가방 칸 제한은 M2-3.</summary>
        public ItemAmountDefinition Gather(int index)
        {
            var ev = RequireEvent(index, DataKeys.NodeEvent.Gather);
            Bag.Add(ev.item, ev.amount);
            Map.CompleteEvent(Map.Current.Id, index);
            log.Add($"{Time} 채집 — {db.GetItem(ev.item).name} {ev.amount}개");
            return new ItemAmountDefinition { item = ev.item, amount = ev.amount };
        }

        /// <summary>
        /// 조사: 보급품(예: 열쇠)이 있고 쓰기로 하면 소모하고 좋음 확정. 노드에 고정 결과가 있으면 그것.
        /// 아니면 좋음·보통·나쁨 확률로 굴린다. (나쁨의 전투·피해 효과는 M2-4, 지금은 문구와 보상만.)
        /// </summary>
        public InvestigationOutcome Investigate(int index, bool useSupply)
        {
            var ev = RequireEvent(index, DataKeys.NodeEvent.Investigate);
            var obj = db.GetInvestigation(ev.objectId);
            var outcome = new InvestigationOutcome();
            if (useSupply && !string.IsNullOrEmpty(obj.supplyItem) && Bag.TryConsume(obj.supplyItem))
            {
                outcome.UsedSupply = true;
                outcome.Grade = "good";
            }
            else if (!string.IsNullOrEmpty(ev.fixedResult)) outcome.Grade = ev.fixedResult;
            else
            {
                double roll = random.NextDouble();
                double g = obj.good?.chance ?? 0, n = obj.normal?.chance ?? 0;
                outcome.Grade = roll < g ? "good" : roll < g + n ? "normal" : "bad";
            }
            var res = outcome.Grade == "good" ? obj.good : outcome.Grade == "normal" ? obj.normal : obj.bad;
            outcome.Text = res?.text ?? "";
            if (res?.rewards != null)
                foreach (var rw in res.rewards)
                {
                    Bag.Add(rw.item, rw.amount);
                    outcome.Gained.Add($"{db.GetItem(rw.item).name} {rw.amount}");
                }
            Map.CompleteEvent(Map.Current.Id, index);
            log.Add($"{Time} 조사 — {obj.name}{(outcome.UsedSupply ? $" ({db.GetItem(obj.supplyItem).name} 사용)" : "")}: {outcome.Text}" +
                    (outcome.Gained.Count > 0 ? " · " + string.Join(", ", outcome.Gained) : ""));
            return outcome;
        }

        private NodeEventDefinition RequireEvent(int index, string kind)
        {
            if (Phase != ExpeditionPhase.Exploring || HasForcedBattle) throw new InvalidOperationException("지금은 할 수 없습니다.");
            var node = Map.Current;
            if (index < 0 || index >= node.Events.Count || node.IsEventDone(index) || node.Events[index].kind != kind)
                throw new InvalidOperationException("그 이벤트를 할 수 없습니다.");
            return node.Events[index];
        }

        // ── 귀환 ─────────────────────────────

        /// <summary>수동 귀환: 시작 노드에서만.</summary>
        public bool CanReturn => Phase == ExpeditionPhase.Exploring && Map.Current == Map.Start;

        public void ReturnToTown()
        {
            if (!CanReturn) throw new InvalidOperationException("시작 노드에서만 귀환할 수 있습니다.");
            Phase = ExpeditionPhase.Ended;
            End = ExpeditionEnd.Returned;
            log.Add($"{Time} 마을로 귀환");
        }

        // ── 표시용 ─────────────────────────────

        public static string NodeLabel(MapNodeState n) =>
            string.IsNullOrEmpty(n.Definition.name) ? $"{n.Id}번 노드" : n.Definition.name;

        /// <summary>이벤트 한 줄 설명 (전투 · 약초 3개 · 조사: 뒤집힌 수레).</summary>
        public string DescribeEvent(NodeEventDefinition ev)
        {
            switch (ev.kind)
            {
                case DataKeys.NodeEvent.Battle: return "전투";
                case DataKeys.NodeEvent.Boss: return "보스 전투";
                case DataKeys.NodeEvent.Gather: return $"채집: {db.GetItem(ev.item).name} {ev.amount}개";
                case DataKeys.NodeEvent.Investigate: return $"조사: {db.GetInvestigation(ev.objectId).name}";
                case DataKeys.NodeEvent.Treasure: return "보물";
                default: return ev.kind;
            }
        }

        public string DescribeEvents(MapNodeState n) =>
            n.HasEvents ? string.Join(" + ", n.Events.Select(DescribeEvent)) : "빈 곳";
    }
}
