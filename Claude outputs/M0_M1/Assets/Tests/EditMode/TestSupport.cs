using System;
using System.Collections.Generic;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Units;

namespace Grimward.Tests
{
    /// <summary>미리 넣은 값을 순서대로 돌려주는 난수. 비면 기본값(실수 0.0 = 항상 명중·치명 실패 없음 주의, 정수 = 최솟값).</summary>
    public sealed class FakeRandom : IRandomSource
    {
        private readonly Queue<double> doubles = new Queue<double>();
        private readonly Queue<int> ints = new Queue<int>();
        public double DefaultDouble { get; set; } = 0.5;
        /// <summary>true면 정수 기본값은 최솟값, false면 최댓값.</summary>
        public bool IntDefaultMin { get; set; } = true;

        public FakeRandom Doubles(params double[] values) { foreach (var v in values) doubles.Enqueue(v); return this; }
        public FakeRandom Ints(params int[] values) { foreach (var v in values) ints.Enqueue(v); return this; }

        public double NextDouble() => doubles.Count > 0 ? doubles.Dequeue() : DefaultDouble;

        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (ints.Count == 0) return IntDefaultMin ? minInclusive : maxInclusive;
            int v = ints.Dequeue();
            if (v < minInclusive || v > maxInclusive)
                throw new InvalidOperationException($"FakeRandom 값 {v}가 범위 [{minInclusive},{maxInclusive}] 밖");
            return v;
        }
    }

    /// <summary>테스트용 최소 데이터. 실제 수치는 gamedata.json에 있고, 여기 값은 규칙 확인용이다.</summary>
    public static class TestData
    {
        public static BalanceConfig Balance() => new BalanceConfig
        {
            dayStartMinute = 360, nightStartMinute = 1200, wakeUpMinute = 360,
            hpBase = 16, hpPerVitality = 3, attackStrengthDivisor = 2, accuracyPerDexterity = 2, accuracyPerLevel = 2,
            dodgePerDexterity = 3, dodgePerceptionDivisor = 2, speedStatDivisor = 3, critBase = 0.03, critPerDexterity = 0.005,
            baseHitChance = 0.85, hitChanceMin = 0.05, hitChanceMax = 0.95, critMin = 0.05, critMultiplier = 1.5,
            damageReductionCap = 0.8, nightPartyAccuracyPenalty = 0.1, nightEnemyAccuracyBonus = 0.1, speedRollDie = 8,
            ranksPerSide = 4, itemsPerTurn = 1, fleeHpDamageRatio = 0.2, aiBuffChance = 0.3, aiHealThreshold = 0.5,
            defaultPerception = 3
        };

        public static SkillDefinition Skill(string id, int[] use, string target, int[] targetRanks = null,
            bool multi = false, bool damaging = true, double dmg = 0, int acc = 0)
        {
            return new SkillDefinition
            {
                id = id, name = id, useRanks = use, target = target, targetRanks = targetRanks ?? new int[0],
                multi = multi, damaging = damaging,
                levels = new[] { new SkillLevel { accuracy = acc, damage = dmg, crit = 0 } }
            };
        }

        public static EffectDefinition Stat(string stat, double value, int duration, string target = "target",
            string doubled = null) =>
            new EffectDefinition { kind = "stat", stat = stat, value = value, duration = duration, target = target, doubledVsType = doubled };

        public static readonly int[] All = { 1, 2, 3, 4 };

        public static GameDataFile File()
        {
            var skills = new List<SkillDefinition>
            {
                Skill("hit", All, "enemy", All),
                Skill("front_hit", new[] { 1, 2 }, "enemy", new[] { 1, 2 }),
                Skill("sweep", All, "enemy", new[] { 1, 2 }, multi: true, dmg: -0.4),
                Skill("back_only", new[] { 3, 4 }, "enemy", All),
                Skill("pull", All, "enemy", new[] { 2, 3, 4 }),
                Skill("push", All, "enemy", new[] { 1 }),
                Skill("poison", All, "enemy", All, dmg: -0.3),
                Skill("heal", All, "ally", damaging: false),
                Skill("cry", All, "allAllies", damaging: false),
                Skill("summon", All, "allySlot", damaging: false),
                Skill("holy", All, "enemy", All),
            };
            skills.Find(s => s.id == "pull").displacement = new DisplacementDefinition { ranks = -2, chance = 1.0 };
            skills.Find(s => s.id == "push").displacement = new DisplacementDefinition { ranks = 2, chance = 1.0 };
            skills.Find(s => s.id == "poison").effects = new[]
            {
                new EffectDefinition { kind = "poison", duration = 3, target = "target", damagePerTurn = 2 }
            };
            skills.Find(s => s.id == "heal").heal = new HealDefinition { min = 5, max = 7 };
            skills.Find(s => s.id == "cry").effects = new[]
            {
                Stat("damagePct", 0.15, 3, "self"), Stat("accuracy", 5, 3, "allAllies")
            };
            var summon = skills.Find(s => s.id == "summon");
            summon.summon = new SummonDefinition { monsterId = "dummy", count = 1, countWhenAlone = 2 };
            summon.cooldown = 2;
            skills.Find(s => s.id == "holy").typeBonuses = new[] { new TypeBonusDefinition { type = "unholy", damage = 0.5 } };

            return new GameDataFile
            {
                balance = Balance(),
                classes = new[]
                {
                    new ClassDefinition
                    {
                        id = "swordsman", name = "검사", strength = 5, dexterity = 3, vitality = 4, intelligence = 1,
                        weaponMin = 4, weaponMax = 8, attackStat = "strength", armor = 0.05, formationResist = 0.4,
                        skills = new[] { "hit", "front_hit", "sweep", "cry" }
                    },
                    new ClassDefinition
                    {
                        id = "mage", name = "마법사", strength = 1, dexterity = 4, vitality = 2, intelligence = 6,
                        weaponMin = 2, weaponMax = 5, attackStat = "intelligence", armor = 0, formationResist = 0.2,
                        skills = new[] { "hit", "back_only", "heal", "poison" }
                    }
                },
                skills = skills.ToArray(),
                monsters = new[]
                {
                    new MonsterDefinition
                    {
                        id = "dummy", name = "허수아비", hp = 10, attackMin = 2, attackMax = 4, accuracy = 10, dodge = 10,
                        speed = 4, armor = 0, crit = 0.05, formationResist = 0.2, types = new[] { "normal" },
                        skills = new[] { "hit" }, family = "goblin"
                    },
                    new MonsterDefinition
                    {
                        id = "back_dummy", name = "후열 허수아비", hp = 8, attackMin = 2, attackMax = 4, accuracy = 10, dodge = 10,
                        speed = 5, armor = 0, crit = 0.05, formationResist = 0.1, types = new[] { "normal" },
                        skills = new[] { "back_only" }, family = "goblin"
                    },
                    new MonsterDefinition
                    {
                        id = "boss", name = "우두머리", hp = 30, attackMin = 4, attackMax = 7, accuracy = 12, dodge = 8,
                        speed = 5, armor = 0.05, crit = 0.05, formationResist = 0.5, types = new[] { "normal" },
                        skills = new[] { "summon", "hit" }, family = "goblin"
                    },
                    new MonsterDefinition
                    {
                        id = "demon", name = "악마", hp = 45, attackMin = 6, attackMax = 10, accuracy = 18, dodge = 12,
                        speed = 7, armor = 0, crit = 0.05, formationResist = 0.5, types = new[] { "unholy" },
                        skills = new[] { "hit" }, family = "demon"
                    }
                },
                encounters = new[]
                {
                    new EncounterDefinition
                    {
                        id = "two", name = "둘",
                        slots = new[]
                        {
                            new EncounterSlotDefinition { candidates = new[] { "dummy" }, chance = 1 },
                            new EncounterSlotDefinition { candidates = new[] { "dummy" }, chance = 1 }
                        }
                    }
                },
                items = new[]
                {
                    new ItemDefinition { id = "potion", name = "회복약", heal = 10, stack = 5 },
                    new ItemDefinition { id = "herb", name = "약초", heal = 2, curePoison = true, stack = 5 }
                }
            };
        }

        public static GameDatabase Db() => new GameDatabase(File());

        public static Adventurer Hero(GameDatabase db, string classId, string name = null)
        {
            var a = new Adventurer(name ?? classId, name ?? classId, db.GetClass(classId), db.Balance.defaultPerception, 3);
            a.RestoreFullHp(db.Balance);
            return a;
        }

        /// <summary>전투원 목록으로 전투를 만들고 시작한다.</summary>
        public static BattleState Battle(GameDatabase db, IRandomSource rng, IList<Combatant> party,
            IList<Combatant> enemies, CombatantFactory factory, bool night = false, BattleInventory inv = null)
        {
            var b = new BattleState(db, rng, party, enemies, night, inv, factory);
            return b;
        }
    }
}
