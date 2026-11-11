using System;
using System.Collections.Generic;
using System.Linq;

namespace Grimward.Domain.Combat
{
    /// <summary>
    /// 양쪽 진형. 칸 번호는 1부터, 항상 1번부터 채운다. 큰 몬스터는 Size만큼 연속한 칸을 차지한다.
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

        /// <summary>진형에 있는 전투원 수 (칸 수가 아님).</summary>
        public int Count(BattleSide side) => ListOf(side).Count;

        /// <summary>차지한 칸 수 합 (큰 몬스터는 2칸 이상).</summary>
        public int OccupiedRanks(BattleSide side) => ListOf(side).Sum(c => c.Size);

        public bool HasRoom(BattleSide side, int size = 1) => OccupiedRanks(side) + size <= RanksPerSide;

        public void Add(Combatant c)
        {
            if (!HasRoom(c.Side, c.Size)) throw new InvalidOperationException("빈 칸이 없습니다.");
            ListOf(c.Side).Add(c);
        }

        /// <summary>앞에서 index번째(0부터) 자리에 넣는다. 뒤의 전투원은 그만큼 뒤로 물러난다 (진형 붕괴 아님).</summary>
        public void Insert(Combatant c, int index)
        {
            if (!HasRoom(c.Side, c.Size)) throw new InvalidOperationException("빈 칸이 없습니다.");
            var list = ListOf(c.Side);
            list.Insert(Math.Max(0, Math.Min(index, list.Count)), c);
        }

        /// <summary>차지한 첫 칸 번호(1부터). 없으면 0.</summary>
        public int RankOf(Combatant c)
        {
            int rank = 1;
            foreach (var x in ListOf(c.Side))
            {
                if (x == c) return rank;
                rank += x.Size;
            }
            return 0;
        }

        /// <summary>차지한 모든 칸 번호.</summary>
        public IEnumerable<int> RanksOf(Combatant c)
        {
            int first = RankOf(c);
            if (first == 0) yield break;
            for (int i = 0; i < c.Size; i++) yield return first + i;
        }

        /// <summary>진형 안 순서(1부터, 칸 수와 무관). 이동·위치 교환 계산용.</summary>
        public int PositionOf(Combatant c)
        {
            int i = ListOf(c.Side).IndexOf(c);
            return i < 0 ? 0 : i + 1;
        }

        /// <summary>그 칸을 차지한 전투원 (큰 몬스터는 여러 칸에서 같은 전투원).</summary>
        public Combatant At(BattleSide side, int rank)
        {
            foreach (var x in ListOf(side))
            {
                if (rank >= 1 && rank <= x.Size) return x;
                rank -= x.Size;
            }
            return null;
        }

        /// <summary>delta 자리만큼 이동(양수 = 뒤로). 진형 끝을 넘지 않게 자르고, 목적지 전투원과 교환.
        /// 큰 몬스터는 한 자리가 여러 칸이다. 이동 뒤 첫 칸 번호를 돌려준다.</summary>
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
