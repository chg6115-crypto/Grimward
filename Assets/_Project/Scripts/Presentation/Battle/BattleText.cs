using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using UnityEngine;

namespace Grimward.Presentation.Battle
{
    /// <summary>전투 화면 표시 문자열·색. 규칙 계산 없이 값만 꾸민다.</summary>
    public static class BattleText
    {
        public static Color HighlightColor(CombatantSlotView.Highlight h)
        {
            switch (h)
            {
                case CombatantSlotView.Highlight.Actor: return new Color(0.95f, 0.78f, 0.25f);
                case CombatantSlotView.Highlight.EnemyTarget: return new Color(0.9f, 0.25f, 0.2f);
                case CombatantSlotView.Highlight.AllyTarget: return new Color(0.3f, 0.85f, 0.4f);
                case CombatantSlotView.Highlight.Inspected: return new Color(0.85f, 0.85f, 0.9f);
                default: return Color.clear;
            }
        }

        /// <summary>
        /// 칸 아래 짧은 상태 표시 (버프 ▲ · 디버프 ▼ · 중독 등).
        /// 능력치 효과는 기술(출처)마다 아이콘 1개로 묶는다 — 축복처럼 효과가 여러 개여도 ▲ 하나, 중첩되면 ▲2.
        /// </summary>
        public static string StatusIcons(Combatant c)
        {
            var sb = new StringBuilder();
            foreach (var e in c.Effects.All)
            {
                if (e.IsBleed) { sb.Append("<color=#e55>출혈</color> "); continue; }
                if (e.IsStun) { sb.Append("<color=#fd5>기절</color> "); continue; }
                if (e.IsBurn) { sb.Append("<color=#f93>화상</color> "); continue; }
                if (e.IsPoison) sb.Append("<color=#7d7>독</color> ");
            }
            foreach (var g in StatGroups(c))
            {
                sb.Append(g.IsBuff ? "<color=#7bf>▲</color>" : "<color=#f88>▼</color>");
                if (g.Stacks > 1) sb.Append($"<size=70%>{g.Stacks}</size>");
            }
            if (c.Injuries.Count > 0) sb.Append(" <color=#e77>부상</color>");
            if (c.EscapeRound > 0) sb.Append($" <color=#fd5>도주 {c.EscapeRound}R</color>");
            else if (c.StolenItems.Count > 0) sb.Append(" <color=#e77>약탈</color>");
            return sb.ToString();
        }

        public static string Stats(Combatant c, BalanceConfig cfg)
        {
            double crit = c.BaseCrit + c.Effects.Sum(DataKeys.Stat.Crit);
            if (crit < cfg.critMin) crit = cfg.critMin;
            int acc = c.Accuracy + (int)c.Effects.Sum(DataKeys.Stat.Accuracy);
            int dodge = c.Dodge + (int)c.Effects.Sum(DataKeys.Stat.Dodge);
            int speed = TurnOrder.EffectiveSpeed(c);
            double ward = -c.Effects.Sum(DataKeys.Stat.DamageTaken);
            const string tab = "<pos=48%>";
            return
                $"HP{tab}{c.Hp} / {c.MaxHp}\n" +
                $"명중{tab}{acc}\n" +
                $"치명타{tab}{crit * 100:0.#}%\n" +
                (c.Side == BattleSide.Party ? $"무기{tab}{c.WeaponMin}-{c.WeaponMax}\n" : $"피해{tab}{c.AttackMin}-{c.AttackMax}\n") +
                $"회피{tab}{dodge}\n" +
                $"방어{tab}{c.EffectiveArmor * 100:0.#}%{(ward != 0 ? $" (+{ward * 100:0}%)" : "")}\n" +
                $"속도{tab}{speed}\n" +
                $"진형 저항{tab}{c.EffectiveFormationResist * 100:0}%";
        }

        /// <summary>상태이상 저항·마법 저항·면역. 확률 = 기술 확률 − 저항.</summary>
        public static string Resists(Combatant c)
        {
            // 능력치 칸과 같은 정렬: 두 항목씩 3줄
            string P(string k) => c.IsImmune(k) ? "<color=#eb4>면역</color>" : $"{c.Resist(k) * 100:0}%";
            const string v1 = "<pos=24%>", k2 = "<pos=48%>", v2 = "<pos=72%>";
            return
                $"기절{v1}{P(DataKeys.Resist.Stun)}{k2}중독{v2}{P(DataKeys.Resist.Poison)}\n" +
                $"출혈{v1}{P(DataKeys.Resist.Bleed)}{k2}화상{v2}{P(DataKeys.Resist.Burn)}\n" +
                $"약화{v1}{P(DataKeys.Resist.Debuff)}{k2}마법{v2}{System.Math.Max(0, c.EffectiveMagicResist) * 100:0}%";
        }

        public static string Effects(Combatant c)
        {
            if (c.Effects.All.Count == 0) return "<color=#888>걸린 효과 없음</color>";
            var lines = new List<string>();
            foreach (var e in c.Effects.All)
            {
                if (e.IsPoison) lines.Add($"<color=#7d7>중독</color> 차례마다 {e.DamagePerTurn} · {e.Remaining}라운드");
                else if (e.IsBleed) lines.Add($"<color=#e55>출혈</color> ({e.SourceSkillName}) 차례마다 {e.DamagePerTurn} · {e.Remaining}라운드");
                else if (e.IsBurn) lines.Add($"<color=#f93>화상</color> ({e.SourceSkillName}) 차례마다 {e.DamagePerTurn} · {e.Remaining}라운드");
                else if (e.IsStun) lines.Add($"<color=#fd5>기절</color> ({e.SourceSkillName}) 다음 차례를 건너뜀");
                else if (e.IsInjury) lines.Add($"<color=#e77>{e.SourceSkillName}</color>: {StatLabel(e)} · 부상");
            }
            // 능력치 효과: 기술마다 한 줄 (중첩은 합계)
            foreach (var g in StatGroups(c))
            {
                string name = g.Passive ? $"<color=#eb4>{g.Name}</color>" : g.Name;
                string stacks = g.Stacks > 1 ? $" ×{g.Stacks}" : "";
                string tail = g.Passive ? "패시브"
                    : g.MinRemaining == g.MaxRemaining ? $"{g.MaxRemaining}라운드" : $"{g.MinRemaining}~{g.MaxRemaining}라운드";
                lines.Add($"{name}{stacks}: {g.Label} · {tail}");
            }
            return string.Join("\n", lines);
        }

        /// <summary>같은 기술(출처)에서 온 능력치 효과 묶음.</summary>
        private sealed class StatGroup
        {
            public string Name;
            public bool Passive;
            public bool IsBuff;
            public int Stacks;
            public int MinRemaining;
            public int MaxRemaining;
            public string Label;
        }

        /// <summary>
        /// 능력치 효과(부상 제외)를 출처 기술로 묶는다. 중첩 수 = 한 능력치가 몇 번 걸렸는지의 최댓값.
        /// 수치는 같은 능력치끼리 더하고, 저항 여러 종은 한 항목으로 묶는다.
        /// </summary>
        private static List<StatGroup> StatGroups(Combatant c)
        {
            var result = new List<StatGroup>();
            var stats = c.Effects.All.Where(e => e.Kind == DataKeys.EffectKind.Stat && !e.IsInjury);
            foreach (var g in stats.GroupBy(e => (e.SourceSkillId, e.IsPassive)))
            {
                var list = g.ToList();
                var byStat = list.GroupBy(e => e.Stat).ToList();
                var summed = byStat.Select(s => new EffectDefinition
                    { kind = DataKeys.EffectKind.Stat, stat = s.Key, value = s.Sum(e => e.Value) }).ToList();
                var resists = summed.Where(d => DataKeys.Stat.IsResist(d.stat)).ToList();
                var parts = summed.Where(d => !DataKeys.Stat.IsResist(d.stat)).Select(d => SkillResolver.DescribeEffect(d)).ToList();
                if (resists.Count > 0) parts.Add(SkillResolver.DescribeResists(resists));
                var first = list[0];
                result.Add(new StatGroup
                {
                    Name = first.SourceSkillName,
                    Passive = first.IsPassive,
                    IsBuff = first.Value >= 0 == (first.Stat != DataKeys.Stat.DamageTaken),
                    Stacks = byStat.Max(s => s.Count()),
                    MinRemaining = list.Min(e => e.Remaining),
                    MaxRemaining = list.Max(e => e.Remaining),
                    Label = string.Join(" · ", parts),
                });
            }
            return result;
        }

        private static string StatLabel(ActiveEffect e)
        {
            var def = new EffectDefinition { kind = e.Kind, stat = e.Stat, value = e.Value, damagePerTurn = e.DamagePerTurn };
            return SkillResolver.DescribeEffect(def);
        }

        public static string Ranks(int[] ranks, bool reverse)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                int rank = reverse ? 4 - i : i + 1;
                sb.Append(ranks != null && System.Array.IndexOf(ranks, rank) >= 0 ? "●" : "○");
            }
            return sb.ToString();
        }

        /// <summary>기술 설명: 사용 위치(4→1) · 대상 위치(1→4) · 설명.</summary>
        public static string Skill(SkillDefinition s)
        {
            string target = s.target == DataKeys.Target.Enemy ? Ranks(s.targetRanks, false) + (s.multi ? " (다중)" : "")
                : s.target == DataKeys.Target.Ally ? "아군 1명"
                : s.target == DataKeys.Target.AllAllies ? "아군 전체"
                : s.target == DataKeys.Target.Self ? "자신" : "빈 칸";
            return $"<b>{s.name}</b>  사용 {Ranks(s.useRanks, true)} → 대상 {target}  {s.description}";
        }

        private static string Dots(int[] ranks, bool reverse, string on)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                int rank = reverse ? 4 - i : i + 1;
                bool lit = ranks != null && System.Array.IndexOf(ranks, rank) >= 0;
                sb.Append(lit ? $"<color={on}>●</color>" : "<color=#555>●</color>");
            }
            return sb.ToString();
        }

        /// <summary>기술 툴팁 (다키스트 던전 방식): 이름·레벨 · 종류 · 사용/대상 칸 · 보정 · 효과 · 설명.</summary>
        public static string SkillTooltip(SkillDefinition s, int level, GameDatabase db, bool forEnemy = false,
            Combatant user = null)
        {
            var lv = SkillTargeting.LevelOf(s, level);
            var sb = new StringBuilder();
            sb.Append($"<align=center><b>{s.name} {level}</b>\n<size=85%><color=#a99>{s.kind}</color></size>\n");
            // 플레이어 기술: 내 칸(4→1) → 적 칸(1→4). 몬스터 기술: 몬스터 칸(1→4) ← 영웅 칸(4→1) — 화면 배치와 같은 방향
            string target = s.target == DataKeys.Target.Enemy ? Dots(s.targetRanks, forEnemy, "#e55") + (s.multi ? " <size=80%>(다중)</size>" : "")
                : s.target == DataKeys.Target.Ally ? "아군 1명"
                : s.target == DataKeys.Target.AllAllies ? "아군 전체"
                : s.target == DataKeys.Target.Self ? "자신" : "빈 칸";
            if (forEnemy) sb.Append($"{target}  <color=#777>←</color>  {Dots(s.useRanks, false, "#eb4")}</align>\n");
            else sb.Append($"{Dots(s.useRanks, true, "#eb4")}  <color=#777>→</color>  {target}</align>\n");

            if (s.target == DataKeys.Target.Enemy)
            {
                sb.Append($"명중 보정: {lv.accuracy:+0;-0;0}\n");
                if (s.damaging && user != null && (s.fixedDamage == null || s.fixedDamage.max <= 0))
                {
                    CombatFormulas.DamageRange(db.Balance, user, s, out int min, out int max);
                    sb.Append($"<b>피해 {min}~{max}</b>");
                    if (s.variance > 0) sb.Append($" <size=80%><color=#999>{VarianceName(s.variance)} ±{s.variance * 100:0}%</color></size>");
                    sb.Append("\n");
                }
                if (s.weights != null && s.weights.Sum > 0) sb.Append($"<size=85%><color=#aaa>{Weights(s.weights)}</color></size>\n");
                if (s.damaging) sb.Append($"피해 보정: {lv.damage * 100:+0;-0;0}%\n");
                if (s.damaging) sb.Append($"치명타 보정: {lv.crit * 100:+0.#;-0.#;0}%\n");
                if (s.damaging && s.damageType == DataKeys.DamageType.Magical)
                    sb.Append("<color=#9cf>마법 피해</color> <size=80%><color=#999>(방어도 대신 마법 저항)</color></size>\n");
                if (s.ignoreArmor) sb.Append("대상 방어도 무시\n");
                if (s.fixedDamage != null && s.fixedDamage.max > 0)
                    sb.Append($"피해 {s.fixedDamage.min}~{s.fixedDamage.max}" +
                              (s.fixedDamage.intelligenceMultiplier > 0 ? " + 지능" : "") + " (무기 대신)\n");
            }
            if (s.typeBonuses != null)
                foreach (var b in s.typeBonuses)
                    sb.Append($"<color=#eb4>{db.TypeName(b.type)}</color>에게 피해 {b.damage * 100:+0;-0}%\n");
            if (s.heal != null && s.heal.max > 0)
            {
                bool weighted = s.weights != null && s.weights.Sum > 0;
                int bonus = weighted && user != null ? CombatFormulas.StatBonus(db.Balance, user, s.weights) : 0;
                sb.Append($"회복 {s.heal.min + bonus}{(s.heal.max > s.heal.min ? $"~{s.heal.max + bonus}" : "")}" +
                          (!weighted && s.heal.intelligenceDivisor > 0 ? $" + 지능÷{s.heal.intelligenceDivisor}" : "") + "\n");
                if (weighted && s.target != DataKeys.Target.Enemy)
                    sb.Append($"<size=85%><color=#aaa>{Weights(s.weights)}</color></size>\n");
            }
            if (s.curePoison) sb.Append("해독\n");
            if (s.displacement != null && s.displacement.ranks != 0)
                sb.Append($"<color=#c9f>{(s.displacement.ranks > 0 ? "밀어내기" : "끌어당기기")} {System.Math.Abs(s.displacement.ranks)}칸</color>" +
                          $" (기본 {s.displacement.chance * 100:0}% − 저항)\n");
            if (s.effects != null)
            {
                // 저항 효과(같은 대상·지속)는 한 줄로 묶는다
                var resists = s.effects.Where(e => e.kind == DataKeys.EffectKind.Stat && DataKeys.Stat.IsResist(e.stat)).ToList();
                foreach (var e in s.effects)
                {
                    if (resists.Contains(e))
                    {
                        if (e != resists[0]) continue;
                        string scale = e.perLevel != 0 ? $" <size=80%><color=#999>(레벨당 +{e.perLevel * 100:0}%p)</color></size>" : "";
                        sb.Append($"{SkillResolver.DescribeResists(resists, level)}, {e.duration}라운드{scale}\n");
                        continue;
                    }
                    string who = e.target == DataKeys.EffectTarget.Self ? "자신: "
                        : e.target == DataKeys.EffectTarget.AllAllies ? "아군 전체: "
                        : s.target == DataKeys.Target.Enemy ? "명중 시: " : "";
                    string doubled = string.IsNullOrEmpty(e.doubledVsType) ? "" : $" ({db.TypeName(e.doubledVsType)} 상대 2배)";
                    string length = e.kind == DataKeys.EffectKind.Stun ? "" : $", {e.duration}라운드";
                    string chance = s.target == DataKeys.Target.Enemy && e.target == DataKeys.EffectTarget.Target
                        ? $" <size=80%><color=#999>(기본 {(e.chance > 0 ? e.chance : 1) * 100:0}% − 저항)</color></size>" : "";
                    sb.Append($"{who}{SkillResolver.DescribeEffect(e, level)}{length}{doubled}{chance}\n");
                }
            }
            if (s.injury != null && !string.IsNullOrEmpty(s.injury.injuryId))
                sb.Append($"<color=#e77>부상: {db.InjuryStageName(s.injury.injuryId, false)} {s.injury.chance * 100:0}%</color>" +
                          " <size=80%><color=#999>(치명타 시 확정)</color></size>\n");
            if (s.treatInjury) sb.Append("<color=#7d7>현장 치료</color>: 얕은 베임 제거 · 깊은 베임 → 얕은 베임\n");
            if (s.summon != null && !string.IsNullOrEmpty(s.summon.monsterId))
                sb.Append($"{db.GetMonster(s.summon.monsterId).name} {s.summon.count}마리 소환" +
                          (s.summon.countWhenAlone != s.summon.count ? $" (혼자면 {s.summon.countWhenAlone})" : "") +
                          (s.summon.inFront ? " · 앞칸에 소환, 아군은 뒤로 물러남" : "") + "\n");
            if (s.cooldown > 0) sb.Append($"재사용 대기 {s.cooldown}라운드\n");
            if (!string.IsNullOrEmpty(s.description)) sb.Append($"<size=85%><color=#999>{s.description}</color></size>");
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>상태 표시(▲▼독)에 올렸을 때: 걸린 효과와 남은 라운드.</summary>
        public static string EffectsTooltip(Combatant c) =>
            $"<align=center><b>{c.Name}</b> <size=85%><color=#a99>걸린 효과</color></size></align>\n" + Effects(c);

        /// <summary>패시브 툴팁.</summary>
        public static string PassiveTooltip(PassiveDefinition p) =>
            $"<align=center><b>{p.name}</b>\n<size=85%><color=#a99>패시브</color></size></align>\n{p.description}";

        /// <summary>이동 툴팁.</summary>
        public static string MoveTooltip() =>
            "<align=center><b>이동</b>\n<size=85%><color=#a99>공통 행동</color></size></align>\n앞 또는 뒤로 1칸, 그 칸의 아군과 자리 교환\n행동 1회 소모";

        /// <summary>모험가 기본 능력치 줄 (근력 · 민첩 · 건강 · 지능 · 지각 · 매력).</summary>
        public static string VarianceName(double v) => v <= 0.15 ? "정밀" : v <= 0.35 ? "보통" : "거침";

        /// <summary>"근력 2 · 민첩 1" (0은 생략).</summary>
        public static string Weights(StatWeightDefinition w)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (w.strength > 0) parts.Add($"근력 {w.strength}");
            if (w.dexterity > 0) parts.Add($"민첩 {w.dexterity}");
            if (w.intelligence > 0) parts.Add($"지능 {w.intelligence}");
            return string.Join(" · ", parts);
        }

        public static string Attributes(Combatant c, BalanceConfig cfg)
        {
            var a = c.Source;
            if (a == null) return "";
            const string tab = "<pos=48%>";
            return $"근력{tab}{a.Strength}\n민첩{tab}{a.Dexterity}\n건강{tab}{a.Vitality}\n" +
                   $"지능{tab}{a.Intelligence}\n지각{tab}{a.Perception}\n매력{tab}{a.Charisma}\n" +
                   $"안정성{tab}{CombatFormulas.Stability(cfg, c.Perception) * 100:0}%";
        }
    }
}
