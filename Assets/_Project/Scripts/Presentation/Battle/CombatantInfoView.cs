using System;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using Grimward.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Battle
{
    /// <summary>
    /// 우하단 상세 정보 패널. 진형의 모험가·몬스터 칸을 누르면 가방·기록 대신 표시하고 X로 닫는다.
    /// 왼쪽: 능력치(+모험가는 기본 능력치) · 오른쪽: 걸린 효과와 기술.
    /// </summary>
    public sealed class CombatantInfoView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Color heroColor = new Color(0.9f, 0.75f, 0.35f);
        [SerializeField] private Color monsterColor = new Color(0.85f, 0.25f, 0.2f);

        public event Action OnClose;
        /// <summary>기술·패시브 이름에 포인터가 올라감: ("skill:id" | "passive:id", 화면 좌표).</summary>
        public event Action<string, Vector2> OnLinkHover;
        public event Action OnLinkHoverEnd;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(TMP_Text nameText, TMP_Text hpText, TMP_Text statsText, TMP_Text detailText, Button closeButton)
        {
            this.nameText = nameText; this.hpText = hpText; this.statsText = statsText;
            this.detailText = detailText; this.closeButton = closeButton;
        }

        private void Awake()
        {
            closeButton.onClick.AddListener(() => OnClose?.Invoke());
            var hover = detailText.GetComponent<TmpLinkHover>();
            if (hover == null) hover = detailText.gameObject.AddComponent<TmpLinkHover>();
            hover.Entered += (id, pos) => OnLinkHover?.Invoke(id, pos);
            hover.Exited += () => OnLinkHoverEnd?.Invoke();
        }

        public void Bind(Combatant c, GameDatabase db, int rank)
        {
            bool hero = c.Side == BattleSide.Party;
            var color = hero ? heroColor : monsterColor;
            nameText.color = color;
            hpText.color = color;

            if (hero)
            {
                var cls = db.GetClass(c.DefinitionId);
                nameText.text = $"{c.Name} <size=70%><color=#aaa>Lv{c.Level} {cls.name} · {rank}번 칸</color></size>";
            }
            else
            {
                string types = string.Join(" · ", c.Types.Select(db.TypeName));
                nameText.text = $"{c.Name} <size=70%><color=#aaa>{types} · {rank}번 칸</color></size>";
            }
            hpText.text = $"체력: {c.Hp}/{c.MaxHp}";

            string stats = BattleText.Stats(c, db.Balance);
            if (hero) stats += "\n\n" + BattleText.Attributes(c);
            statsText.text = stats;

            string skills = string.Join("\n", c.Skills.Select(s =>
                $"<link=\"skill:{s.id}\"><u>{s.name}</u></link> <size=80%><color=#999>{s.kind}</color></size>"));
            string passives = c.Passives.Count == 0 ? ""
                : "\n\n<b>패시브</b>\n" + string.Join("\n", c.Passives.Select(p =>
                    $"<link=\"passive:{p.id}\"><u><color=#eb4>{p.name}</color></u></link>"));
            string injuries = c.Injuries.Count == 0 ? ""
                : "<b>부상</b>\n" + string.Join("\n", c.Injuries.Select(i =>
                    $"<color=#e77>{db.InjuryStageName(i.Id, i.Severe)}</color> <size=80%><color=#999>{db.GetInjury(i.Id).description}</color></size>")) + "\n\n";
            detailText.text = injuries + "<b>효과</b>\n" + BattleText.Effects(c) + "\n\n<b>기술</b>\n" + skills + passives;
        }
    }
}
