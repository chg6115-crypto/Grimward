using System;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Battle
{
    /// <summary>좌하단 모험가 패널: 이름·직업 · 기술 4 + 이동 + 넘기기 · 능력치 · 효과.</summary>
    public sealed class HeroPanelView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text classText;
        [SerializeField] private Button[] skillButtons = new Button[4];
        [SerializeField] private Button moveButton;
        [SerializeField] private Button passButton;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text effectsText;
        [SerializeField] private Color normalButton = new Color(0.2f, 0.2f, 0.24f);
        [SerializeField] private Color selectedButton = new Color(0.62f, 0.5f, 0.18f);

        public event Action<int> OnSkill;
        public event Action OnMove;
        public event Action OnPass;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(Image portrait, TMP_Text nameText, TMP_Text classText, Button[] skillButtons,
            Button moveButton, Button passButton, TMP_Text statsText, TMP_Text effectsText)
        {
            this.portrait = portrait; this.nameText = nameText; this.classText = classText;
            this.skillButtons = skillButtons; this.moveButton = moveButton; this.passButton = passButton;
            this.statsText = statsText; this.effectsText = effectsText;
        }

        private void Awake()
        {
            for (int i = 0; i < skillButtons.Length; i++)
            {
                int index = i;
                skillButtons[i].onClick.AddListener(() => OnSkill?.Invoke(index));
            }
            moveButton.onClick.AddListener(() => OnMove?.Invoke());
            passButton.onClick.AddListener(() => OnPass?.Invoke());
        }

        /// <param name="canAct">이 모험가의 차례이고 입력을 받을 수 있으면 true.</param>
        /// <param name="selected">선택 중인 기술 번호 (−1 없음, 4 = 이동).</param>
        public void Bind(Combatant hero, GameDatabase db, BattleState battle, bool canAct, int selected)
        {
            if (hero == null) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            nameText.text = hero.Name;
            var cls = db.GetClass(hero.DefinitionId);
            classText.text = $"{cls.name} · {cls.role} · Lv{hero.Level}" +
                             (hero.IsAlive ? $" · {battle.Formation.RankOf(hero)}번 칸" : " · 사망");
            statsText.text = BattleText.Stats(hero, db.Balance);
            effectsText.text = BattleText.Effects(hero);

            for (int i = 0; i < skillButtons.Length; i++)
            {
                var b = skillButtons[i];
                bool has = i < hero.Skills.Count;
                b.gameObject.SetActive(has);
                if (!has) continue;
                var skill = hero.Skills[i];
                b.GetComponentInChildren<TMP_Text>().text = skill.name;
                b.interactable = canAct && battle.CanUseSkill(skill, out _);
                ((Image)b.targetGraphic).color = i == selected ? selectedButton : normalButton;
            }
            moveButton.interactable = canAct && (battle.CanMove(-1) || battle.CanMove(1));
            ((Image)moveButton.targetGraphic).color = selected == 4 ? selectedButton : normalButton;
            passButton.interactable = canAct;
        }
    }
}
