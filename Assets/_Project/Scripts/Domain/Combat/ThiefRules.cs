using System.Linq;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>
    /// 소매치기 고블린 (기획안 11장).
    /// 낚아채기 명중 → 가방에서 의뢰 아이템을 뺀 아이템 1개 약탈(한 마리당 1회) → 다음 행동은 반드시 도망:
    /// 뒤로 2칸(자리 교환) + 회피 +10, 2라운드 뒤 자기 차례에 무조건 도주(훔친 아이템 손실).
    /// 도주 준비 중 진형 붕괴에 맞거나 기절하면 1턴 유예. 처치하면 되찾는다.
    /// </summary>
    public static class ThiefRules
    {
        public const int RunRanks = 2;
        public const int RunRounds = 2;
        public const int RunDodge = 10;

        /// <summary>약탈 기술이 명중한 직후.</summary>
        public static void OnHit(BattleContext ctx, Combatant actor, SkillDefinition skill)
        {
            if (!skill.steal || actor.HasStolen || !actor.IsAlive || ctx.Inventory == null) return;
            var candidates = ctx.Inventory.Entries
                .Where(e => e.Value > 0 && !ctx.Inventory.Protected.Contains(e.Key)).Select(e => e.Key).ToList();
            if (candidates.Count == 0) return;
            string id = candidates[ctx.Random.NextInt(0, candidates.Count - 1)];
            if (!ctx.Inventory.TryConsume(id)) return;
            actor.StolenItems.Add(id);
            actor.HasStolen = true;
            ctx.Emit(new BattleEvent(BattleEventKind.Stole, $"  {actor.Name}: 「{ItemName(ctx, id)}」 약탈!", actor, actor));
        }

        /// <summary>약탈 직후 강제 행동: 뒤로 2칸 + 회피 +10, 도주 라운드 지정.</summary>
        public static void StartRun(BattleContext ctx, Combatant thief)
        {
            int before = ctx.Formation.RankOf(thief);
            int after = ctx.Formation.Shift(thief, RunRanks);
            if (after != before)
                ctx.Emit(new BattleEvent(BattleEventKind.Moved, $"{thief.Name}: 도망 {before}번 → {after}번 칸", thief, thief, after));
            thief.Effects.Apply(new ActiveEffect("thief_run", "도주 준비", new EffectDefinition
            {
                kind = DataKeys.EffectKind.Stat, stat = DataKeys.Stat.Dodge, value = RunDodge, duration = 99,
                target = DataKeys.EffectTarget.Self
            }), true);
            thief.EscapeRound = ctx.Round + RunRounds;
            ctx.Emit(new BattleEvent(BattleEventKind.EffectApplied,
                $"  {thief.Name}: 도주 준비 (회피 +{RunDodge}, {RunRounds}라운드 뒤 도주)", thief, thief));
        }

        public static void Escape(BattleContext ctx, Combatant thief)
        {
            string items = string.Join("·", thief.StolenItems.Select(id => ItemName(ctx, id)));
            thief.MarkEscaped();
            ctx.Emit(new BattleEvent(BattleEventKind.Escaped,
                $"{thief.Name} 도주!" + (items.Length > 0 ? $" 약탈당한 「{items}」 잃음" : ""), thief, thief));
        }

        /// <summary>도주 준비 중 진형 붕괴·기절 → 1턴 유예.</summary>
        public static void Delay(BattleContext ctx, Combatant thief, string reason)
        {
            if (thief.EscapeRound <= 0 || !thief.IsAlive) return;
            thief.EscapeRound++;
            ctx.Emit(new BattleEvent(BattleEventKind.EscapeDelayed, $"  {thief.Name}: {reason} — 도주 1턴 지연", null, thief));
        }

        /// <summary>쓰러진 소매치기: 훔친 아이템을 되찾는다.</summary>
        public static void OnDeath(BattleContext ctx, Combatant c)
        {
            if (c.StolenItems.Count == 0 || ctx.Inventory == null) return;
            foreach (var id in c.StolenItems) ctx.Inventory.Add(id, 1);
            string items = string.Join("·", c.StolenItems.Select(id => ItemName(ctx, id)));
            c.StolenItems.Clear();
            ctx.Emit(new BattleEvent(BattleEventKind.ItemRecovered, $"  「{items}」 되찾음", null, c));
        }

        private static string ItemName(BattleContext ctx, string id)
        {
            var db = ctx.Factory.Database;
            return db.Items.Any(i => i.id == id) ? db.GetItem(id).name : id;
        }
    }
}
