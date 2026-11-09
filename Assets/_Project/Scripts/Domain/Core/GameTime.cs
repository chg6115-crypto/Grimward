using System;
using Grimward.Domain.Config;
namespace Grimward.Domain.Core
{
    public sealed class GameTime
    {
        // 달력 단위 상수이며 밸런스 수치가 아니다.
        public const int MinutesPerDay = 24 * 60;
        public int Day { get; private set; }
        public int MinuteOfDay { get; private set; }
        public GameTime(int day, int minuteOfDay)
        {
            if (day < 1 || minuteOfDay < 0 || minuteOfDay >= MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
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
        public bool IsDay(BalanceConfig config) =>
            MinuteOfDay >= config.DayStartMinute && MinuteOfDay < config.NightStartMinute;
        public override string ToString() => $"{Day}일차 {MinuteOfDay / 60:00}:{MinuteOfDay % 60:00}";
    }
}
