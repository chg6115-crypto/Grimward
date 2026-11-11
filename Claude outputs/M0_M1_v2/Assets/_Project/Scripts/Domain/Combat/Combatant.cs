using System;
using System.Collections.Generic;
using Grimward.Domain.Data;
using Grimward.Domain.Units;

namespace Grimward.Domain.Combat
{
    public enum BattleSide { Party, Enemy }

    /// <summary>전투 중 한 명. 모험가와 몬스터 모두 같은 형태.</summary>
    public sealed class Combatant
    {
        private readonly Dictionary<string, int> cooldownUntilRound = new Dictionary<string, int>();

        public int Uid { get; }
        public BattleSide Side { get; }
        public string Name { get; }
        public string DefinitionId { get; }
        public string Family { get; }
        public int Level { get; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public int AttackMin { get; }
        public int AttackMax { get; }
        public int Accuracy { get; }
        public int Dodge { get; }
        public int Speed { get; }
        public double Armor { get; }
        public double BaseCrit { get; }
        public double FormationResist { get; }
        public int Intelligence { get; }
        public IReadOnlyList<string> Types { get; }
        public IReadOnlyList<SkillDefinition> Skills { get; }
        /// <summary>모험가일 때만. 전투 후 HP를 되돌려 쓴다.</summary>
        public Adventurer Source { get; }
        public StatusEffectSet Effects { get; } = new StatusEffectSet();

        public bool IsAlive => Hp > 0;

        public Combatant(int uid, BattleSide side, string name, string definitionId, string family, int level,
            int maxHp, int hp, int attackMin, int attackMax, int accuracy, int dodge, int speed, double armor,
            double baseCrit, double formationResist, int intelligence, IReadOnlyList<string> types,
            IReadOnlyList<SkillDefinition> skills, Adventurer source)
        {
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            Uid = uid; Side = side; Name = name; DefinitionId = definitionId; Family = family; Level = level;
            MaxHp = maxHp; Hp = Math.Max(0, Math.Min(hp, maxHp));
            AttackMin = attackMin; AttackMax = attackMax; Accuracy = accuracy; Dodge = dodge; Speed = speed;
            Armor = armor; BaseCrit = baseCrit; FormationResist = formationResist; Intelligence = intelligence;
            Types = types ?? Array.Empty<string>();
            Skills = skills ?? Array.Empty<SkillDefinition>();
            Source = source;
        }

        /// <summary>실제로 깎인 HP를 돌려준다.</summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0 || !IsAlive) return 0;
            int dealt = Math.Min(amount, Hp);
            Hp -= dealt;
            return dealt;
        }

        /// <summary>실제로 회복한 양을 돌려준다.</summary>
        public int Heal(int amount)
        {
            if (amount <= 0 || !IsAlive) return 0;
            int healed = Math.Min(amount, MaxHp - Hp);
            Hp += healed;
            return healed;
        }

        /// <summary>HP를 floor 아래로 내리지 않는 피해 (도주).</summary>
        public int TakeDamageWithFloor(int amount, int floor)
        {
            if (!IsAlive) return 0;
            int target = Math.Max(floor, Hp - amount);
            int dealt = Math.Max(0, Hp - target);
            Hp -= dealt;
            return dealt;
        }

        public bool IsOnCooldown(string skillId, int round) =>
            cooldownUntilRound.TryGetValue(skillId, out int until) && round <= until;

        public void StartCooldown(SkillDefinition skill, int round)
        {
            if (skill.cooldown > 0) cooldownUntilRound[skill.id] = round + skill.cooldown;
        }

        public bool HasType(string type)
        {
            foreach (var t in Types) if (t == type) return true;
            return false;
        }

        public override string ToString() => $"{Name}({Hp}/{MaxHp})";
    }
}
