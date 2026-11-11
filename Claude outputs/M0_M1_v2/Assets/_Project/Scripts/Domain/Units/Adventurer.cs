using System;
using System.Collections.Generic;
using Grimward.Domain.Data;

namespace Grimward.Domain.Units
{
    /// <summary>로스터의 모험가. 영구 사망.</summary>
    public sealed class Adventurer
    {
        public string Id { get; }
        public string Name { get; }
        public ClassDefinition Class { get; }
        public int Level { get; set; }
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Vitality { get; set; }
        public int Intelligence { get; set; }
        public int Perception { get; set; }
        public int Charisma { get; set; }
        public int CurrentHp { get; set; }
        public bool IsDead { get; set; }
        public List<string> EquippedSkills { get; }

        public Adventurer(string id, string name, ClassDefinition classDef, int perception, int charisma)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Class = classDef ?? throw new ArgumentNullException(nameof(classDef));
            Level = 1;
            Strength = classDef.strength;
            Dexterity = classDef.dexterity;
            Vitality = classDef.vitality;
            Intelligence = classDef.intelligence;
            Perception = perception;
            Charisma = charisma;
            EquippedSkills = new List<string>(classDef.skills);
        }

        public void RestoreFullHp(BalanceConfig config) => CurrentHp = StatFormulas.MaxHp(config, this);
    }
}
