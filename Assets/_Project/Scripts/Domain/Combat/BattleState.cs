using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Config;
using Grimward.Domain.Core;
namespace Grimward.Domain.Combat
{
    public sealed class BattleState
    {
        private readonly BalanceConfig config;
        private readonly IRandomSource random;
        private readonly List<string> logs = new List<string>();
        private int turnIndex;
        private int killed;
        private bool started;
        public Combatant Player { get; }
        public IReadOnlyList<Combatant> Enemies { get; }
        public BattleKind Kind { get; }
        public bool IsNight { get; }
        public int Round { get; private set; }
        public IReadOnlyList<TurnEntry> Order { get; private set; } = Array.Empty<TurnEntry>();
        public IReadOnlyList<string> Logs => logs.AsReadOnly();
        public Combatant CurrentActor { get; private set; }
        public BattleResult Result { get; private set; }
        public bool IsPlayerTurn => Result == null && CurrentActor == Player;
        public BattleState(Combatant player, IReadOnlyList<Combatant> enemies, BattleKind kind,
            bool isNight, BalanceConfig config, IRandomSource random)
        {
            Player = player;
            Enemies = enemies;
            Kind = kind;
            IsNight = isNight;
            this.config = config;
            this.random = random;
        }
        public void Start()
        {
            if (started) return;
            started = true;
            Player.IsDefending = false;
            foreach (var enemy in Enemies) enemy.IsDefending = false;
            Log($"{(Kind == BattleKind.Node ? "노드 전투" : "경로 매복")} 시작 · {(IsNight ? "밤" : "낮")}");
            Advance();
        }
        public bool TryAct(BattleAction action, int targetId = -1)
        {
            if (!IsPlayerTurn) return false;
            switch (action)
            {
                case BattleAction.Attack:
                    var target = Enemies.FirstOrDefault(e => e.Id == targetId && e.IsAlive);
                    if (target == null) return false;
                    Attack(Player, target);
                    break;
                case BattleAction.Defend:
                    Player.IsDefending = true;
                    Log("검사 방어 · 다음 자기 턴 시작까지 유지");
                    break;
                case BattleAction.Flee:
                    int damage = CombatFormulas.FleeDamage(Player, config);
                    Player.TakeDamage(damage);
                    Log($"도주 성공 · HP -{damage}");
                    Finish(BattleOutcome.Fled);
                    return true;
                default:
                    return false;
            }
            turnIndex++;
            Advance();
            return true;
        }
        private void Advance()
        {
            while (Result == null)
            {
                if (!Player.IsAlive) { Finish(BattleOutcome.Death); return; }
                if (Enemies.All(e => !e.IsAlive)) { Finish(BattleOutcome.Victory); return; }
                if (Order.Count == 0 || turnIndex >= Order.Count)
                {
                    Round++;
                    Order = TurnOrder.Roll(new[] {Player}.Concat(Enemies), random, config);
                    turnIndex = 0;
                    Log($"{Round}라운드 순서: " + string.Join(" → ", Order.Select(e => $"{e.Fighter.Name}({e.Initiative})")));
                }
                CurrentActor = Order[turnIndex].Fighter;
                if (!CurrentActor.IsAlive) { turnIndex++; continue; }
                CurrentActor.IsDefending = false;
                if (CurrentActor.IsPlayer) return;
                Attack(CurrentActor, Player);
                turnIndex++;
            }
        }
        private void Attack(Combatant attacker, Combatant defender)
        {
            double chance = CombatFormulas.HitChance(attacker, defender, IsNight, config);
            bool hit = random.NextDouble() < chance;
            string prefix = $"{attacker.Name} → {defender.Name} · 명중률 {chance:P0} · 방어 {(defender.IsDefending ? "중" : "없음")}";
            if (!hit) { Log(prefix + " · 빗나감"); return; }
            bool critical = random.NextDouble() < attacker.CritChance;
            int rolled = random.NextInt(attacker.AttackMin, attacker.AttackMax);
            int damage = CombatFormulas.Damage(rolled, critical, defender, config);
            defender.TakeDamage(damage);
            Log(prefix + $" · 명중 · 치명타 {(critical ? "예" : "아니오")} · 피해 {damage} · HP {defender.Hp}/{defender.MaxHp}");
            if (!defender.IsAlive)
            {
                if (!defender.IsPlayer) killed++;
                Log(defender.Name + " 쓰러짐");
            }
        }
        private void Finish(BattleOutcome outcome)
        {
            var drops = new List<DropEntry>();
            if (outcome == BattleOutcome.Victory)
            {
                drops.Add(new DropEntry(DropItem.LowStone, random.NextInt(config.DropLowStoneMin, config.DropLowStoneMax)));
                if (random.NextDouble() < config.DropMidStoneChance)
                    drops.Add(new DropEntry(DropItem.MidStone, config.DropMidStoneCount));
            }
            Player.IsDefending = false;
            CurrentActor = null;
            Result = new BattleResult(Kind, outcome, killed, drops);
            Log($"전투 종료: {outcome} · 처치 {killed}");
        }
        private void Log(string message) => logs.Add(message);
    }
}
