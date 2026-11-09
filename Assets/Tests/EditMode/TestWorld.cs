using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Characters;
using Grimward.Domain.Combat;
using Grimward.Domain.Config;
using Grimward.Domain.Core;
using Grimward.Domain.Exploration;
using Grimward.Domain.Map;
namespace Grimward.Tests
{
    internal sealed class TestRandom : IRandomSource
    {
        public readonly Queue<int> Ints = new Queue<int>();
        public readonly Queue<double> Doubles = new Queue<double>();
        public double Value = .5;
        public bool UseMaximum;
        public int DoubleCalls;
        public int IntCalls;
        public double NextDouble()
        {
            DoubleCalls++;
            return Doubles.Count > 0 ? Doubles.Dequeue() : Value;
        }
        public int NextInt(int minInclusive, int maxInclusive)
        {
            IntCalls++;
            int value = Ints.Count > 0 ? Ints.Dequeue() : UseMaximum ? maxInclusive : minInclusive;
            if (value < minInclusive || value > maxInclusive) throw new InvalidOperationException($"Fake random {value} outside {minInclusive}..{maxInclusive}");
            return value;
        }
    }
    internal static class TestWorld
    {
        public static BalanceConfig NoAmbush()
        {
            return new BalanceConfig { PathEventChanceDayFirst = 0, PathEventChanceDayRevisit = 0,
                PathEventChanceNightFirst = 0, PathEventChanceNightRevisit = 0 };
        }
        public static ExplorationState Create(BalanceConfig c = null, IRandomSource random = null, MapData map = null)
        {
            c = c ?? NoAmbush();
            random = random ?? new TestRandom();
            return new ExplorationState(map ?? new FixedMapProvider().CreateMap(), c, new TravelTimeCalculator(c),
                new FogOfWar(), new PathEventResolver(c,random), new ReturnPathFinder(c),
                new Adventurer(c), new BattleFactory(c,random,new EnemyFactory(c,random)));
        }
        public static MapData MovementMap()
        {
            var source = new FixedMapProvider().CreateMap();
            // M1 이동/안개 단위 검증용 데이터: 전투 처리와 독립적인 미완료 보물 노드.
            return new MapData(source.Radius, source.Nodes.Select(n => new NodeData(n.Id,n.Coord,
                n.EventType == NodeEventType.Battle ? NodeEventType.Treasure : n.EventType)),
                source.Edges.Select(e => new EdgeData(e.Id,e.FromNodeId,e.ToNodeId)), source.StartNodeId);
        }
        public static void Win(BattleState battle)
        {
            for (int i = 0; battle.Result == null && i < 1000; i++)
                battle.TryAct(BattleAction.Attack,battle.Enemies.First(e => e.IsAlive).Id);
            if (battle.Result == null || battle.Result.Outcome != BattleOutcome.Victory)
                throw new InvalidOperationException("Test battle did not finish with victory.");
        }
        public static void WinCurrent(ExplorationState state)
        {
            var initial = state.Battle;
            for (int i = 0; state.Battle == initial && state.Battle != null && i < 1000; i++)
                state.TryBattleAction(BattleAction.Attack,state.Battle.Enemies.First(e => e.IsAlive).Id);
            if (state.Battle == initial || state.IsDead) throw new InvalidOperationException("Test battle did not finish.");
        }
        public static BalanceConfig StrongPlayer()
        {
            var c = NoAmbush();
            c.PlayerWeaponMin = c.PlayerWeaponMax = 100;
            c.PlayerArmor = 1;
            return c;
        }
        public static BattleState Battle(BalanceConfig c = null, TestRandom random = null, bool night = false)
        {
            c = c ?? new BalanceConfig();
            random = random ?? new TestRandom();
            var b = new BattleState(new Adventurer(c).Fighter, new EnemyFactory(c,random).CreateGoblins(),
                BattleKind.Node,night,c,random);
            b.Start();
            return b;
        }
    }
}
