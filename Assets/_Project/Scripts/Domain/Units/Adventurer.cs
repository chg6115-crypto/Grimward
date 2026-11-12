using System;
using System.Collections.Generic;
using Grimward.Domain.Data;

namespace Grimward.Domain.Units
{
    /// <summary>로스터의 모험가. 영구 사망. 전투불능은 한 출정에 한 번까지 (두 번째면 사망).</summary>
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
        /// <summary>이번 출정에서 전투불능이 된 횟수. 출정을 시작할 때 0으로.</summary>
        public int KnockdownsThisExpedition { get; set; }
        public List<string> EquippedSkills { get; }
        /// <summary>부상. 출정이 끝나도 남고, 교회 치료소나 (뇌진탕은) 휴식으로 낫는다.</summary>
        public List<InjuryState> Injuries { get; } = new List<InjuryState>();

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

        /// <summary>출정 시작: 전투불능 횟수 초기화.</summary>
        public void BeginExpedition() => KnockdownsThisExpedition = 0;

        public void RestoreFullHp(BalanceConfig config) => CurrentHp = StatFormulas.MaxHp(config, this);

        /// <summary>하룻밤 자택 휴식: 휴식으로 낫는 부상(뇌진탕)만 사라진다.</summary>
        public void RestOvernight(GameDatabase db) => Injuries.RemoveAll(i => db.GetInjury(i.Id).healedByRest);

        /// <summary>교회 치료소에 하루 맡김: 모든 부상 회복.</summary>
        public void TreatAtInfirmary() => Injuries.Clear();
    }
}
