using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>전투원에게 붙은 효과 하나.</summary>
    public sealed class ActiveEffect
    {
        public string SourceSkillId { get; }
        public string SourceSkillName { get; }
        public string Kind { get; }
        public string Stat { get; }
        public double Value { get; }
        public string DoubledVsType { get; }
        public int DamagePerTurn { get; }
        public int Remaining { get; internal set; }
        /// <summary>이번 차례에 걸린 효과는 이번 차례 끝에 줄지 않는다.</summary>
        internal bool FreshThisTurn { get; set; }

        public ActiveEffect(string sourceSkillId, string sourceSkillName, EffectDefinition def)
        {
            SourceSkillId = sourceSkillId;
            SourceSkillName = sourceSkillName;
            Kind = def.kind;
            Stat = def.stat;
            Value = def.value;
            DoubledVsType = def.doubledVsType;
            DamagePerTurn = def.damagePerTurn;
            Remaining = def.duration;
        }

        public string Key => SourceSkillId + "|" + Kind + "|" + Stat;
        public bool IsPoison => Kind == DataKeys.EffectKind.Poison;
    }

    /// <summary>
    /// 상태 효과 묶음. 같은 기술의 같은 효과는 중첩 없이 지속만 갱신.
    /// 감소: 받은 전투원의 자기 차례 끝에 −1. 전투 종료 시 해제.
    /// </summary>
    public sealed class StatusEffectSet
    {
        private readonly List<ActiveEffect> effects = new List<ActiveEffect>();

        public IReadOnlyList<ActiveEffect> All => effects;
        public bool HasPoison => effects.Any(e => e.IsPoison);

        /// <param name="isHoldersTurn">효과를 받은 전투원이 지금 행동 중이면 true.</param>
        public void Apply(ActiveEffect effect, bool isHoldersTurn)
        {
            effect.FreshThisTurn = isHoldersTurn;
            int i = effects.FindIndex(e => e.Key == effect.Key);
            if (i >= 0) effects[i] = effect;
            else effects.Add(effect);
        }

        /// <summary>stat 효과 합계. opponentTypes가 doubledVsType을 포함하면 2배.</summary>
        public double Sum(string stat, IReadOnlyCollection<string> opponentTypes = null)
        {
            double total = 0;
            foreach (var e in effects)
            {
                if (e.Kind != DataKeys.EffectKind.Stat || e.Stat != stat) continue;
                bool doubled = !string.IsNullOrEmpty(e.DoubledVsType) && opponentTypes != null &&
                               opponentTypes.Contains(e.DoubledVsType);
                total += doubled ? e.Value * 2 : e.Value;
            }
            return total;
        }

        public int PoisonDamage() => effects.Where(e => e.IsPoison).Sum(e => e.DamagePerTurn);

        public void RemovePoison() => effects.RemoveAll(e => e.IsPoison);

        /// <summary>자기 차례 끝: 남은 라운드 −1, 0이면 제거.</summary>
        public void TickEndOfOwnTurn()
        {
            foreach (var e in effects)
            {
                if (e.FreshThisTurn) { e.FreshThisTurn = false; continue; }
                e.Remaining--;
            }
            effects.RemoveAll(e => e.Remaining <= 0);
        }

        public void Clear() => effects.Clear();
    }
}
