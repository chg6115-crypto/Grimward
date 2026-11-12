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
        BleedTicked,
        BleedStopped,
        BurnTicked,
        /// <summary>상태이상 저항·면역.</summary>
        StatusResisted,
        /// <summary>기절로 차례를 넘김.</summary>
        Stunned,
        /// <summary>전투 후 처리: 전투불능에서 일어남 / 영구 사망.</summary>
        KnockedDown,
        /// <summary>소매치기: 아이템 약탈 / 도주 지연 / 도주 성공 / 처치로 되찾음.</summary>
        Stole,
        EscapeDelayed,
        Escaped,
        ItemRecovered,
        Injured,
        InjuryTreated,
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
