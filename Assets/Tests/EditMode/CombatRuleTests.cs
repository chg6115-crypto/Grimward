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
        public void StatusEffect_Stacks_EachWithOwnDuration()
        {
            var hero = AddHero(); AddEnemy();
            Act(hero, "cry", hero);
            hero.Effects.TickEndOfOwnTurn(); // 같은 차례: 줄지 않음
            hero.Effects.TickEndOfOwnTurn(); // 첫 효과 3 → 2
            Act(hero, "cry", hero);          // 두 번째 효과 3
            Assert.That(hero.Effects.Sum(DataKeys.Stat.DamagePct), Is.EqualTo(0.30).Within(1e-9));
            Assert.That(hero.Effects.Sum(DataKeys.Stat.Accuracy), Is.EqualTo(10).Within(1e-9));
            hero.Effects.TickEndOfOwnTurn(); hero.Effects.TickEndOfOwnTurn(); hero.Effects.TickEndOfOwnTurn();
            Assert.That(hero.Effects.Sum(DataKeys.Stat.DamagePct), Is.EqualTo(0.15).Within(1e-9)); // 먼저 건 효과만 끝남
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
        public void Encounter_Variants_PickOneAtRandom()
        {
            var a = factory.RollEncounter("boss_any", new FakeRandom().Ints(0));
            var b = factory.RollEncounter("boss_any", new FakeRandom().Ints(1));
            Assert.That(a.Single().DefinitionId, Is.EqualTo("boss"));
            Assert.That(b.Single().DefinitionId, Is.EqualTo("demon"));
        }

        [Test]
        public void AllyAura_AppliesToOthers_AndEndsWhenHolderDies()
        {
            AddHero();
            var captain = AddEnemy("captain"); var g = AddEnemy();
            PassiveRules.RefreshAuras(ctx);
            Assert.That(g.EffectiveArmor - g.Armor, Is.EqualTo(0.1).Within(1e-9));
            Assert.That(captain.Effects.Sum(DataKeys.Stat.Armor), Is.EqualTo(0));
            Assert.That(g.MaxHp, Is.EqualTo(15)); // 10 + 50%
            Assert.That(g.Hp, Is.EqualTo(15));
            g.TakeDamage(3); // 12/15
            g.Effects.TickEndOfOwnTurn(); // 패시브는 줄지 않음
            Assert.That(g.Effects.Sum(DataKeys.Stat.Accuracy), Is.EqualTo(5).Within(1e-9));
            captain.TakeDamage(999);
            ctx.Formation.Compact();
            PassiveRules.RefreshAuras(ctx);
            Assert.That(g.Effects.All.Count, Is.EqualTo(0));
            Assert.That(g.MaxHp, Is.EqualTo(10)); // 넘치는 HP는 깎임
            Assert.That(g.Hp, Is.EqualTo(10));
        }

        [Test]
        public void AllyAura_ReachesSummonedAlly()
        {
            AddHero();
            var captain = AddEnemy("captain");
            Act(captain, "summon", captain); // 혼자 → 2마리
            var summoned = ctx.Formation.Alive(BattleSide.Enemy).Where(c => c != captain).ToList();
            Assert.That(summoned.Count, Is.EqualTo(2));
            Assert.That(summoned.All(c => c.Effects.Sum(DataKeys.Stat.Armor) > 0.09), Is.True);
        }

        [Test]
        public void SummonInFront_PushesAlliesBack_WithoutDisplacementRoll()
        {
            AddHero();
            var captain = AddEnemy("captain"); var g = AddEnemy();
            captain.Skills.First(s => s.id == "summon").summon.inFront = true;
            Act(captain, "summon", captain); // 둘 → 1마리, 앞칸에
            Assert.That(ctx.Formation.RankOf(captain), Is.EqualTo(2));
            Assert.That(ctx.Formation.RankOf(g), Is.EqualTo(3));
            Assert.That(ctx.Formation.At(BattleSide.Enemy, 1).DefinitionId, Is.EqualTo("dummy"));
            Assert.That(ctx.Log.Any(e => e.Message.Contains("물러남")), Is.True);
        }

        [Test]
        public void Summon_MaxAlive_BlocksUntilOneDies()
        {
            AddHero();
            var captain = AddEnemy("captain");
            var summon = captain.Skills.First(s => s.id == "summon");
            summon.summon.maxAlive = 1;
            Act(captain, "summon", captain); // 혼자 → 2마리지만 최대 1
            var minions = ctx.Formation.Alive(BattleSide.Enemy).Where(c => c != captain).ToList();
            Assert.That(minions.Count, Is.EqualTo(1));
            Assert.That(SkillTargeting.ValidTargets(ctx, captain, summon), Is.Empty);
            minions[0].TakeDamage(999);
            ctx.Formation.Compact();
            Assert.That(SkillTargeting.ValidTargets(ctx, captain, summon), Is.Not.Empty);
        }

        [Test]
        public void GuardBackline_BlocksPullWhileGuardInFront()
        {
            var hero = AddHero();
            AddEnemy("brute"); var mid = AddEnemy(); var back = AddEnemy("back_dummy"); // 거구 1~2번, 3번, 4번
            Assert.That(ctx.Formation.RankOf(back), Is.EqualTo(4));
            Act(hero, "pull", back);
            Assert.That(ctx.Formation.RankOf(back), Is.EqualTo(4));
            Assert.That(ctx.Log.Any(e => e.Message.Contains("거구의 벽")), Is.True);
        }

        [Test]
        public void GuardBackline_NoEffectWhenGuardNotInFront()
        {
            var hero = AddHero();
            var a = AddEnemy(); var b = AddEnemy(); var brute = AddEnemy("brute"); // 1, 2, 거구 3~4
            Assert.That(PassiveRules.DisplacementGuard(ctx, b), Is.Null);
            Assert.That(ctx.Formation.RanksOf(brute), Is.EqualTo(new[] { 3, 4 }));
        }

        [Test]
        public void BigMonster_OccupiesTwoRanks_AndIsTargetableFromEither()
        {
            AddHero();
            var brute = AddEnemy("brute"); var g = AddEnemy();
            Assert.That(ctx.Formation.At(BattleSide.Enemy, 1), Is.SameAs(brute));
            Assert.That(ctx.Formation.At(BattleSide.Enemy, 2), Is.SameAs(brute));
            Assert.That(ctx.Formation.RankOf(g), Is.EqualTo(3));
            Assert.That(ctx.Formation.HasRoom(BattleSide.Enemy, 1), Is.True);
            Assert.That(ctx.Formation.HasRoom(BattleSide.Enemy, 2), Is.False);
            // 2번 칸만 노리는 기술도 거구를 맞힐 수 있다
            var hero = ctx.Formation.At(BattleSide.Party, 1);
            var only2 = TestData.Skill("only2", TestData.All, "enemy", new[] { 2 });
            Assert.That(SkillTargeting.ValidTargets(ctx, hero, only2), Is.EqualTo(new[] { brute }));
        }

        [Test]
        public void MonsterStats_RolledOnceWithinVariance_FixedWithoutRandom()
        {
            var fixedOne = factory.FromMonster("dummy");
            Assert.That(fixedOne.MaxHp, Is.EqualTo(10));
            var low = new CombatantFactory(db, new FakeRandom()).FromMonster("dummy");            // 모두 최솟값
            var high = new CombatantFactory(db, new FakeRandom { IntDefaultMin = false }).FromMonster("dummy");
            Assert.That(low.MaxHp, Is.EqualTo(9));   // 10 × 0.15 = 1.5 → ±1
            Assert.That(high.MaxHp, Is.EqualTo(11));
            Assert.That(low.Speed, Is.EqualTo(3));
            Assert.That(high.Accuracy, Is.EqualTo(12));
            Assert.That(low.Hp, Is.EqualTo(low.MaxHp));
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
