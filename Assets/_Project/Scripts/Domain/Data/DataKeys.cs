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
            /// <summary>출혈: 차례 시작 시 피해. 부상(베임)에서만 생기고 약초로 풀리지 않는다.</summary>
            public const string Bleed = "bleed";
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
            /// <summary>방어도 비율 가산 (0.1 = 방어도 +10%p).</summary>
            public const string Armor = "armor";
            /// <summary>최대 HP 비율 증가 (패시브 오라 전용). 걸릴 때 그만큼 HP도 늘고, 풀리면 넘치는 HP는 깎인다.</summary>
            public const string MaxHpPct = "maxHpPct";
        }

        public static class Passive
        {
            public const string AllyAura = "allyAura";
            public const string GuardBackline = "guardBackline";
        }

        public static class AttackStat
        {
            public const string Strength = "strength";
            public const string Intelligence = "intelligence";
        }
    }
}
