using Grimward.Domain.Config;
namespace Grimward.Domain.Characters
{
    public static class StatFormulas
    {
        public static int MaxHp(CharacterStats s, BalanceConfig c) => c.HpBase + s.Vitality * c.HpPerVit;
        public static int Attack(int weaponDamage, CharacterStats s, BalanceConfig c) => weaponDamage + s.Strength / c.AttackStrDivisor;
        public static int Accuracy(CharacterStats s, int level, BalanceConfig c) => s.Dexterity * c.AccuracyPerDex + level * c.AccuracyPerLevel;
        public static int Evasion(CharacterStats s, BalanceConfig c) => s.Dexterity * c.EvasionPerDex + s.Perception / c.EvasionPerPerDivisor;
        public static double CritChance(CharacterStats s, BalanceConfig c) => c.BaseCritChance + s.Dexterity * c.CritChancePerDex;
        public static int Speed(CharacterStats s, BalanceConfig c) => s.Dexterity + (s.Strength + s.Intelligence + s.Vitality) / c.SpeedStatDivisor;
    }
}
