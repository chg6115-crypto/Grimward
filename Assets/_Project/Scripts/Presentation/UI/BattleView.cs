using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Exploration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Grimward.Presentation.UI
{
    public sealed class BattleView
    {
        private readonly ExplorationState state;
        private readonly RectTransform root;
        private readonly RectTransform targets;
        private readonly TMP_FontAsset font;
        private readonly TMP_Text title;
        private readonly TMP_Text hp;
        private readonly TMP_Text order;
        private readonly Button attack;
        private readonly Button defend;
        private readonly Button flee;
        private readonly LogView log;
        private readonly List<Button> targetButtons = new List<Button>();
        private BattleState shown;
        private int selected = -1;
        private int loggedCount;

        public BattleView(RectTransform parent, ExplorationState state, TMP_FontAsset font)
        {
            this.state = state;
            this.font = font;
            root = UiFactory.Panel("Panel_Battle", parent, new Vector2(.02f,.02f), new Vector2(.98f,.89f), new Color(.07f,.095f,.12f));
            root.GetComponent<Image>().raycastTarget = true;
            title = UiFactory.Text("Txt_BattleTitle", root, font, "", 27, new Vector2(.03f,.88f), new Vector2(.54f,.99f));
            hp = UiFactory.Text("Txt_PlayerHp", root, font, "", 23, new Vector2(.03f,.76f), new Vector2(.53f,.87f));
            targets = UiFactory.Rect("Panel_Targets", root, new Vector2(.03f,.38f), new Vector2(.53f,.74f));
            order = UiFactory.Text("Txt_TurnOrder", root, font, "", 18, new Vector2(.03f,.2f), new Vector2(.53f,.36f));
            order.textWrappingMode = TextWrappingModes.Normal;
            var logPanel = UiFactory.Panel("Panel_BattleLog", root, new Vector2(.56f,.04f), new Vector2(.98f,.96f), new Color(.1f,.13f,.16f));
            log = new LogView(logPanel, font, "전투 기록", "Battle");
            attack = UiFactory.Button("Btn_Attack", root, font, "공격", new Vector2(.03f,.05f), new Vector2(.18f,.16f),
                () => state.TryBattleAction(BattleAction.Attack,selected));
            defend = UiFactory.Button("Btn_Defend", root, font, "방어", new Vector2(.205f,.05f), new Vector2(.355f,.16f),
                () => state.TryBattleAction(BattleAction.Defend));
            flee = UiFactory.Button("Btn_Flee", root, font, "도주", new Vector2(.38f,.05f), new Vector2(.53f,.16f),
                () => state.TryBattleAction(BattleAction.Flee));
        }
        public void Refresh()
        {
            var battle = state.Battle;
            root.gameObject.SetActive(battle != null && !state.IsDead);
            if (battle == null) return;
            if (shown != battle)
            {
                shown = battle;
                loggedCount = 0;
                log.Clear();
                selected = battle.Enemies.FirstOrDefault(e => e.IsAlive)?.Id ?? -1;
                foreach (var button in targetButtons)
                {
                    button.gameObject.SetActive(false);
                    Object.Destroy(button.gameObject);
                }
                targetButtons.Clear();
                for (int i = 0; i < battle.Enemies.Count; i++)
                {
                    int id = battle.Enemies[i].Id;
                    float top = 1f - (float)i / battle.Enemies.Count;
                    float bottom = 1f - (float)(i + 1) / battle.Enemies.Count + .035f;
                    targetButtons.Add(UiFactory.Button($"Btn_Target_{id:00}", targets, font, "",
                        new Vector2(0,bottom), new Vector2(1,top), () => { selected = id; Refresh(); }));
                }
            }
            if (!battle.Enemies.Any(e => e.Id == selected && e.IsAlive))
                selected = battle.Enemies.FirstOrDefault(e => e.IsAlive)?.Id ?? -1;
            title.text = $"{(battle.Kind == BattleKind.Ambush ? "경로 매복" : "노드 전투")} · {battle.Round}라운드 · {(battle.IsNight ? "밤" : "낮")}";
            hp.text = $"검사 HP {battle.Player.Hp}/{battle.Player.MaxHp}";
            order.text = "행동 순서\n" + string.Join(" → ", battle.Order.Select(e => $"{e.Fighter.Name}({e.Initiative})"));
            for (int i = 0; i < targetButtons.Count; i++)
            {
                var enemy = battle.Enemies[i];
                targetButtons[i].interactable = battle.IsPlayerTurn && enemy.IsAlive;
                targetButtons[i].GetComponentInChildren<TMP_Text>().text =
                    $"{(selected == enemy.Id ? "▶ " : "")}{enemy.Name} · HP {enemy.Hp}/{enemy.MaxHp}{(!enemy.IsAlive ? " · 쓰러짐" : "")}";
            }
            attack.interactable = battle.IsPlayerTurn && selected >= 0;
            defend.interactable = flee.interactable = battle.IsPlayerTurn;
            while (loggedCount < battle.Logs.Count) log.Append(battle.Logs[loggedCount++]);
        }
    }
}
