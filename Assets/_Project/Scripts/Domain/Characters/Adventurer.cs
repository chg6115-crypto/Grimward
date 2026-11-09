using Grimward.Domain.Combat;
using Grimward.Domain.Config;
namespace Grimward.Domain.Characters
{
    public sealed class Adventurer
    {
        public CharacterStats Stats { get; }
        public int Level { get; }
        public Combatant Fighter { get; }
        public Adventurer(BalanceConfig c)
        {
            Stats = CharacterStats.Swordsman(c);
            Level = c.PlayerLevel;
            Fighter = new Combatant(-1, "검사", true, StatFormulas.MaxHp(Stats,c),
                StatFormulas.Attack(c.PlayerWeaponMin,Stats,c), StatFormulas.Attack(c.PlayerWeaponMax,Stats,c),
                StatFormulas.Accuracy(Stats,Level,c), StatFormulas.Evasion(Stats,c),
                StatFormulas.Speed(Stats,c), StatFormulas.CritChance(Stats,c), c.PlayerArmor, c.SwordsmanDefendBonus);
        }
    }
}
