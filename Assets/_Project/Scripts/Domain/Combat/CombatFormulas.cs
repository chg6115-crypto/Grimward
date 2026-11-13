using System;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>명중·치명·피해 공식 (확정기획 6장). 상태 없는 순수 함수.</summary>
    public static class CombatFormulas
    {
        /// <param name="accuracyBonus">기술 명중 보정 + 버프 (명중 수치, %p와 같은 단위).</param>
        public static double HitChance(BalanceConfig c, int attackerAccuracy, int defenderDodge,
            double accuracyBonus, BattleSide attackerSide, bool isNight, double nightScale = 1.0)
        {
            double chance = c.baseHitChance + (accuracyBonus + attackerAccuracy - defenderDodge) / 100.0;
            if (isNight)
                chance += (attackerSide == BattleSide.Party ? -c.nightPartyAccuracyPenalty : c.nightEnemyAccuracyBonus) * nightScale;
            return Clamp(chance, c.hitChanceMin, c.hitChanceMax);
        }

        public static double CritChance(BalanceConfig c, double baseCrit, double bonus) =>
            Math.Max(c.critMin, baseCrit + bonus);

        /// <summary>피해 감소율 = 방어도 − 받는 피해 보정, 상한 적용. 음수면 피해 증가.</summary>
        public static double Reduction(BalanceConfig c, double armor, double damageTakenModifier) =>
            Math.Min(c.damageReductionCap, armor - damageTakenModifier);

        /// <summary>피해 = 굴린 값 × (1 + 피해 보정) × 치명 × (1 − 감소율) → 소수점 버림, 최소 minHitDamage(명중 시 1).
        /// decimal로 계산해 부동소수 오차로 한 단계 더 버려지지 않게 한다.</summary>
        public static int Damage(BalanceConfig c, int rolled, double damageModifier, bool crit, double reduction)
        {
            decimal value = rolled
                            * (1m + ToDecimal(damageModifier))
                            * (crit ? ToDecimal(c.critMultiplier) : 1m)
                            * (1m - ToDecimal(reduction));
            int floored = value <= 0m ? 0 : (int)decimal.Floor(value);
            return Math.Max(c.minHitDamage, floored);
        }

        /// <summary>기술 능력치 보너스 = 내림((근력×a + 민첩×b + 지능×c) ÷ statWeightDivisor). 가중치 없으면 0.</summary>
        public static int StatBonus(BalanceConfig c, Combatant a, StatWeightDefinition w)
        {
            if (w == null || w.Sum == 0 || c.statWeightDivisor <= 0) return 0;
            return (a.Strength * w.strength + a.Dexterity * w.dexterity + a.Intelligence * w.intelligence) / c.statWeightDivisor;
        }

        /// <summary>안정성 = 지각 × 계수, 상한 stabilityMax.</summary>
        public static double Stability(BalanceConfig c, int perception) =>
            Math.Max(0, Math.Min(c.stabilityMax, perception * c.stabilityPerPerception));

        /// <summary>
        /// 기술 피해 굴림 범위 (피해 보정·치명·감소 전).
        /// 편차가 있으면: 중앙값 = 무기 평균 + 능력치 보너스, 하한 = 중앙값 × (1 − 편차 × (1 − 안정성)), 상한 = 중앙값 × (1 + 편차), 각각 내림.
        /// 편차가 0이면 공격력 범위 그대로 (몬스터 기본).
        /// </summary>
        public static void DamageRange(BalanceConfig c, Combatant a, SkillDefinition s, out int min, out int max)
        {
            if (s.variance <= 0)
            {
                min = a.AttackMin; max = a.AttackMax;
                return;
            }
            decimal center = (a.WeaponMin + a.WeaponMax) / 2m + StatBonus(c, a, s.weights);
            decimal v = ToDecimal(s.variance), st = ToDecimal(Stability(c, a.Perception));
            min = (int)decimal.Floor(center * (1m - v * (1m - st)));
            max = (int)decimal.Floor(center * (1m + v));
            if (min < 0) min = 0;
            if (min > max) min = max;
        }

        /// <summary>double 입력값(데이터의 0.4 등)을 짧은 십진 표현으로 바꾼다.</summary>
        public static decimal ToDecimal(double value) => decimal.Round((decimal)value, 6);

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
