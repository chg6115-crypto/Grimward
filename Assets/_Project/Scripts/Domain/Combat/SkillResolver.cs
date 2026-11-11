using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>
    /// 기술 하나의 효과를 처리한다: 대상 고정 → 대상별 명중·치명·피해 → 적중 시 효과·위치 이동 → 자신·아군 효과 → 회복·소환.
    /// 사망자 정리(진형 당김)는 하지 않는다 — 행동이 끝난 뒤 BattleState가 한 번.
    /// </summary>
    public sealed class SkillResolver
    {
        private readonly BattleContext ctx;

        public SkillResolver(BattleContext ctx)
        {
            this.ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public void Resolve(Combatant actor, SkillDefinition skill, Combatant chosen)
        {
            if (!SkillTargeting.CanUse(ctx, actor, skill, out string reason))
                throw new InvalidOperationException($"{skill.name}: {reason}");
            var valid = SkillTargeting.ValidTargets(ctx, actor, skill);
            if (chosen == null || !valid.Contains(chosen))
                throw new InvalidOperationException($"{skill.name}: 고를 수 없는 대상입니다.");

            ctx.Emit(new BattleEvent(BattleEventKind.SkillUsed, $"{actor.Name}: {skill.name}", actor, chosen));
            var level = SkillTargeting.LevelOf(skill, actor.Level);

            // 광역 대상은 공격 시작 시 위치로 고정한다.
            var targets = FixTargets(actor, skill, chosen);

            if (skill.target == DataKeys.Target.Enemy)
            {
                foreach (var target in targets)
                {
                    if (!target.IsAlive) continue; // 중간에 죽은 대상은 건너뜀
                    ResolveAgainstEnemy(actor, skill, level, target);
                }
            }
            else
            {
                foreach (var target in targets)
                {
                    if (!target.IsAlive) continue;
                    ApplySupport(actor, skill, target);
                }
            }

            ApplyCasterEffects(actor, skill);
            if (skill.summon != null && !string.IsNullOrEmpty(skill.summon.monsterId)) Summon(actor, skill);
            actor.StartCooldown(skill, ctx.Round);
            ctx.LastSkillRound[BattleContext.SkillRoundKey(actor.Side, skill.id)] = ctx.Round;
        }

        private List<Combatant> FixTargets(Combatant actor, SkillDefinition skill, Combatant chosen)
        {
            if (skill.target == DataKeys.Target.Enemy && skill.multi)
                return SkillTargeting.ValidTargets(ctx, actor, skill);
            if (skill.target == DataKeys.Target.AllAllies)
                return ctx.Formation.Alive(actor.Side).ToList();
            if (skill.target == DataKeys.Target.AllySlot)
                return new List<Combatant>();
            return new List<Combatant> { chosen };
        }

        private void ResolveAgainstEnemy(Combatant actor, SkillDefinition skill, SkillLevel level, Combatant target)
        {
            var cfg = ctx.Config;
            double accBonus = level.accuracy + actor.Effects.Sum(DataKeys.Stat.Accuracy, target.Types);
            int dodge = target.Dodge + (int)target.Effects.Sum(DataKeys.Stat.Dodge);
            double hit = CombatFormulas.HitChance(cfg, actor.Accuracy, dodge, accBonus, actor.Side, ctx.IsNight);
            if (ctx.Random.NextDouble() >= hit)
            {
                ctx.Emit(new BattleEvent(BattleEventKind.Missed, $"  {target.Name}에게 빗나감", actor, target));
                return;
            }

            if (skill.damaging)
            {
                double critBonus = level.crit + actor.Effects.Sum(DataKeys.Stat.Crit, target.Types);
                bool crit = ctx.Random.NextDouble() < CombatFormulas.CritChance(cfg, actor.BaseCrit, critBonus);
                int rolled = RollBase(actor, skill);
                double dmgMod = level.damage + actor.Effects.Sum(DataKeys.Stat.DamagePct, target.Types) +
                                TypeBonus(skill, target);
                double armor = skill.ignoreArmor ? 0 : target.EffectiveArmor;
                double reduction = CombatFormulas.Reduction(cfg, armor, target.Effects.Sum(DataKeys.Stat.DamageTaken));
                int damage = CombatFormulas.Damage(cfg, rolled, dmgMod, crit, reduction);
                int dealt = target.TakeDamage(damage);
                ctx.Emit(new BattleEvent(BattleEventKind.Damaged,
                    $"  {target.Name}에게 {dealt} 피해{(crit ? " (치명타!)" : "")}", actor, target, dealt, crit));
                if (!target.IsAlive) { ReportDeath(target); return; }
                if (dealt > 0) InjuryRules.OnHit(ctx, skill, target, crit);
            }

            ApplyEffects(actor, skill, target, DataKeys.EffectTarget.Target);
            if (skill.displacement != null && skill.displacement.ranks != 0) Displace(skill, target);
        }

        private int RollBase(Combatant actor, SkillDefinition skill)
        {
            var fixedDmg = skill.fixedDamage;
            if (fixedDmg != null && fixedDmg.max > 0)
                return ctx.Random.NextInt(fixedDmg.min, fixedDmg.max) + actor.Intelligence * fixedDmg.intelligenceMultiplier;
            return ctx.Random.NextInt(actor.AttackMin, actor.AttackMax);
        }

        private static double TypeBonus(SkillDefinition skill, Combatant target)
        {
            if (skill.typeBonuses == null) return 0;
            return skill.typeBonuses.Where(b => target.HasType(b.type)).Sum(b => b.damage);
        }

        private void ApplySupport(Combatant actor, SkillDefinition skill, Combatant target)
        {
            if (skill.heal != null && skill.heal.max > 0)
            {
                int amount = ctx.Random.NextInt(skill.heal.min, skill.heal.max);
                if (skill.heal.intelligenceDivisor > 0) amount += actor.Intelligence / skill.heal.intelligenceDivisor;
                int healed = target.Heal(amount);
                ctx.Emit(new BattleEvent(BattleEventKind.Healed, $"  {target.Name} HP +{healed}", actor, target, healed));
            }
            if (skill.curePoison && target.Effects.HasPoison)
            {
                target.Effects.RemovePoison();
                ctx.Emit(new BattleEvent(BattleEventKind.PoisonCured, $"  {target.Name} 해독", actor, target));
            }
            if (skill.treatInjury) InjuryRules.Treat(ctx, target);
            ApplyEffects(actor, skill, target, DataKeys.EffectTarget.Target);
        }

        private void ApplyCasterEffects(Combatant actor, SkillDefinition skill)
        {
            if (skill.effects == null) return;
            ApplyEffects(actor, skill, actor, DataKeys.EffectTarget.Self);
            foreach (var ally in ctx.Formation.Alive(actor.Side).ToList())
                ApplyEffects(actor, skill, ally, DataKeys.EffectTarget.AllAllies);
        }

        private void ApplyEffects(Combatant actor, SkillDefinition skill, Combatant holder, string effectTarget)
        {
            if (skill.effects == null || !holder.IsAlive) return;
            foreach (var def in skill.effects)
            {
                if (def.target != effectTarget) continue;
                holder.Effects.Apply(new ActiveEffect(skill.id, skill.name, def), holder == ctx.CurrentActor);
                ctx.Emit(new BattleEvent(BattleEventKind.EffectApplied,
                    $"  {holder.Name}: {DescribeEffect(def)} ({def.duration}라운드)", actor, holder));
            }
        }

        private void Displace(SkillDefinition skill, Combatant target)
        {
            if (!target.IsAlive) return;
            var guard = PassiveRules.DisplacementGuard(ctx, target);
            if (guard != null)
            {
                string passive = guard.Passives.First(p => p.kind == DataKeys.Passive.GuardBackline).name;
                ctx.Emit(new BattleEvent(BattleEventKind.DisplaceResisted,
                    $"  {guard.Name}의 「{passive}」: {target.Name}을(를) 움직일 수 없다", guard, target));
                return;
            }
            double chance = skill.displacement.chance - target.FormationResist;
            if (ctx.Random.NextDouble() >= chance)
            {
                ctx.Emit(new BattleEvent(BattleEventKind.DisplaceResisted, $"  {target.Name}: 진형 붕괴 저항", null, target));
                return;
            }
            int before = ctx.Formation.RankOf(target);
            int after = ctx.Formation.Shift(target, skill.displacement.ranks);
            if (after != before)
                ctx.Emit(new BattleEvent(BattleEventKind.Displaced,
                    $"  {target.Name}: {before}번 → {after}번 칸", null, target, after));
        }

        private void Summon(Combatant actor, SkillDefinition skill)
        {
            bool alone = ctx.Formation.Alive(actor.Side).Count() == 1;
            int count = alone && skill.summon.countWhenAlone > 0 ? skill.summon.countWhenAlone : skill.summon.count;
            count = Math.Min(count, SkillTargeting.SummonQuota(ctx, actor, skill));
            int size = ctx.Factory.SizeOf(skill.summon.monsterId);
            var before = ctx.Formation.Alive(actor.Side).ToList();
            int placed = 0;
            for (int i = 0; i < count && ctx.Formation.HasRoom(actor.Side, size); i++)
            {
                var summoned = ctx.Factory.FromMonster(skill.summon.monsterId);
                if (skill.summon.inFront) ctx.Formation.Insert(summoned, i);
                else ctx.Formation.Add(summoned);
                placed++;
                PassiveRules.RefreshAuras(ctx);
                ctx.Emit(new BattleEvent(BattleEventKind.Summoned,
                    $"  {summoned.Name} 등장 ({ctx.Formation.RankOf(summoned)}번 칸)", actor, summoned));
            }
            if (skill.summon.inFront && placed > 0 && before.Count > 0)
                ctx.Emit(new BattleEvent(BattleEventKind.Moved,
                    $"  {string.Join("·", before.Select(c => c.Name))}: 뒤로 {placed * size}칸 물러남", actor, actor));
        }

        private void ReportDeath(Combatant target)
        {
            ctx.Emit(new BattleEvent(BattleEventKind.Died, $"  {target.Name} 쓰러짐", null, target));
            if (target.Side == BattleSide.Enemy) ctx.KilledMonsterIds.Add(target.DefinitionId);
        }

        public static string DescribeEffect(EffectDefinition def)
        {
            if (def.kind == DataKeys.EffectKind.Poison) return $"중독(차례마다 {def.damagePerTurn})";
            if (def.kind == DataKeys.EffectKind.Bleed) return $"출혈(차례마다 {def.damagePerTurn})";
            string sign = def.value >= 0 ? "+" : "";
            switch (def.stat)
            {
                case DataKeys.Stat.DamagePct: return $"피해 {sign}{def.value * 100:0}%";
                case DataKeys.Stat.Accuracy: return $"명중 {sign}{def.value:0}";
                case DataKeys.Stat.Crit: return $"치명 {sign}{def.value * 100:0}%p";
                case DataKeys.Stat.Dodge: return $"회피 {sign}{def.value:0}";
                case DataKeys.Stat.Speed: return $"속도 {sign}{def.value:0}";
                case DataKeys.Stat.DamageTaken: return $"받는 피해 {sign}{def.value * 100:0}%";
                case DataKeys.Stat.Armor: return $"방어도 {sign}{def.value * 100:0}%";
                case DataKeys.Stat.MaxHpPct: return $"최대 HP {sign}{def.value * 100:0}%";
                default: return def.stat;
            }
        }
    }
}
