using System;
using System.Collections.Generic;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Battle
{
    /// <summary>가방 6×2 칸. 칸을 누르면 그 칸의 아이템 ID로 OnItem.</summary>
    public sealed class BagView : MonoBehaviour
    {
        [SerializeField] private Button[] slots = new Button[12];
        [SerializeField] private Color emptyColor = new Color(0.1f, 0.1f, 0.12f);
        [SerializeField] private Color filledColor = new Color(0.22f, 0.2f, 0.17f);
        [SerializeField] private Color selectedColor = new Color(0.62f, 0.5f, 0.18f);

        private readonly List<string> slotItems = new List<string>();

        public event Action<string> OnItem;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(Button[] slots) => this.slots = slots;

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].onClick.AddListener(() =>
                {
                    if (index < slotItems.Count) OnItem?.Invoke(slotItems[index]);
                });
            }
        }

        public void Bind(BattleInventory inventory, GameDatabase db, bool canUse, string selectedItem)
        {
            var filled = inventory.ToSlots(db, slots.Length);
            slotItems.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                var b = slots[i];
                var texts = b.GetComponentsInChildren<TMP_Text>(true); // [0] 이름, [1] 개수
                if (i >= filled.Count)
                {
                    texts[0].text = ""; texts[1].text = "";
                    ((Image)b.targetGraphic).color = emptyColor;
                    b.interactable = false;
                    continue;
                }
                var item = db.GetItem(filled[i].Key);
                slotItems.Add(item.id);
                texts[0].text = item.name;
                texts[1].text = filled[i].Value.ToString();
                bool usable = canUse && BattleInventory.IsUsableInBattle(item);
                b.interactable = usable;
                ((Image)b.targetGraphic).color = item.id == selectedItem ? selectedColor : filledColor;
            }
        }
    }
}
