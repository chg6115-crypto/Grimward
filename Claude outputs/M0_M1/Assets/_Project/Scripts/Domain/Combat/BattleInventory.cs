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
