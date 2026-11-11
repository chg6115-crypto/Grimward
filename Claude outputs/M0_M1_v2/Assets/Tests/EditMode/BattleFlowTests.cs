using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Units;
using NUnit.Framework;

namespace Grimward.Tests
{
    public class BattleFlowTests
    {
        private GameDatabase db;
        private CombatantFactory factory;

        [SetUp]
        public void SetUp()
        {
            db = TestData.Db();
            factory = new CombatantFactory(db);
        }

        private BattleState Make(IRandomSource rng, List<Adventurer> heroes, params string[] enemyIds)
        {
            var party = heroes.Select(factory.FromAdventurer).ToList();
            var enemies = enemyIds.Select(factory.FromMonster).ToList();
            var inv = new BattleInventory();
            inv.Add("potion", 2);
            inv.Add("herb", 1);
            return new BattleState(db, rng, party, enemies, false, inv, factory);
        }

        [Test]
        public void Start_FasterSideActsFirst()
        {
            var b = Make(new FakeRandom(), new List<Adventurer> { TestData.Hero(db, "swordsman") }, "dummy");
            b.Start();
            Assert.That(b.Phase, Is.EqualTo(BattlePhase.PlayerTurn));
            Assert.That(b.Round, Is.EqualTo(1));
        }

        [Test]
        public void Victory_WritesBackHpAndKills()
        {
            var hero = TestData.Hero(db, "swordsman");
            hero.CurrentHp = 20;
            var b = Make(new FakeRandom(), new List<Adventurer> { hero }, "dummy");
            b.Start();
            var dummy = b.Formation.Alive(BattleSide.Enemy).First();
            dummy.TakeDamage(9);
            b.UseSkill(db.GetSkill("hit"), dummy);
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(b.Phase, Is.EqualTo(BattlePhase.Ended));
            CollectionAssert.AreEqual(new[] { "dummy" }, b.KilledMonsterIds);
            Assert.That(hero.CurrentHp, Is.EqualTo(20));
        }

        [Test]
        public void Flee_DamagesEachButLeavesOneHp()
        {
            var a = TestData.Hero(db, "swordsman", "A");
            var c = TestData.Hero(db, "mage", "C");
            c.CurrentHp = 3;
            var b = Make(new FakeRandom(), new List<Adventurer> { a, c }, "dummy");
            b.Start();
            b.Flee();
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.Fled));
            Assert.That(a.CurrentHp, Is.EqualTo(28 - 5)); // 28 × 0.2 = 5
            Assert.That(c.CurrentHp, Is.EqualTo(1));
            Assert.That(c.IsDead, Is.False);
        }

        [Test]
        public void Item_DoesNotEndTurn_OncePerTurn()
        {
            var hero = TestData.Hero(db, "swordsman");
            hero.CurrentHp = 10;
            var b = Make(new FakeRandom(), new List<Adventurer> { hero }, "dummy");
            b.Start();
            var actor = b.CurrentActor;
            b.UseItem("potion", actor);
            Assert.That(actor.Hp, Is.EqualTo(20));
            Assert.That(b.CurrentActor, Is.SameAs(actor));
            Assert.That(b.Phase, Is.EqualTo(BattlePhase.PlayerTurn));
            Assert.That(b.CanUseItem, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => b.UseItem("potion", actor));
            Assert.That(b.Inventory.Count("potion"), Is.EqualTo(1));
        }

        [Test]
        public void Inventory_SplitsIntoStackSlots_AndKeyNotUsableInBattle()
        {
            var inv = new BattleInventory();
            inv.Add("potion", 7);
            inv.Add("key", 1);
            var slots = inv.ToSlots(db, 12);
            Assert.That(slots.Count, Is.EqualTo(3));
            Assert.That(slots[0].Value, Is.EqualTo(5));
            Assert.That(slots[1].Value, Is.EqualTo(2));
            Assert.That(BattleInventory.IsUsableInBattle(db.GetItem("key")), Is.False);

            var b = Make(new FakeRandom(), new List<Adventurer> { TestData.Hero(db, "swordsman") }, "dummy");
            b.Inventory.Add("key", 1);
            b.Start();
            Assert.Throws<System.InvalidOperationException>(() => b.UseItem("key", b.CurrentActor));
        }

        [Test]
        public void Poison_TicksAtTargetTurnStart()
        {
            var rng = new FakeRandom();
            var b = Make(rng, new List<Adventurer> { TestData.Hero(db, "mage") }, "dummy");
            b.Start(); // 마법사 속도 7 > 4
            var dummy = b.Formation.Alive(BattleSide.Enemy).First();
            b.UseSkill(db.GetSkill("poison"), dummy); // 5 × 0.7 = 3.5 → 3 (10 → 7)
            // 마법사 차례가 끝나고 바로 적 차례가 시작되며 중독 피해 2 (7 → 5)
            Assert.That(b.CurrentActor, Is.SameAs(dummy));
            Assert.That(dummy.Hp, Is.EqualTo(5));
            Assert.That(b.Log.Any(e => e.Kind == BattleEventKind.PoisonTicked && e.Amount == 2), Is.True);
        }

        [Test]
        public void Defeat_MarksAdventurersDead()
        {
            var hero = TestData.Hero(db, "mage");
            hero.CurrentHp = 1;
            var rng = new FakeRandom();
            rng.Ints(1, 8); // 영웅 7+1=8, 적 4+8=12 → 적 먼저
            var b = Make(rng, new List<Adventurer> { hero }, "dummy");
            b.Start();
            Assert.That(b.Phase, Is.EqualTo(BattlePhase.EnemyTurn));
            b.RunEnemyTurn();
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(hero.IsDead, Is.True);
            Assert.That(hero.CurrentHp, Is.EqualTo(0));
        }

        [Test]
        public void SameSeed_SameBattle()
        {
            string RunOnce()
            {
                var heroes = new List<Adventurer> { TestData.Hero(db, "swordsman", "A"), TestData.Hero(db, "mage", "B") };
                var rng = new SeededRandom(2026);
                var localFactory = new CombatantFactory(db);
                var b = new BattleState(db, rng, heroes.Select(localFactory.FromAdventurer).ToList(),
                    localFactory.RollEncounter("two", rng), false, new BattleInventory(), localFactory);
                b.Start();
                int guard = 0;
                while (b.Phase != BattlePhase.Ended && guard++ < 500)
                {
                    if (b.Phase == BattlePhase.EnemyTurn) { b.RunEnemyTurn(); continue; }
                    var skill = b.CurrentSkills.FirstOrDefault(s => b.CanUseSkill(s, out _));
                    if (skill == null) { b.Pass(); continue; }
                    b.UseSkill(skill, b.TargetsFor(skill).First());
                }
                return string.Join("\n", b.Log.Select(e => e.Message));
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()));
        }

        [Test]
        public void SummonedEnemy_ActsFromNextRound()
        {
            var rng = new FakeRandom();
            rng.Ints(1, 8); // 영웅 6+1, 보스 5+8 → 보스 먼저
            var b = Make(rng, new List<Adventurer> { TestData.Hero(db, "swordsman") }, "boss");
            b.Start();
            Assert.That(b.CurrentActor.DefinitionId, Is.EqualTo("boss"));
            b.RunEnemyTurn(); // 혼자 → 소환 2
            Assert.That(b.Formation.Count(BattleSide.Enemy), Is.EqualTo(3));
            Assert.That(b.RoundOrder.Count, Is.EqualTo(2)); // 이번 라운드 순서에는 없음
            Assert.That(b.Phase, Is.EqualTo(BattlePhase.PlayerTurn));
        }
    }
}
