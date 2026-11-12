using System.Collections.Generic;
using Grimward.Domain.Data;

namespace Grimward.Domain.Combat
{
    /// <summary>전투에서 쓸 수 있는 소모품 개수. M2에서 가방(Inventory)이 이 역할을 넘겨받는다.</summary>
    public sealed class BattleInventory
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public int Count(string itemId) => counts.TryGetValue(itemId, out int n) ? n : 0;

        public void Add(string itemId, int amount)
        {
            if (amount <= 0) return;
            counts[itemId] = Count(itemId) + amount;
        }

        public bool TryConsume(string itemId)
        {
            int n = Count(itemId);
            if (n <= 0) return false;
            counts[itemId] = n - 1;
            return true;
        }

        public IEnumerable<KeyValuePair<string, int>> Entries => counts;

        /// <summary>약탈되지 않는 아이템 (의뢰 아이템). M2에서 수락한 의뢰에 맞춰 채운다.</summary>
        public HashSet<string> Protected { get; } = new HashSet<string>();

        /// <summary>가방 칸 배치: 아이템마다 stack 단위로 칸을 나눈다. 칸이 모자라면 남는 것은 표시하지 않는다.
        /// (M2에서 가방 규칙과 함께 Inventory로 옮긴다.)</summary>
        public List<KeyValuePair<string, int>> ToSlots(GameDatabase db, int slotCount)
        {
            var slots = new List<KeyValuePair<string, int>>();
            foreach (var e in counts)
            {
                int left = e.Value;
                int stack = db.GetItem(e.Key).stack;
                while (left > 0 && slots.Count < slotCount)
                {
                    int n = System.Math.Min(stack, left);
                    slots.Add(new KeyValuePair<string, int>(e.Key, n));
                    left -= n;
                }
            }
            return slots;
        }

        /// <summary>전투 중에 쓸 수 있는 아이템인가 (회복 또는 해독).</summary>
        public static bool IsUsableInBattle(ItemDefinition item) => item.heal > 0 || item.curePoison || item.treatInjury || item.cureBleed;

        public static BattleInventory From(GameDatabase db, IEnumerable<KeyValuePair<string, int>> entries)
        {
            var inv = new BattleInventory();
            foreach (var e in entries)
            {
                db.GetItem(e.Key); // 존재 확인
                inv.Add(e.Key, e.Value);
            }
            return inv;
        }
    }
}
