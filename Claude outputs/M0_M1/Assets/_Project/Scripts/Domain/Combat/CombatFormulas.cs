using System;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>명중·치명·피해 공식 (확정기획 6장). 상태 없는 순수 함수.</summary>
    public static class CombatFormulas
    {
        /// <param name="accuracyBonus">기술 명중 보정 + 버프 (명중 수치, %p와 같은 단위).</param>
        public static double HitChance(BalanceConfig c, int attackerAccuracy, int defenderDodge,
            double accuracyBonus, BattleSide attackerSide, bool isNight)
        {
            double chance = c.baseHitChance + (accuracyBonus + attackerAccuracy - defenderDodge) / 100.0;
            if (isNight)
                chance += attackerSide == BattleSide.Party ? -c.nightPartyAccuracyPenalty : c.nightEnemyAccuracyBonus;
            return Clamp(chance, c.hitChanceMin, c.hitChanceMax);
        }

        public static double CritChance(BalanceConfig c, double baseCrit, double bonus) =>
            Math.Max(c.critMin, baseCrit + bonus);

        /// <summary>피해 감소율 = 방어도 − 받는 피해 보정, 상한 적용. 음수면 피해 증가.</summary>
        public static double Reduction(BalanceConfig c, double armor, double damageTakenModifier) =>
            Math.Min(c.damageReductionCap, armor - damageTakenModifier);

        /// <summary>피해 = 굴린 값 × (1 + 피해 보정) × 치명 × (1 − 감소율) → 소수점 버림, 최소 0.
        /// decimal로 계산해 부동소수 오차로 한 단계 더 버려지지 않게 한다.</summary>
        public static int Damage(BalanceConfig c, int rolled, double damageModifier, bool crit, double reduction)
        {
            decimal value = rolled
                            * (1m + ToDecimal(damageModifier))
                            * (crit ? ToDecimal(c.critMultiplier) : 1m)
                            * (1m - ToDecimal(reduction));
            return value <= 0m ? 0 : (int)decimal.Floor(value);
        }

        /// <summary>double 입력값(데이터의 0.4 등)을 짧은 십진 표현으로 바꾼다.</summary>
        public static decimal ToDecimal(double value) => decimal.Round((decimal)value, 6);

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
