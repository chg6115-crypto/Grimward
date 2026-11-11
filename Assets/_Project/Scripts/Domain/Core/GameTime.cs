using System;
using Grimward.Domain.Data;

namespace Grimward.Domain.Core
{
    /// <summary>일차 + 하루 중 분. 낮·밤 경계는 BalanceConfig에서 읽는다.</summary>
    public sealed class GameTime
    {
        // 달력 단위 상수이며 밸런스 수치가 아니다.
        public const int MinutesPerDay = 24 * 60;

        public int Day { get; private set; }
        public int MinuteOfDay { get; private set; }

        public GameTime(int day, int minuteOfDay)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            if (minuteOfDay < 0 || minuteOfDay >= MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
            Day = day;
            MinuteOfDay = minuteOfDay;
        }

        public void AddMinutes(int minutes)
        {
            if (minutes < 0) throw new ArgumentOutOfRangeException(nameof(minutes));
            long total = (long)MinuteOfDay + minutes;
            Day = checked(Day + (int)(total / MinutesPerDay));
            MinuteOfDay = (int)(total % MinutesPerDay);
        }

        /// <summary>휴식: 다음 날 기상 시각으로 고정.</summary>
        public void RestUntilNextMorning(BalanceConfig config)
        {
            Day = checked(Day + 1);
            MinuteOfDay = config.wakeUpMinute;
        }

        public bool IsDay(BalanceConfig config) =>
            MinuteOfDay >= config.dayStartMinute && MinuteOfDay < config.nightStartMinute;

        public bool IsNight(BalanceConfig config) => !IsDay(config);

        public override string ToString() => $"{Day}일차 {MinuteOfDay / 60:00}:{MinuteOfDay % 60:00}";
    }
}
