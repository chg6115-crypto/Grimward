using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using Grimward.Domain.Units;
using NUnit.Framework;

namespace Grimward.Tests
{
    /// <summary>부상 규칙 (기획안 11장).</summary>
    public class InjuryTests
    {
        private GameDatabase db;
        private CombatantFactory factory;
        private FakeRandom rng;
        private BattleContext ctx;
        private SkillResolver resolver;

        [SetUp]
        public void SetUp()
        {
            db = TestData.Db();
            factory = new CombatantFactory(db);
            rng = new FakeRandom { DefaultDouble = 0.0 }; // 기본: 명중, 치명타, 부상 판정 모두 성공 쪽
            ctx = new BattleContext(db.Balance, rng, new Formation(4), factory, false) { Round = 1 };
            resolver = new SkillResolver(ctx);
        }

        private Combatant Hero(Adventurer a = null)
        {
            var c = factory.FromAdventurer(a ?? TestData.Hero(db, "swordsman"));
            ctx.Formation.Add(c);
            return c;
        }

        private Combatant Enemy()
        {
            var c = factory.FromMonster("dummy");
            ctx.Formation.Add(c);
            return c;
        }

        private void Hit(Combatant actor, string skill, Combatant target)
        {
            ctx.CurrentActor = actor;
            resolver.Resolve(actor, db.GetSkill(skill), target);
        }

        [Test]
        public void TaggedHit_InflictsMinor_WithChance()
        {
            var hero = Hero(); var e = Enemy();
            rng.Doubles(0.0, 0.9, 0.29); // 명중, 치명 실패, 부상 0.29 < 0.3
            Hit(e, "bonk", hero);
            Assert.That(hero.Injuries.Single().Id, Is.EqualTo("concussion"));
            Assert.That(hero.Injuries.Single().Severe, Is.False);
            Assert.That(hero.Effects.Sum(DataKeys.Stat.Accuracy), Is.EqualTo(-5));
        }

        [Test]
        public void TaggedHit_RollAboveChance_NoInjury()
        {
            var hero = Hero(); var e = Enemy();
            rng.Doubles(0.0, 0.9, 0.31);
            Hit(e, "bonk", hero);
            Assert.That(hero.Injuries, Is.Empty);
        }

        [Test]
        public void TaggedCrit_AlwaysInjures()
        {
            var hero = Hero(); var e = Enemy();
            rng.Doubles(0.0, 0.0, 0.99); // 명중, 치명타 → 부상 판정 0.99도 성공
            Hit(e, "bonk", hero);
            Assert.That(hero.Injuries.Count, Is.EqualTo(1));
        }

        [Test]
        public void UntaggedCrit_NoInjury()
        {
            var hero = Hero(); var e = Enemy();
            rng.Doubles(0.0, 0.0);
            Hit(e, "hit", hero);
            Assert.That(hero.Injuries, Is.Empty);
        }

        [Test]
        public void SameInjuryAgain_Worsens_AndMaxTwoKinds()
        {
            var hero = Hero(); var e = Enemy();
            InjuryRules.Inflict(ctx, hero, "concussion");
            InjuryRules.Inflict(ctx, hero, "concussion");
            Assert.That(hero.Injuries.Single().Severe, Is.True);
            Assert.That(hero.Effects.Sum(DataKeys.Stat.Accuracy), Is.EqualTo(-10)); // 경상 효과는 빠지고 중상만
            InjuryRules.Inflict(ctx, hero, "shallow_cut");
            Assert.That(hero.Injuries.Count, Is.EqualTo(2));
            db.Balance.injuryMaxPerAdventurer = 1;
            var other = Hero();
            InjuryRules.Inflict(ctx, other, "concussion");
            InjuryRules.Inflict(ctx, other, "shallow_cut");
            Assert.That(other.Injuries.Count, Is.EqualTo(1));
        }

        [Test]
        public void HpThreshold_WorsensMinor_Once()
        {
            var hero = Hero(); var e = Enemy();
            InjuryRules.Inflict(ctx, hero, "shallow_cut");
            hero.TakeDamage(hero.Hp - (int)(hero.MaxHp * 0.3) - 1); // 임계 바로 위
            rng.Doubles(0.0, 0.9); // 명중, 치명 실패 (태그 없는 기술)
            Hit(e, "hit", hero);
            Assert.That(hero.IsAlive, Is.True);
            Assert.That(hero.Injuries.Single().Severe, Is.True);
            Assert.That(hero.InjuryThresholdChecked, Is.True);
        }

        [Test]
        public void HpThreshold_NoInjury_RollsTagForMinor()
        {
            var hero = Hero(); var e = Enemy();
            hero.TakeDamage(hero.Hp - (int)(hero.MaxHp * 0.3) - 1);
            rng.Doubles(0.0, 0.9, 0.5, 0.4); // 명중, 치명 실패, 태그 판정 실패(0.5 ≥ 0.2), 임계 판정 성공(0.4 < 0.5)
            Hit(e, "cut", hero);
            Assert.That(hero.Injuries.Single().Id, Is.EqualTo("shallow_cut"));
            Assert.That(hero.Injuries.Single().Severe, Is.False);
        }

        [Test]
        public void Bleed_HitsOnlyFirstRounds_AndHerbDoesNotCure()
        {
            var a = TestData.Hero(db, "swordsman");
            a.Injuries.Add(new InjuryState("shallow_cut"));
            var hero = factory.FromAdventurer(a);
            var b = TestData.Battle(db, new FakeRandom { DefaultDouble = 0.99 }, new[] { hero }, new[] { factory.FromMonster("dummy") },
                factory, inv: new BattleInventory());
            b.Inventory.Add("herb", 1);
            b.Start();
            int bleeds = 0;
            for (int guard = 0; guard < 40 && b.Phase != BattlePhase.Ended; guard++)
            {
                if (b.Phase == BattlePhase.EnemyTurn) { b.RunEnemyTurn(); continue; }
                if (b.CanUseItem && b.Inventory.Count("herb") > 0) b.UseItem("herb", hero);
                b.Pass();
            }
            bleeds = b.Log.Count(e => e.Kind == BattleEventKind.BleedTicked);
            Assert.That(bleeds, Is.EqualTo(3));
        }

        [Test]
        public void FieldTreatment_RemovesMinorCut_ReducesMajor_NotConcussion()
        {
            var hero = Hero(); var healer = Hero();
            InjuryRules.Inflict(ctx, hero, "shallow_cut");
            InjuryRules.Inflict(ctx, hero, "shallow_cut"); // 깊은 베임
            InjuryRules.Inflict(ctx, hero, "concussion");
            Hit(healer, "aid", hero);
            Assert.That(hero.Injuries.First(i => i.Id == "shallow_cut").Severe, Is.False);
            Assert.That(hero.Effects.BleedDamage(), Is.EqualTo(0)); // 치료한 상처는 이번 전투에서 다시 벌어지지 않음
            Hit(healer, "aid", hero);
            Assert.That(hero.Injuries.Select(i => i.Id), Is.EquivalentTo(new[] { "concussion" }));
        }

        [Test]
        public void Bandage_IsUsableInBattle_AndTreats()
        {
            var a = TestData.Hero(db, "swordsman");
            a.Injuries.Add(new InjuryState("shallow_cut"));
            var hero = factory.FromAdventurer(a);
            var inv = new BattleInventory(); inv.Add("bandage", 1);
            var b = TestData.Battle(db, new FakeRandom { DefaultDouble = 0.99 }, new[] { hero }, new[] { factory.FromMonster("dummy") }, factory, inv: inv);
            b.Start();
            while (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn();
            Assert.That(hero.Effects.HasBleed, Is.True);
            b.UseItem("bandage", hero);
            Assert.That(hero.Injuries, Is.Empty);
            Assert.That(hero.Effects.HasBleed, Is.False);
        }

        [Test]
        public void Injuries_WriteBack_AndRestRules()
        {
            var a = TestData.Hero(db, "swordsman");
            var hero = factory.FromAdventurer(a);
            var b = TestData.Battle(db, new FakeRandom { DefaultDouble = 0.99 }, new[] { hero }, new[] { factory.FromMonster("dummy") }, factory);
            b.Start();
            InjuryRules.Inflict(b.Context, hero, "concussion");
            InjuryRules.Inflict(b.Context, hero, "shallow_cut");
            while (b.Phase != BattlePhase.Ended)
            {
                if (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn(); else b.Flee();
            }
            Assert.That(a.Injuries.Count, Is.EqualTo(2));
            a.RestOvernight(db);
            Assert.That(a.Injuries.Select(i => i.Id), Is.EquivalentTo(new[] { "shallow_cut" })); // 뇌진탕만 자연 회복
            a.TreatAtInfirmary();
            Assert.That(a.Injuries, Is.Empty);
        }
    }
}
