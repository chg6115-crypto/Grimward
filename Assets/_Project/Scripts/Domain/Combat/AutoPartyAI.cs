using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>자동 진행: 플레이어 파티 차례의 행동 하나.</summary>
    public sealed class AutoAction
    {
        public enum ActionKind { Item, Skill, Move, Pass }

        public ActionKind Kind { get; private set; }
        public string ItemId { get; private set; }
        public SkillDefinition Skill { get; private set; }
        public Combatant Target { get; private set; }
        public int MoveDelta { get; private set; }

        public static AutoAction UseItem(string id, Combatant t) => new AutoAction { Kind = ActionKind.Item, ItemId = id, Target = t };
        public static AutoAction UseSkill(SkillDefinition s, Combatant t) => new AutoAction { Kind = ActionKind.Skill, Skill = s, Target = t };
        public static AutoAction Move(int delta) => new AutoAction { Kind = ActionKind.Move, MoveDelta = delta };
        public static AutoAction Pass() => new AutoAction { Kind = ActionKind.Pass };
    }

    /// <summary>
    /// 플레이어 파티 자동 조작 (테스트·연출 확인용 단순 규칙).
    /// 1) 아이템(턴 소모 없음): HP 1/3 미만 → 회복약, 출혈 → 붕대, 중독 → 약초
    /// 2) 회복 기술: HP 1/2 미만 아군 중 가장 낮은 쪽
    /// 3) 공격 기술 무작위 + 공략 방식으로 대상 선택
    /// 4) 그 밖에 쓸 수 있는 기술(버프 등) → 앞으로 이동 → 넘기기
    /// </summary>
    public static class AutoPartyAI
    {
        public enum Focus
        {
            /// <summary>HP가 가장 낮은 적 (마무리 우선)</summary>
            LowestHp,
            /// <summary>최대 HP가 가장 큰 적 (보스 우선)</summary>
            Biggest,
            /// <summary>주술사 먼저, 그다음 HP 낮은 적</summary>
            SupportFirst,
            /// <summary>맨 앞 적</summary>
            Front
        }

        public static AutoAction Decide(BattleState b, Focus focus)
        {
            var rng = b.Context.Random;
            var party = b.Formation.Alive(BattleSide.Party).ToList();

            if (b.CanUseItem)
            {
                var hurt = party.Where(c => c.Hp < c.MaxHp / 3.0).OrderBy(c => c.Hp).FirstOrDefault();
                if (hurt != null && b.Inventory.Count("potion") > 0) return AutoAction.UseItem("potion", hurt);
                var bleeding = party.FirstOrDefault(c => c.Effects.HasBleed);
                if (bleeding != null && b.Inventory.Count("bandage") > 0) return AutoAction.UseItem("bandage", bleeding);
                var poisoned = party.FirstOrDefault(c => c.Effects.HasPoison);
                if (poisoned != null && b.Inventory.Count("herb") > 0) return AutoAction.UseItem("herb", poisoned);
            }

            var usable = b.CurrentSkills.Where(s => b.CanUseSkill(s, out _)).ToList();

            var heal = usable.FirstOrDefault(s => s.heal != null && s.heal.max > 0);
            if (heal != null)
            {
                var low = b.TargetsFor(heal).Where(c => c.Hp < c.MaxHp / 2.0).OrderBy(c => c.Hp).FirstOrDefault();
                if (low != null) return AutoAction.UseSkill(heal, low);
            }

            var attacks = usable.Where(s => s.damaging && s.target == DataKeys.Target.Enemy).ToList();
            if (attacks.Count > 0)
            {
                var skill = attacks[rng.NextInt(0, attacks.Count - 1)];
                return AutoAction.UseSkill(skill, PickEnemy(b.TargetsFor(skill), focus));
            }

            var other = usable.FirstOrDefault(s => !(s.heal != null && s.heal.max > 0));
            if (other != null)
            {
                var targets = b.TargetsFor(other);
                var t = other.target == DataKeys.Target.Enemy ? PickEnemy(targets, focus)
                    : targets.Contains(b.CurrentActor) ? b.CurrentActor : targets.First();
                return AutoAction.UseSkill(other, t);
            }

            if (b.CanMove(-1)) return AutoAction.Move(-1);
            return AutoAction.Pass();
        }

        private static Combatant PickEnemy(List<Combatant> targets, Focus focus)
        {
            switch (focus)
            {
                case Focus.Biggest: return targets.OrderByDescending(c => c.MaxHp).ThenBy(c => c.Hp).First();
                case Focus.SupportFirst:
                    return targets.OrderByDescending(c => c.DefinitionId == "goblin_shaman").ThenBy(c => c.Hp).First();
                case Focus.Front: return targets.First();
                default: return targets.OrderBy(c => c.Hp).First();
            }
        }

        /// <summary>결정한 행동을 실행한다 (화면 없이 쓸 때).</summary>
        public static void Apply(BattleState b, AutoAction a)
        {
            switch (a.Kind)
            {
                case AutoAction.ActionKind.Item: b.UseItem(a.ItemId, a.Target); break;
                case AutoAction.ActionKind.Skill: b.UseSkill(a.Skill, a.Target); break;
                case AutoAction.ActionKind.Move: b.Move(a.MoveDelta); break;
                default: b.Pass(); break;
            }
        }
    }
}
