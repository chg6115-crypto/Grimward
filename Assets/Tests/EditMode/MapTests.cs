using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Map;
using Grimward.Domain.Units;
using NUnit.Framework;

namespace Grimward.Tests
{
    /// <summary>탐험: 맵 검증 · 이동 · 시간 · 시야·지각 탐지 · 완료 · 이벤트(전투·채집·조사) · 귀환 (기획안 5장).</summary>
    public class MapTests
    {
        private GameDatabase db;

        [SetUp]
        public void SetUp() => db = TestData.Db();

        private Expedition NewExpedition(FakeRandom rng = null, int perception = 0, BattleInventory bag = null)
        {
            var a = TestData.Hero(db, "swordsman"); a.Perception = perception;
            var b = TestData.Hero(db, "mage", "m"); b.Perception = perception;
            return new Expedition(db, rng ?? new FakeRandom { DefaultDouble = 0.99 }, new[] { a, b }, bag ?? new BattleInventory(),
                db.GetMap("test_map"), new GameTime(1, 360));
        }

        [Test]
        public void TestMap_Validates() => CollectionAssert.IsEmpty(MapValidator.Validate(db.GetMap("test_map"), db));

        [Test]
        public void Move_OnlyAlongEdges()
        {
            var x = NewExpedition();
            Assert.That(x.CanMoveTo("n2"), Is.True);
            Assert.That(x.CanMoveTo("n3"), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => x.MoveTo("n3"));
        }

        [Test]
        public void Travel_FirstEdge60_Repeat30()
        {
            var x = NewExpedition();
            x.MoveTo("n4");
            Assert.That(x.Time.MinuteOfDay, Is.EqualTo(360 + 60));
            x.MoveTo("n1");
            Assert.That(x.Time.MinuteOfDay, Is.EqualTo(360 + 90));
            Assert.That(x.ArrivalTime("n2").MinuteOfDay, Is.EqualTo(360 + 150));
        }

        [Test]
        public void Fog_VisionRangeOne()
        {
            var x = NewExpedition();
            var m = x.Map;
            Assert.That(m.Node("n2").Discovered && m.Node("n4").Discovered, Is.True);
            Assert.That(m.Node("n3").Discovered, Is.False);
            Assert.That(m.Node("n2").EventsRevealed, Is.False);
            x.MoveTo("n4");
            Assert.That(m.Node("n3").Discovered, Is.False);   // n4에서 n3까지는 2칸
        }

        [Test]
        public void Fog_VisionRangeTwo_RevealsFurther()
        {
            var x = NewExpedition();
            x.Map.SetVisionRange(2);
            Assert.That(x.Map.Node("n3").Discovered && x.Map.Node("n5").Discovered, Is.True);
        }

        [Test]
        public void Perception_DetectsEventOnDiscovery()
        {
            var x = NewExpedition(new FakeRandom { DefaultDouble = 0.0 }, perception: 3); // 확률 25%, 굴림 0
            Assert.That(x.DetectChance(), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(x.Map.Node("n2").EventsRevealed, Is.True);
            Assert.That(x.Map.Node("n4").Detected, Is.False); // 빈 노드는 판정 안 함
        }

        [Test]
        public void ForcedBattle_BlocksMovingOn_VictoryCompletes()
        {
            var x = NewExpedition(new FakeRandom { DefaultDouble = 0.0 });
            x.MoveTo("n2");
            Assert.That(x.HasForcedBattle, Is.True);
            Assert.That(x.CanMoveTo("n3"), Is.False);
            var b = x.BeginBattle();
            int guard = 0;
            while (b.Phase != BattlePhase.Ended && guard++ < 100)
            {
                if (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn();
                else AutoPartyAI.Apply(b, AutoPartyAI.Decide(b, AutoPartyAI.Focus.LowestHp));
            }
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.Victory));
            int before = x.Time.MinuteOfDay;
            x.FinishBattle();
            Assert.That(x.Time.MinuteOfDay, Is.EqualTo(before + 30));
            Assert.That(x.Map.Node("n2").Completed, Is.True);
            Assert.That(x.Killed.Count, Is.EqualTo(2));
            Assert.That(x.CanMoveTo("n3"), Is.True);
        }

        [Test]
        public void Flee_RetreatsToPreviousNode_EventRemains()
        {
            var x = NewExpedition();
            x.MoveTo("n2");
            var b = x.BeginBattle();
            while (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn();
            b.Flee();
            int before = x.Time.MinuteOfDay;
            x.FinishBattle();
            Assert.That(x.Map.Current.Id, Is.EqualTo("n1"));
            Assert.That(x.Time.MinuteOfDay, Is.EqualTo(before + 60));
            Assert.That(x.Map.Node("n2").Completed, Is.False);
        }

        [Test]
        public void Gather_AddsItemsAndCompletes()
        {
            var x = NewExpedition();
            x.MoveTo("n2"); x.Map.CompleteEvent("n2", 0);
            x.MoveTo("n3");
            var (i, ev) = x.NextEvent();
            x.Gather(i);
            Assert.That(x.Bag.Count("herb"), Is.EqualTo(2));
            Assert.That(x.Map.Node("n3").Completed, Is.True);
        }

        [Test]
        public void Investigate_SupplyForcesGood_ElseRoll()
        {
            var bag = new BattleInventory(); bag.Add("key", 1);
            var x = NewExpedition(new FakeRandom { DefaultDouble = 0.95 }, bag: bag);
            x.MoveTo("n2"); x.Map.CompleteEvent("n2", 0);
            x.MoveTo("n5");
            var o = x.Investigate(0, useSupply: true);
            Assert.That(o.Grade, Is.EqualTo("good"));
            Assert.That(o.UsedSupply, Is.True);
            Assert.That(bag.Count("key"), Is.EqualTo(0));
            Assert.That(bag.Count("potion"), Is.EqualTo(2));
        }

        [Test]
        public void Investigate_WithoutSupply_RollsBad()
        {
            var x = NewExpedition(new FakeRandom { DefaultDouble = 0.95 });
            x.MoveTo("n2"); x.Map.CompleteEvent("n2", 0);
            x.MoveTo("n5");
            Assert.That(x.Investigate(0, useSupply: true).Grade, Is.EqualTo("bad")); // 열쇠 없음 → 굴림 0.95
        }

        [Test]
        public void Return_OnlyAtStart()
        {
            var x = NewExpedition();
            x.MoveTo("n4");
            Assert.That(x.CanReturn, Is.False);
            x.MoveTo("n1");
            x.ReturnToTown();
            Assert.That(x.End, Is.EqualTo(ExpeditionEnd.Returned));
        }

        [Test]
        public void PathToStart_OnlyThroughCompletedNodes()
        {
            var x = NewExpedition();
            x.MoveTo("n2"); x.Map.CompleteEvent("n2", 0);
            x.MoveTo("n3");
            x.Map.CompleteEvent("n3", 0);
            Assert.That(x.Map.PathToStart().Select(n => n.Id), Is.EqualTo(new[] { "n2", "n1" }));
        }

        [Test]
        public void Validator_CatchesBrokenMaps()
        {
            var m = TestData.TestMap();
            m.edges = m.edges.Append(new MapEdgeDefinition { a = "n1", b = "n3" }).ToArray();
            m.nodes[0].events = new[] { new NodeEventDefinition { kind = "treasure" } };
            var errors = MapValidator.Validate(m, db);
            Assert.That(errors.Any(e => e.Contains("이웃 칸이 아님")), Is.True, string.Join("\n", errors));
            Assert.That(errors.Any(e => e.Contains("시작 노드")), Is.True);
        }

        [Test]
        public void Validator_UnreachableNodeAndDeadEnd()
        {
            var m = TestData.TestMap();
            m.edges = m.edges.Where(e => !(e.a == "n2" && e.b == "n3")).ToArray();
            Assert.That(MapValidator.Validate(m, db).Any(e => e.Contains("n3에 갈 수 없음")), Is.True);
            var m2 = TestData.TestMap();
            m2.nodes[2].events = new NodeEventDefinition[0];
            Assert.That(MapValidator.Validate(m2, db).Any(e => e.Contains("막다른 노드 n3")), Is.True);
        }

        [Test]
        public void Travel_MultiHop_OnlyThroughCompletedNodes()
        {
            var x = NewExpedition();
            x.MoveTo("n2");                                   // 전투 미완료
            x.Map.CompleteEvent("n2", 0);
            x.MoveTo("n1");
            Assert.That(x.CanTravelTo("n3"), Is.True);        // n1 → n2(완료) → n3
            var (path, minutes) = x.Route("n3");
            Assert.That(path.Select(n => n.Id), Is.EqualTo(new[] { "n2", "n3" }));
            Assert.That(minutes, Is.EqualTo(30 + 60));        // 지나온 길 30 + 처음 길 60
            int before = x.Time.MinuteOfDay;
            x.TravelTo("n3");
            Assert.That(x.Map.Current.Id, Is.EqualTo("n3"));
            Assert.That(x.Time.MinuteOfDay, Is.EqualTo(before + 90));
        }

        [Test]
        public void Travel_CannotPassIncompleteNode()
        {
            var x = NewExpedition();
            x.Map.SetVisionRange(2);                          // n3·n5까지 보임
            Assert.That(x.CanTravelTo("n3"), Is.False);       // n2(전투 미완료)를 지나쳐야 함
            Assert.That(x.CanTravelTo("n2"), Is.True);
        }

        [Test]
        public void ReturnEstimate_SumsAutoReturnPath()
        {
            var x = NewExpedition();
            x.MoveTo("n2"); x.Map.CompleteEvent("n2", 0);     // 07:00
            x.MoveTo("n3"); x.Map.CompleteEvent("n3", 0);     // 08:00
            var eta = x.ReturnEstimate();
            Assert.That(eta.MinuteOfDay, Is.EqualTo(8 * 60 + 30 + 30)); // n3→n2 30, n2→n1 30
        }
    }
}
