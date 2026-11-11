using System;

namespace Grimward.Domain.Data
{
    // 정의 데이터. 필드 이름 = JSON 키. Unity JsonUtility와 테스트 코드 양쪽에서 채운다.
    // 문자열 종류값(kind, target 등)은 GameDatabase가 검증한다.

    [Serializable]
    public sealed class GameDataFile
    {
        public BalanceConfig balance;
        public ClassDefinition[] classes;
        public SkillDefinition[] skills;
        public MonsterDefinition[] monsters;
        public EncounterDefinition[] encounters;
        public ItemDefinition[] items;
        public NameDefinition[] monsterTypes;
    }

    /// <summary>id → 표시 이름 (몬스터 타입 등).</summary>
    [Serializable]
    public sealed class NameDefinition
    {
        public string id;
        public string name;
    }

    [Serializable]
    public sealed class ClassDefinition
    {
        public string id;
        public string name;
        public string role;
        public int strength;
        public int dexterity;
        public int vitality;
        public int intelligence;
        public int weaponMin;
        public int weaponMax;
        /// <summary>"strength" 또는 "intelligence". 공격력 보너스에 쓰는 능력치.</summary>
        public string attackStat;
        public double armor;
        public double formationResist;
        /// <summary>v0.1 고정 장착 기술 4개.</summary>
        public string[] skills;
    }

    [Serializable]
    public sealed class MonsterDefinition
    {
        public string id;
        public string name;
        public int hp;
        public int attackMin;
        public int attackMax;
        public int accuracy;
        public int dodge;
        public int speed;
        public double armor;
        public double crit;
        public double formationResist;
        public string[] types;
        public string[] skills;
        /// <summary>의뢰 집계용 분류 (예: "goblin").</summary>
        public string family;
    }

    [Serializable]
    public sealed class SkillLevel
    {
        public int accuracy;
        public double damage;
        public double crit;
    }

    [Serializable]
    public sealed class EffectDefinition
    {
        /// <summary>"stat" | "poison"</summary>
        public string kind;
        /// <summary>stat 종류: damagePct · accuracy · crit · dodge · speed · damageTaken</summary>
        public string stat;
        public double value;
        public int duration;
        /// <summary>"target" | "self" | "allAllies"</summary>
        public string target;
        /// <summary>이 타입의 적을 상대할 때 효과 2배 (예: 축복 vs unholy).</summary>
        public string doubledVsType;
        /// <summary>poison: 차례 시작 시 피해.</summary>
        public int damagePerTurn;
    }

    [Serializable]
    public sealed class HealDefinition
    {
        public int min;
        public int max;
        /// <summary>0이면 미사용. 회복량에 지능 ÷ 값을 더한다.</summary>
        public int intelligenceDivisor;
    }

    [Serializable]
    public sealed class FixedDamageDefinition
    {
        public int min;
        public int max;
        /// <summary>피해에 지능 × 값을 더한다.</summary>
        public int intelligenceMultiplier;
    }

    [Serializable]
    public sealed class DisplacementDefinition
    {
        /// <summary>양수 = 뒤로 밀기, 음수 = 앞으로 당기기 (칸 수).</summary>
        public int ranks;
        /// <summary>기본 확률. 실제 = chance − 대상 저항.</summary>
        public double chance;
    }

    [Serializable]
    public sealed class SummonDefinition
    {
        public string monsterId;
        public int count;
        /// <summary>시전자 혼자 남았을 때 소환 수.</summary>
        public int countWhenAlone;
    }

    [Serializable]
    public sealed class TypeBonusDefinition
    {
        public string type;
        public double damage;
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string id;
        public string name;
        /// <summary>사용 가능한 자기 칸 (1~4).</summary>
        public int[] useRanks;
        /// <summary>"enemy" | "ally" | "self" | "allAllies" | "allySlot"(소환)</summary>
        public string target;
        /// <summary>target = enemy일 때 노릴 수 있는 적 칸 (1~4).</summary>
        public int[] targetRanks;
        /// <summary>true면 targetRanks의 적 전원에게 각각 판정.</summary>
        public bool multi;
        public bool damaging;
        public bool ignoreArmor;
        /// <summary>레벨 1~5. 비어 있으면 0 보정.</summary>
        public SkillLevel[] levels;
        public FixedDamageDefinition fixedDamage;
        public TypeBonusDefinition[] typeBonuses;
        public EffectDefinition[] effects;
        public HealDefinition heal;
        public DisplacementDefinition displacement;
        public SummonDefinition summon;
        public bool curePoison;
        /// <summary>사용 후 이 라운드 수 동안 사용 불가.</summary>
        public int cooldown;
        public string description;
    }

    [Serializable]
    public sealed class EncounterSlotDefinition
    {
        public string[] candidates;
        /// <summary>1이면 항상, 0.5면 50%로 등장.</summary>
        public double chance;
    }

    [Serializable]
    public sealed class EncounterDefinition
    {
        public string id;
        public string name;
        public EncounterSlotDefinition[] slots;
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string id;
        public string name;
        public int heal;
        public bool curePoison;
        public int stack;
        public int price;
    }
}
