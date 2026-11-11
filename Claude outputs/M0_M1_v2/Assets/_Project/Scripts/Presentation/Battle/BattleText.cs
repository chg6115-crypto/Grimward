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
                if (e.IsPoison) sb.Append("<color=#7d7>독</color> ");
                else if (e.Value >= 0 == (e.Stat != DataKeys.Stat.DamageTaken)) sb.Append("<color=#7bf>▲</color>");
                else sb.Append("<color=#f88>▼</color>");
            }
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
                $"방어{tab}{c.Armor * 100:0.#}%{(ward != 0 ? $" (+{ward * 100:0}%)" : "")}\n" +
                $"속도{tab}{speed}\n" +
                $"진형 저항{tab}{c.FormationResist * 100:0}%";
        }

        public static string Effects(Combatant c)
        {
            if (c.Effects.All.Count == 0) return "<color=#888>걸린 효과 없음</color>";
            return string.Join("\n", c.Effects.All.Select(e =>
                e.IsPoison ? $"<color=#7d7>중독</color> 차례마다 {e.DamagePerTurn} · {e.Remaining}라운드"
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
    }
}
