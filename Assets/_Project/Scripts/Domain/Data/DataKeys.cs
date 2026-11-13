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
            /// <summary>기절: 다음 자기 차례를 건너뛴다. 풀린 뒤 기절 저항이 잠시 오른다.</summary>
            public const string Stun = "stun";
            /// <summary>화상: 차례 시작 시 피해 (화염구).</summary>
            public const string Burn = "burn";
        }

        /// <summary>상태이상 저항 분류. 확률 = 기술 확률 − 대상 저항. 면역이면 걸리지 않는다.</summary>
        public static class Resist
        {
            public const string Stun = "stun";
            public const string Poison = "poison";
            public const string Bleed = "bleed";
            public const string Burn = "burn";
            /// <summary>약화: 적에게 거는 능력치 감소 효과 전체.</summary>
            public const string Debuff = "debuff";
        }

        public static bool IsResistKind(string k) =>
            k == Resist.Stun || k == Resist.Poison || k == Resist.Bleed || k == Resist.Burn || k == Resist.Debuff;

        public static class DamageType
        {
            public const string Physical = "physical";
            /// <summary>마법 피해: 방어도 대신 마법 저항으로 줄어든다.</summary>
            public const string Magical = "magical";
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
            /// <summary>진형 붕괴 저항 가산 (0.7 = +70%p).</summary>
            public const string FormationResist = "formationResist";
            /// <summary>기절 저항 가산 (기절이 풀린 뒤 보호).</summary>
            public const string StunResist = "stunResist";
            /// <summary>마법 저항 가산.</summary>
            public const string MagicResist = "magicResist";
            /// <summary>중독 저항 가산.</summary>
            public const string PoisonResist = "poisonResist";
            /// <summary>출혈 저항 가산.</summary>
            public const string BleedResist = "bleedResist";
            /// <summary>화상 저항 가산.</summary>
            public const string BurnResist = "burnResist";
            /// <summary>약화 저항 가산.</summary>
            public const string DebuffResist = "debuffResist";

            /// <summary>상태이상 저항 분류(Resist) → 그 저항을 올리는 stat. 버프·아이템·패시브가 같은 stat을 쓴다.</summary>
            public static string ResistStatOf(string resistKind)
            {
                switch (resistKind)
                {
                    case Resist.Stun: return StunResist;
                    case Resist.Poison: return PoisonResist;
                    case Resist.Bleed: return BleedResist;
                    case Resist.Burn: return BurnResist;
                    case Resist.Debuff: return DebuffResist;
                    default: return null;
                }
            }

            /// <summary>저항 계열 stat (상태이상 5종 + 마법).</summary>
            public static bool IsResist(string stat) =>
                stat == StunResist || stat == PoisonResist || stat == BleedResist || stat == BurnResist ||
                stat == DebuffResist || stat == MagicResist;
        }

        public static class NodeEvent
        {
            public const string Battle = "battle";
            public const string Boss = "boss";
            public const string Gather = "gather";
            public const string Investigate = "investigate";
            public const string Treasure = "treasure";
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
