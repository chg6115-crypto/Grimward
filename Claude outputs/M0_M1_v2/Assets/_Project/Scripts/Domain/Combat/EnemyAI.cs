using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>적의 이번 행동.</summary>
    public sealed class EnemyDecision
    {
        public SkillDefinition Skill { get; }
        public Combatant Target { get; }
        /// <summary>기술이 없을 때 이동 방향 (−1 앞, +1 뒤, 0 넘기기).</summary>
        public int MoveDelta { get; }

        private EnemyDecision(SkillDefinition skill, Combatant target, int moveDelta)
        {
            Skill = skill; Target = target; MoveDelta = moveDelta;
        }

        public static EnemyDecision UseSkill(SkillDefinition s, Combatant t) => new EnemyDecision(s, t, 0);
        public static EnemyDecision Move(int delta) => new EnemyDecision(null, null, delta);
        public static EnemyDecision Pass() => new EnemyDecision(null, null, 0);
    }

    /// <summary>
    /// 규칙 기반 단순 적 AI.
    /// 1) 아군이 절반 이하 HP면 회복  2) 빈 칸이 있으면 소환  3) 걸려 있지 않은 강화 기술은 가끔
    /// 4) 나머지는 쓸 수 있는 공격 기술 중 무작위, 대상도 무작위  5) 쓸 기술이 없으면 쓸 수 있는 칸 쪽으로 이동.
    /// </summary>
    public sealed class EnemyAI
    {
        private readonly BattleContext ctx;
        public EnemyAI(BattleContext ctx) { this.ctx = ctx; }

        public EnemyDecision Decide(Combatant actor)
        {
            var usable = SkillTargeting.UsableSkills(ctx, actor).ToList();
            if (usable.Count == 0) return DecideMove(actor);

            var heal = usable.FirstOrDefault(s => s.heal != null && s.heal.max > 0);
            if (heal != null)
            {
                var wounded = SkillTargeting.ValidTargets(ctx, actor, heal)
                    .Where(c => c.Hp <= c.MaxHp * ctx.Config.aiHealThreshold).OrderBy(c => c.Hp).FirstOrDefault();
                if (wounded != null) return EnemyDecision.UseSkill(heal, wounded);
            }

            var summon = usable.FirstOrDefault(s => s.target == DataKeys.Target.AllySlot);
            if (summon != null) return EnemyDecision.UseSkill(summon, actor);

            var buffs = usable.Where(s => !s.damaging && (s.heal == null || s.heal.max <= 0) && s.target != DataKeys.Target.Enemy &&
                                          s.target != DataKeys.Target.AllySlot).ToList();
            foreach (var buff in buffs)
            {
                var candidates = SkillTargeting.ValidTargets(ctx, actor, buff)
                    .Where(c => !c.Effects.All.Any(e => e.SourceSkillId == buff.id)).ToList();
                if (candidates.Count > 0 && ctx.Random.NextDouble() < ctx.Config.aiBuffChance)
                    return EnemyDecision.UseSkill(buff, Pick(candidates));
            }

            var attacks = usable.Where(s => s.target == DataKeys.Target.Enemy).ToList();
            if (attacks.Count > 0)
            {
                var skill = Pick(attacks);
                return EnemyDecision.UseSkill(skill, Pick(SkillTargeting.ValidTargets(ctx, actor, skill)));
            }

            var any = Pick(usable);
            return EnemyDecision.UseSkill(any, Pick(SkillTargeting.ValidTargets(ctx, actor, any)));
        }

        private EnemyDecision DecideMove(Combatant actor)
        {
            int rank = ctx.Formation.RankOf(actor);
            var wanted = actor.Skills.Where(s => s.useRanks != null).SelectMany(s => s.useRanks).Distinct().ToList();
            if (wanted.Count == 0) return EnemyDecision.Pass();
            int nearest = wanted.OrderBy(r => System.Math.Abs(r - rank)).First();
            int delta = nearest > rank ? 1 : nearest < rank ? -1 : 0;
            int count = ctx.Formation.Alive(actor.Side).Count();
            if (delta == 0 || rank + delta < 1 || rank + delta > count) return EnemyDecision.Pass();
            return EnemyDecision.Move(delta);
        }

        private T Pick<T>(IReadOnlyList<T> list) => list[ctx.Random.NextInt(0, list.Count - 1)];
    }
}
