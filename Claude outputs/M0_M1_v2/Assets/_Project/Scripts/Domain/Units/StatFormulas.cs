using Grimward.Domain.Data;

namespace Grimward.Domain.Units
{
    /// <summary>모험가 파생 능력치 공식 (확정기획 7장).</summary>
    public static class StatFormulas
    {
        public static int MaxHp(BalanceConfig c, Adventurer a) => c.hpBase + a.Vitality * c.hpPerVitality;

        public static int AttackBonus(BalanceConfig c, Adventurer a) =>
            a.Class.attackStat == DataKeys.AttackStat.Intelligence
                ? a.Intelligence / c.attackStrengthDivisor
                : a.Strength / c.attackStrengthDivisor;

        public static int AttackMin(BalanceConfig c, Adventurer a) => a.Class.weaponMin + AttackBonus(c, a);
        public static int AttackMax(BalanceConfig c, Adventurer a) => a.Class.weaponMax + AttackBonus(c, a);

        public static int Accuracy(BalanceConfig c, Adventurer a) =>
            a.Dexterity * c.accuracyPerDexterity + a.Level * c.accuracyPerLevel;

        public static int Dodge(BalanceConfig c, Adventurer a) =>
            a.Dexterity * c.dodgePerDexterity + a.Perception / c.dodgePerceptionDivisor;

        public static int Speed(BalanceConfig c, Adventurer a) =>
            a.Dexterity + (a.Strength + a.Intelligence + a.Vitality) / c.speedStatDivisor;

        /// <summary>기본 치명타 확률 (최소값 보정은 전투 공식에서).</summary>
        public static double Crit(BalanceConfig c, Adventurer a) => c.critBase + a.Dexterity * c.critPerDexterity;
    }
}
