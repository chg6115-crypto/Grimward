using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using Grimward.Domain.Units;
using NUnit.Framework;

namespace Grimward.Tests
{
    /// <summary>상태이상 저항·면역·기절·마법 피해·진형 붕괴 저항 오라 (기획안 6·12장).</summary>
    public class StatusResistTests
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
            rng = new FakeRandom { DefaultDouble = 0.99 };
            ctx = new BattleContext(db.Balance, rng, new Formation(4), factory, false) { Round = 1 };
            resolver = new SkillResolver(ctx);
        }

        private Combatant Add(Combatant c) { ctx.Formation.Add(c); return c; }

        private void Use(Combatant actor, string skill, Combatant target)
        {
            ctx.CurrentActor = actor;
            resolver.Resolve(actor, db.GetSkill(skill), target);
        }

        [Test]
        public void Status_ChanceMinusResist()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman")));
            var golem = Add(factory.FromMonster("golem"));
            rng.Doubles(0.0, 0.99, 0.39); // 명중, 치명 실패, 기절 0.39 < 0.8 − 0.4
            Use(hero, "bash", golem);
            Assert.That(golem.Effects.HasStun, Is.True);

            var golem2 = Add(factory.FromMonster("golem"));
            rng.Doubles(0.0, 0.99, 0.41); // 0.41 ≥ 0.4 → 저항
            Use(hero, "bash", golem2);
            Assert.That(golem2.Effects.HasStun, Is.False);
            Assert.That(ctx.Log.Any(e => e.Kind == BattleEventKind.StatusResisted && e.Target == golem2), Is.True);
        }

        [Test]
        public void Status_Immune_NeverApplies()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "mage")));
            var golem = Add(factory.FromMonster("golem"));
            rng.Doubles(0.0, 0.99, 0.0);
            Use(hero, "poison", golem);
            Assert.That(golem.Effects.HasPoison, Is.False);
            Assert.That(ctx.Log.Last(e => e.Kind == BattleEventKind.StatusResisted).Message, Does.EndWith("면역"));
        }

        [Test]
        public void Debuff_UsesDebuffResist()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman")));
            var golem = Add(factory.FromMonster("golem"));
            rng.Doubles(0.0, 0.71); // 명중, 약화 0.71 ≥ 1 − 0.3
            Use(hero, "hex", golem);
            Assert.That(golem.Effects.Sum(DataKeys.Stat.Accuracy), Is.EqualTo(0));
        }

        [Test]
        public void HeroResists_FromVitalityAndIntelligence()
        {
            var file = TestData.File();
            file.balance.heroResistBase = 0.1; file.balance.heroResistPerVitality = 0.05;
            file.balance.heroDebuffResistPerIntelligence = 0.03;
            var db2 = new GameDatabase(file);
            var f2 = new CombatantFactory(db2);
            var sword = f2.FromAdventurer(TestData.Hero(db2, "swordsman")); // 체력 4, 지능 1
            var mage = f2.FromAdventurer(TestData.Hero(db2, "mage"));       // 체력 2, 지능 6
            Assert.That(sword.Resist(DataKeys.Resist.Stun), Is.EqualTo(0.3).Within(1e-9));
            Assert.That(sword.Resist(DataKeys.Resist.Bleed), Is.EqualTo(0.3).Within(1e-9));
            Assert.That(mage.Resist(DataKeys.Resist.Poison), Is.EqualTo(0.2).Within(1e-9));
            Assert.That(mage.Resist(DataKeys.Resist.Debuff), Is.EqualTo(0.28).Within(1e-9));
        }

        [Test]
        public void MagicalDamage_UsesMagicResistInsteadOfArmor()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman"))); // 공격 6~10, 최소값 굴림
            var golem = Add(factory.FromMonster("golem")); // 방어도 30%, 마법 저항 50%
            rng.Doubles(0.0, 0.99);
            Use(hero, "hit", golem);
            rng.Doubles(0.0, 0.99);
            Use(hero, "zap", golem);
            var hits = ctx.Log.Where(e => e.Kind == BattleEventKind.Damaged).Select(e => e.Amount).ToList();
            Assert.That(hits, Is.EqualTo(new[] { 4, 3 })); // floor(6×0.7), floor(6×0.5)
        }

        [Test]
        public void NegativeMagicResist_NoExtraDamage()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman")));
            var golem = Add(factory.FromMonster("golem"));
            golem.MagicResist = -0.5;
            rng.Doubles(0.0, 0.99);
            Use(hero, "zap", golem);
            Assert.That(ctx.Log.Last(e => e.Kind == BattleEventKind.Damaged).Amount, Is.EqualTo(6));
        }

        [Test]
        public void FormationResistAura_AddsToAllies()
        {
            var hero = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman")));
            var wall = Add(factory.FromMonster("wall"));
            var dummy = Add(factory.FromMonster("dummy"));
            PassiveRules.RefreshAuras(ctx);
            Assert.That(dummy.EffectiveFormationResist, Is.EqualTo(0.9).Within(1e-9));
            Assert.That(wall.EffectiveFormationResist, Is.EqualTo(1.2).Within(1e-9)); // 본인에게는 걸리지 않음
            rng.Doubles(0.0, 0.99, 0.15); // 끌어당기기 1.0 − 0.9 = 0.1 → 0.15면 저항
            Use(hero, "pull", dummy);
            Assert.That(ctx.Formation.RankOf(dummy), Is.EqualTo(2)); // 그대로
            Assert.That(ctx.Log.Any(e => e.Kind == BattleEventKind.DisplaceResisted), Is.True);
        }

        [Test]
        public void Stun_SkipsTurn_ThenRaisesStunResist()
        {
            var hero = factory.FromAdventurer(TestData.Hero(db, "swordsman"));
            var dummy = factory.FromMonster("dummy");
            dummy.Effects.Apply(new ActiveEffect("bash", "bash",
                new EffectDefinition { kind = DataKeys.EffectKind.Stun, duration = 1, target = "target" }), false);
            var b = TestData.Battle(db, rng, new[] { hero }, new[] { dummy }, factory);
            b.Start();
            int guard = 0;
            while (!b.Log.Any(e => e.Kind == BattleEventKind.Stunned) && guard++ < 10) b.Pass();
            Assert.That(b.Log.Any(e => e.Kind == BattleEventKind.Stunned && e.Target == dummy), Is.True);
            Assert.That(dummy.Effects.HasStun, Is.False);
            Assert.That(dummy.Resist(DataKeys.Resist.Stun), Is.EqualTo(0.3).Within(1e-9));
            Assert.That(b.Log.Any(e => e.Kind == BattleEventKind.SkillUsed && e.Actor == dummy), Is.False);
        }

        // ── 전투불능 ──

        private BattleState KnockdownBattle(Adventurer downed, out Adventurer other, string enemy = "dummy")
        {
            downed.CurrentHp = 0;
            other = TestData.Hero(db, "swordsman", "other");
            var party = new[] { factory.FromAdventurer(other), factory.FromAdventurer(downed) };
            var b = TestData.Battle(db, new FakeRandom { DefaultDouble = 0.0 }, party, new[] { factory.FromMonster(enemy) }, factory);
            b.Start();
            return b;
        }

        private static void Fight(BattleState b)
        {
            int guard = 0;
            while (b.Phase != BattlePhase.Ended && guard++ < 50)
            {
                if (b.Phase == BattlePhase.EnemyTurn) { b.RunEnemyTurn(); continue; }
                var hit = b.CurrentSkills.First(s => s.id == "hit");
                b.UseSkill(hit, b.TargetsFor(hit).First());
            }
        }

        [Test]
        public void Knockdown_Victory_GetsUpWithHp1AndSevereInjury()
        {
            var a = TestData.Hero(db, "mage", "down");
            var b = KnockdownBattle(a, out _);
            Fight(b);
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(a.IsDead, Is.False);
            Assert.That(a.CurrentHp, Is.EqualTo(1));
            Assert.That(a.KnockdownsThisExpedition, Is.EqualTo(1));
            Assert.That(a.Injuries.Single().Severe, Is.True);
        }

        [Test]
        public void Knockdown_SecondInSameExpedition_Dies()
        {
            var a = TestData.Hero(db, "mage", "down");
            a.KnockdownsThisExpedition = 1;
            var b = KnockdownBattle(a, out _);
            Fight(b);
            Assert.That(a.IsDead, Is.True);
        }

        [Test]
        public void Knockdown_Flee_AlsoGetsUp()
        {
            var a = TestData.Hero(db, "mage", "down");
            var b = KnockdownBattle(a, out _);
            while (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn();
            b.Flee();
            Assert.That(a.IsDead, Is.False);
            Assert.That(a.CurrentHp, Is.EqualTo(1));
        }

        [Test]
        public void Knockdown_ExistingMinorInjury_Worsens()
        {
            var a = TestData.Hero(db, "mage", "down");
            a.Injuries.Add(new InjuryState("concussion"));
            a.Injuries.Add(new InjuryState("shallow_cut", true));
            var b = KnockdownBattle(a, out _);
            Fight(b);
            Assert.That(a.Injuries.Count, Is.EqualTo(2));
            Assert.That(a.Injuries.All(i => i.Severe), Is.True);
        }

        [Test]
        public void Knockdown_WithSevereInjuriesFull_Dies()
        {
            var a = TestData.Hero(db, "mage", "down");
            a.Injuries.Add(new InjuryState("concussion", true));
            a.Injuries.Add(new InjuryState("shallow_cut", true));
            var b = KnockdownBattle(a, out _);
            Fight(b);
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(a.IsDead, Is.True);
            Assert.That(b.Log.Last(e => e.Kind == BattleEventKind.KnockedDown).Message, Does.Contain("중상 가득"));
        }

        [Test]
        public void Stun_TwoActionMonster_SkipsWholeTurn()
        {
            var hero = factory.FromAdventurer(TestData.Hero(db, "swordsman"));
            var brute = factory.FromMonster("brute"); // 차례마다 2회 행동
            brute.Effects.Apply(new ActiveEffect("bash", "bash",
                new EffectDefinition { kind = DataKeys.EffectKind.Stun, duration = 1, target = "target" }), false);
            var b = TestData.Battle(db, rng, new[] { hero }, new[] { brute }, factory);
            b.Start();
            int guard = 0;
            while (!b.Log.Any(e => e.Kind == BattleEventKind.Stunned) && guard++ < 10) b.Pass();
            int stunnedAt = b.Log.ToList().FindIndex(e => e.Kind == BattleEventKind.Stunned);
            int nextRound = b.Log.ToList().FindIndex(stunnedAt, e => e.Kind == BattleEventKind.RoundStarted);
            var between = b.Log.Skip(stunnedAt).Take((nextRound < 0 ? b.Log.Count : nextRound) - stunnedAt);
            Assert.That(between.Any(e => e.Kind == BattleEventKind.SkillUsed && e.Actor == brute), Is.False); // 두 행동 모두 건너뜀
            Assert.That(brute.Resist(DataKeys.Resist.Stun), Is.EqualTo(0.3).Within(1e-9));
        }

        [Test]
        public void Defeat_EveryoneDies()
        {
            var a = TestData.Hero(db, "mage", "solo");
            a.CurrentHp = 1;
            var b = TestData.Battle(db, new FakeRandom { DefaultDouble = 0.0 }, new[] { factory.FromAdventurer(a) },
                new[] { factory.FromMonster("demon") }, factory);
            b.Start();
            int guard = 0;
            while (b.Phase != BattlePhase.Ended && guard++ < 20)
            {
                if (b.Phase == BattlePhase.EnemyTurn) b.RunEnemyTurn(); else b.Pass();
            }
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(a.IsDead, Is.True);
        }

        [Test]
        public void BeginExpedition_ResetsKnockdowns()
        {
            var a = TestData.Hero(db, "mage");
            a.KnockdownsThisExpedition = 1;
            a.BeginExpedition();
            Assert.That(a.KnockdownsThisExpedition, Is.EqualTo(0));
        }

        // ── 레벨에 따라 오르는 버프 (사제 축복, 기획안 6장) ─────────────────

        private static EffectDefinition Scaled(string stat, double value, double per, int step, string doubled = null) =>
            new EffectDefinition { kind = "stat", stat = stat, value = value, duration = 3, target = "target",
                doubledVsType = doubled, perLevel = per, levelStep = step };

        [Test]
        public void EffectValue_ScalesWithLevelStep()
        {
            var every2 = Scaled(DataKeys.Stat.DamagePct, 0.1, 0.05, 2);   // Lv3·5마다 +5%
            Assert.That(new[] { 1, 2, 3, 4, 5, 6 }.Select(l => System.Math.Round(every2.ValueAt(l), 3)),
                Is.EqualTo(new[] { 0.1, 0.1, 0.15, 0.15, 0.2, 0.2 }));
            var every1 = Scaled(DataKeys.Stat.PoisonResist, 0.05, 0.05, 1); // 레벨 × 5%p
            Assert.That(new[] { 1, 2, 3, 4, 5, 6 }.Select(l => System.Math.Round(every1.ValueAt(l), 3)),
                Is.EqualTo(new[] { 0.05, 0.1, 0.15, 0.2, 0.25, 0.3 }));
            var flat = Scaled(DataKeys.Stat.Accuracy, 10, 0, 0);
            Assert.That(flat.ValueAt(6), Is.EqualTo(10));
        }

        private GameDatabase DbWithBlessing()
        {
            var file = TestData.File();
            var bless = TestData.Skill("bless", TestData.All, "ally", damaging: false);
            var effects = new System.Collections.Generic.List<EffectDefinition>
            {
                Scaled(DataKeys.Stat.DamagePct, 0.1, 0.05, 2, "unholy"),
                Scaled(DataKeys.Stat.Armor, 0.1, 0.05, 2),
            };
            foreach (var r in new[] { DataKeys.Stat.StunResist, DataKeys.Stat.PoisonResist, DataKeys.Stat.BleedResist,
                         DataKeys.Stat.BurnResist, DataKeys.Stat.DebuffResist, DataKeys.Stat.MagicResist })
                effects.Add(Scaled(r, 0.05, 0.05, 1));
            bless.effects = effects.ToArray();
            file.skills = file.skills.Concat(new[] { bless }).ToArray();
            return new GameDatabase(file);
        }

        [Test]
        public void Blessing_RaisesAllResistsByCasterLevel()
        {
            db = DbWithBlessing(); factory = new CombatantFactory(db);
            ctx = new BattleContext(db.Balance, rng, new Formation(4), factory, false) { Round = 1 };
            resolver = new SkillResolver(ctx);
            var adv = TestData.Hero(db, "swordsman"); adv.Level = 3;
            var priest = Add(factory.FromAdventurer(adv));
            var ally = Add(factory.FromAdventurer(TestData.Hero(db, "mage")));
            var before = new[] { DataKeys.Resist.Stun, DataKeys.Resist.Poison, DataKeys.Resist.Bleed,
                DataKeys.Resist.Burn, DataKeys.Resist.Debuff }.Select(ally.Resist).ToArray();
            double magicBefore = ally.EffectiveMagicResist, armorBefore = ally.EffectiveArmor;

            Use(priest, "bless", ally);

            var after = new[] { DataKeys.Resist.Stun, DataKeys.Resist.Poison, DataKeys.Resist.Bleed,
                DataKeys.Resist.Burn, DataKeys.Resist.Debuff }.Select(ally.Resist).ToArray();
            for (int i = 0; i < 5; i++) Assert.That(after[i] - before[i], Is.EqualTo(0.15).Within(1e-9)); // Lv3 = 15%p
            Assert.That(ally.EffectiveMagicResist - magicBefore, Is.EqualTo(0.15).Within(1e-9));
            Assert.That(ally.EffectiveArmor - armorBefore, Is.EqualTo(0.15).Within(1e-9));               // Lv3 = 10 + 5
            Assert.That(ally.Effects.Sum(DataKeys.Stat.DamagePct), Is.EqualTo(0.15).Within(1e-9));
            // 저항 6종은 알림 한 줄로 묶인다
            Assert.That(ctx.Log.Count(e => e.Kind == BattleEventKind.EffectApplied && e.Message.Contains("저항")), Is.EqualTo(1));
        }

        [Test]
        public void Blessing_Stacks()
        {
            db = DbWithBlessing(); factory = new CombatantFactory(db);
            ctx = new BattleContext(db.Balance, rng, new Formation(4), factory, false) { Round = 1 };
            resolver = new SkillResolver(ctx);
            var priest = Add(factory.FromAdventurer(TestData.Hero(db, "swordsman"))); // Lv1
            var ally = Add(factory.FromAdventurer(TestData.Hero(db, "mage")));
            double before = ally.Resist(DataKeys.Resist.Poison);
            Use(priest, "bless", ally);
            Use(priest, "bless", ally);
            Assert.That(ally.Resist(DataKeys.Resist.Poison) - before, Is.EqualTo(0.1).Within(1e-9)); // 5%p × 2
        }
    }
}
