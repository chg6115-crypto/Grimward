using System;

namespace Grimward.Domain.Data
{
    /// <summary>
    /// 밸런스 상수. 값은 gamedata.json(엑셀 10_밸런스상수에서 내보냄)에서 채운다.
    /// 필드 이름은 JSON 키와 같다. 코드에 기본값을 두지 않는다.
    /// </summary>
    [Serializable]
    public sealed class BalanceConfig
    {
        // 시간
        public int dayStartMinute;
        public int nightStartMinute;
        public int wakeUpMinute;

        // 능력치 공식
        public int hpBase;
        public int hpPerVitality;
        public int attackStrengthDivisor;
        public int accuracyPerDexterity;
        public int accuracyPerLevel;
        public int dodgePerDexterity;
        public int dodgePerceptionDivisor;
        public int speedStatDivisor;
        public double critBase;
        public double critPerDexterity;

        // 전투 공식
        public double baseHitChance;
        public double hitChanceMin;
        public double hitChanceMax;
        public double critMin;
        public double critMultiplier;
        public double damageReductionCap;
        public double nightPartyAccuracyPenalty;
        public double nightEnemyAccuracyBonus;
        public int speedRollDie;
        public int ranksPerSide;
        public int itemsPerTurn;
        public double fleeHpDamageRatio;

        // 가방
        public int bagSlots;

        // 적 AI [임시]
        public double aiBuffChance;
        public double aiHealThreshold;

        // 고용 (v0.1 테스트용 기본 지각)
        public int defaultPerception;
    }
}
