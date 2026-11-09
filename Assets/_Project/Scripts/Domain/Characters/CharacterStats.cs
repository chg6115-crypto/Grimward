using Grimward.Domain.Config;
namespace Grimward.Domain.Characters
{
    public sealed class CharacterStats
    {
        public int Strength { get; }
        public int Dexterity { get; }
        public int Vitality { get; }
        public int Intelligence { get; }
        public int Perception { get; }
        public int Charisma { get; }
        public CharacterStats(int strength, int dexterity, int vitality, int intelligence, int perception, int charisma)
        {
            Strength = strength;
            Dexterity = dexterity;
            Vitality = vitality;
            Intelligence = intelligence;
            Perception = perception;
            Charisma = charisma;
        }
        public static CharacterStats Swordsman(BalanceConfig c) => new CharacterStats(
            c.PlayerStrength, c.PlayerDexterity, c.PlayerVitality, c.PlayerIntelligence, c.PlayerPerception, c.PlayerCharisma);
    }
}
