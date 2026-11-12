using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using NUnit.Framework;

namespace Grimward.Tests
{
    /// <summary>기술 피해 범위: 능력치 가중치 · 편차 · 지각 안정성 (기획안 6·7장).</summary>
    public class DamageRangeTests
    {
        private GameDatabase db;
        private CombatantFactory factory;

        [SetUp]
        public void SetUp()
        {
            db = TestData.Db();
            factory = new CombatantFactory(db);
        }

        private Combatant Sword(int perception)
        {
            var a = TestData.Hero(db, "swordsman"); // 근력 5 · 민첩 3 · 지능 1, 무기 4~8
            a.Perception = perception;
            return factory.FromAdventurer(a);
        }

        [Test]
        public void StatBonus_WeightedSumDividedBy6()
        {
            var c = Sword(0);
            Assert.That(CombatFormulas.StatBonus(db.Balance, c, db.GetSkill("smash").weights), Is.EqualTo(2)); // 15/6
            Assert.That(CombatFormulas.StatBonus(db.Balance, c, db.GetSkill("aim").weights), Is.EqualTo(1));   // 9/6
        }

        [Test]
        public void Range_CenterTimesVariance_NoStability()
        {
            CombatFormulas.DamageRange(db.Balance, Sword(0), db.GetSkill("smash"), out int min, out int max);
            Assert.That((min, max), Is.EqualTo((4, 12))); // 중앙 8 ± 50%
            CombatFormulas.DamageRange(db.Balance, Sword(0), db.GetSkill("aim"), out min, out max);
            Assert.That((min, max), Is.EqualTo((6, 7))); // 중앙 7 ± 10% → 6.3~7.7
        }

        [Test]
        public void Stability_RaisesOnlyLowerBound()
        {
            CombatFormulas.DamageRange(db.Balance, Sword(10), db.GetSkill("smash"), out int min, out int max);
            Assert.That((min, max), Is.EqualTo((5, 12))); // 안정성 40%: 하한 8 × (1 − 0.5 × 0.6) = 5.6
        }

        [Test]
        public void Stability_Capped()
        {
            Assert.That(CombatFormulas.Stability(db.Balance, 20), Is.EqualTo(0.4).Within(1e-9));
            Assert.That(CombatFormulas.Stability(db.Balance, 3), Is.EqualTo(0.12).Within(1e-9));
        }

        [Test]
        public void NoVariance_UsesAttackRange()
        {
            var c = Sword(10);
            CombatFormulas.DamageRange(db.Balance, c, db.GetSkill("hit"), out int min, out int max);
            Assert.That((min, max), Is.EqualTo((c.AttackMin, c.AttackMax)));
            var m = factory.FromMonster("dummy");
            CombatFormulas.DamageRange(db.Balance, m, db.GetSkill("hit"), out min, out max);
            Assert.That((min, max), Is.EqualTo((2, 4)));
        }

        [Test]
        public void WeightedHeal_AddsStatBonus()
        {
            var rng = new FakeRandom();
            var ctx = new BattleContext(db.Balance, rng, new Formation(4), factory, false) { Round = 1 };
            var mage = factory.FromAdventurer(TestData.Hero(db, "mage")); // 지능 6 → 보너스 3
            ctx.Formation.Add(mage);
            mage.TakeDamage(15);
            ctx.CurrentActor = mage;
            new SkillResolver(ctx).Resolve(mage, db.GetSkill("mend"), mage);
            Assert.That(ctx.Log.Last(e => e.Kind == BattleEventKind.Healed).Amount, Is.EqualTo(6)); // 3(최소) + 3
        }
    }
}
