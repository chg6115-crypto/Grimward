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
        }
    }
}
