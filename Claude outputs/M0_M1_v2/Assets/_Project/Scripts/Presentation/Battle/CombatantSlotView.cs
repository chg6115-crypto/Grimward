using System;
using Grimward.Domain.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Battle
{
    /// <summary>진형 한 칸: 몸(임시 사각형) · 이름 · HP 막대 · 상태. 누르면 OnClicked.</summary>
    public sealed class CombatantSlotView : MonoBehaviour
    {
        public enum Highlight { None, Actor, EnemyTarget, AllyTarget, Inspected }

        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private Image body;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Color partyBodyColor = new Color(0.25f, 0.3f, 0.38f);
        [SerializeField] private Color enemyBodyColor = new Color(0.38f, 0.22f, 0.22f);

        public event Action OnClicked;
        public Combatant Bound { get; private set; }

        /// <summary>에디터 UI 빌더 전용: 자식 참조 연결.</summary>
        public void Assign(Button button, Image frame, Image body, TMP_Text nameText, Image hpFill, TMP_Text hpText,
            TMP_Text statusText)
        {
            this.button = button; this.frame = frame; this.body = body; this.nameText = nameText;
            this.hpFill = hpFill; this.hpText = hpText; this.statusText = statusText;
        }

        private void Awake() => button.onClick.AddListener(() => OnClicked?.Invoke());

        public void Bind(Combatant c, Highlight highlight)
        {
            Bound = c;
            bool visible = c != null && c.IsAlive;
            body.enabled = visible;
            nameText.enabled = visible;
            hpFill.transform.parent.gameObject.SetActive(visible);
            hpText.enabled = visible;
            statusText.enabled = visible;
            button.interactable = visible;
            frame.enabled = visible && highlight != Highlight.None;
            if (!visible) return;

            body.color = c.Side == BattleSide.Party ? partyBodyColor : enemyBodyColor;
            nameText.text = c.Name;
            hpFill.rectTransform.anchorMax = new Vector2((float)c.Hp / c.MaxHp, 1f);
            hpText.text = $"{c.Hp}/{c.MaxHp}";
            statusText.text = BattleText.StatusIcons(c);
            frame.color = BattleText.HighlightColor(highlight);
        }
    }
}
