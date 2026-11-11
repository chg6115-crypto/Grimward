using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using NUnit.Framework;

namespace Grimward.Tests
{
    public class CombatRuleTests
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
            rng = new FakeRandom();
            ctx = new BattleContext(db.Balance, rng, new Formation(4), factory, false) { Round = 1 };
            resolver = new SkillResolver(ctx);
        }

        private Combatant AddHero(string classId = "swordsman")
        {
            var c = factory.FromAdventurer(TestData.Hero(db, classId));
            ctx.Formation.Add(c);
            return c;
        }

        private Combatant AddEnemy(string id = "dummy")
        {
            var c = factory.FromMonster(id);
            ctx.Formation.Add(c);
            return c;
        }

        private Combatant Act(Combatant actor, string skillId, Combatant target)
        {
            ctx.CurrentActor = actor;
            resolver.Resolve(actor, db.GetSkill(skillId), target);
            return actor;
        }

        // ── 진형 ──

        [Test]
        public void Formation_FillsFromRankOne_AndCompactsAfterDeath()
        {
            var a = AddEnemy(); var b = AddEnemy(); var c = AddEnemy();
            Assert.That(ctx.Formation.RankOf(a), Is.EqualTo(1));
            a.TakeDamage(100);
            ctx.Formation.Compact();
            Assert.That(ctx.Formation.RankOf(b), Is.EqualTo(1));
            Assert.That(ctx.Formation.RankOf(c), Is.EqualTo(2));
        }

        [Test]
        public void Formation_ShiftSwapsWithDestinationAndClamps()
        {
            var a = AddEnemy(); var b = AddEnemy(); var c = AddEnemy();
            Assert.That(ctx.Formation.Shift(a, 2), Is.EqualTo(3));
            Assert.That(ctx.Formation.RankOf(c), Is.EqualTo(1)); // 목적지 전투원과 교환
            Assert.That(ctx.Formation.RankOf(b), Is.EqualTo(2));
            Assert.That(ctx.Formation.Shift(b, -5), Is.EqualTo(1)); // 1번 아래로 가지 않음
        }

        // ── 행동 순서 ──

        [Test]
        public void TurnOrder_SpeedPlusRoll_TieBrokenByBaseSpeed()
        {
            var hero = AddHero();          // 속도 6
            var dummy = AddEnemy();        // 속도 4
            rng.Ints(1, 3);                // 6+1=7, 4+3=7 → 동률 → 기본 속도 높은 쪽
            var order = TurnOrder.Roll(ctx.Formation.AllAlive(), rng, db.Balance);
            Assert.That(order[0].Combatant, Is.SameAs(hero));
            Assert.That(order[0].Initiative, Is.EqualTo(7));

            rng.Ints(1, 8);                // 7 vs 12
            order = TurnOrder.Roll(ctx.Formation.AllAlive(), rng, db.Balance);
            Assert.That(order[0].Combatant, Is.SameAs(dummy));
        }

        // ── 상태 효과 ──

        [Test]
        public void StatusEffect_RefreshesInsteadOfStacking()
        {
            var hero = AddHero(); AddEnemy();
            Act(hero, "cry", hero);
            Act(hero, "cry", hero);
            Assert.That(hero.Effects.Sum(DataKeys.Stat.DamagePct), Is.EqualTo(0.15).Within(1e-9));
            Assert.That(hero.Effects.Sum(DataKeys.Stat.Accuracy), Is.EqualTo(5).Within(1e-9));
        }

        [Test]
        public void StatusEffect_AppliedOnOwnTurn_LastsFullRounds()
        {
            var hero = AddHero(); AddEnemy();
            Act(hero, "cry", hero); // 3라운드, 자기 차례에 걸림
            hero.Effects.TickEndOfOwnTurn(); // 같은 차례 끝: 줄지 않음
            Assert.That(hero.Effects.All.First().Remaining, Is.EqualTo(3));
            hero.Effects.TickEndOfOwnTurn();
            hero.Effects.TickEndOfOwnTurn();
            Assert.That(hero.Effects.All.Count, Is.GreaterThan(0));
            hero.Effects.TickEndOfOwnTurn();
            Assert.That(hero.Effects.All.Count, Is.EqualTo(0));
        }

        [Test]
        public void StatusEffect_DoubledAgainstType()
        {
            var hero = AddHero();
            var def = TestData.Stat(DataKeys.Stat.DamagePct, 0.1, 3, "target", "unholy");
            hero.Effects.Apply(new ActiveEffect("blessing", "축복", def), false);
            Assert.That(hero.Effects.Sum(DataKeys.Stat.DamagePct, new[] { "normal" }), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(hero.Effects.Sum(DataKeys.Stat.DamagePct, new[] { "unholy" }), Is.EqualTo(0.2).Within(1e-9));
        }

        // ── 기술 처리 ──

        [Test]
        public void Multi_HitsEachTargetSeparately_AndSkipsNothingAlive()
        {
            var hero = AddHero();
            var a = AddEnemy(); var b = AddEnemy();
            Act(hero, "sweep", a); // 공격 6 × 0.6 = 3.6 → 3
            Assert.That(a.Hp, Is.EqualTo(7));
            Assert.That(b.Hp, Is.EqualTo(7));
        }

        [Test]
        public void Multi_TargetsFixedAtStart_DeadTargetNotRetargeted()
        {
            var hero = AddHero();
            var a = AddEnemy(); var b = AddEnemy(); var c = AddEnemy();
            a.TakeDamage(8); // HP 2 → 휩쓸기 3으로 사망
            Act(hero, "sweep", a);
            Assert.That(a.IsAlive, Is.False);
            Assert.That(b.Hp, Is.EqualTo(7));
            Assert.That(c.Hp, Is.EqualTo(10)); // 3번 칸은 처음부터 대상이 아님
            Assert.That(ctx.Formation.RankOf(a), Is.EqualTo(1)); // 정리는 행동 뒤
            Assert.That(ctx.KilledMonsterIds, Is.EqualTo(new[] { "dummy" }));
        }

        [Test]
        public void Miss_WhenRollAboveHitChance()
        {
            var hero = AddHero(); var a = AddEnemy();
            rng.Doubles(0.9); // 명중률 0.83
            Act(hero, "hit", a);
            Assert.That(a.Hp, Is.EqualTo(10));
            Assert.That(ctx.Log.Any(e => e.Kind == BattleEventKind.Missed), Is.True);
        }

        [Test]
        public void Pull_MovesTargetForwardAndSwaps()
        {
            var hero = AddHero();
            var front = AddEnemy(); var mid = AddEnemy(); var back = AddEnemy("back_dummy");
            Act(hero, "pull", back); // 확률 1.0 − 0.1
            Assert.That(ctx.Formation.RankOf(back), Is.EqualTo(1));
            Assert.That(ctx.Formation.RankOf(front), Is.EqualTo(3));
            Assert.That(ctx.Formation.RankOf(mid), Is.EqualTo(2));
        }

        [Test]
        public void Displacement_CanBeResisted()
        {
            var hero = AddHero();
            var front = AddEnemy(); AddEnemy();
            rng.Doubles(0.5, 0.5, 0.85); // 명중, 치명 아님, 이동 판정 0.85 ≥ 1.0 − 0.2
            Act(hero, "push", front);
            Assert.That(ctx.Formation.RankOf(front), Is.EqualTo(1));
            Assert.That(ctx.Log.Any(e => e.Kind == BattleEventKind.DisplaceResisted), Is.True);
        }

        [Test]
        public void Heal_IsCappedAtMaxHp()
        {
            var mage = AddHero("mage"); AddEnemy();
            mage.TakeDamage(3);
            Act(mage, "heal", mage);
            Assert.That(mage.Hp, Is.EqualTo(mage.MaxHp));
        }

        [Test]
        public void TypeBonus_AppliesOnlyToMatchingType()
        {
            var hero = AddHero();
            var demon = AddEnemy("demon");
            Act(hero, "holy", demon); // 6 × 1.5 = 9
            Assert.That(demon.Hp, Is.EqualTo(45 - 9));
        }

        [Test]
        public void Summon_TwoWhenAlone_ThenCooldown()
        {
            AddHero();
            var boss = AddEnemy("boss");
            Act(boss, "summon", boss);
            Assert.That(ctx.Formation.Count(BattleSide.Enemy), Is.EqualTo(3));
            Assert.That(SkillTargeting.CanUse(ctx, boss, db.GetSkill("summon")), Is.False);
            ctx.Round = 3;
            Assert.That(SkillTargeting.CanUse(ctx, boss, db.GetSkill("summon")), Is.False);
            ctx.Round = 4;
            Assert.That(SkillTargeting.CanUse(ctx, boss, db.GetSkill("summon")), Is.True);
        }

        [Test]
        public void Skill_UnusableFromWrongRank()
        {
            var hero = AddHero(); AddEnemy();
            Assert.That(SkillTargeting.CanUse(ctx, hero, db.GetSkill("back_only"), out var reason), Is.False);
            Assert.That(reason, Does.Contain("1번"));
        }

        [Test]
        public void EnemyAI_MovesTowardUsableRank()
        {
            AddHero();
            var back = AddEnemy("back_dummy"); AddEnemy(); AddEnemy();
            var decision = new EnemyAI(ctx).Decide(back);
            Assert.That(decision.Skill, Is.Null);
            Assert.That(decision.MoveDelta, Is.EqualTo(1));
        }
    }
}
