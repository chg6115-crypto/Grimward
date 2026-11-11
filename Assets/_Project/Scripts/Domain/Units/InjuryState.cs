namespace Grimward.Domain.Units
{
    /// <summary>모험가가 가진 부상 하나 (종류 + 경상/중상).</summary>
    public sealed class InjuryState
    {
        public string Id { get; }
        public bool Severe { get; set; }

        public InjuryState(string id, bool severe = false)
        {
            Id = id;
            Severe = severe;
        }

        public InjuryState Clone() => new InjuryState(Id, Severe);
    }
}
