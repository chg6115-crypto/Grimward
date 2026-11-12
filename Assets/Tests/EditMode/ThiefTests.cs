using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using NUnit.Framework;

namespace Grimward.Tests
{
    /// <summary>소매치기 고블린 · 일반 방 로스터 (기획안 11장).</summary>
    public class ThiefTests
    {
        private GameDatabase db;
        private CombatantFactory factory;
        private FakeRandom rng;
        private BattleContext ctx;
        private SkillResolver resolver;
        private BattleInventory bag;

        [SetUp]
        public void SetUp()
        {
            db = TestData.Db();
            factory = new CombatantFactory(db);
            rng = new FakeRandom { DefaultDouble = 0.0 };
            ctx = new BattleContext(db.Balance, rng, new Formation(4), factory, false) { Round = 1 };
            bag = new BattleInventory();
            bag.Add("potion", 2);
            ctx.Inventory = bag;
            resolver = new SkillResolver(ctx);
        }

        private Combatant Add(Combatant c) { ctx.Formation.Add(c); return c; }

        private void Use(Combatant actor, string skill, Combatant target)
        {
            ctx.CurrentActor = actor;
            resolver.Resolve(actor, db.GetSkill(skill), target);
        }

        [Test]
        public void Snatch_StealsOneItem_OncePerThief()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman")));
            var thief = Add(factory.FromMonster("thief"));
            Use(thief, "snatch", hero);
            Assert.That(bag.Count("potion"), Is.EqualTo(1));
            Assert.That(thief.StolenItems, Is.EqualTo(new[] { "potion" }));
            Use(thief, "snatch", hero);
            Assert.That(bag.Count("potion"), Is.EqualTo(1)); // 한 마리당 1회
        }

        [Test]
        public void Snatch_SkipsProtectedQuestItems()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman")));
            var thief = Add(factory.FromMonster("thief"));
            bag.Protected.Add("potion");
            Use(thief, "snatch", hero);
            Assert.That(bag.Count("potion"), Is.EqualTo(2));
            Assert.That(thief.HasStolen, Is.False);
        }

        [Test]
        public void KillingThief_RecoversItems()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman")));
            var thief = Add(factory.FromMonster("thief"));
            Use(thief, "snatch", hero);
            thief.TakeDamage(thief.Hp - 1);
            Use(hero, "hit", thief);
            Assert.That(thief.IsAlive, Is.False);
            Assert.That(bag.Count("potion"), Is.EqualTo(2));
            Assert.That(ctx.Log.Any(e => e.Kind == BattleEventKind.ItemRecovered), Is.True);
        }

        [Test]
        public void FleeingThief_DisplacedOrStunned_DelaysEscape()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman")));
            Add(factory.FromMonster("dummy"));
            var thief = Add(factory.FromMonster("thief"));
            thief.HasStolen = true; thief.EscapeRound = 3;
            rng.Doubles(0.0, 0.99, 0.0); // 명중 · 치명 실패 · 진형 붕괴 성공
            Use(hero, "pull", thief);  // 끌어당기기 명중 → 진형 붕괴
            Assert.That(thief.EscapeRound, Is.EqualTo(4));
            rng.Doubles(0.0, 0.99, 0.0); // 명중 · 치명 실패 · 기절 성공
            Use(hero, "bash", thief);  // 기절
            Assert.That(thief.EscapeRound, Is.EqualTo(5));
        }

        private static void RunUntil(BattleState b, System.Func<bool> done, int limit = 60)
        {
            int guard = 0;
            while (!done() && b.Phase != BattlePhase.Ended && guard++ < limit)
            {
                if (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn(); else b.Pass();
            }
        }

        [Test]
        public void ThiefAfterSteal_RunsBack_ThenEscapesAfterTwoRounds()
        {
            var hero = factory.FromAdventurer(TestData.Hero(db, "swordsman"));
            var d1 = factory.FromMonster("dummy");
            var d2 = factory.FromMonster("dummy");
            var thief = factory.FromMonster("thief");
            thief.HasStolen = true; thief.StolenItems.Add("potion");
            var b = TestData.Battle(db, new FakeRandom { DefaultDouble = 0.99 }, new[] { hero }, new[] { thief, d1, d2 }, factory, inv: bag);
            b.Start();

            RunUntil(b, () => thief.EscapeRound > 0);
            int runRound = b.Round;
            Assert.That(b.Formation.RankOf(thief), Is.EqualTo(3));          // 뒤로 2칸
            Assert.That(thief.Effects.Sum(DataKeys.Stat.Dodge), Is.EqualTo(10));
            Assert.That(thief.EscapeRound, Is.EqualTo(runRound + 2));

            RunUntil(b, () => thief.Escaped, 200);
            Assert.That(thief.Escaped, Is.True);
            Assert.That(b.Round, Is.EqualTo(runRound + 2));
            Assert.That(b.KilledMonsterIds, Is.Empty);                         // 처치 아님
            Assert.That(b.Formation.Side(BattleSide.Enemy).Contains(thief), Is.False);
            Assert.That(bag.Count("potion"), Is.EqualTo(2));                   // 약탈품은 처음부터 가방에서 빠져 있던 것 (여기선 직접 넣음)
        }

        [Test]
        public void OnlyThiefLeft_EscapeEndsBattleAsVictory()
        {
            var hero = factory.FromAdventurer(TestData.Hero(db, "swordsman"));
            var thief = factory.FromMonster("thief");
            thief.HasStolen = true; thief.EscapeRound = 1;
            var b = TestData.Battle(db, new FakeRandom { DefaultDouble = 0.99 }, new[] { hero }, new[] { thief }, factory, inv: bag);
            b.Start();
            RunUntil(b, () => false);
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(thief.Escaped, Is.True);
        }

        [Test]
        public void WeightedVariants_FollowWeights()
        {
            var f = new CombatantFactory(db);
            for (int seed = 1; seed <= 20; seed++)
                Assert.That(f.RollEncounter("weighted", new SeededRandom(seed)).Single().DefinitionId, Is.EqualTo("demon"));
        }
    }
}
