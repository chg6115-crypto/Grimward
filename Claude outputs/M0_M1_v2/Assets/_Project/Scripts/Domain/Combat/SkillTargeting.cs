using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>기술 사용 가능 여부와 고를 수 있는 대상. 화면과 적 AI가 같이 쓴다.</summary>
    public static class SkillTargeting
    {
        public static bool CanUse(BattleContext ctx, Combatant actor, SkillDefinition skill, out string reason)
        {
            int rank = ctx.Formation.RankOf(actor);
            if (skill.useRanks == null || Array.IndexOf(skill.useRanks, rank) < 0)
            {
                reason = $"{rank}번 칸에서는 쓸 수 없습니다.";
                return false;
            }
            if (actor.IsOnCooldown(skill.id, ctx.Round))
            {
                reason = "재사용 대기 중입니다.";
                return false;
            }
            if (ValidTargets(ctx, actor, skill).Count == 0)
            {
                reason = "대상이 없습니다.";
                return false;
            }
            reason = null;
            return true;
        }

        public static bool CanUse(BattleContext ctx, Combatant actor, SkillDefinition skill) =>
            CanUse(ctx, actor, skill, out _);

        /// <summary>플레이어가 누를 수 있는 대상.</summary>
        public static List<Combatant> ValidTargets(BattleContext ctx, Combatant actor, SkillDefinition skill)
        {
            var f = ctx.Formation;
            switch (skill.target)
            {
                case DataKeys.Target.Enemy:
                    var foe = BattleContext.Opposite(actor.Side);
                    return f.Alive(foe).Where(c => Array.IndexOf(skill.targetRanks, f.RankOf(c)) >= 0).ToList();
                case DataKeys.Target.Ally:
                    return f.Alive(actor.Side).ToList();
                case DataKeys.Target.AllAllies:
                    return f.Alive(actor.Side).ToList();
                case DataKeys.Target.Self:
                    return new List<Combatant> { actor };
                case DataKeys.Target.AllySlot:
                    return f.HasRoom(actor.Side) ? new List<Combatant> { actor } : new List<Combatant>();
                default:
                    return new List<Combatant>();
            }
        }

        public static IEnumerable<SkillDefinition> UsableSkills(BattleContext ctx, Combatant actor) =>
            actor.Skills.Where(s => CanUse(ctx, actor, s));

        public static SkillLevel LevelOf(SkillDefinition skill, int level)
        {
            if (skill.levels == null || skill.levels.Length == 0) return new SkillLevel();
            int i = Math.Max(1, Math.Min(level, skill.levels.Length)) - 1;
            return skill.levels[i] ?? new SkillLevel();
        }
    }
}
