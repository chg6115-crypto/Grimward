using System;
using System.Linq;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>패시브 처리: 아군 오라 갱신, 후열 보호(진형 붕괴 차단).</summary>
    public static class PassiveRules
    {
        /// <summary>오라를 다시 계산한다: 쓰러진 보유자의 오라는 지우고, 살아 있는 보유자의 오라를 같은 편 전원(본인 제외)에게 건다.
        /// 전투 시작, 소환 직후, 행동이 끝난 뒤 부른다.</summary>
        public static void RefreshAuras(BattleContext ctx)
        {
            var alive = ctx.Formation.AllAlive().ToList();
            var liveUids = new System.Collections.Generic.HashSet<int>(alive.Select(c => c.Uid));
            foreach (var c in alive) c.Effects.RemovePassives(e => !liveUids.Contains(e.SourceUid));

            foreach (var holder in alive)
                foreach (var p in holder.Passives)
                {
                    if (p.kind != DataKeys.Passive.AllyAura || p.effects == null) continue;
                    string key = p.id + "#" + holder.Uid;
                    foreach (var ally in alive.Where(a => a.Side == holder.Side && a != holder))
                        foreach (var def in p.effects)
                        {
                            bool has = ally.Effects.All.Any(e => e.IsPassive && e.SourceSkillId == key && e.Stat == def.stat);
                            if (!has) ally.Effects.Apply(new ActiveEffect(key, p.name, def, true, holder.Uid), false);
                        }
                }

            // 최대 HP 보너스는 효과 합계에서 다시 계산 (걸리면 HP 증가, 풀리면 넘치는 HP 감소)
            foreach (var c in alive)
                c.SetMaxHpBonus((int)Math.Floor(c.BaseMaxHp * c.Effects.Sum(DataKeys.Stat.MaxHpPct)));
        }

        /// <summary>target의 위치 이동을 막는 같은 편 보호자. 없으면 null.</summary>
        public static Combatant DisplacementGuard(BattleContext ctx, Combatant target)
        {
            int targetRank = ctx.Formation.RankOf(target);
            foreach (var guard in ctx.Formation.Alive(target.Side))
            {
                if (guard == target) continue;
                int guardRank = ctx.Formation.RankOf(guard);
                foreach (var p in guard.Passives)
                    if (p.kind == DataKeys.Passive.GuardBackline &&
                        Array.IndexOf(p.ranks, guardRank) >= 0 && Array.IndexOf(p.protectedRanks, targetRank) >= 0)
                        return guard;
            }
            return null;
        }
    }
}
