using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Characters;
using Grimward.Domain.Combat;
using Grimward.Domain.Config;
using Grimward.Domain.Core;
using Grimward.Domain.Exploration;
using Grimward.Domain.Map;
using NUnit.Framework;
namespace Grimward.Tests
{
    public sealed class CombatTests
    {
        [Test]
        public void SwordsmanAndGoblinStatsMatchSpecification()
        {
            var c = new BalanceConfig();
            var a = new Adventurer(c);
            var p = a.Fighter;
            Assert.That(new[] {p.MaxHp,p.AttackMin,p.AttackMax,p.Accuracy,p.Evasion,p.Speed},
                Is.EqualTo(new[] {32,6,10,8,10,6}));
            Assert.That(p.CritChance, Is.EqualTo(.045).Within(1e-12));
            Assert.That(StatFormulas.Accuracy(a.Stats,2,c), Is.EqualTo(10));
            Assert.That(new EnemyFactory(c,new TestRandom()).CreateGoblins().First().Speed, Is.EqualTo(4));
        }
        [TestCase(false,.83,.85)]
        [TestCase(true,.73,.95)]
        public void HitChanceAppliesNightAtBattleStart(bool night, double playerChance, double enemyChance)
        {
            var c = new BalanceConfig();
            var b = TestWorld.Battle(c,night:night);
            Assert.That(CombatFormulas.HitChance(b.Player,b.Enemies[0],night,c), Is.EqualTo(playerChance).Within(1e-12));
            Assert.That(CombatFormulas.HitChance(b.Enemies[0],b.Player,night,c), Is.EqualTo(enemyChance).Within(1e-12));
            Assert.That(b.IsNight, Is.EqualTo(night));
        }
        [TestCase(-1000,.05)]
        [TestCase(1000,.95)]
        public void HitChanceClamps(int accuracy, double expected)
        {
            var c = new BalanceConfig { GoblinAccuracy = accuracy };
            var b = TestWorld.Battle(c);
            Assert.That(CombatFormulas.HitChance(b.Enemies[0],b.Player,false,c), Is.EqualTo(expected));
        }
        [TestCase(3,false,false,2)]
        [TestCase(10,false,true,6)]
        [TestCase(3,true,false,4)]
        [TestCase(3,true,true,2)]
        [TestCase(0,false,false,0)]
        [TestCase(1,false,true,0)]
        public void DamageCriticalThenReductionThenFloor(int roll, bool critical, bool defend, int expected)
        {
            var c = new BalanceConfig();
            var p = new Adventurer(c).Fighter;
            p.IsDefending = defend;
            Assert.That(CombatFormulas.Damage(roll,critical,p,c), Is.EqualTo(expected));
        }
        [Test]
        public void DefendProtectsEnemyTurns_AndExpiresAtNextPlayerTurn()
        {
            var c = new BalanceConfig { GoblinAttackMin = 10, GoblinAttackMax = 10 };
            var battle = TestWorld.Battle(c);
            Assert.That(battle.TryAct(BattleAction.Defend), Is.True);
            Assert.That(battle.Player.Hp, Is.EqualTo(20));
            Assert.That(battle.Round, Is.EqualTo(2));
            Assert.That(battle.IsPlayerTurn, Is.True);
            Assert.That(battle.Player.IsDefending, Is.False);
            Assert.That(battle.Logs.Count(l => l.Contains("방어 중") && l.Contains("피해 6")), Is.EqualTo(2));
        }
        [Test]
        public void DefendPersistsAcrossRoundBoundaryUntilPlayersActualTurn()
        {
            var c = new BalanceConfig { GoblinGroupMin=1,GoblinGroupMax=1,GoblinAttackMin=10,GoblinAttackMax=10 };
            var r = new TestRandom();
            // 무리 수, 첫 라운드 검사/적, 적 피해, 다음 라운드 검사/적, 적 피해.
            foreach(int n in new[]{1,8,1,10,1,8,10}) r.Ints.Enqueue(n);
            var b = TestWorld.Battle(c,r);
            b.TryAct(BattleAction.Defend);
            Assert.That(b.Player.Hp, Is.EqualTo(20));
            Assert.That(b.Logs.Count(l=>l.Contains("방어 중")),Is.EqualTo(2));
            Assert.That(b.Player.IsDefending,Is.False);
        }
        [Test]
        public void TurnOrderSortsPriorityThenSpeed_AndSkipsDead()
        {
            var c = new BalanceConfig();
            var p = new Adventurer(c).Fighter;
            var e = new EnemyFactory(c,new TestRandom()).CreateGoblins();
            e[1].TakeDamage(e[1].MaxHp);
            var r = new TestRandom();
            r.Ints.Enqueue(1); r.Ints.Enqueue(3);
            var order = TurnOrder.Roll(new[]{p}.Concat(e),r,c);
            Assert.That(order.Select(x=>x.Initiative),Is.EqualTo(new[]{7,7}));
            Assert.That(order.Select(x=>x.Fighter),Is.EqualTo(new[]{p,e[0]}));
        }
        [Test]
        public void CompleteInitiativeTiesUseInjectedRandom()
        {
            var c = new BalanceConfig();
            var e = new EnemyFactory(c,new TestRandom()).CreateGoblins();
            var r = new TestRandom();
            r.Ints.Enqueue(1); r.Ints.Enqueue(1); r.Ints.Enqueue(0);
            Assert.That(TurnOrder.Roll(e,r,c).Select(x=>x.Fighter.Id),Is.EqualTo(new[]{1,0}));
        }
        [Test]
        public void EnemyKilledBeforeItsTurnDoesNotAttack()
        {
            var c = TestWorld.StrongPlayer();
            c.PlayerArmor = .05;
            var b = TestWorld.Battle(c);
            int target = b.Enemies[0].Id;
            b.TryAct(BattleAction.Attack,target);
            Assert.That(b.Enemies.First(e=>e.Id==target).IsAlive,Is.False);
            Assert.That(b.Logs.Count(l=>l.StartsWith("고블린") && l.Contains("→ 검사")),Is.EqualTo(1));
        }
        [TestCase(32,26)]
        [TestCase(3,1)]
        [TestCase(1,1)]
        public void FleeDamageNeverKills(int hp, int expected)
        {
            var b = TestWorld.Battle();
            b.Player.TakeDamage(b.Player.Hp-hp);
            Assert.That(b.TryAct(BattleAction.Flee),Is.True);
            Assert.That(b.Player.Hp,Is.EqualTo(expected));
            Assert.That(b.Result.Outcome,Is.EqualTo(BattleOutcome.Fled));
            Assert.That(b.Result.Drops,Is.Empty);
            Assert.That(b.TryAct(BattleAction.Attack,0),Is.False);
        }
        [TestCase(DamageRounding.Floor,6)]
        [TestCase(DamageRounding.Ceiling,7)]
        [TestCase(DamageRounding.Nearest,6)]
        public void FleeRoundingIsConfigurable(DamageRounding mode,int expected)
        {
            var c = new BalanceConfig { FleeDamageRounding=mode };
            Assert.That(CombatFormulas.FleeDamage(new Adventurer(c).Fighter,c),Is.EqualTo(expected));
        }
        [Test]
        public void KillsRemainOnFleeWithoutDrops()
        {
            var b = TestWorld.Battle(TestWorld.StrongPlayer());
            b.TryAct(BattleAction.Attack,0);
            b.TryAct(BattleAction.Flee);
            Assert.That(b.Result.KilledGoblins,Is.EqualTo(1));
            Assert.That(b.Result.Drops,Is.Empty);
        }
        [Test]
        public void NodeVictoryCompletesEvent_AddsTime_AndEnablesReturn()
        {
            var s=TestWorld.Create(TestWorld.StrongPlayer());
            s.TryMove(1); s.TryMove(3);
            Assert.That(s.Battle.Kind,Is.EqualTo(BattleKind.Node));
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(480));
            Assert.That(s.TryMove(1),Is.False);
            Assert.That(s.TryAutoReturn(),Is.False);
            TestWorld.WinCurrent(s);
            Assert.That(s.Map.Node(3).IsEventCompleted,Is.True);
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(510));
            Assert.That(s.FindReturnPath(),Is.EqualTo(new[]{3,1,0}));
            s.TryAutoReturn();
            Assert.That(s.IsAtStart,Is.True);
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(570));
            s.TryMove(1); s.TryMove(3);
            Assert.That(s.Battle,Is.Null);
        }
        [Test]
        public void NodeFleeRetreatsAndPreservesSurvivorsWithoutDoubleCountingKills()
        {
            var c=TestWorld.StrongPlayer();
            var s=TestWorld.Create(c);
            s.TryMove(1); s.TryMove(3);
            var enemies=s.Battle.Enemies;
            s.TryBattleAction(BattleAction.Attack,enemies[0].Id);
            enemies[1].TakeDamage(3);
            s.TryBattleAction(BattleAction.Flee);
            Assert.That(s.LastBattleResult.KilledGoblins,Is.EqualTo(1));
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(540));
            Assert.That(s.CurrentNodeId,Is.EqualTo(1));
            Assert.That(s.Map.Node(3).IsEventCompleted,Is.False);
            int hp=s.Player.Fighter.Hp;
            s.TryMove(3);
            Assert.That(s.Player.Fighter.Hp,Is.EqualTo(hp));
            Assert.That(s.Battle.Enemies,Is.SameAs(enemies));
            Assert.That(s.Battle.Enemies[1].Hp,Is.EqualTo(7));
            TestWorld.WinCurrent(s);
            Assert.That(s.LastBattleResult.KilledGoblins,Is.EqualTo(1));
        }
        [TestCase(true,540)]
        [TestCase(false,570)]
        public void FleeTimeOptionControlsBattleTimeAddition(bool replace,int minute)
        {
            var c=TestWorld.NoAmbush(); c.FleeTimeReplacesBattleTime=replace;
            var s=TestWorld.Create(c);
            s.TryMove(1); s.TryMove(3); s.TryBattleAction(BattleAction.Flee);
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(minute));
        }
        [TestCase(true,450)]
        [TestCase(false,390)]
        public void AmbushVictoryArrivesAndChargesConfiguredTravel(bool charge,int minute)
        {
            var c=TestWorld.StrongPlayer();
            c.PathEventChanceDayFirst=1; c.ChargeTravelOnAmbushVictory=charge;
            var s=TestWorld.Create(c);
            s.TryMove(1);
            Assert.That(s.CurrentNodeId,Is.EqualTo(0));
            Assert.That(s.Map.Node(1).IsVisited,Is.False);
            Assert.That(s.Map.Edge(0).IsTraversed,Is.False);
            TestWorld.WinCurrent(s);
            Assert.That(s.CurrentNodeId,Is.EqualTo(1));
            Assert.That(s.Map.Edge(0).IsTraversed,Is.True);
            Assert.That(s.Map.Node(1).IsEventCompleted,Is.True);
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(minute));
        }
        [TestCase(true,480)]
        [TestCase(false,420)]
        public void AmbushFleeDoesNotVisitOrMarkNewEdge(bool charge,int minute)
        {
            var c=TestWorld.NoAmbush();
            c.PathEventChanceDayFirst=1; c.ChargeTravelOnAmbushFlee=charge;
            var s=TestWorld.Create(c);
            s.TryMove(1); s.TryBattleAction(BattleAction.Flee);
            Assert.That(s.CurrentNodeId,Is.EqualTo(0));
            Assert.That(s.Map.Edge(0).IsTraversed,Is.False);
            Assert.That(s.Map.Node(1).IsVisited,Is.False);
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(minute));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void AutoReturnPausesForAmbush_ResumesOnVictory_CancelsOnFlee(bool flee)
        {
            var c=TestWorld.StrongPlayer();
            var s=TestWorld.Create(c);
            s.TryMove(1); s.TryMove(2);
            c.PathEventChanceDayRevisit=1;
            Assert.That(s.TryAutoReturn(),Is.True);
            Assert.That(s.IsReturning,Is.True);
            Assert.That(s.CurrentNodeId,Is.EqualTo(2));
            Assert.That(s.TryMove(1),Is.False);
            Assert.That(s.TryAutoReturn(),Is.False);
            if(flee)
            {
                s.TryBattleAction(BattleAction.Flee);
                Assert.That(s.CurrentNodeId,Is.EqualTo(2));
                Assert.That(s.IsReturning,Is.False);
                Assert.That(s.Map.Connection(1,2).IsTraversed,Is.True);
                Assert.That(s.Time.MinuteOfDay,Is.EqualTo(570));
            }
            else
            {
                c.PathEventChanceDayRevisit=0;
                TestWorld.WinCurrent(s);
                Assert.That(s.IsAtStart,Is.True);
                Assert.That(s.IsReturning,Is.False);
                Assert.That(s.Time.MinuteOfDay,Is.EqualTo(570));
            }
        }
        [Test]
        public void AmbushVictoryCanImmediatelyStartNodeBattle_AtArrivalNight()
        {
            var c=TestWorld.StrongPlayer();
            c.GameStartMinute=1100;
            var s=TestWorld.Create(c);
            s.TryMove(1);
            c.PathEventChanceDayFirst=1;
            s.TryMove(3);
            Assert.That(s.Battle.IsNight,Is.False);
            TestWorld.WinCurrent(s);
            Assert.That(s.Battle.Kind,Is.EqualTo(BattleKind.Node));
            Assert.That(s.Battle.IsNight,Is.True);
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(1250));
            TestWorld.WinCurrent(s);
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(1280));
            Assert.That(s.Map.Node(3).IsEventCompleted,Is.True);
        }
        [Test]
        public void DeathStopsExplorationAndAllBattleActions()
        {
            var c=TestWorld.NoAmbush(); c.GoblinAttackMin=c.GoblinAttackMax=100;
            var s=TestWorld.Create(c);
            s.TryMove(1); s.TryMove(3);
            s.TryBattleAction(BattleAction.Defend);
            Assert.That(s.IsDead,Is.True);
            Assert.That(s.Player.Fighter.Hp,Is.Zero);
            Assert.That(s.LastBattleResult.Outcome,Is.EqualTo(BattleOutcome.Death));
            Assert.That(s.TryMove(1),Is.False);
            Assert.That(s.TryAutoReturn(),Is.False);
            Assert.That(s.TryManualReturn(),Is.False);
            Assert.That(s.TryBattleAction(BattleAction.Flee),Is.False);
        }
        [Test]
        public void AmbushDeathDoesNotCompleteTravel_AndCancelsReturn()
        {
            var c=TestWorld.NoAmbush();
            var s=TestWorld.Create(c);
            s.TryMove(1);
            c.PathEventChanceDayRevisit=1;
            c.GoblinAttackMin=c.GoblinAttackMax=100;
            s.TryAutoReturn();
            s.TryBattleAction(BattleAction.Defend);
            Assert.That(s.IsDead,Is.True);
            Assert.That(s.IsReturning,Is.False);
            Assert.That(s.CurrentNodeId,Is.EqualTo(1));
            Assert.That(s.Time.MinuteOfDay,Is.EqualTo(480));
        }
        [TestCase(.19,true)]
        [TestCase(.20,false)]
        public void VictoryDropsUseInclusiveCountsAndExclusiveChance(double roll,bool mid)
        {
            var c=TestWorld.StrongPlayer();
            var r=new TestRandom { Value=roll,UseMaximum=true };
            var b=TestWorld.Battle(c,r);
            TestWorld.Win(b);
            Assert.That(b.Result.Drops.First().Count,Is.EqualTo(2));
            Assert.That(b.Result.Drops.Any(d=>d.Item==DropItem.MidStone),Is.EqualTo(mid));
        }
        [Test]
        public void SameSeedAndActionsReproduceLogsHpAndDrops()
        {
            string Run()
            {
                var c=new BalanceConfig();
                var r=new SeededRandom(12345);
                var b=new BattleState(new Adventurer(c).Fighter,new EnemyFactory(c,r).CreateGoblins(),BattleKind.Node,true,c,r);
                b.Start();
                for(int i=0;b.Result==null && i<1000;i++)
                    b.TryAct(BattleAction.Attack,b.Enemies.First(e=>e.IsAlive).Id);
                Assert.That(b.Result,Is.Not.Null);
                return string.Join("\n",b.Logs)+$"|{b.Player.Hp}|{b.Result.Outcome}|"+string.Join(",",b.Result.Drops);
            }
            Assert.That(Run(),Is.EqualTo(Run()));
        }
        [Test]
        public void InvalidTargetDoesNotConsumeTurnOrRandom()
        {
            var r=new TestRandom();
            var b=TestWorld.Battle(random:r);
            int calls=r.DoubleCalls+r.IntCalls;
            Assert.That(b.TryAct(BattleAction.Attack,99),Is.False);
            Assert.That(b.Round,Is.EqualTo(1));
            Assert.That(b.IsPlayerTurn,Is.True);
            Assert.That(r.DoubleCalls+r.IntCalls,Is.EqualTo(calls));
        }
        [Test]
        public void FogOnlyDiscovers_AndRandomIntegerBoundsAreInclusive()
        {
            var map=new FixedMapProvider().CreateMap();
            new FogOfWar().RevealFrom(map,0);
            Assert.That(map.Node(0).IsVisited,Is.False);
            var r=new SeededRandom(123);
            var seen=new HashSet<int>();
            for(int i=0;i<1000;i++) seen.Add(r.NextInt(1,8));
            Assert.That(seen,Is.EquivalentTo(Enumerable.Range(1,8)));
            Assert.That(r.NextInt(int.MaxValue,int.MaxValue),Is.EqualTo(int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(()=>r.NextInt(2,1));
        }
        [Test]
        public void ConfigRejectsInvalidCombatRanges_AndDomainHasNoEngineReferences()
        {
            Assert.Throws<ArgumentException>(()=>new BalanceConfig { InitiativeDieSides=0 }.Validate());
            Assert.Throws<ArgumentException>(()=>new BalanceConfig { AttackStrDivisor=0 }.Validate());
            Assert.Throws<ArgumentException>(()=>new BalanceConfig { GoblinGroupMin=4 }.Validate());
            Assert.That(typeof(BattleState).Assembly.GetReferencedAssemblies().Any(a=>a.Name.StartsWith("Unity")),Is.False);
        }
    }
}
