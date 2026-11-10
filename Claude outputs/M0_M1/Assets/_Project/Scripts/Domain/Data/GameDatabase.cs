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
        }

        public ClassDefinition GetClass(string id) => Get(classes, id, "직업");
        public SkillDefinition GetSkill(string id) => Get(skills, id, "기술");
        public MonsterDefinition GetMonster(string id) => Get(monsters, id, "몬스터");
        public EncounterDefinition GetEncounter(string id) => Get(encounters, id, "편성");
        public ItemDefinition GetItem(string id) => Get(items, id, "아이템");

        /// <summary>참조·범위 오류 목록. 비어 있으면 정상.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            int ranks = Balance.ranksPerSide;
            if (ranks <= 0) errors.Add("balance.ranksPerSide가 0 이하입니다.");

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
                CheckSkillRefs(errors, $"몬스터 {m.id}", m.skills);
            }

            foreach (var s in skills.Values) ValidateSkill(errors, s, ranks);

            foreach (var e in encounters.Values)
            {
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
            if (s.summon != null && !string.IsNullOrEmpty(s.summon.monsterId) && !monsters.ContainsKey(s.summon.monsterId))
                errors.Add($"{p}: 소환 몬스터 '{s.summon.monsterId}' 없음");

            if (s.effects != null)
                foreach (var e in s.effects)
                {
                    if (e.kind != DataKeys.EffectKind.Stat && e.kind != DataKeys.EffectKind.Poison)
                        errors.Add($"{p}: 효과 kind '{e.kind}'");
                    if (e.duration <= 0) errors.Add($"{p}: 효과 지속 0 이하");
                    if (e.target != DataKeys.EffectTarget.Target && e.target != DataKeys.EffectTarget.Self &&
                        e.target != DataKeys.EffectTarget.AllAllies)
                        errors.Add($"{p}: 효과 target '{e.target}'");
                    if (e.kind == DataKeys.EffectKind.Stat && !IsKnownStat(e.stat))
                        errors.Add($"{p}: 효과 stat '{e.stat}'");
                }
        }

        private static bool IsKnownStat(string stat) =>
            stat == DataKeys.Stat.DamagePct || stat == DataKeys.Stat.Accuracy || stat == DataKeys.Stat.Crit ||
            stat == DataKeys.Stat.Dodge || stat == DataKeys.Stat.Speed || stat == DataKeys.Stat.DamageTaken;

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
