using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using NUnit.Framework;

namespace Grimward.Tests
{
    /// <summary>전투 테스트 씬 「자동 진행」의 파티 조작.</summary>
    public class AutoPartyAITests
    {
        [Test]
        public void AutoPlay_FinishesBattles()
        {
            var db = TestData.Db();
            for (int seed = 1; seed <= 30; seed++)
            {
                var party = new[] { TestData.Hero(db, "swordsman"), TestData.Hero(db, "mage") };
                var inv = new BattleInventory(); inv.Add("potion", 2);
                var b = BattleState.Create(db, new SeededRandom(seed), party, "two", false, inv);
                b.Start();
                int guard = 0;
                while (b.Phase != BattlePhase.Ended && guard++ < 500)
                {
                    if (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn();
                    else AutoPartyAI.Apply(b, AutoPartyAI.Decide(b, AutoPartyAI.Focus.LowestHp));
                }
                Assert.That(b.Phase, Is.EqualTo(BattlePhase.Ended), $"seed {seed}");
            }
        }

        [Test]
        public void AutoPlay_UsesPotionOnBadlyHurtAlly()
        {
            var db = TestData.Db();
            var factory = new CombatantFactory(db);
            var hero = factory.FromAdventurer(TestData.Hero(db, "swordsman"));
            hero.TakeDamage(hero.MaxHp - 2);
            var inv = new BattleInventory(); inv.Add("potion", 1);
            var b = TestData.Battle(db, new FakeRandom(), new[] { hero }, new[] { factory.FromMonster("dummy") }, factory, inv: inv);
            b.Start();
            while (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn();
            var a = AutoPartyAI.Decide(b, AutoPartyAI.Focus.LowestHp);
            Assert.That(a.Kind, Is.EqualTo(AutoAction.ActionKind.Item));
            Assert.That(a.ItemId, Is.EqualTo("potion"));
        }
    }
}
