using System.Linq;
using Grimward.Domain.Data;
using Grimward.Domain.Units;

namespace Grimward.Domain.Combat
{
    /// <summary>
    /// 부상 규칙 (기획안 11장).
    /// 발생: 부상 태그 기술 명중 시 확률(치명타면 100%) · 전투 중 HP가 처음 임계 이하 → 경상 악화(없으면 태그 부상 판정).
    /// 같은 종류는 중복되지 않고 다시 걸리면 중상으로 악화. 종류는 최대 injuryMaxPerAdventurer개.
    /// 전투 중 효과: 단계의 능력치 효과(패시브) + 출혈(전투마다 처음 N라운드).
    /// </summary>
    public static class InjuryRules
    {
        private static GameDatabase Db(BattleContext ctx) => ctx.Factory.Database;

        /// <summary>전투 시작: 가진 부상의 효과를 붙인다.</summary>
        public static void ApplyAtBattleStart(BattleContext ctx, Combatant c)
        {
            foreach (var inj in c.Injuries) ApplyStage(ctx, c, inj);
        }

        /// <summary>적의 기술이 명중해 피해를 준 직후 (대상이 살아 있을 때).</summary>
        public static void OnHit(BattleContext ctx, SkillDefinition skill, Combatant target, bool crit)
        {
            if (target.Side != BattleSide.Party || !target.IsAlive) return;
            string tag = skill.injury != null && !string.IsNullOrEmpty(skill.injury.injuryId) ? skill.injury.injuryId : null;

            bool changed = false;
            if (tag != null)
            {
                double chance = crit ? 1.0 : skill.injury.chance;
                if (ctx.Random.NextDouble() < chance) changed = Inflict(ctx, target, tag);
            }

            var cfg = ctx.Config;
            if (!target.InjuryThresholdChecked && cfg.injuryHpThreshold > 0 && target.Hp <= target.MaxHp * cfg.injuryHpThreshold)
            {
                target.InjuryThresholdChecked = true;
                if (changed) return; // 같은 타격에서 두 번 바꾸지 않는다
                var minor = target.Injuries.FirstOrDefault(i => !i.Severe);
                if (minor != null) Worsen(ctx, target, minor);
                else if (target.Injuries.Count == 0 && tag != null && ctx.Random.NextDouble() < cfg.injuryThresholdChance)
                    Inflict(ctx, target, tag);
            }
        }

        /// <summary>부상을 건다. 같은 종류가 경상이면 중상으로 악화. 바뀌었으면 true.</summary>
        public static bool Inflict(BattleContext ctx, Combatant target, string injuryId)
        {
            var existing = target.Injuries.FirstOrDefault(i => i.Id == injuryId);
            if (existing != null)
            {
                if (existing.Severe) return false;
                Worsen(ctx, target, existing);
                return true;
            }
            if (target.Injuries.Count >= ctx.Config.injuryMaxPerAdventurer) return false;
            var inj = new InjuryState(injuryId);
            target.Injuries.Add(inj);
            ApplyStage(ctx, target, inj);
            ctx.Emit(new BattleEvent(BattleEventKind.Injured,
                $"  {target.Name}: 부상 「{Db(ctx).InjuryStageName(injuryId, false)}」", null, target));
            return true;
        }

        public static void Worsen(BattleContext ctx, Combatant target, InjuryState inj)
        {
            if (inj.Severe) return;
            RemoveStage(target, inj);
            inj.Severe = true;
            ApplyStage(ctx, target, inj);
            ctx.Emit(new BattleEvent(BattleEventKind.Injured,
                $"  {target.Name}: 부상 악화 「{Db(ctx).InjuryStageName(inj.Id, true)}」", null, target));
        }

        /// <summary>현장 치료: 현장 치료 가능한 부상 — 경상은 제거, 중상은 경상으로. 바뀐 게 있으면 true.</summary>
        public static bool Treat(BattleContext ctx, Combatant target)
        {
            bool any = false;
            foreach (var inj in target.Injuries.ToList())
            {
                if (!Db(ctx).GetInjury(inj.Id).fieldTreatable) continue;
                RemoveStage(target, inj);
                if (inj.Severe)
                {
                    inj.Severe = false;
                    ApplyStage(ctx, target, inj, withBleed: false); // 치료한 상처는 이번 전투에서 다시 벌어지지 않는다
                    ctx.Emit(new BattleEvent(BattleEventKind.InjuryTreated,
                        $"  {target.Name}: 「{Db(ctx).InjuryStageName(inj.Id, true)}」 → 「{Db(ctx).InjuryStageName(inj.Id, false)}」", null, target));
                }
                else
                {
                    target.Injuries.Remove(inj);
                    ctx.Emit(new BattleEvent(BattleEventKind.InjuryTreated,
                        $"  {target.Name}: 「{Db(ctx).InjuryStageName(inj.Id, false)}」 치료", null, target));
                }
                any = true;
            }
            return any;
        }

        private static string Key(InjuryState inj) => ActiveEffect.InjuryPrefix + inj.Id;

        private static void RemoveStage(Combatant c, InjuryState inj)
        {
            string key = Key(inj);
            c.Effects.RemoveAll(e => e.SourceSkillId == key);
        }

        private static void ApplyStage(BattleContext ctx, Combatant c, InjuryState inj, bool withBleed = true)
        {
            var def = Db(ctx).GetInjury(inj.Id);
            var stage = inj.Severe ? def.major : def.minor;
            if (stage == null) return;
            string key = Key(inj), name = Db(ctx).InjuryStageName(inj.Id, inj.Severe);
            if (stage.effects != null)
                foreach (var e in stage.effects)
                    c.Effects.Apply(new ActiveEffect(key, name, e, isPassive: true, sourceUid: c.Uid), false);
            if (withBleed && stage.bleedPerTurn > 0 && stage.bleedRounds > 0)
            {
                var bleed = new EffectDefinition
                {
                    kind = DataKeys.EffectKind.Bleed, damagePerTurn = stage.bleedPerTurn, duration = stage.bleedRounds,
                    target = DataKeys.EffectTarget.Target
                };
                c.Effects.Apply(new ActiveEffect(key, name, bleed), c == ctx.CurrentActor);
            }
        }
    }
}
