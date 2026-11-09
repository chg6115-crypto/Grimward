using System;
namespace Grimward.Domain.Combat
{
    public sealed class Combatant
    {
        public int Id { get; }
        public string Name { get; }
        public bool IsPlayer { get; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public int AttackMin { get; }
        public int AttackMax { get; }
        public int Accuracy { get; }
        public int Evasion { get; }
        public int Speed { get; }
        public double CritChance { get; }
        public double Armor { get; }
        public double DefendBonus { get; }
        public bool IsDefending { get; internal set; }
        public bool IsAlive => Hp > 0;
        public Combatant(int id, string name, bool isPlayer, int hp, int attackMin, int attackMax,
            int accuracy, int evasion, int speed, double critChance, double armor, double defendBonus = 0)
        {
            if (hp <= 0 || attackMin < 0 || attackMax < attackMin) throw new ArgumentException("잘못된 전투원 수치");
            Id = id;
            Name = name;
            IsPlayer = isPlayer;
            MaxHp = Hp = hp;
            AttackMin = attackMin;
            AttackMax = attackMax;
            Accuracy = accuracy;
            Evasion = evasion;
            Speed = speed;
            CritChance = critChance;
            Armor = armor;
            DefendBonus = defendBonus;
        }
        public void TakeDamage(int damage)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            Hp = Math.Max(0, Hp - damage);
        }
    }
}
