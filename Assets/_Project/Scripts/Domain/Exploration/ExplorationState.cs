using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Characters;
using Grimward.Domain.Combat;
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
        private readonly BattleFactory battles;
        private readonly Queue<int> returnSteps = new Queue<int>();
        private PendingTravel pending;
        private int previousNodeId;
        public MapData Map { get; }
        public GameTime Time { get; }
        public Adventurer Player { get; }
        public BattleState Battle { get; private set; }
        public BattleResult LastBattleResult { get; private set; }
        public int CurrentNodeId { get; private set; }
        public bool IsDay => Time.IsDay(config);
        public bool IsAtStart => CurrentNodeId == Map.StartNodeId;
        public bool IsDead => !Player.Fighter.IsAlive;
        public bool IsReturning { get; private set; }
        public bool CanExplore => !IsDead && Battle == null && !IsReturning;
        public event Action<string> Logged;
        public event Action Changed;
        public event Action<BattleResult> BattleFinished;

        public ExplorationState(MapData map, BalanceConfig config, ITravelTimeCalculator travelTime,
            FogOfWar fog, PathEventResolver pathEvents, ReturnPathFinder returnPaths,
            Adventurer player, BattleFactory battles)
        {
            config.Validate();
            Map = map;
            this.config = config;
            this.travelTime = travelTime;
            this.fog = fog;
            this.pathEvents = pathEvents;
            this.returnPaths = returnPaths;
            this.battles = battles;
            Player = player;
            Time = new GameTime(config.GameStartDay, config.GameStartMinute);
            CurrentNodeId = previousNodeId = map.StartNodeId;
            Visit(CurrentNodeId);
            map.Node(CurrentNodeId).CompleteEvent();
        }
        public bool CanMoveTo(int nodeId) => CanExplore && Map.Connection(CurrentNodeId, nodeId) != null;
        public bool TryMove(int nodeId)
        {
            if (!CanMoveTo(nodeId)) return false;
            Move(nodeId);
            Changed?.Invoke();
            return true;
        }
        private void Move(int nodeId)
        {
            var edge = Map.Connection(CurrentNodeId, nodeId);
            int minutes = travelTime.CalculateMinutes(edge);
            if (minutes <= 0) throw new InvalidOperationException("이동 시간은 양수여야 합니다.");
            pending = new PendingTravel(CurrentNodeId, nodeId, edge, minutes);
            if (pathEvents.Roll(IsDay, edge.IsTraversed))
            {
                Logged?.Invoke("매복 발생");
                BeginBattle(BattleKind.Ambush);
            }
            else
            {
                Time.AddMinutes(minutes);
                Arrive();
            }
        }
        private void Arrive()
        {
            var trip = pending;
            pending = null;
            previousNodeId = trip.From;
            CurrentNodeId = trip.To;
            trip.Edge.IsTraversed = true;
            Visit(CurrentNodeId);
            var node = Map.Node(CurrentNodeId);
            Logged?.Invoke($"{CurrentNodeId:00}번 노드 도착 · {Time}");
            if (node.EventType == NodeEventType.Start || node.EventType == NodeEventType.Empty)
                node.CompleteEvent();
            else if (!node.IsEventCompleted && node.EventType == NodeEventType.Battle)
                BeginBattle(BattleKind.Node);
            else if (!node.IsEventCompleted && node.EventType == NodeEventType.Treasure)
                Logged?.Invoke("보물 발견 — M3에서 구현");
        }
        private void Visit(int id)
        {
            Map.Node(id).IsVisited = true;
            fog.RevealFrom(Map, id);
        }
        private void BeginBattle(BattleKind kind)
        {
            Battle = battles.Create(Player, kind, CurrentNodeId, !IsDay);
            Battle.Start();
            ResolveFinishedBattle();
        }
        public bool TryBattleAction(BattleAction action, int targetId = -1)
        {
            if (Battle == null || IsDead || !Battle.TryAct(action, targetId)) return false;
            ResolveFinishedBattle();
            ContinueReturn();
            Changed?.Invoke();
            return true;
        }
        private void ResolveFinishedBattle()
        {
            if (Battle == null || Battle.Result == null) return;
            var result = Battle.Result;
            Battle = null;
            LastBattleResult = result;
            bool fled = result.Outcome == BattleOutcome.Fled;
            int elapsed = fled ? config.FleeMinutes + (config.FleeTimeReplacesBattleTime ? 0 : config.BattleMinutes)
                : config.BattleMinutes;
            Time.AddMinutes(elapsed);
            Logged?.Invoke($"전투 결과: {result.Outcome} · 처치 {result.KilledGoblins} · +{elapsed}분");
            foreach (var drop in result.Drops) Logged?.Invoke($"드랍: {drop} — 가방 획득은 M3에서 구현");
            BattleFinished?.Invoke(result);

            if (result.Kind == BattleKind.Ambush)
            {
                bool chargeTravel = result.Outcome == BattleOutcome.Victory ? config.ChargeTravelOnAmbushVictory :
                    fled ? config.ChargeTravelOnAmbushFlee : config.ChargeTravelOnAmbushDeath;
                if (chargeTravel) Time.AddMinutes(pending.Minutes);
                if (result.Outcome == BattleOutcome.Victory) Arrive();
                else pending = null; // 기존에 통과한 경로의 기록은 지우지 않는다.
            }
            else if (result.Outcome == BattleOutcome.Victory)
                Map.Node(CurrentNodeId).CompleteEvent();
            else if (fled)
                CurrentNodeId = previousNodeId;

            if (fled || result.Outcome == BattleOutcome.Death)
            {
                IsReturning = false;
                returnSteps.Clear();
                if (fled) Logged?.Invoke("출발 노드로 후퇴 · 자동 귀환 취소");
                if (IsDead) Logged?.Invoke("사망 — M4에서 처리");
            }
        }
        public IReadOnlyList<int> FindReturnPath() =>
            IsDead || Battle != null ? null : returnPaths.Find(Map, CurrentNodeId);
        public bool TryAutoReturn()
        {
            if (!CanExplore) return false;
            var path = FindReturnPath();
            if (path == null)
            {
                Logged?.Invoke("자동 귀환 불가: 통과한 경로와 완료 노드로 이어지는 길이 없습니다.");
                return false;
            }
            foreach (int id in path.Skip(1)) returnSteps.Enqueue(id);
            IsReturning = true;
            ContinueReturn();
            Changed?.Invoke();
            return true;
        }
        private void ContinueReturn()
        {
            while (IsReturning && Battle == null && !IsDead)
            {
                if (returnSteps.Count == 0)
                {
                    IsReturning = false;
                    TryManualReturn();
                    return;
                }
                Move(returnSteps.Dequeue());
            }
        }
        public bool TryManualReturn()
        {
            if (!CanExplore || !IsAtStart) return false;
            Logged?.Invoke("마을로 귀환 — M4에서 구현");
            return true;
        }
    }
}
