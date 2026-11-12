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
        /// <summary>기술 능력치 보너스 = 내림(가중 합 ÷ 이 값).</summary>
        public int statWeightDivisor;
        /// <summary>안정성 = 지각 × 계수 (상한 stabilityMax). 피해 하한을 편차 × 안정성만큼 끌어올린다.</summary>
        public double stabilityPerPerception;
        public double stabilityMax;

        // 전투 공식
        public double baseHitChance;
        public double hitChanceMin;
        public double hitChanceMax;
        public double critMin;
        public double critMultiplier;
        public double damageReductionCap;
        /// <summary>명중했을 때 최소 피해 (내림 후 적용).</summary>
        public int minHitDamage;
        /// <summary>모험가 한 명이 가질 수 있는 부상 종류 수.</summary>
        public int injuryMaxPerAdventurer;
        /// <summary>전투 중 HP가 처음 이 비율 이하가 되면 부상 악화 판정.</summary>
        public double injuryHpThreshold;
        /// <summary>임계 도달 시 부상이 없으면 공격 기술의 태그 부상을 이 확률로 경상.</summary>
        public double injuryThresholdChance;
        /// <summary>모험가 기절·중독·출혈·화상 저항 = 기본 + 체력 × 계수.</summary>
        public double heroResistBase;
        public double heroResistPerVitality;
        /// <summary>모험가 약화 저항 = 기본 + 지능 × 계수.</summary>
        public double heroDebuffResistPerIntelligence;
        /// <summary>기절이 풀린 뒤 기절 저항 가산 (다음 자기 차례 끝까지).</summary>
        public double stunRecoveryResist;
        public double nightPartyAccuracyPenalty;
        public double nightEnemyAccuracyBonus;
        public int speedRollDie;
        public int ranksPerSide;
        public int itemsPerTurn;
        public double fleeHpDamageRatio;

        // 몬스터 능력치 편차 (만들 때 한 번 굴림)
        public double monsterHpVariance;
        public int monsterAccuracyDodgeVariance;
        public int monsterSpeedVariance;

        // 탐험 시간 (분)
        /// <summary>처음 지나는 길.</summary>
        public int travelFirstMinutes;
        /// <summary>이미 지나온 길.</summary>
        public int travelRepeatMinutes;
        public int battleMinutes;
        public int bossBattleMinutes;
        public int fleeMinutes;
        /// <summary>시야: 방문한 노드에서 길 몇 칸까지 노드·길이 보이는가 (기본 1, 특성·조사 물건으로 늘어남).</summary>
        public int mapVisionRange;
        /// <summary>지각 탐지: 노드를 처음 발견할 때 이벤트 종류 공개 확률 = 기본 + 지각 × 계수 (최소·최대).</summary>
        public double perceptionDetectBase;
        public double perceptionDetectPerPoint;
        public double perceptionDetectMin;
        public double perceptionDetectMax;

        // 가방
        public int bagSlots;

        // 적 AI [임시]
        public double aiBuffChance;
        public double aiHealThreshold;

        // 고용 (v0.1 테스트용 기본 지각)
        public int defaultPerception;
    }
}
