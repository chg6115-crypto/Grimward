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

        /// <summary>칸 아래 짧은 상태 표시 (버프 ▲ · 디버프 ▼ · 중독).</summary>
        public static string StatusIcons(Combatant c)
        {
            var sb = new StringBuilder();
            foreach (var e in c.Effects.All)
            {
                if (e.IsBleed) { sb.Append("<color=#e55>출혈</color> "); continue; }
                if (e.IsInjury) continue; // 부상 능력치 효과는 아래 「부상」 표시로 묶는다
                if (e.IsPoison) sb.Append("<color=#7d7>독</color> ");
                else if (e.Value >= 0 == (e.Stat != DataKeys.Stat.DamageTaken)) sb.Append("<color=#7bf>▲</color>");
                else sb.Append("<color=#f88>▼</color>");
            }
            if (c.Injuries.Count > 0) sb.Append(" <color=#e77>부상</color>");
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
                $"피해{tab}{c.AttackMin}-{c.AttackMax}\n" +
                $"회피{tab}{dodge}\n" +
                $"방어{tab}{c.EffectiveArmor * 100:0.#}%{(ward != 0 ? $" (+{ward * 100:0}%)" : "")}\n" +
                $"속도{tab}{speed}\n" +
                $"진형 저항{tab}{c.FormationResist * 100:0}%";
        }

        public static string Effects(Combatant c)
        {
            if (c.Effects.All.Count == 0) return "<color=#888>걸린 효과 없음</color>";
            return string.Join("\n", c.Effects.All.Select(e =>
                e.IsPoison ? $"<color=#7d7>중독</color> 차례마다 {e.DamagePerTurn} · {e.Remaining}라운드"
                    : e.IsBleed ? $"<color=#e55>출혈</color> ({e.SourceSkillName}) 차례마다 {e.DamagePerTurn} · {e.Remaining}라운드"
                    : e.IsInjury ? $"<color=#e77>{e.SourceSkillName}</color>: {StatLabel(e)} · 부상"
                    : e.IsPassive ? $"<color=#eb4>{e.SourceSkillName}</color>: {StatLabel(e)} · 패시브"
                    : $"{e.SourceSkillName}: {StatLabel(e)} · {e.Remaining}라운드"));
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
        public static string SkillTooltip(SkillDefinition s, int level, GameDatabase db, bool forEnemy = false)
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
                if (s.damaging) sb.Append($"피해 보정: {lv.damage * 100:+0;-0;0}%\n");
                if (s.damaging) sb.Append($"치명타 보정: {lv.crit * 100:+0.#;-0.#;0}%\n");
                if (s.ignoreArmor) sb.Append("대상 방어도 무시\n");
                if (s.fixedDamage != null && s.fixedDamage.max > 0)
                    sb.Append($"피해 {s.fixedDamage.min}~{s.fixedDamage.max}" +
                              (s.fixedDamage.intelligenceMultiplier > 0 ? " + 지능" : "") + " (무기 대신)\n");
            }
            if (s.typeBonuses != null)
                foreach (var b in s.typeBonuses)
                    sb.Append($"<color=#eb4>{db.TypeName(b.type)}</color>에게 피해 {b.damage * 100:+0;-0}%\n");
            if (s.heal != null && s.heal.max > 0)
                sb.Append($"회복 {s.heal.min}{(s.heal.max > s.heal.min ? $"~{s.heal.max}" : "")}" +
                          (s.heal.intelligenceDivisor > 0 ? $" + 지능÷{s.heal.intelligenceDivisor}" : "") + "\n");
            if (s.curePoison) sb.Append("해독\n");
            if (s.displacement != null && s.displacement.ranks != 0)
                sb.Append($"<color=#c9f>{(s.displacement.ranks > 0 ? "밀어내기" : "끌어당기기")} {System.Math.Abs(s.displacement.ranks)}칸</color>" +
                          $" (기본 {s.displacement.chance * 100:0}% − 저항)\n");
            if (s.effects != null)
                foreach (var e in s.effects)
                {
                    string who = e.target == DataKeys.EffectTarget.Self ? "자신: "
                        : e.target == DataKeys.EffectTarget.AllAllies ? "아군 전체: "
                        : s.target == DataKeys.Target.Enemy ? "명중 시: " : "";
                    string doubled = string.IsNullOrEmpty(e.doubledVsType) ? "" : $" ({db.TypeName(e.doubledVsType)} 상대 2배)";
                    sb.Append($"{who}{SkillResolver.DescribeEffect(e)}, {e.duration}라운드{doubled}\n");
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
        public static string Attributes(Combatant c)
        {
            var a = c.Source;
            if (a == null) return "";
            const string tab = "<pos=48%>";
            return $"근력{tab}{a.Strength}\n민첩{tab}{a.Dexterity}\n건강{tab}{a.Vitality}\n" +
                   $"지능{tab}{a.Intelligence}\n지각{tab}{a.Perception}\n매력{tab}{a.Charisma}";
        }
    }
}
