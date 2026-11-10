namespace Grimward.Domain.Data
{
    /// <summary>데이터 문자열 종류값. 밸런스 수치가 아니라 데이터 형식의 이름이다.</summary>
    public static class DataKeys
    {
        public static class Target
        {
            public const string Enemy = "enemy";
            public const string Ally = "ally";
            public const string Self = "self";
            public const string AllAllies = "allAllies";
            public const string AllySlot = "allySlot";
        }

        public static class EffectKind
        {
            public const string Stat = "stat";
            public const string Poison = "poison";
        }

        public static class EffectTarget
        {
            public const string Target = "target";
            public const string Self = "self";
            public const string AllAllies = "allAllies";
        }

        public static class Stat
        {
            public const string DamagePct = "damagePct";
            public const string Accuracy = "accuracy";
            public const string Crit = "crit";
            public const string Dodge = "dodge";
            public const string Speed = "speed";
            public const string DamageTaken = "damageTaken";
        }

        public static class AttackStat
        {
            public const string Strength = "strength";
            public const string Intelligence = "intelligence";
        }
    }
}
