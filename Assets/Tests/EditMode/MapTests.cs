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

        [Test]
        public void Travel_PassesSkippedCurio_ButNotPendingBattle()
        {
            // s ─ g(채집) ─ e : 채집을 하지 않고 지나친 g는 지나갈 수 있다
            var map = new MapDefinition
            {
                id = "line", name = "일자", radius = 2, startNode = "s",
                nodes = new[]
                {
                    new MapNodeDefinition { id = "s", name = "입구", q = 0, r = 0 },
                    new MapNodeDefinition { id = "g", name = "약초", q = 1, r = 0,
                        events = new[] { new NodeEventDefinition { kind = "gather", item = "herb", amount = 1 } } },
                    new MapNodeDefinition { id = "e", name = "끝", q = 2, r = 0 },
                },
                edges = new[] { new MapEdgeDefinition { a = "s", b = "g" }, new MapEdgeDefinition { a = "g", b = "e" } }
            };
            var a = TestData.Hero(db, "swordsman");
            var x = new Expedition(db, new FakeRandom { DefaultDouble = 0.99 }, new[] { a }, new BattleInventory(), map, new GameTime(1, 360));
            x.MoveTo("g");                       // 채집은 하지 않는다
            x.MoveTo("e");
            Assert.That(x.Map.Node("g").Completed, Is.False);
            Assert.That(x.Map.Node("g").Passable, Is.True);
            Assert.That(x.CanTravelTo("s"), Is.True);          // e → g(지나친 골동품) → s
            Assert.That(x.ReturnEstimate(), Is.Not.Null);

            // 전투가 남은 노드는 여전히 못 지나간다 (테스트 맵: 도주로 n2 전투가 남은 상황과 같음)
            var y = NewExpedition();
            y.Map.SetVisionRange(2);
            y.MoveTo("n4");
            Assert.That(y.CanTravelTo("n3"), Is.False);        // n4 → n2(안 가 봄·전투 남음) → n3
        }

        [Test]
        public void SwapAdjacent_ChangesBattleOrder()
        {
            var x = NewExpedition();
            x.SwapAdjacent(1, 2);
            Assert.That(x.Party.Select(p => p.Name), Is.EqualTo(new[] { "m", "swordsman" }));
            Assert.Throws<System.InvalidOperationException>(() => x.SwapAdjacent(1, 3));
        }

        [Test]
        public void UseItem_OutsideBattle_HealsAndTreats()
        {
            var bag = new BattleInventory(); bag.Add("potion", 1); bag.Add("bandage", 1); bag.Add("herb", 1);
            var x = NewExpedition(bag: bag);
            var hero = x.Party[0];
            Assert.Throws<System.InvalidOperationException>(() => x.UseItem("potion", hero)); // HP 가득 → 효과 없음, 소모 안 함
            Assert.That(bag.Count("potion"), Is.EqualTo(1));

            int max = hero.CurrentHp;
            hero.CurrentHp = max - 4;
            x.UseItem("potion", hero);
            Assert.That(hero.CurrentHp, Is.EqualTo(max));      // 최대 HP까지만
            Assert.That(bag.Count("potion"), Is.EqualTo(0));

            hero.Injuries.Add(new InjuryState("shallow_cut", severe: true));
            x.UseItem("bandage", hero);
            Assert.That(hero.Injuries.Single().Severe, Is.False); // 중상 → 경상
            Assert.That(Expedition.IsUsableOutsideBattle(db.GetItem("key")), Is.False);
        }

        // ── 밤 · 횃불 · 기습 (11-13) ─────────────────────────────

        private static GameDatabase NightDb(System.Action<BalanceConfig> tweak = null)
        {
            var file = TestData.File();
            var b = file.balance;
            b.nightDetectPenalty = 0.2; b.torchDetectBonus = 0.1; b.torchNightPenaltyMultiplier = 0.5; b.torchMaxMinutes = 720;
            b.ambushedBase = 0.3; b.ambushedPerPerception = 0.03; b.ambushedMin = 0.05; b.ambushedMax = 0.3; b.ambushedNightBonus = 0.1;
            b.ambushSpeedBonus = 5; b.ambushRounds = 1;
            foreach (var i in file.items) if (i.id == "key") i.torchMinutes = 240; // 테스트 데이터에 횃불이 없어 열쇠를 횃불로 쓴다
            tweak?.Invoke(b);
            return new GameDatabase(file);
        }

        private static Expedition At(GameDatabase d, int minute, int perception = 3, IRandomSource rng = null, BattleInventory bag = null)
        {
            var a = TestData.Hero(d, "swordsman"); a.Perception = perception;
            return new Expedition(d, rng ?? new FakeRandom { DefaultDouble = 0.99 }, new[] { a }, bag ?? new BattleInventory(),
                d.GetMap("test_map"), new GameTime(1, minute));
        }

        [Test]
        public void Detect_NightPenalty_TorchBonus()
        {
            var d = NightDb();
            var day = At(d, 12 * 60);
            var night = At(d, 22 * 60, bag: Bag("key", 1));
            double dayChance = day.DetectChance();
            Assert.That(night.DetectChance(), Is.EqualTo(System.Math.Max(d.Balance.perceptionDetectMin, dayChance - 0.2)).Within(1e-9));
            night.UseItem("key", null);                                   // 횃불
            Assert.That(night.TorchLit, Is.True);
            Assert.That(night.DetectChance(), Is.EqualTo(System.Math.Max(d.Balance.perceptionDetectMin, dayChance - 0.1)).Within(1e-9));
        }

        private static BattleInventory Bag(string id, int n) { var b = new BattleInventory(); b.Add(id, n); return b; }

        [Test]
        public void Torch_DurationStacksToMax()
        {
            var d = NightDb();
            var x = At(d, 20 * 60, bag: Bag("key", 4));
            x.UseItem("key", null); x.UseItem("key", null); x.UseItem("key", null);
            Assert.That(x.TorchMinutesLeft, Is.EqualTo(720));
            Assert.Throws<System.InvalidOperationException>(() => x.UseItem("key", null)); // 최대 12시간
            Assert.That(x.Bag.Count("key"), Is.EqualTo(1));
            x.MoveTo("n4");                                               // 60분 지나감
            Assert.That(x.TorchMinutesLeft, Is.EqualTo(660));
            Assert.That(x.NightPenaltyScale, Is.EqualTo(0.5));
        }

        [Test]
        public void Ambush_DetectedNode_PartyAmbushes()
        {
            var d = NightDb(b => { b.perceptionDetectMin = 1; b.perceptionDetectMax = 1; }); // 무조건 탐지
            var x = At(d, 12 * 60);
            Assert.That(x.Map.Node("n2").Detected, Is.True);
            x.MoveTo("n2");
            var battle = x.BeginBattle();
            Assert.That(battle.AmbushSide, Is.EqualTo(Grimward.Domain.Combat.BattleSide.Party));
            Assert.That(battle.Log.Any(e => e.Kind == Grimward.Domain.Combat.BattleEventKind.Ambush), Is.True);
        }

        [Test]
        public void Ambush_Undetected_ChanceFromPerceptionAndNight()
        {
            var d = NightDb();
            Assert.That(At(d, 12 * 60, perception: 3).AmbushedChance(), Is.EqualTo(0.21).Within(1e-9));  // 30 − 9
            Assert.That(At(d, 22 * 60, perception: 3).AmbushedChance(), Is.EqualTo(0.31).Within(1e-9));  // 밤 +10
            var lit = At(d, 22 * 60, perception: 3, bag: Bag("key", 1)); lit.UseItem("key", null);
            Assert.That(lit.AmbushedChance(), Is.EqualTo(0.26).Within(1e-9));                             // 횃불: 밤 가산 절반
            Assert.That(At(d, 12 * 60, perception: 20).AmbushedChance(), Is.EqualTo(0.05).Within(1e-9)); // 최소

            var sure = NightDb(b => { b.ambushedMin = 1; b.ambushedMax = 1; });                         // 무조건 기습당함
            var x = At(sure, 12 * 60);                                    // 탐지 실패 (0.99)
            x.MoveTo("n2");
            Assert.That(x.BeginBattle().AmbushSide, Is.EqualTo(Grimward.Domain.Combat.BattleSide.Enemy));
        }

        [Test]
        public void Ambush_None_OnRevisitOrNoAmbushMap()
        {
            var sure = NightDb(b => { b.ambushedMin = 1; b.ambushedMax = 1; });
            var x = At(sure, 12 * 60);
            x.MoveTo("n2");
            var b1 = x.BeginBattle();
            while (b1.Phase == BattlePhase.EnemyTurn) b1.RunEnemyTurn(); // 기습당해 적이 먼저
            b1.Flee();                                                    // 도주 → n1으로 물러남
            x.FinishBattle();
            x.MoveTo("n2");                                               // 다시 감 (첫 방문 아님)
            Assert.That(x.BeginBattle().AmbushSide, Is.Null);

            var file = TestData.File();
            file.balance.ambushedMin = 1; file.balance.ambushedMax = 1; file.balance.ambushSpeedBonus = 5;
            file.maps[0].noAmbush = true;
            var d2 = new GameDatabase(file);
            var y = At(d2, 12 * 60);
            y.MoveTo("n2");
            Assert.That(y.BeginBattle().AmbushSide, Is.Null);
        }

        [Test]
        public void NightAccuracy_HalvedByScale()
        {
            var c = TestData.Balance();
            c.nightPartyAccuracyPenalty = 0.1; c.nightEnemyAccuracyBonus = 0.1;
            double day = Grimward.Domain.Combat.CombatFormulas.HitChance(c, 8, 10, 0, Grimward.Domain.Combat.BattleSide.Party, false);
            double lit = Grimward.Domain.Combat.CombatFormulas.HitChance(c, 8, 10, 0, Grimward.Domain.Combat.BattleSide.Party, true, 0.5);
            Assert.That(day - lit, Is.EqualTo(0.05).Within(1e-9));
        }

        [Test]
        public void Tutorial_FixedDetectAndAmbush_OverridesNoAmbush()
        {
            var file = TestData.File();
            file.balance.ambushSpeedBonus = 5; file.balance.ambushRounds = 1;
            file.maps[0].noAmbush = true;
            var n2 = file.maps[0].nodes.First(n => n.id == "n2");
            n2.forceDetect = true; n2.ambush = Expedition.AmbushParty;
            var d = new GameDatabase(file);
            CollectionAssert.IsEmpty(MapValidator.Validate(d.GetMap("test_map"), d));
            var x = At(d, 12 * 60, perception: 0);                         // 지각 0, 판정 0.99여도
            Assert.That(x.Map.Node("n2").Detected, Is.True);                // 무조건 탐지
            x.MoveTo("n2");
            var battle = x.BeginBattle();
            Assert.That(battle.AmbushSide, Is.EqualTo(BattleSide.Party));   // 기습 없는 맵이어도 고정 기습
            Assert.That(battle.Formation.Alive(BattleSide.Party).All(c => c.Effects.Sum(DataKeys.Stat.Speed) == 5), Is.True);
        }

        [Test]
        public void FixedDetection_OnlyForcedNodesDetected()
        {
            var file = TestData.File();
            file.balance.perceptionDetectMin = 1; file.balance.perceptionDetectMax = 1; // 판정하면 무조건 탐지
            file.maps[0].fixedDetection = true;
            file.maps[0].nodes.First(n => n.id == "n5").forceDetect = true;
            var d = new GameDatabase(file);
            var x = At(d, 12 * 60);
            Assert.That(x.Map.Node("n2").Detected, Is.False);   // 지각 판정 안 함
            x.MoveTo("n4"); x.MoveTo("n2");
            Assert.That(x.Map.Node("n5").Detected, Is.True);    // 고정 탐지만
        }

        // ── 밤 랜덤 전투 (11-13) ─────────────────────────────

        private static GameDatabase NightBattleDb(double chance)
        {
            return NightDb(b => b.nightRandomBattleChance = chance);
        }

        private static Expedition NightAt(GameDatabase d, int minute, BattleInventory bag = null)
        {
            d.GetMap("test_map").nightEncounter = "two";
            return At(d, minute, bag: bag);
        }

        [Test]
        public void NightBattle_OnRevisit_AtNight_StopsAndFights()
        {
            var x = NightAt(NightBattleDb(1.0), 22 * 60);
            x.MoveTo("n4");                                       // 첫 방문: 없음
            Assert.That(x.HasForcedBattle, Is.False);
            x.MoveTo("n1");                                       // 시작 노드: 없음
            Assert.That(x.HasForcedBattle, Is.False);
            x.MoveTo("n4");                                       // 다시 방문 + 밤 → 습격
            Assert.That(x.InRandomBattle, Is.True);
            Assert.That(x.CanMoveTo("n1"), Is.False);
            var b = x.BeginBattle();
            Assert.That(b.AmbushSide, Is.Null);                   // 재방문은 기습 없음
            while (b.Phase != BattlePhase.Ended)
            {
                if (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn();
                else b.Flee();
            }
            x.FinishBattle();
            Assert.That(x.InRandomBattle, Is.False);
            Assert.That(x.Map.Node("n4").Completed, Is.True);     // 노드 상태는 그대로
        }

        [Test]
        public void NightBattle_ChanceByTimeAndTorch()
        {
            var d = NightBattleDb(0.2);
            Assert.That(NightAt(d, 12 * 60).NightBattleChance(), Is.EqualTo(0));
            Assert.That(NightAt(d, 22 * 60).NightBattleChance(), Is.EqualTo(0.2).Within(1e-9));
            var lit = NightAt(d, 22 * 60, Bag("key", 1)); lit.UseItem("key", null);
            Assert.That(lit.NightBattleChance(), Is.EqualTo(0.1).Within(1e-9));
        }

        [Test]
        public void NightBattle_NoneWithoutMapEncounter()
        {
            var x = At(NightBattleDb(1.0), 22 * 60);              // test_map 기본: nightEncounter 없음
            x.MoveTo("n4"); x.MoveTo("n1"); x.MoveTo("n4");
            Assert.That(x.HasForcedBattle, Is.False);
        }

        // ── 자동 귀환 (11-13) ─────────────────────────────

        [Test]
        public void GoToExit_WalksBackToStart_NoTownReturn()
        {
            var x = NewExpedition();
            x.MoveTo("n4"); x.MoveTo("n2"); x.Map.CompleteEvent("n2", 0);
            Assert.That(x.CanGoToExit, Is.True);
            var eta = x.ReturnEstimate();
            x.GoToExit();
            Assert.That(x.Map.Current.IsStart, Is.True);
            Assert.That(x.Phase, Is.EqualTo(ExpeditionPhase.Exploring));   // 귀환은 따로
            Assert.That(x.Time.MinuteOfDay, Is.EqualTo(eta.MinuteOfDay));
            Assert.That(x.CanGoToExit, Is.False);
            x.ReturnToTown();
            Assert.That(x.End, Is.EqualTo(ExpeditionEnd.Returned));
        }

        [Test]
        public void GoToExit_StopsOnNightRaid()
        {
            // s ─ a ─ b 일자 맵. 18:00 출발 → a(19:00) → b(20:00, 밤). 자동 귀환하면 a 재방문(밤)에서 습격으로 멈춘다.
            var d = NightBattleDb(1.0);
            var map = new MapDefinition
            {
                id = "line", name = "일자", radius = 2, startNode = "s", nightEncounter = "two",
                nodes = new[]
                {
                    new MapNodeDefinition { id = "s", name = "입구", q = 0, r = 0 },
                    new MapNodeDefinition { id = "a", name = "가", q = 1, r = 0 },
                    new MapNodeDefinition { id = "b", name = "나", q = 2, r = 0 },
                },
                edges = new[] { new MapEdgeDefinition { a = "s", b = "a" }, new MapEdgeDefinition { a = "a", b = "b" } }
            };
            var x = new Expedition(d, new FakeRandom { DefaultDouble = 0.99 }, new[] { TestData.Hero(d, "swordsman") },
                new BattleInventory(), map, new GameTime(1, 18 * 60));
            x.MoveTo("a"); x.MoveTo("b");
            Assert.That(x.IsNight, Is.True);
            Assert.That(x.GoToExit().Last().To.Id, Is.EqualTo("a"));
            Assert.That(x.Map.Current.Id, Is.EqualTo("a"));
            Assert.That(x.InRandomBattle, Is.True);
            Assert.That(x.CanGoToExit, Is.False);            // 전투를 끝내야 다시 누를 수 있다
            Assert.That(x.Phase, Is.EqualTo(ExpeditionPhase.Exploring));
        }
    }
}
