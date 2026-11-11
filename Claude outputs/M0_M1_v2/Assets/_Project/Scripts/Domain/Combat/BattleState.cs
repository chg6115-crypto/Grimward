using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Units;

namespace Grimward.Domain.Combat
{
    public enum BattlePhase { NotStarted, PlayerTurn, EnemyTurn, Ended }
    public enum BattleOutcome { None, Victory, Defeat, Fled }

    /// <summary>
    /// 전투 진행. 화면은 Phase를 보고 플레이어 행동 API 또는 RunEnemyTurn()을 호출한다.
    /// 기록은 Log에 쌓이며, 연출은 Presentation이 맡는다.
    /// </summary>
    public sealed class BattleState
    {
        private readonly GameDatabase db;
        private readonly BattleContext ctx;
        private readonly SkillResolver resolver;
        private readonly EnemyAI ai;
        private List<TurnOrder.Entry> queue = new List<TurnOrder.Entry>();
        private int queueIndex;
        private int itemsUsedThisTurn;
        private readonly List<Combatant> partyMembers;

        public BattlePhase Phase { get; private set; } = BattlePhase.NotStarted;
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.None;
        public BattleContext Context => ctx;
        public Formation Formation => ctx.Formation;
        public Combatant CurrentActor => ctx.CurrentActor;
        public int Round => ctx.Round;
        public IReadOnlyList<BattleEvent> Log => ctx.Log;
        public IReadOnlyList<string> KilledMonsterIds => ctx.KilledMonsterIds;
        public BattleInventory Inventory { get; }
        public bool IsNight => ctx.IsNight;
        public IReadOnlyList<TurnOrder.Entry> RoundOrder => queue;
        public bool CanUseItem => Phase == BattlePhase.PlayerTurn && itemsUsedThisTurn < ctx.Config.itemsPerTurn;

        public BattleState(GameDatabase db, IRandomSource random, IEnumerable<Combatant> partyInRankOrder,
            IEnumerable<Combatant> enemiesInRankOrder, bool isNight, BattleInventory inventory,
            CombatantFactory factory)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            var formation = new Formation(db.Balance.ranksPerSide);
            ctx = new BattleContext(db.Balance, random, formation, factory, isNight);
            foreach (var c in partyInRankOrder) formation.Add(c);
            foreach (var c in enemiesInRankOrder) formation.Add(c);
            partyMembers = formation.Side(BattleSide.Party).ToList();
            if (formation.Count(BattleSide.Party) == 0) throw new ArgumentException("파티가 비었습니다.");
            if (formation.Count(BattleSide.Enemy) == 0) throw new ArgumentException("적이 없습니다.");
            resolver = new SkillResolver(ctx);
            ai = new EnemyAI(ctx);
            Inventory = inventory ?? new BattleInventory();
        }

        /// <summary>모험가 목록(1번 칸부터)과 편성 ID로 전투를 만든다.</summary>
        public static BattleState Create(GameDatabase db, IRandomSource random, IReadOnlyList<Adventurer> party,
            string encounterId, bool isNight, BattleInventory inventory)
        {
            var factory = new CombatantFactory(db);
            var heroes = party.Where(a => !a.IsDead).Select(factory.FromAdventurer).ToList();
            var enemies = factory.RollEncounter(encounterId, random);
            return new BattleState(db, random, heroes, enemies, isNight, inventory, factory);
        }

        public void Start()
        {
            if (Phase != BattlePhase.NotStarted) throw new InvalidOperationException("이미 시작했습니다.");
            ctx.Emit(new BattleEvent(BattleEventKind.RoundStarted, IsNight ? "전투 시작 (밤)" : "전투 시작"));
            AdvanceToNextActor();
        }

        // ── 플레이어 행동 ─────────────────────────────

        public IReadOnlyList<SkillDefinition> CurrentSkills => CurrentActor?.Skills ?? Array.Empty<SkillDefinition>();

        public bool CanUseSkill(SkillDefinition skill, out string reason) =>
            SkillTargeting.CanUse(ctx, CurrentActor, skill, out reason);

        public List<Combatant> TargetsFor(SkillDefinition skill) => SkillTargeting.ValidTargets(ctx, CurrentActor, skill);

        public void UseSkill(SkillDefinition skill, Combatant target)
        {
            RequirePhase(BattlePhase.PlayerTurn);
            resolver.Resolve(CurrentActor, skill, target);
            FinishTurn();
        }

        /// <summary>−1 = 앞(1번 쪽), +1 = 뒤.</summary>
        public bool CanMove(int delta)
        {
            if (CurrentActor == null || Phase == BattlePhase.Ended) return false;
            int rank = Formation.RankOf(CurrentActor) + delta;
            return Math.Abs(delta) == 1 && rank >= 1 && rank <= Formation.Alive(CurrentActor.Side).Count();
        }

        public void Move(int delta)
        {
            RequirePhase(BattlePhase.PlayerTurn);
            DoMove(delta);
            FinishTurn();
        }

        public void Pass()
        {
            RequirePhase(BattlePhase.PlayerTurn);
            ctx.Emit(new BattleEvent(BattleEventKind.Passed, $"{CurrentActor.Name}: 넘기기", CurrentActor));
            FinishTurn();
        }

        /// <summary>도주: 반드시 성공. 생존자 각자 최대 HP 비율만큼 피해(최소 1 남김).</summary>
        public void Flee()
        {
            RequirePhase(BattlePhase.PlayerTurn);
            ctx.Emit(new BattleEvent(BattleEventKind.Fled, "파티가 도주했다", CurrentActor));
            foreach (var c in Formation.Alive(BattleSide.Party).ToList())
            {
                int amount = (int)Math.Floor(c.MaxHp * ctx.Config.fleeHpDamageRatio);
                int dealt = c.TakeDamageWithFloor(amount, 1);
                ctx.Emit(new BattleEvent(BattleEventKind.Damaged, $"  {c.Name} 도주 피해 {dealt}", null, c, dealt));
            }
            End(BattleOutcome.Fled);
        }

        /// <summary>아이템: 턴을 소모하지 않는다. 한 턴에 itemsPerTurn개.</summary>
        public void UseItem(string itemId, Combatant target)
        {
            if (!CanUseItem) throw new InvalidOperationException("이번 턴에는 아이템을 더 쓸 수 없습니다.");
            if (target == null || target.Side != BattleSide.Party || !target.IsAlive)
                throw new InvalidOperationException("아이템 대상이 잘못됐습니다.");
            var item = db.GetItem(itemId);
            if (!BattleInventory.IsUsableInBattle(item)) throw new InvalidOperationException($"{item.name}은 전투에서 쓸 수 없습니다.");
            if (!Inventory.TryConsume(itemId)) throw new InvalidOperationException($"남은 {item.name}: 0개");
            itemsUsedThisTurn++;
            ctx.Emit(new BattleEvent(BattleEventKind.ItemUsed, $"{CurrentActor.Name}: {item.name} → {target.Name}",
                CurrentActor, target));
            if (item.heal > 0)
            {
                int healed = target.Heal(item.heal);
                ctx.Emit(new BattleEvent(BattleEventKind.Healed, $"  {target.Name} HP +{healed}", CurrentActor, target, healed));
            }
            if (item.curePoison && target.Effects.HasPoison)
            {
                target.Effects.RemovePoison();
                ctx.Emit(new BattleEvent(BattleEventKind.PoisonCured, $"  {target.Name} 해독", CurrentActor, target));
            }
        }

        // ── 적 행동 ─────────────────────────────

        public EnemyDecision RunEnemyTurn()
        {
            RequirePhase(BattlePhase.EnemyTurn);
            var decision = ai.Decide(CurrentActor);
            if (decision.Skill != null) resolver.Resolve(CurrentActor, decision.Skill, decision.Target);
            else if (decision.MoveDelta != 0) DoMove(decision.MoveDelta);
            else ctx.Emit(new BattleEvent(BattleEventKind.Passed, $"{CurrentActor.Name}: 대기", CurrentActor));
            FinishTurn();
            return decision;
        }

        // ── 진행 ─────────────────────────────

        private void DoMove(int delta)
        {
            if (!CanMove(delta)) throw new InvalidOperationException("그쪽으로 움직일 수 없습니다.");
            int before = Formation.RankOf(CurrentActor);
            int after = Formation.Shift(CurrentActor, delta);
            ctx.Emit(new BattleEvent(BattleEventKind.Moved, $"{CurrentActor.Name}: {before}번 → {after}번 칸",
                CurrentActor, null, after));
        }

        private void FinishTurn()
        {
            if (CurrentActor != null && CurrentActor.IsAlive) CurrentActor.Effects.TickEndOfOwnTurn();
            Formation.Compact(); // 재정렬은 행동이 끝난 뒤 한 번
            if (CheckEnd()) return;
            AdvanceToNextActor();
        }

        private void AdvanceToNextActor()
        {
            while (true)
            {
                if (queueIndex >= queue.Count) BeginRound();
                var next = queue[queueIndex++].Combatant;
                if (!next.IsAlive) continue;

                ctx.CurrentActor = next;
                itemsUsedThisTurn = 0;
                ctx.Emit(new BattleEvent(BattleEventKind.TurnStarted, $"▶ {next.Name}의 차례", next));

                int poison = next.Effects.PoisonDamage();
                if (poison > 0)
                {
                    int dealt = next.TakeDamage(poison);
                    ctx.Emit(new BattleEvent(BattleEventKind.PoisonTicked, $"  {next.Name} 중독 피해 {dealt}", null, next, dealt));
                    if (!next.IsAlive)
                    {
                        ctx.Emit(new BattleEvent(BattleEventKind.Died, $"  {next.Name} 쓰러짐", null, next));
                        if (next.Side == BattleSide.Enemy) ctx.KilledMonsterIds.Add(next.DefinitionId);
                        Formation.Compact();
                        if (CheckEnd()) return;
                        continue;
                    }
                }

                Phase = next.Side == BattleSide.Party ? BattlePhase.PlayerTurn : BattlePhase.EnemyTurn;
                return;
            }
        }

        private void BeginRound()
        {
            ctx.Round++;
            queue = TurnOrder.Roll(Formation.AllAlive(), ctx.Random, ctx.Config);
            queueIndex = 0;
            string order = string.Join(" → ", queue.Select(e => $"{e.Combatant.Name}({e.Initiative})"));
            ctx.Emit(new BattleEvent(BattleEventKind.RoundStarted, $"── {ctx.Round}라운드: {order}"));
        }

        private bool CheckEnd()
        {
            if (!Formation.Alive(BattleSide.Enemy).Any()) { End(BattleOutcome.Victory); return true; }
            if (!Formation.Alive(BattleSide.Party).Any()) { End(BattleOutcome.Defeat); return true; }
            return false;
        }

        private void End(BattleOutcome outcome)
        {
            Outcome = outcome;
            Phase = BattlePhase.Ended;
            ctx.CurrentActor = null;
            WriteBackParty();
            string text = outcome == BattleOutcome.Victory ? "승리" : outcome == BattleOutcome.Defeat ? "전멸" : "도주";
            ctx.Emit(new BattleEvent(BattleEventKind.Ended, $"전투 종료: {text}"));
        }

        /// <summary>전투 종료: 효과 해제, HP와 사망을 모험가에게 되돌려 쓴다.</summary>
        private void WriteBackParty()
        {
            foreach (var c in partyMembers.Concat(Formation.Side(BattleSide.Enemy)))
                c.Effects.Clear();
            foreach (var c in partyMembers)
            {
                if (c.Source == null) continue;
                c.Source.CurrentHp = c.Hp;
                if (!c.IsAlive) c.Source.IsDead = true;
            }
        }

        private void RequirePhase(BattlePhase phase)
        {
            if (Phase != phase) throw new InvalidOperationException($"지금은 {phase} 단계가 아닙니다 (현재 {Phase}).");
        }
    }
}
