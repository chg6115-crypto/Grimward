using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Config;
using Grimward.Domain.Core;
using Grimward.Domain.Map;
using Grimward.Domain.Exploration;
using NUnit.Framework;

namespace Grimward.Tests
{
    public sealed class DomainTests
    {
        private sealed class ConstantRandom : IRandomSource
        {
            private readonly double value;
            public int Calls { get; private set; }
            public ConstantRandom(double value) { this.value = value; }
            public double NextDouble() { Calls++; return value; }
        }

        private static ExplorationState Create(MapData map = null, BalanceConfig config = null, IRandomSource random = null)
        {
            config = config ?? new BalanceConfig();
            return new ExplorationState(map ?? new FixedMapProvider().CreateMap(), config,
                new TravelTimeCalculator(config), new FogOfWar(),
                new PathEventResolver(config, random ?? new ConstantRandom(.99)), new ReturnPathFinder(config));
        }

        [Test]
        public void HexCoordinates_HaveSixUniqueAdjacentNeighbors()
        {
            var center = new HexCoord(1, -1);
            var neighbors = center.Neighbors().ToArray();
            Assert.That(neighbors.Distinct().Count(), Is.EqualTo(6));
            Assert.That(neighbors.All(n => n.DistanceTo(center) == 1), Is.True);
            Assert.That(new HexCoord(-2, 1).DistanceFromOrigin, Is.EqualTo(2));
            Assert.That(new HexCoord(-2, 0).DistanceTo(new HexCoord(2, 0)), Is.EqualTo(4));
            Assert.That(HexCoord.WithinRadius(2).Distinct().Count(), Is.EqualTo(19));
            Assert.That(HexCoord.WithinRadius(0).Count(), Is.EqualTo(1));
        }

        [Test]
        public void FixedMap_MeetsEveryConstraint_AndReturnsFreshState()
        {
            var map = new FixedMapProvider().CreateMap();
            Assert.That(new MapValidator(new BalanceConfig()).Validate(map), Is.Empty);
            Assert.That(map.Nodes.Count, Is.EqualTo(11));
            Assert.That(map.Edges.Count - map.Nodes.Count + 1, Is.EqualTo(2));
            Create(map).TryMove(1);
            Assert.That(new FixedMapProvider().CreateMap().Nodes.All(n => !n.IsVisited), Is.True);
        }

        [Test]
        public void Validator_RejectsBadCounts_DisconnectedAndNonAdjacentEdges()
        {
            var config = new BalanceConfig { ActiveNodeCount = 12 };
            Assert.That(new MapValidator(config).Validate(new FixedMapProvider().CreateMap()), Is.Not.Empty);
            var source = new FixedMapProvider().CreateMap();
            var nodes = source.Nodes.Select(n => new NodeData(n.Id, n.Coord, n.EventType));
            var edges = source.Edges.Where(e => e.Id != 0).Select(e => new EdgeData(e.Id, e.FromNodeId, e.ToNodeId)).ToList();
            var disconnected = new MapData(2, nodes, edges, 0);
            Assert.That(new MapValidator(new BalanceConfig()).Validate(disconnected), Does.Contain("연결되지 않은 노드"));
            var bad = new MapData(2, new[] {
                new NodeData(0, new HexCoord(-2,0), NodeEventType.Start),
                new NodeData(1, new HexCoord(2,0), NodeEventType.Empty)
            }, new[] { new EdgeData(0,0,1) }, 0);
            Assert.That(new MapValidator(new BalanceConfig()).Validate(bad), Does.Contain("인접하지 않은 경로"));
        }

        [Test]
        public void Validator_RejectsDuplicateCoordinates_MissingEndpoint_AndBadStart()
        {
            var map = new MapData(2, new[] {
                new NodeData(0, new HexCoord(0,0), NodeEventType.Start),
                new NodeData(1, new HexCoord(0,0), NodeEventType.Empty)
            }, new[] { new EdgeData(0,0,99) }, 0);
            var errors = new MapValidator(new BalanceConfig()).Validate(map);
            Assert.That(errors, Does.Contain("중복 좌표"));
            Assert.That(errors, Does.Contain("경로 끝 노드 없음"));
            Assert.That(errors, Does.Contain("시작 노드 가장자리 조건 위반"));
        }

        [Test]
        public void Validator_RejectsTooManyCycles_AndExcessDistance()
        {
            var map = new FixedMapProvider().CreateMap();
            var config = new BalanceConfig { MaxCycleCount = 1, MaxPathDistance = 3 };
            var errors = new MapValidator(config).Validate(map);
            Assert.That(errors, Does.Contain("순환 경로 수 위반"));
            Assert.That(errors, Does.Contain("최대 경로 거리 초과"));
        }

        [Test]
        public void Movement_UsesEdgeHistory_NotDestinationVisit()
        {
            var state = Create();
            var calculator = new TravelTimeCalculator(new BalanceConfig());
            Assert.That(calculator.CalculateMinutes(state.Map.Edge(0)), Is.EqualTo(60));
            Assert.That(state.TryMove(1), Is.True);
            Assert.That(state.Map.Edge(0).IsTraversed, Is.True);
            Assert.That(state.Map.Node(1).IsVisited, Is.True);
            Assert.That(state.Map.Node(1).IsEventCompleted, Is.True);
            Assert.That(calculator.CalculateMinutes(state.Map.Edge(0)), Is.EqualTo(30));
            state.TryMove(2);
            state.TryMove(3);
            Assert.That(state.Map.Node(1).IsVisited, Is.True);
            Assert.That(calculator.CalculateMinutes(state.Map.Connection(3,1)), Is.EqualTo(60));
            state.TryMove(1);
            Assert.That(state.Time.MinuteOfDay, Is.EqualTo(600));
            state.TryMove(0);
            Assert.That(state.Time.MinuteOfDay, Is.EqualTo(630));
        }

        [Test]
        public void InvalidMove_DoesNotMutateOrConsumeRandom()
        {
            var random = new ConstantRandom(0);
            var state = Create(random: random);
            Assert.That(state.TryMove(10), Is.False);
            Assert.That(state.TryMove(999), Is.False);
            Assert.That(state.TryMove(0), Is.False);
            Assert.That(state.CurrentNodeId, Is.EqualTo(0));
            Assert.That(state.Time.MinuteOfDay, Is.EqualTo(360));
            Assert.That(random.Calls, Is.Zero);
            Assert.That(state.Map.Edges.Any(e => e.IsTraversed), Is.False);
            Assert.That(state.TryManualReturn(), Is.True);
            state.TryMove(1);
            Assert.That(state.TryManualReturn(), Is.False);
        }

        [Test]
        public void BattleAndTreasure_RemainIncomplete_AndCanBeLeft()
        {
            var state = Create();
            var logs = new List<string>(); state.Logged += logs.Add;
            state.TryMove(1); state.TryMove(3); state.TryMove(5);
            Assert.That(state.Map.Node(3).IsVisited, Is.True);
            Assert.That(state.Map.Node(3).IsEventCompleted, Is.False);
            Assert.That(state.Map.Node(5).IsVisited, Is.True);
            Assert.That(state.Map.Node(5).IsEventCompleted, Is.False);
            Assert.That(logs, Does.Contain("전투 발생 — M2에서 구현"));
            Assert.That(logs, Does.Contain("보물 발견 — M3에서 구현"));
            Assert.That(state.TryMove(3), Is.True);
        }

        [Test]
        public void Fog_RevealsOnlyConnectedNodesAndEdges()
        {
            var state = Create();
            Assert.That(state.Map.Nodes.Where(n => n.IsDiscovered).Select(n => n.Id), Is.EquivalentTo(new[] {0,1}));
            Assert.That(state.Map.Node(0).IsVisited && state.Map.Node(0).IsEventCompleted, Is.True);
            Assert.That(state.Map.Node(1).IsVisited, Is.False);
            Assert.That(state.Map.Edges.Where(e => e.IsDiscovered).Select(e => e.Id), Is.EquivalentTo(new[] {0}));
            state.TryMove(1);
            Assert.That(state.Map.Nodes.Where(n => n.IsDiscovered).Select(n => n.Id), Is.EquivalentTo(new[] {0,1,2,3,4}));
            Assert.That(state.Map.Edges.Where(e => e.IsDiscovered).Select(e => e.Id), Is.EquivalentTo(new[] {0,1,2,3}));
            Assert.That(state.Map.Node(3).IsVisited, Is.False);
            Assert.That(state.Map.Node(3).IsEventCompleted, Is.False);
        }

        [TestCase(1199, true)]
        [TestCase(1200, false)]
        [TestCase(359, false)]
        [TestCase(360, true)]
        public void Time_DayNightBoundaries(int minute, bool expected) =>
            Assert.That(new GameTime(1, minute).IsDay(new BalanceConfig()), Is.EqualTo(expected));

        [Test]
        public void Time_RollsAcrossMidnightAndMultipleDays()
        {
            var time = new GameTime(1, 1410);
            time.AddMinutes(60);
            Assert.That(time.Day, Is.EqualTo(2));
            Assert.That(time.MinuteOfDay, Is.EqualTo(30));
            Assert.That(time.ToString(), Is.EqualTo("2일차 00:30"));
            time.AddMinutes(2880);
            Assert.That(time.Day, Is.EqualTo(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => time.AddMinutes(-1));
        }

        [TestCase(true, false, .25)]
        [TestCase(true, true, .05)]
        [TestCase(false, false, .40)]
        [TestCase(false, true, .25)]
        public void Events_ChooseFourProbabilities_AndExclusiveThreshold(bool day, bool traversed, double chance)
        {
            var config = new BalanceConfig();
            var below = new PathEventResolver(config, new ConstantRandom(chance - .001));
            var equal = new PathEventResolver(config, new ConstantRandom(chance));
            Assert.That(below.SelectChance(day,traversed), Is.EqualTo(chance));
            Assert.That(below.Roll(day,traversed), Is.True);
            Assert.That(equal.Roll(day,traversed), Is.False);
        }

        [TestCase(1199, .30, false)]
        [TestCase(359, .30, true)]
        public void Events_UseDepartureTime_WhenCrossingBoundary(int minute, double value, bool expected)
        {
            var random = new ConstantRandom(value);
            var state = Create(config: new BalanceConfig { GameStartMinute = minute }, random: random);
            var logs = new List<string>(); state.Logged += logs.Add;
            state.TryMove(1);
            Assert.That(logs.Contains("매복 발생 — M2에서 구현"), Is.EqualTo(expected));
            Assert.That(random.Calls, Is.EqualTo(1));
            Assert.That(state.CurrentNodeId, Is.EqualTo(1));
            Assert.That(state.Time.MinuteOfDay, Is.EqualTo(minute + 60));
        }

        [Test]
        public void AutoReturn_ExecutesEverySegment_WithRevisitTimeAndRolls()
        {
            var random = new ConstantRandom(0);
            var state = Create(random: random);
            var logs = new List<string>(); state.Logged += logs.Add;
            state.TryMove(1); state.TryMove(2);
            Assert.That(state.FindReturnPath(), Is.EqualTo(new[] {2,1,0}));
            Assert.That(state.TryAutoReturn(), Is.True);
            Assert.That(state.CurrentNodeId, Is.EqualTo(0));
            Assert.That(state.Time.MinuteOfDay, Is.EqualTo(540));
            Assert.That(random.Calls, Is.EqualTo(4));
            Assert.That(logs.Count(s => s == "매복 발생 — M2에서 구현"), Is.EqualTo(4));
            Assert.That(logs.Last(), Is.EqualTo("마을로 귀환 — M4에서 구현"));
            Assert.That(state.TryAutoReturn(), Is.True);
            Assert.That(random.Calls, Is.EqualTo(4));
        }

        [Test]
        public void Return_ExcludesUntraversedShortcut_AndChoosesShortestCompletedPath()
        {
            var state = Create();
            state.TryMove(1); state.TryMove(2); state.TryMove(3);
            state.Map.Node(3).CompleteEvent();
            Assert.That(state.FindReturnPath(), Is.EqualTo(new[] {3,2,1,0}));
            state.TryMove(1); state.TryMove(3);
            Assert.That(state.FindReturnPath(), Is.EqualTo(new[] {3,1,0}));
        }

        [Test]
        public void Return_DetoursAroundIncompleteNodes()
        {
            var map = new MapData(2, new[] {
                new NodeData(0,new HexCoord(-2,0),NodeEventType.Start),
                new NodeData(1,new HexCoord(-1,0),NodeEventType.Battle),
                new NodeData(2,new HexCoord(0,0),NodeEventType.Empty),
                new NodeData(3,new HexCoord(0,-1),NodeEventType.Empty),
                new NodeData(4,new HexCoord(-1,-1),NodeEventType.Empty)
            }, new[] {new EdgeData(0,0,1),new EdgeData(1,1,2),new EdgeData(2,2,3),
                new EdgeData(3,3,4),new EdgeData(4,4,0)},0);
            var state = Create(map);
            foreach (int id in new[] {1,2,3,4,0,1,2}) state.TryMove(id);
            Assert.That(state.FindReturnPath(), Is.EqualTo(new[] {2,3,4,0}));
        }

        [Test]
        public void Return_AllowsLeavingCurrentIncompleteNode_ButFailsWhenIntermediateBlocked()
        {
            var state = Create();
            state.TryMove(1); state.TryMove(3);
            Assert.That(state.FindReturnPath(), Is.EqualTo(new[] {3,1,0}));
            state.TryMove(5);
            Assert.That(state.FindReturnPath(), Is.Null);
            int minute = state.Time.MinuteOfDay;
            Assert.That(state.TryAutoReturn(), Is.False);
            Assert.That(state.Time.MinuteOfDay, Is.EqualTo(minute));
            Assert.That(state.CurrentNodeId, Is.EqualTo(5));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void Return_OptionalTreasureException_DoesNotAllowBattle(bool allow, bool expected)
        {
            var state = Create(config: new BalanceConfig { AllowIncompleteSafeNodesOnReturn = allow });
            state.TryMove(1); state.TryMove(3); state.Map.Node(3).CompleteEvent();
            state.TryMove(5); state.TryMove(6);
            Assert.That(state.FindReturnPath() != null, Is.EqualTo(expected));
            if (allow) Assert.That(state.FindReturnPath(), Is.EqualTo(new[] {6,5,3,1,0}));
            var blocked = Create(config: new BalanceConfig { AllowIncompleteSafeNodesOnReturn = allow });
            blocked.TryMove(1); blocked.TryMove(3); blocked.TryMove(5);
            Assert.That(blocked.FindReturnPath(), Is.Null);
        }

        [Test]
        public void Config_IsUsedByTimeAndMovement_AndRejectsInvalidProbability()
        {
            var config = new BalanceConfig { FirstTraversalMinutes = 7, RevisitTraversalMinutes = 3 };
            var state = Create(config: config);
            state.TryMove(1); state.TryMove(0);
            Assert.That(state.Time.MinuteOfDay, Is.EqualTo(370));
            config.PathEventChanceDayFirst = double.NaN;
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void SeededRandom_RepeatsSequence()
        {
            var first = new SeededRandom(12345); var second = new SeededRandom(12345);
            for (int i = 0; i < 100; i++) Assert.That(first.NextDouble(), Is.EqualTo(second.NextDouble()));
        }
    }
}
