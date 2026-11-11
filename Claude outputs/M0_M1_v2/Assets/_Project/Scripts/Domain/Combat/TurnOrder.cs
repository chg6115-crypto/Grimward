using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Core;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>라운드 행동 순서: 속도(+속도 효과) + 1dN 내림차순. 동률은 기본 속도, 그다음 무작위.</summary>
    public static class TurnOrder
    {
        public sealed class Entry
        {
            public Combatant Combatant { get; }
            public int Initiative { get; }
            public Entry(Combatant c, int initiative) { Combatant = c; Initiative = initiative; }
        }

        public static int EffectiveSpeed(Combatant c) => c.Speed + (int)c.Effects.Sum(DataKeys.Stat.Speed);

        public static List<Entry> Roll(IEnumerable<Combatant> combatants, IRandomSource random, BalanceConfig config)
        {
            var rolled = new List<(Entry entry, double tie)>();
            foreach (var c in combatants)
            {
                int initiative = EffectiveSpeed(c) + random.NextInt(1, config.speedRollDie);
                rolled.Add((new Entry(c, initiative), random.NextDouble()));
            }
            return rolled
                .OrderByDescending(x => x.entry.Initiative)
                .ThenByDescending(x => x.entry.Combatant.Speed)
                .ThenBy(x => x.tie)
                .Select(x => x.entry)
                .ToList();
        }
    }
}
