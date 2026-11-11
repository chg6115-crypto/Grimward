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
        /// <summary>패시브(오라)로 붙은 효과. 라운드가 줄지 않고, 보유자가 쓰러지면 사라진다.</summary>
        public bool IsPassive { get; }
        /// <summary>패시브 효과를 준 전투원 Uid (일반 효과는 0).</summary>
        public int SourceUid { get; }
        /// <summary>이번 차례에 걸린 효과는 이번 차례 끝에 줄지 않는다.</summary>
        internal bool FreshThisTurn { get; set; }

        public ActiveEffect(string sourceSkillId, string sourceSkillName, EffectDefinition def, bool isPassive = false,
            int sourceUid = 0)
        {
            IsPassive = isPassive;
            SourceUid = sourceUid;
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
        public bool IsBleed => Kind == DataKeys.EffectKind.Bleed;
        /// <summary>부상에서 온 효과 (출혈 포함). SourceSkillId = "injury:{부상 id}".</summary>
        public bool IsInjury => SourceSkillId != null && SourceSkillId.StartsWith(InjuryPrefix);
        public const string InjuryPrefix = "injury:";
    }

    /// <summary>
    /// 상태 효과 묶음. 기술 효과는 걸 때마다 따로 쌓인다(각자 남은 라운드를 가짐, 다키스트 던전 방식).
    /// 패시브(오라) 효과는 보유자마다 하나만 유지한다.
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
            if (effect.IsPassive)
            {
                int i = effects.FindIndex(e => e.IsPassive && e.Key == effect.Key);
                if (i >= 0) { effects[i] = effect; return; }
            }
            effects.Add(effect);
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
        public int BleedDamage() => effects.Where(e => e.IsBleed).Sum(e => e.DamagePerTurn);

        /// <summary>조건에 맞는 효과(패시브 포함)를 모두 지운다.</summary>
        public void RemoveAll(System.Predicate<ActiveEffect> match) => effects.RemoveAll(match);

        public void RemovePoison() => effects.RemoveAll(e => e.IsPoison);
        public bool HasBleed => effects.Any(e => e.IsBleed);
        public void RemoveBleed() => effects.RemoveAll(e => e.IsBleed);

        /// <summary>자기 차례 끝: 남은 라운드 −1, 0이면 제거.</summary>
        public void TickEndOfOwnTurn()
        {
            foreach (var e in effects)
            {
                if (e.IsPassive) continue;
                if (e.FreshThisTurn) { e.FreshThisTurn = false; continue; }
                e.Remaining--;
            }
            effects.RemoveAll(e => !e.IsPassive && e.Remaining <= 0);
        }

        public void Clear() => effects.Clear();

        /// <summary>조건에 맞는 패시브 효과를 지운다.</summary>
        public void RemovePassives(System.Predicate<ActiveEffect> match) => effects.RemoveAll(e => e.IsPassive && match(e));
    }
}
