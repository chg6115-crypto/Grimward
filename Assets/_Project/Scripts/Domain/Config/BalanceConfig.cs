using System;

namespace Grimward.Domain.Config
{
    [Serializable]
    public sealed class BalanceConfig
    {
        public int FirstTraversalMinutes = 60;
        public int RevisitTraversalMinutes = 30;
        public int DayStartMinute = 360;
        public int NightStartMinute = 1200;
        public int GameStartDay = 1;
        public int GameStartMinute = 360;
        public double PathEventChanceDayFirst = 0.25;
        public double PathEventChanceDayRevisit = 0.05;
        public double PathEventChanceNightFirst = 0.40;
        public double PathEventChanceNightRevisit = 0.25;
        public int MapRadius = 2;
        public int ActiveNodeCount = 11;
        public int StartNodeCount = 1;
        public int BattleNodeCount = 6;
        public int TreasureNodeCount = 2;
        public int EmptyNodeCount = 2;
        public int MinCycleCount = 1;
        public int MaxCycleCount = 2;
        public int MaxPathDistance = 5;
        public bool AllowIncompleteSafeNodesOnReturn = false;
        public int DefaultSeed = 12345;

        public double BaseHitChance = 0.85;
        public double MinHitChance = 0.05;
        public double MaxHitChance = 0.95;
        public double HitChancePerPoint = 0.01;
        public double NightPlayerHitPenalty = 0.10;
        public double NightEnemyHitBonus = 0.10;
        public double BaseCritChance = 0.03;
        public double CritChancePerDex = 0.005;
        public double CritMultiplier = 1.5;
        public double DefendReduction = 0.30;
        public double SwordsmanDefendBonus = 0.05;
        public int HpBase = 20;
        public int HpPerVit = 3;
        public int AccuracyPerDex = 2;
        public int AccuracyPerLevel = 2;
        public int EvasionPerDex = 3;
        public int EvasionPerPerDivisor = 2;
        public int AttackStrDivisor = 2;
        public int SpeedStatDivisor = 3;
        public int InitiativeDieSides = 8;
        public int PlayerLevel = 1;
        public int PlayerStrength = 5;
        public int PlayerDexterity = 3;
        public int PlayerVitality = 4;
        public int PlayerIntelligence = 1;
        public int PlayerPerception = 3;
        public int PlayerCharisma = 3;
        public int PlayerWeaponMin = 4;
        public int PlayerWeaponMax = 8;
        public double PlayerArmor = 0.05;
        public int GoblinHp = 10;
        public int GoblinAttackMin = 2;
        public int GoblinAttackMax = 4;
        public int GoblinAccuracy = 10;
        public int GoblinEvasion = 10;
        public int GoblinSpeed = 4;
        public double GoblinArmor = 0;
        public double GoblinCritChance = 0;
        public int GoblinGroupMin = 2;
        public int GoblinGroupMax = 3;
        public int DropLowStoneMin = 1;
        public int DropLowStoneMax = 2;
        public double DropMidStoneChance = 0.20;
        public int DropMidStoneCount = 1;
        public int BattleMinutes = 30;
        public int FleeMinutes = 60;
        public double FleeHpDamageRatio = 0.20;
        public int FleeMinimumHp = 1;
        public DamageRounding FleeDamageRounding = DamageRounding.Floor;
        public bool FleeTimeReplacesBattleTime = true;
        public bool ChargeTravelOnAmbushVictory = true;
        public bool ChargeTravelOnAmbushFlee = true;
        public bool ChargeTravelOnAmbushDeath = true;

        public BalanceConfig Copy() => (BalanceConfig)MemberwiseClone();

        public void Validate()
        {
            if (FirstTraversalMinutes <= 0 || RevisitTraversalMinutes <= 0)
                throw new ArgumentException("이동 시간은 양수여야 합니다.");
            if (DayStartMinute < 0 || NightStartMinute >= 1440 || DayStartMinute >= NightStartMinute)
                throw new ArgumentException("낮/밤 경계 시각이 잘못되었습니다.");
            if (GameStartDay < 1 || GameStartMinute < 0 || GameStartMinute >= 1440)
                throw new ArgumentException("게임 시작 시각이 잘못되었습니다.");
            foreach (var chance in new[] { PathEventChanceDayFirst, PathEventChanceDayRevisit,
                PathEventChanceNightFirst, PathEventChanceNightRevisit })
                if (double.IsNaN(chance) || chance < 0 || chance > 1)
                    throw new ArgumentException("확률은 0~1이어야 합니다.");
            if (MapRadius < 1 || ActiveNodeCount < 1 || StartNodeCount != 1 ||
                BattleNodeCount < 0 || TreasureNodeCount < 0 || EmptyNodeCount < 0 ||
                ActiveNodeCount != StartNodeCount + BattleNodeCount + TreasureNodeCount + EmptyNodeCount ||
                MinCycleCount < 0 || MaxCycleCount < MinCycleCount || MaxPathDistance < 1)
                throw new ArgumentException("맵 설정이 잘못되었습니다.");
            ValidateCombat();
        }

        private void ValidateCombat()
        {
            foreach (double value in new[] { BaseHitChance, MinHitChance, MaxHitChance, NightPlayerHitPenalty,
                NightEnemyHitBonus, BaseCritChance, CritChancePerDex, PlayerArmor, GoblinArmor,
                GoblinCritChance, DefendReduction, SwordsmanDefendBonus, DropMidStoneChance, FleeHpDamageRatio })
                if (double.IsNaN(value) || value < 0 || value > 1)
                    throw new ArgumentException("전투 확률과 비율은 0~1이어야 합니다.");
            if (MinHitChance > MaxHitChance || double.IsNaN(CritMultiplier) || double.IsInfinity(CritMultiplier) ||
                CritMultiplier < 1 || HitChancePerPoint <= 0 || double.IsNaN(HitChancePerPoint) ||
                double.IsInfinity(HitChancePerPoint) || AttackStrDivisor <= 0 || SpeedStatDivisor <= 0 ||
                EvasionPerPerDivisor <= 0 || InitiativeDieSides <= 0 || PlayerLevel < 1 ||
                HpBase <= 0 || HpPerVit < 0 || PlayerStrength < 0 || PlayerDexterity < 0 ||
                PlayerVitality < 0 || PlayerIntelligence < 0 || PlayerPerception < 0 || PlayerCharisma < 0 ||
                AccuracyPerDex < 0 || AccuracyPerLevel < 0 || EvasionPerDex < 0 ||
                PlayerWeaponMin < 0 || PlayerWeaponMax < PlayerWeaponMin || GoblinHp <= 0 ||
                GoblinAttackMin < 0 || GoblinAttackMax < GoblinAttackMin || GoblinSpeed < 0 ||
                GoblinGroupMin < 1 || GoblinGroupMax < GoblinGroupMin ||
                DropLowStoneMin < 1 || DropLowStoneMax < DropLowStoneMin || DropMidStoneCount < 1 ||
                BattleMinutes < 0 || FleeMinutes < 0 || FleeMinimumHp < 1 ||
                FleeMinimumHp > HpBase + PlayerVitality * HpPerVit ||
                !Enum.IsDefined(typeof(DamageRounding), FleeDamageRounding))
                throw new ArgumentException("전투 설정이 잘못되었습니다.");
        }
    }
}
