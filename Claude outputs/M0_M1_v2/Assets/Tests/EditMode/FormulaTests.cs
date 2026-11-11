using Grimward.Domain.Combat;
using Grimward.Domain.Units;
using NUnit.Framework;

namespace Grimward.Tests
{
    public class FormulaTests
    {
        [Test]
        public void Swordsman_Level1_MatchesDesign()
        {
            var db = TestData.Db();
            var a = TestData.Hero(db, "swordsman");
            var c = db.Balance;
            Assert.That(StatFormulas.MaxHp(c, a), Is.EqualTo(28));
            Assert.That(StatFormulas.AttackMin(c, a), Is.EqualTo(6));
            Assert.That(StatFormulas.AttackMax(c, a), Is.EqualTo(10));
            Assert.That(StatFormulas.Accuracy(c, a), Is.EqualTo(8));
            Assert.That(StatFormulas.Dodge(c, a), Is.EqualTo(10));
            Assert.That(StatFormulas.Speed(c, a), Is.EqualTo(6));
            Assert.That(StatFormulas.Crit(c, a), Is.EqualTo(0.045).Within(1e-9));
        }

        [Test]
        public void Mage_UsesIntelligenceForAttack()
        {
            var db = TestData.Db();
            var a = TestData.Hero(db, "mage");
            var c = db.Balance;
            Assert.That(StatFormulas.MaxHp(c, a), Is.EqualTo(22));
            Assert.That(StatFormulas.AttackMin(c, a), Is.EqualTo(5));
            Assert.That(StatFormulas.AttackMax(c, a), Is.EqualTo(8));
            Assert.That(StatFormulas.Dodge(c, a), Is.EqualTo(13));
            Assert.That(StatFormulas.Speed(c, a), Is.EqualTo(7));
        }

        [Test]
        public void HitChance_LinearWithNightAndClamp()
        {
            var c = TestData.Balance();
            Assert.That(CombatFormulas.HitChance(c, 8, 10, 0, BattleSide.Party, false), Is.EqualTo(0.83).Within(1e-9));
            Assert.That(CombatFormulas.HitChance(c, 8, 10, 0, BattleSide.Party, true), Is.EqualTo(0.73).Within(1e-9));
            Assert.That(CombatFormulas.HitChance(c, 8, 10, 0, BattleSide.Enemy, true), Is.EqualTo(0.93).Within(1e-9));
            Assert.That(CombatFormulas.HitChance(c, 50, 0, 0, BattleSide.Party, false), Is.EqualTo(0.95).Within(1e-9));
            Assert.That(CombatFormulas.HitChance(c, 0, 200, 0, BattleSide.Party, false), Is.EqualTo(0.05).Within(1e-9));
            Assert.That(CombatFormulas.HitChance(c, 8, 10, -10, BattleSide.Party, false), Is.EqualTo(0.73).Within(1e-9));
        }

        [Test]
        public void CritChance_HasMinimum()
        {
            var c = TestData.Balance();
            Assert.That(CombatFormulas.CritChance(c, 0.045, 0), Is.EqualTo(0.05).Within(1e-9));
            Assert.That(CombatFormulas.CritChance(c, 0.06, 0.05), Is.EqualTo(0.11).Within(1e-9));
        }

        [Test]
        public void Damage_FloorsWithoutFloatingPointLoss()
        {
            var c = TestData.Balance();
            Assert.That(CombatFormulas.Damage(c, 10, 0.4, false, 0), Is.EqualTo(14));
            Assert.That(CombatFormulas.Damage(c, 10, 0, true, 0), Is.EqualTo(15));
            Assert.That(CombatFormulas.Damage(c, 7, -0.4, false, 0.05), Is.EqualTo(3)); // 3.99 → 3
            Assert.That(CombatFormulas.Damage(c, 10, 0.1 + 0.2, false, 0), Is.EqualTo(13));
            Assert.That(CombatFormulas.Damage(c, 10, 0, false, 0.7), Is.EqualTo(3)); // 0.3 오차로 2가 되지 않음
            Assert.That(CombatFormulas.Damage(c, 2, -0.5, false, 0.6), Is.EqualTo(0));
        }

        [Test]
        public void Reduction_ArmorPlusWard_Capped()
        {
            var c = TestData.Balance();
            Assert.That(CombatFormulas.Reduction(c, 0.05, -0.3), Is.EqualTo(0.35).Within(1e-9));
            Assert.That(CombatFormulas.Reduction(c, 0.6, -0.5), Is.EqualTo(0.8).Within(1e-9));
        }
    }
}
