using System.Collections.Generic;
using System.Linq;
namespace Grimward.Domain.Combat
{
    public enum BattleKind { Node, Ambush }
    public enum BattleOutcome { Victory, Fled, Death }
    public enum BattleAction { Attack, Defend, Flee }
    public enum DropItem { LowStone, MidStone }
    public sealed class DropEntry
    {
        public DropItem Item { get; }
        public int Count { get; }
        public DropEntry(DropItem item, int count) { Item = item; Count = count; }
        public override string ToString() => $"{(Item == DropItem.LowStone ? "마정석(하)" : "마정석(중)")} {Count}개";
    }
    public sealed class BattleResult
    {
        public BattleKind Kind { get; }
        public BattleOutcome Outcome { get; }
        public int KilledGoblins { get; }
        public IReadOnlyList<DropEntry> Drops { get; }
        public BattleResult(BattleKind kind, BattleOutcome outcome, int killed, IEnumerable<DropEntry> drops)
        {
            Kind = kind;
            Outcome = outcome;
            KilledGoblins = killed;
            Drops = System.Array.AsReadOnly(drops.ToArray());
        }
    }
}
