namespace Grimward.Domain.Combat
{
    public enum BattleEventKind
    {
        RoundStarted,
        TurnStarted,
        SkillUsed,
        Missed,
        Damaged,
        Healed,
        EffectApplied,
        PoisonTicked,
        PoisonCured,
        Displaced,
        DisplaceResisted,
        Moved,
        Died,
        Summoned,
        ItemUsed,
        Passed,
        Fled,
        Ended
    }

    /// <summary>화면·로그용 전투 기록 한 줄. Domain은 연출 없이 기록만 남긴다.</summary>
    public sealed class BattleEvent
    {
        public BattleEventKind Kind { get; }
        public Combatant Actor { get; }
        public Combatant Target { get; }
        public int Amount { get; }
        public bool Critical { get; }
        public string Message { get; }

        public BattleEvent(BattleEventKind kind, string message, Combatant actor = null, Combatant target = null,
            int amount = 0, bool critical = false)
        {
            Kind = kind; Message = message; Actor = actor; Target = target; Amount = amount; Critical = critical;
        }

        public override string ToString() => Message;
    }
}
