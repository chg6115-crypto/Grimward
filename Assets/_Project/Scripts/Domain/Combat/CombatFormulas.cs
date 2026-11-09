using System;
using Grimward.Domain.Config;
namespace Grimward.Domain.Combat
{
    public static class CombatFormulas
    {
        public static double HitChance(Combatant attacker, Combatant defender, bool isNight, BalanceConfig c)
        {
            double night = !isNight ? 0 : attacker.IsPlayer ? -c.NightPlayerHitPenalty : c.NightEnemyHitBonus;
            return Math.Max(c.MinHitChance, Math.Min(c.MaxHitChance,
                c.BaseHitChance + (attacker.Accuracy - defender.Evasion) * c.HitChancePerPoint + night));
        }
        public static int Damage(int rolledDamage, bool critical, Combatant defender, BalanceConfig c)
        {
            // decimal 경계에서 5.999999...로 한 단계 더 버려지는 것을 피한다.
            decimal exact = (decimal)rolledDamage * (critical ? (decimal)c.CritMultiplier : 1m)
                * (1m - Math.Min(1m, (decimal)defender.Armor +
                (defender.IsDefending ? (decimal)c.DefendReduction + (decimal)defender.DefendBonus : 0m)));
            return Math.Max(0, (int)Math.Floor(exact));
        }
        public static int FleeDamage(Combatant player, BalanceConfig c)
        {
            double value = player.MaxHp * c.FleeHpDamageRatio;
            double rounded = c.FleeDamageRounding == DamageRounding.Ceiling ? Math.Ceiling(value) :
                c.FleeDamageRounding == DamageRounding.Nearest ? Math.Round(value, MidpointRounding.AwayFromZero) : Math.Floor(value);
            return Math.Min((int)rounded, Math.Max(0, player.Hp - c.FleeMinimumHp));
        }
    }
}
