using System;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Battle
{
    /// <summary>우하단 몬스터 정보 패널 (몬스터 칸을 누르면 가방·기록 대신 표시).</summary>
    public sealed class MonsterPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text skillsText;
        [SerializeField] private Button closeButton;

        public event Action OnClose;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(TMP_Text nameText, TMP_Text hpText, TMP_Text statsText, TMP_Text skillsText, Button closeButton)
        {
            this.nameText = nameText; this.hpText = hpText; this.statsText = statsText;
            this.skillsText = skillsText; this.closeButton = closeButton;
        }

        private void Awake() => closeButton.onClick.AddListener(() => OnClose?.Invoke());

        public void Bind(Combatant monster, GameDatabase db)
        {
            nameText.text = monster.Name;
            hpText.text = $"체력: {monster.Hp}/{monster.MaxHp}";
            string types = string.Join(" · ", monster.Types.Select(db.TypeName));
            statsText.text = $"<b>{types}</b>\n" + BattleText.Stats(monster, db.Balance) + "\n\n" + BattleText.Effects(monster);
            skillsText.text = "<b>기술</b>\n" + string.Join("\n", monster.Skills.Select(s =>
                $"{s.name}  <size=80%>{(s.target == DataKeys.Target.Enemy ? BattleText.Ranks(s.targetRanks, false) : "")} {s.description}</size>"));
        }
    }
}
