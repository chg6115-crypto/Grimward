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
            hpBase = 15, hpPerVitality = 3, attackStrengthDivisor = 2, accuracyPerDexterity = 2, accuracyPerLevel = 2,
            dodgePerDexterity = 3, dodgePerceptionDivisor = 2, speedStatDivisor = 3, critBase = 0.03, critPerDexterity = 0.005,
            baseHitChance = 0.85, hitChanceMin = 0.05, hitChanceMax = 0.95, critMin = 0.05, critMultiplier = 1.5,
            damageReductionCap = 0.8, minHitDamage = 1, injuryMaxPerAdventurer = 2, injuryHpThreshold = 0.3, injuryThresholdChance = 0.5, nightPartyAccuracyPenalty = 0.1, nightEnemyAccuracyBonus = 0.1, speedRollDie = 8,
            ranksPerSide = 4, itemsPerTurn = 1, fleeHpDamageRatio = 0.2, aiBuffChance = 0.3, aiHealThreshold = 0.5,
            defaultPerception = 3, bagSlots = 12,
            heroResistBase = 0, heroResistPerVitality = 0, heroDebuffResistPerIntelligence = 0, stunRecoveryResist = 0.3, mapVisionRange = 1, perceptionDetectBase = 0.1, perceptionDetectPerPoint = 0.05, perceptionDetectMin = 0.05, perceptionDetectMax = 0.9, travelFirstMinutes = 60, travelRepeatMinutes = 30, battleMinutes = 30, bossBattleMinutes = 60, fleeMinutes = 60, statWeightDivisor = 6, stabilityPerPerception = 0.04, stabilityMax = 0.4,
            monsterHpVariance = 0.15, monsterAccuracyDodgeVariance = 2, monsterSpeedVariance = 1
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
                Skill("bonk", All, "enemy", All),
                Skill("cut", All, "enemy", All),
                Skill("aid", All, "ally", damaging: false),
                Skill("bash", All, "enemy", All),
                Skill("zap", All, "enemy", All),
                Skill("hex", All, "enemy", All, damaging: false),
            };
            skills.Find(s => s.id == "bash").effects = new[]
            {
                new EffectDefinition { kind = "stun", duration = 1, target = "target", chance = 0.8 }
            };
            skills.Find(s => s.id == "zap").damageType = "magical";
            var snatch = Skill("snatch", new[] { 1, 2 }, "enemy", new[] { 1, 2 }, dmg: -0.3);
            snatch.steal = true;
            skills.Add(snatch);
            var smash = Skill("smash", All, "enemy", All);
            smash.weights = new StatWeightDefinition { strength = 3 };
            smash.variance = 0.5;
            skills.Add(smash);
            var aim = Skill("aim", All, "enemy", All);
            aim.weights = new StatWeightDefinition { dexterity = 3 };
            aim.variance = 0.1;
            skills.Add(aim);
            var mend = Skill("mend", All, "ally", damaging: false);
            mend.heal = new HealDefinition { min = 3, max = 5 };
            mend.weights = new StatWeightDefinition { intelligence = 3 };
            skills.Add(mend);
            skills.Find(s => s.id == "hex").effects = new[] { Stat("accuracy", -10, 2) };
            skills.Find(s => s.id == "bonk").injury = new SkillInjuryDefinition { injuryId = "concussion", chance = 0.3 };
            skills.Find(s => s.id == "cut").injury = new SkillInjuryDefinition { injuryId = "shallow_cut", chance = 0.2 };
            var aid = skills.Find(s => s.id == "aid");
            aid.heal = new HealDefinition { min = 3, max = 3 };
            aid.treatInjury = true;
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
                        id = "captain", name = "대장", hp = 30, attackMin = 4, attackMax = 7, accuracy = 12, dodge = 8,
                        speed = 5, armor = 0, crit = 0.05, formationResist = 0.5, types = new[] { "normal" },
                        skills = new[] { "summon", "hit" }, family = "goblin",
                        passives = new[]
                        {
                            new PassiveDefinition
                            {
                                id = "commander", name = "지휘관", kind = "allyAura",
                                effects = new[] { Stat("armor", 0.1, 0), Stat("accuracy", 5, 0), Stat("maxHpPct", 0.5, 0) }
                            }
                        }
                    },
                    new MonsterDefinition
                    {
                        id = "brute", name = "거구", hp = 40, size = 2, actionsPerTurn = 2, attackMin = 4, attackMax = 7, accuracy = 12, dodge = 6,
                        speed = 5, armor = 0, crit = 0.05, formationResist = 0.7, types = new[] { "normal" },
                        skills = new[] { "hit" }, family = "goblin",
                        passives = new[]
                        {
                            new PassiveDefinition
                            {
                                id = "bulwark", name = "거구의 벽", kind = "guardBackline",
                                ranks = new[] { 1, 2 }, protectedRanks = new[] { 3, 4 }
                            }
                        }
                    },
                    new MonsterDefinition
                    {
                        id = "thief", name = "소매치기", hp = 15, attackMin = 3, attackMax = 5, accuracy = 12, dodge = 25,
                        speed = 6, armor = 0, crit = 0.05, formationResist = 0.2, types = new[] { "normal" },
                        skills = new[] { "snatch", "hit" }, family = "goblin"
                    },
                    new MonsterDefinition
                    {
                        id = "golem", name = "골렘", hp = 50, attackMin = 4, attackMax = 7, accuracy = 10, dodge = 0,
                        speed = 3, armor = 0.3, magicResist = 0.5, crit = 0.05, formationResist = 0.2, types = new[] { "normal" },
                        skills = new[] { "hit" }, family = "golem",
                        resists = new ResistDefinition { stun = 0.4, poison = 0.3, bleed = 0.3, burn = 0.3, debuff = 0.3 },
                        immunities = new[] { "poison", "bleed" }
                    },
                    new MonsterDefinition
                    {
                        id = "wall", name = "벽", hp = 40, attackMin = 4, attackMax = 7, accuracy = 12, dodge = 6,
                        speed = 5, armor = 0, crit = 0.05, formationResist = 1.2, types = new[] { "normal" },
                        skills = new[] { "hit" }, family = "goblin",
                        passives = new[]
                        {
                            new PassiveDefinition
                            {
                                id = "bulwark", name = "거구의 벽", kind = "allyAura",
                                effects = new[] { Stat("formationResist", 0.7, 0) }
                            }
                        }
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
                    },
                    new EncounterDefinition
                    {
                        id = "boss_any", name = "보스 무작위", variants = new[] { "boss_a", "boss_b" }
                    },
                    new EncounterDefinition
                    {
                        id = "weighted", name = "가중치", variants = new[] { "boss_a", "boss_b" }, variantWeights = new[] { 0, 1 }
                    },
                    new EncounterDefinition
                    {
                        id = "boss_a", name = "보스 A",
                        slots = new[] { new EncounterSlotDefinition { candidates = new[] { "boss" }, chance = 1 } }
                    },
                    new EncounterDefinition
                    {
                        id = "boss_b", name = "보스 B",
                        slots = new[] { new EncounterSlotDefinition { candidates = new[] { "demon" }, chance = 1 } }
                    }
                },
                items = new[]
                {
                    new ItemDefinition { id = "potion", name = "회복약", heal = 10, stack = 5 },
                    new ItemDefinition { id = "herb", name = "약초", heal = 2, curePoison = true, stack = 5 },
                    new ItemDefinition { id = "key", name = "열쇠", stack = 5 },
                    new ItemDefinition { id = "bandage", name = "붕대", treatInjury = true, cureBleed = true, stack = 3 }
                },
                injuries = new[]
                {
                    new InjuryDefinition
                    {
                        id = "shallow_cut", name = "베임", fieldTreatable = true,
                        minor = new InjuryStageDefinition { name = "얕은 베임", bleedPerTurn = 1, bleedRounds = 3 },
                        major = new InjuryStageDefinition { name = "깊은 베임", bleedPerTurn = 2, bleedRounds = 4 }
                    },
                    new InjuryDefinition
                    {
                        id = "concussion", name = "뇌진탕", healedByRest = true,
                        minor = new InjuryStageDefinition { name = "뇌진탕 (경상)", effects = new[] { Stat("accuracy", -5, 0) } },
                        major = new InjuryStageDefinition { name = "뇌진탕 (중상)", effects = new[] { Stat("accuracy", -10, 0) } }
                    }
                },
                maps = new[] { TestMap() },
                investigations = new[]
                {
                    new InvestigationDefinition
                    {
                        id = "box", name = "상자", supplyItem = "key",
                        good = new InvestigationResultDefinition { chance = 0.6, text = "가득", rewards = new[] { new ItemAmountDefinition { item = "potion", amount = 2 } } },
                        normal = new InvestigationResultDefinition { chance = 0.3, text = "조금", rewards = new[] { new ItemAmountDefinition { item = "potion", amount = 1 } } },
                        bad = new InvestigationResultDefinition { chance = 0.1, text = "빈 상자" }
                    }
                },
                monsterTypes = new[]
                {
                    new NameDefinition { id = "normal", name = "일반" },
                    new NameDefinition { id = "unholy", name = "불경" }
                }
            };
        }

        /// <summary>
        /// 테스트 맵:  n1(시작) ─ n2(전투) ─ n3(약초 2, 막다른 곳)
        ///               \        /
        ///                n4(빈 노드)
        /// </summary>
        public static MapDefinition TestMap() => new MapDefinition
        {
            id = "test_map", name = "테스트 맵", radius = 2, startNode = "n1",
            nodes = new[]
            {
                new MapNodeDefinition { id = "n1", name = "입구", q = 0, r = 0 },
                new MapNodeDefinition { id = "n2", name = "전투", q = 1, r = 0,
                    events = new[] { new NodeEventDefinition { kind = "battle", encounter = "two" } } },
                new MapNodeDefinition { id = "n3", name = "약초밭", q = 2, r = 0,
                    events = new[] { new NodeEventDefinition { kind = "gather", item = "herb", amount = 2 } } },
                new MapNodeDefinition { id = "n4", name = "빈 터", q = 0, r = 1 },
                new MapNodeDefinition { id = "n5", name = "상자", q = 1, r = 1,
                    events = new[] { new NodeEventDefinition { kind = "investigate", objectId = "box" } } }
            },
            edges = new[]
            {
                new MapEdgeDefinition { a = "n1", b = "n2" }, new MapEdgeDefinition { a = "n2", b = "n3" },
                new MapEdgeDefinition { a = "n1", b = "n4" }, new MapEdgeDefinition { a = "n4", b = "n2" },
                new MapEdgeDefinition { a = "n2", b = "n5" }
            }
        };

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
