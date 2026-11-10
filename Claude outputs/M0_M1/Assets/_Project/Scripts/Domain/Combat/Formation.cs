using System;
using System.Collections.Generic;
using System.Linq;

namespace Grimward.Domain.Combat
{
    /// <summary>
    /// 양쪽 진형. 칸 번호는 1부터, 항상 1번부터 채운다.
    /// 위치 이동은 목적지 전투원과 자리 교환. 사망자 정리는 Compact()로 행동이 끝난 뒤 한 번.
    /// </summary>
    public sealed class Formation
    {
        private readonly List<Combatant> party = new List<Combatant>();
        private readonly List<Combatant> enemies = new List<Combatant>();

        public int RanksPerSide { get; }

        public Formation(int ranksPerSide)
        {
            if (ranksPerSide <= 0) throw new ArgumentOutOfRangeException(nameof(ranksPerSide));
            RanksPerSide = ranksPerSide;
        }

        private List<Combatant> ListOf(BattleSide side) => side == BattleSide.Party ? party : enemies;

        /// <summary>칸 순서(1번부터)의 전투원. 사망했지만 아직 정리 전인 전투원도 포함.</summary>
        public IReadOnlyList<Combatant> Side(BattleSide side) => ListOf(side);

        public IEnumerable<Combatant> Alive(BattleSide side) => ListOf(side).Where(c => c.IsAlive);

        public IEnumerable<Combatant> AllAlive() => party.Concat(enemies).Where(c => c.IsAlive);

        public int Count(BattleSide side) => ListOf(side).Count;

        public bool HasRoom(BattleSide side) => ListOf(side).Count < RanksPerSide;

        public void Add(Combatant c)
        {
            var list = ListOf(c.Side);
            if (list.Count >= RanksPerSide) throw new InvalidOperationException("빈 칸이 없습니다.");
            list.Add(c);
        }

        /// <summary>1부터 시작하는 칸 번호. 없으면 0.</summary>
        public int RankOf(Combatant c)
        {
            int i = ListOf(c.Side).IndexOf(c);
            return i < 0 ? 0 : i + 1;
        }

        public Combatant At(BattleSide side, int rank)
        {
            var list = ListOf(side);
            return rank >= 1 && rank <= list.Count ? list[rank - 1] : null;
        }

        /// <summary>delta 칸만큼 이동(양수 = 뒤로). 진형 끝을 넘지 않게 자르고, 목적지 전투원과 교환.
        /// 실제로 움직인 칸 번호를 돌려준다(움직이지 않았으면 현재 칸).</summary>
        public int Shift(Combatant c, int delta)
        {
            // 정리 전 사망자는 건너뛰고 살아 있는 전투원 사이에서만 움직인다.
            var list = ListOf(c.Side);
            if (!list.Contains(c)) throw new InvalidOperationException("진형에 없는 전투원입니다.");
            var alive = list.Where(x => x.IsAlive || x == c).ToList();
            int from = alive.IndexOf(c);
            int to = Math.Max(0, Math.Min(alive.Count - 1, from + delta));
            if (to != from)
            {
                var other = alive[to];
                int a = list.IndexOf(c), b = list.IndexOf(other);
                list[a] = other;
                list[b] = c;
            }
            return RankOf(c);
        }

        /// <summary>사망자를 빼고 뒤 칸을 당긴다. 빠진 전투원을 돌려준다.</summary>
        public List<Combatant> Compact()
        {
            var removed = party.Where(c => !c.IsAlive).Concat(enemies.Where(c => !c.IsAlive)).ToList();
            party.RemoveAll(c => !c.IsAlive);
            enemies.RemoveAll(c => !c.IsAlive);
            return removed;
        }
    }
}
