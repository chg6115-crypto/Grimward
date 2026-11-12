using System;
using System.Collections.Generic;
using System.Linq;

namespace Grimward.Domain.Data
{
    /// <summary>정의 데이터 조회 + 형식 검증.</summary>
    public sealed class GameDatabase
    {
        private readonly Dictionary<string, ClassDefinition> classes;
        private readonly Dictionary<string, SkillDefinition> skills;
        private readonly Dictionary<string, MonsterDefinition> monsters;
        private readonly Dictionary<string, EncounterDefinition> encounters;
        private readonly Dictionary<string, ItemDefinition> items;
        private readonly Dictionary<string, NameDefinition> monsterTypes;
        private readonly Dictionary<string, InjuryDefinition> injuries;
        private readonly Dictionary<string, MapDefinition> maps;
        private readonly Dictionary<string, InvestigationDefinition> investigations;

        public BalanceConfig Balance { get; }
        public IReadOnlyCollection<ClassDefinition> Classes => classes.Values;
        public IReadOnlyCollection<MonsterDefinition> Monsters => monsters.Values;
        public IReadOnlyCollection<EncounterDefinition> Encounters => encounters.Values;
        public IReadOnlyCollection<ItemDefinition> Items => items.Values;

        public GameDatabase(GameDataFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            Balance = file.balance ?? throw new ArgumentException("balance가 없습니다.");
            classes = ToDictionary(file.classes, c => c.id, "class");
            skills = ToDictionary(file.skills, s => s.id, "skill");
            monsters = ToDictionary(file.monsters, m => m.id, "monster");
            encounters = ToDictionary(file.encounters, e => e.id, "encounter");
            items = ToDictionary(file.items, i => i.id, "item");
            monsterTypes = ToDictionary(file.monsterTypes, t => t.id, "monsterType");
            injuries = ToDictionary(file.injuries, i => i.id, "injury");
            maps = ToDictionary(file.maps, m => m.id, "map");
            investigations = ToDictionary(file.investigations, i => i.id, "investigation");
        }

        public ClassDefinition GetClass(string id) => Get(classes, id, "직업");
        public SkillDefinition GetSkill(string id) => Get(skills, id, "기술");
        public MonsterDefinition GetMonster(string id) => Get(monsters, id, "몬스터");
        public EncounterDefinition GetEncounter(string id) => Get(encounters, id, "편성");
        public ItemDefinition GetItem(string id) => Get(items, id, "아이템");
        public InjuryDefinition GetInjury(string id) => Get(injuries, id, "부상");
        public MapDefinition GetMap(string id) => Get(maps, id, "맵");
        public InvestigationDefinition GetInvestigation(string id) => Get(investigations, id, "조사 물건");
        public bool HasInvestigation(string id) => id != null && investigations.ContainsKey(id);
        public IReadOnlyCollection<MapDefinition> Maps => maps.Values;
        public bool HasEncounter(string id) => id != null && encounters.ContainsKey(id);
        public bool HasItem(string id) => id != null && items.ContainsKey(id);
        public IReadOnlyCollection<InjuryDefinition> Injuries => injuries.Values;

        /// <summary>부상 단계 표시 이름 (경상/중상 이름).</summary>
        public string InjuryStageName(string id, bool severe)
        {
            var d = GetInjury(id);
            var st = severe ? d.major : d.minor;
            return st != null && !string.IsNullOrEmpty(st.name) ? st.name : d.name;
        }

        /// <summary>몬스터 타입 표시 이름. 없으면 id 그대로.</summary>
        public string TypeName(string id) => id != null && monsterTypes.TryGetValue(id, out var t) ? t.name : id;

        /// <summary>참조·범위 오류 목록. 비어 있으면 정상.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            int ranks = Balance.ranksPerSide;
            if (ranks <= 0) errors.Add("balance.ranksPerSide가 0 이하입니다.");
            if (Balance.bagSlots <= 0) errors.Add("balance.bagSlots가 0 이하입니다.");
            foreach (var i in items.Values)
                if (i.stack <= 0) errors.Add($"아이템 {i.id}: stack이 0 이하");

            foreach (var c in classes.Values)
            {
                if (c.weaponMin > c.weaponMax) errors.Add($"직업 {c.id}: weaponMin > weaponMax");
                if (c.attackStat != DataKeys.AttackStat.Strength && c.attackStat != DataKeys.AttackStat.Intelligence)
                    errors.Add($"직업 {c.id}: attackStat '{c.attackStat}'");
                CheckSkillRefs(errors, $"직업 {c.id}", c.skills);
            }

            foreach (var m in monsters.Values)
            {
                if (m.hp <= 0) errors.Add($"몬스터 {m.id}: hp <= 0");
                if (m.attackMin > m.attackMax) errors.Add($"몬스터 {m.id}: attackMin > attackMax");
                if (m.size < 0 || m.size > ranks) errors.Add($"몬스터 {m.id}: size는 0~{ranks}");
                CheckSkillRefs(errors, $"몬스터 {m.id}", m.skills);
                if (m.passives != null)
                    foreach (var p in m.passives)
                    {
                        if (p.kind == DataKeys.Passive.AllyAura)
                        {
                            if (p.effects == null || p.effects.Length == 0) errors.Add($"몬스터 {m.id}: 패시브 {p.id} 효과 없음");
                            else foreach (var e in p.effects)
                                    if (e.kind != DataKeys.EffectKind.Stat || !IsKnownStat(e.stat))
                                        errors.Add($"몬스터 {m.id}: 패시브 {p.id} 효과 '{e.stat}'");
                        }
                        else if (p.kind == DataKeys.Passive.GuardBackline)
                        {
                            if (p.ranks == null || p.ranks.Length == 0 || p.protectedRanks == null || p.protectedRanks.Length == 0)
                                errors.Add($"몬스터 {m.id}: 패시브 {p.id} 칸 지정 없음");
                        }
                        else errors.Add($"몬스터 {m.id}: 패시브 kind '{p.kind}'");
                    }
                if (m.immunities != null)
                    foreach (var im in m.immunities)
                        if (!DataKeys.IsResistKind(im)) errors.Add($"몬스터 {m.id}: 면역 '{im}'");
                if (m.types != null)
                    foreach (var t in m.types)
                        if (!monsterTypes.ContainsKey(t)) errors.Add($"몬스터 {m.id}: 타입 '{t}' 없음");
            }

            foreach (var s in skills.Values) ValidateSkill(errors, s, ranks);

            foreach (var inj in injuries.Values)
            {
                if (inj.minor == null || inj.major == null) errors.Add($"부상 {inj.id}: 경상·중상 정의 필요");
                foreach (var st in new[] { inj.minor, inj.major })
                    if (st?.effects != null)
                        foreach (var e in st.effects)
                            if (e.kind != DataKeys.EffectKind.Stat || !IsKnownStat(e.stat))
                                errors.Add($"부상 {inj.id}: 효과 '{e.stat}'");
            }

            foreach (var e in encounters.Values)
            {
                if (e.variants != null && e.variants.Length > 0)
                {
                    foreach (var v in e.variants)
                    {
                        if (!encounters.TryGetValue(v, out var ve)) errors.Add($"편성 {e.id}: 변형 '{v}' 없음");
                        else if (ve.variants != null && ve.variants.Length > 0) errors.Add($"편성 {e.id}: 변형 '{v}'가 다시 변형을 가짐");
                    }
                    if (e.variantWeights != null && e.variantWeights.Length > 0 &&
                        (e.variantWeights.Length != e.variants.Length || e.variantWeights.Any(x => x < 0)))
                        errors.Add($"편성 {e.id}: 가중치 수가 변형 수와 다르거나 음수");
                    continue;
                }
                if (e.slots == null || e.slots.Length == 0 || e.slots.Length > ranks)
                    errors.Add($"편성 {e.id}: 칸 수가 1~{ranks}가 아닙니다.");
                else
                    foreach (var slot in e.slots)
                    {
                        if (slot.chance <= 0 || slot.chance > 1) errors.Add($"편성 {e.id}: chance는 0 초과 1 이하");
                        if (slot.candidates == null || slot.candidates.Length == 0) errors.Add($"편성 {e.id}: 빈 후보");
                        else foreach (var id in slot.candidates)
                                if (!monsters.ContainsKey(id)) errors.Add($"편성 {e.id}: 몬스터 '{id}' 없음");
                    }
            }
            foreach (var inv in investigations.Values)
                foreach (var res in new[] { inv.good, inv.normal, inv.bad })
                    if (res?.rewards != null)
                        foreach (var rw in res.rewards)
                            if (!items.ContainsKey(rw.item ?? "")) errors.Add($"조사 물건 {inv.id}: 보상 아이템 '{rw.item}' 없음");
            foreach (var m in maps.Values) errors.AddRange(Map.MapValidator.Validate(m, this));
            return errors;
        }

        private void ValidateSkill(List<string> errors, SkillDefinition s, int ranks)
        {
            string p = $"기술 {s.id}";
            if (s.useRanks == null || s.useRanks.Length == 0) errors.Add($"{p}: useRanks 없음");
            else if (s.useRanks.Any(r => r < 1 || r > ranks)) errors.Add($"{p}: useRanks 범위 밖");

            var targets = new[] { DataKeys.Target.Enemy, DataKeys.Target.Ally, DataKeys.Target.Self,
                DataKeys.Target.AllAllies, DataKeys.Target.AllySlot };
            if (!targets.Contains(s.target)) errors.Add($"{p}: target '{s.target}'");
            if (s.target == DataKeys.Target.Enemy &&
                (s.targetRanks == null || s.targetRanks.Length == 0 || s.targetRanks.Any(r => r < 1 || r > ranks)))
                errors.Add($"{p}: targetRanks 오류");
            if (s.target == DataKeys.Target.AllySlot && (s.summon == null || string.IsNullOrEmpty(s.summon.monsterId)))
                errors.Add($"{p}: 소환 대상 없음");
            if (s.injury != null && !string.IsNullOrEmpty(s.injury.injuryId) && !injuries.ContainsKey(s.injury.injuryId))
                errors.Add($"{p}: 부상 '{s.injury.injuryId}' 없음");
            if (s.summon != null && !string.IsNullOrEmpty(s.summon.monsterId) && !monsters.ContainsKey(s.summon.monsterId))
                errors.Add($"{p}: 소환 몬스터 '{s.summon.monsterId}' 없음");

            if (s.effects != null)
                foreach (var e in s.effects)
                {
                    if (e.kind != DataKeys.EffectKind.Stat && e.kind != DataKeys.EffectKind.Poison &&
                        e.kind != DataKeys.EffectKind.Bleed && e.kind != DataKeys.EffectKind.Burn &&
                        e.kind != DataKeys.EffectKind.Stun)
                        errors.Add($"{p}: 효과 kind '{e.kind}'");
                    if (e.duration <= 0) errors.Add($"{p}: 효과 지속 0 이하");
                    if (e.target != DataKeys.EffectTarget.Target && e.target != DataKeys.EffectTarget.Self &&
                        e.target != DataKeys.EffectTarget.AllAllies)
                        errors.Add($"{p}: 효과 target '{e.target}'");
                    if (e.kind == DataKeys.EffectKind.Stat && !IsKnownStat(e.stat))
                        errors.Add($"{p}: 효과 stat '{e.stat}'");
                    if (e.chance < 0 || e.chance > 1) errors.Add($"{p}: 효과 확률은 0~1");
                }
            if (s.steal && s.target != DataKeys.Target.Enemy) errors.Add($"{p}: 약탈은 적 대상 기술만");
            if (s.variance < 0 || s.variance >= 1) errors.Add($"{p}: 편차는 0 이상 1 미만");
            if (s.weights != null && (s.weights.strength < 0 || s.weights.dexterity < 0 || s.weights.intelligence < 0))
                errors.Add($"{p}: 가중치 음수");
            if (!string.IsNullOrEmpty(s.damageType) && s.damageType != DataKeys.DamageType.Physical &&
                s.damageType != DataKeys.DamageType.Magical)
                errors.Add($"{p}: damageType '{s.damageType}'");
        }

        private static bool IsKnownStat(string stat) =>
            stat == DataKeys.Stat.DamagePct || stat == DataKeys.Stat.Accuracy || stat == DataKeys.Stat.Crit ||
            stat == DataKeys.Stat.Dodge || stat == DataKeys.Stat.Speed || stat == DataKeys.Stat.DamageTaken ||
            stat == DataKeys.Stat.MaxHpPct || stat == DataKeys.Stat.Armor || stat == DataKeys.Stat.FormationResist ||
            stat == DataKeys.Stat.StunResist || stat == DataKeys.Stat.MagicResist;

        private void CheckSkillRefs(List<string> errors, string owner, string[] ids)
        {
            if (ids == null || ids.Length == 0) { errors.Add($"{owner}: 기술 없음"); return; }
            foreach (var id in ids)
                if (!skills.ContainsKey(id)) errors.Add($"{owner}: 기술 '{id}' 없음");
        }

        private static Dictionary<string, T> ToDictionary<T>(T[] list, Func<T, string> key, string label)
        {
            var dict = new Dictionary<string, T>();
            if (list == null) return dict;
            foreach (var item in list)
            {
                string k = key(item);
                if (string.IsNullOrEmpty(k)) throw new ArgumentException($"{label} id가 비어 있습니다.");
                if (dict.ContainsKey(k)) throw new ArgumentException($"{label} id 중복: {k}");
                dict.Add(k, item);
            }
            return dict;
        }

        private static T Get<T>(Dictionary<string, T> dict, string id, string label)
        {
            if (id != null && dict.TryGetValue(id, out var value)) return value;
            throw new KeyNotFoundException($"{label} '{id}'를 찾을 수 없습니다.");
        }
    }
}
