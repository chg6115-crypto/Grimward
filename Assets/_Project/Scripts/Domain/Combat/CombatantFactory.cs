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
        private readonly IRandomSource random;
        private int nextUid = 1;

        /// <param name="random">있으면 몬스터를 만들 때 능력치를 한 번 굴린다 (HP·명중·회피·속도 편차). 없으면 고정값.</param>
        public CombatantFactory(GameDatabase db, IRandomSource random = null)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.random = random;
        }

        public GameDatabase Database => db;

        public Combatant FromAdventurer(Adventurer a)
        {
            var c = db.Balance;
            var skills = a.EquippedSkills.Select(db.GetSkill).ToList();
            var combatant = new Combatant(nextUid++, BattleSide.Party, a.Name, a.Class.id, a.Class.id, a.Level,
                StatFormulas.MaxHp(c, a), a.CurrentHp,
                StatFormulas.AttackMin(c, a), StatFormulas.AttackMax(c, a),
                StatFormulas.Accuracy(c, a), StatFormulas.Dodge(c, a), StatFormulas.Speed(c, a),
                a.Class.armor, StatFormulas.Crit(c, a), a.Class.formationResist, a.Intelligence,
                Array.Empty<string>(), skills, a);
            foreach (var inj in a.Injuries) combatant.Injuries.Add(inj.Clone());
            combatant.WeaponMin = a.Class.weaponMin; combatant.WeaponMax = a.Class.weaponMax;
            combatant.Strength = a.Strength; combatant.Dexterity = a.Dexterity; combatant.Perception = a.Perception;
            double body = c.heroResistBase + a.Vitality * c.heroResistPerVitality;
            combatant.SetResist(DataKeys.Resist.Stun, body);
            combatant.SetResist(DataKeys.Resist.Poison, body);
            combatant.SetResist(DataKeys.Resist.Bleed, body);
            combatant.SetResist(DataKeys.Resist.Burn, body);
            combatant.SetResist(DataKeys.Resist.Debuff, c.heroResistBase + a.Intelligence * c.heroDebuffResistPerIntelligence);
            return combatant;
        }

        public Combatant FromMonster(MonsterDefinition m)
        {
            var skills = m.skills.Select(db.GetSkill).ToList();
            int hp = m.hp, accuracy = m.accuracy, dodge = m.dodge, speed = m.speed;
            if (random != null)
            {
                // 편차 = 기본 편차 × 몬스터별 배율 (0이면 1, 보스는 작게). 소수점은 버림.
                var c = db.Balance;
                double scale = m.varianceScale > 0 ? m.varianceScale : 1.0;
                hp = Math.Max(1, hp + Roll(m.hp * c.monsterHpVariance * scale));
                accuracy += Roll(c.monsterAccuracyDodgeVariance * scale);
                dodge += Roll(c.monsterAccuracyDodgeVariance * scale);
                speed = Math.Max(0, speed + Roll(c.monsterSpeedVariance * scale));
            }
            var mon = new Combatant(nextUid++, BattleSide.Enemy, m.name, m.id, m.family, 1,
                hp, hp, m.attackMin, m.attackMax, accuracy, dodge, speed,
                m.armor, m.crit, m.formationResist, 0,
                m.types ?? Array.Empty<string>(), skills, null, m.passives ?? Array.Empty<PassiveDefinition>(),
                Math.Max(1, m.size), Math.Max(1, m.actionsPerTurn));
            mon.MagicResist = m.magicResist;
            var r = m.resists;
            if (r != null)
            {
                mon.SetResist(DataKeys.Resist.Stun, r.stun);
                mon.SetResist(DataKeys.Resist.Poison, r.poison);
                mon.SetResist(DataKeys.Resist.Bleed, r.bleed);
                mon.SetResist(DataKeys.Resist.Burn, r.burn);
                mon.SetResist(DataKeys.Resist.Debuff, r.debuff);
            }
            if (m.immunities != null) foreach (var im in m.immunities) mon.AddImmunity(im);
            return mon;
        }

        public Combatant FromMonster(string monsterId) => FromMonster(db.GetMonster(monsterId));

        private int Roll(double range)
        {
            int r = (int)Math.Floor(range);
            return r <= 0 ? 0 : random.NextInt(-r, r);
        }

        /// <summary>가중치가 있으면 가중치로, 없으면 균등하게 편성 하나를 고른다.</summary>
        private static int PickVariant(EncounterDefinition e, IRandomSource random)
        {
            var w = e.variantWeights;
            if (w == null || w.Length != e.variants.Length || w.Sum() <= 0) return random.NextInt(0, e.variants.Length - 1);
            int roll = random.NextInt(1, w.Sum());
            for (int i = 0; i < w.Length; i++)
            {
                roll -= w[i];
                if (roll <= 0) return i;
            }
            return w.Length - 1;
        }

        public int SizeOf(string monsterId) => Math.Max(1, db.GetMonster(monsterId).size);

        /// <summary>편성 정의를 굴려 1번 칸부터 몬스터 목록을 만든다.</summary>
        /// <summary>칸이 모자라면 남은 칸은 건너뛴다 (큰 몬스터가 칸을 더 차지).</summary>
        public List<Combatant> RollEncounter(string encounterId, IRandomSource random)
        {
            var e = db.GetEncounter(encounterId);
            if (e.variants != null && e.variants.Length > 0)
                e = db.GetEncounter(e.variants[PickVariant(e, random)]);
            var result = new List<Combatant>();
            foreach (var slot in e.slots)
            {
                if (slot.chance < 1.0 && random.NextDouble() >= slot.chance) continue;
                string id = slot.candidates[random.NextInt(0, slot.candidates.Length - 1)];
                var monster = FromMonster(id);
                if (result.Sum(c => c.Size) + monster.Size > db.Balance.ranksPerSide) continue;
                result.Add(monster);
            }
            return result;
        }
    }
}
