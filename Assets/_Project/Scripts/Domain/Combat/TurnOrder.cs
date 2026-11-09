using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Config;
using Grimward.Domain.Core;
namespace Grimward.Domain.Combat
{
    public sealed class TurnEntry
    {
        public Combatant Fighter { get; }
        public int Initiative { get; }
        public TurnEntry(Combatant fighter, int initiative) { Fighter = fighter; Initiative = initiative; }
    }
    public static class TurnOrder
    {
        public static IReadOnlyList<TurnEntry> Roll(IEnumerable<Combatant> fighters, IRandomSource random, BalanceConfig c)
        {
            var entries = fighters.Where(f => f.IsAlive)
                .Select(f => new TurnEntry(f, f.Speed + random.NextInt(1, c.InitiativeDieSides)))
                .OrderByDescending(e => e.Initiative).ThenByDescending(e => e.Fighter.Speed).ToList();
            // 완전 동률 그룹만 Fisher-Yates. 비교 함수 안에서 난수를 호출하지 않는다.
            for (int start = 0; start < entries.Count;)
            {
                int end = start + 1;
                while (end < entries.Count && entries[end].Initiative == entries[start].Initiative &&
                    entries[end].Fighter.Speed == entries[start].Fighter.Speed) end++;
                for (int i = end - 1; i > start; i--)
                {
                    int j = random.NextInt(start, i);
                    var temp = entries[i];
                    entries[i] = entries[j];
                    entries[j] = temp;
                }
                start = end;
            }
            return entries.AsReadOnly();
        }
    }
}
