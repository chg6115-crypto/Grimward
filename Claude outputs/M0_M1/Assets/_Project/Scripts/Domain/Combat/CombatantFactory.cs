using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Units;

namespace Grimward.Domain.Combat
{
    /// <summary>모험가·몬스터 정의 → 전투원. 편성 굴림도 여기서.</summary>
    public sealed class CombatantFactory
    {
        private readonly GameDatabase db;
        private int nextUid = 1;

        public CombatantFactory(GameDatabase db)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public Combatant FromAdventurer(Adventurer a)
        {
            var c = db.Balance;
            var skills = a.EquippedSkills.Select(db.GetSkill).ToList();
            return new Combatant(nextUid++, BattleSide.Party, a.Name, a.Class.id, a.Class.id, a.Level,
                StatFormulas.MaxHp(c, a), a.CurrentHp,
                StatFormulas.AttackMin(c, a), StatFormulas.AttackMax(c, a),
                StatFormulas.Accuracy(c, a), StatFormulas.Dodge(c, a), StatFormulas.Speed(c, a),
                a.Class.armor, StatFormulas.Crit(c, a), a.Class.formationResist, a.Intelligence,
                Array.Empty<string>(), skills, a);
        }

        public Combatant FromMonster(MonsterDefinition m)
        {
            var skills = m.skills.Select(db.GetSkill).ToList();
            return new Combatant(nextUid++, BattleSide.Enemy, m.name, m.id, m.family, 1,
                m.hp, m.hp, m.attackMin, m.attackMax, m.accuracy, m.dodge, m.speed,
                m.armor, m.crit, m.formationResist, 0,
                m.types ?? Array.Empty<string>(), skills, null);
        }

        public Combatant FromMonster(string monsterId) => FromMonster(db.GetMonster(monsterId));

        /// <summary>편성 정의를 굴려 1번 칸부터 몬스터 목록을 만든다.</summary>
        public List<Combatant> RollEncounter(string encounterId, IRandomSource random)
        {
            var e = db.GetEncounter(encounterId);
            var result = new List<Combatant>();
            foreach (var slot in e.slots)
            {
                if (slot.chance < 1.0 && random.NextDouble() >= slot.chance) continue;
                string id = slot.candidates[random.NextInt(0, slot.candidates.Length - 1)];
                result.Add(FromMonster(id));
            }
            return result;
        }
    }
}
