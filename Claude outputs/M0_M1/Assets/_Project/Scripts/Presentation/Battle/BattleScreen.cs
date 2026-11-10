using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using Grimward.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Battle
{
    /// <summary>
    /// 전투 화면 (Panel_Battle). 왼쪽 플레이어 4 3 2 1 | 1 2 3 4 오른쪽 몬스터.
    /// 기술 → 대상 칸 순서로 누른다. 적 차례는 잠깐 기다렸다가 자동 진행.
    /// 규칙 계산은 하지 않고 BattleState만 호출한다.
    /// </summary>
    public sealed class BattleScreen : MonoBehaviour
    {
        private static readonly Color ActorColor = new Color(0.55f, 0.45f, 0.12f);
        private static readonly Color TargetColor = new Color(0.16f, 0.45f, 0.24f);
        private static readonly Color DeadColor = new Color(0.08f, 0.08f, 0.1f);
        private static readonly Color SelectedColor = new Color(0.35f, 0.3f, 0.55f);
        private const float EnemyTurnDelay = 0.6f;
        private const int LogLines = 16;

        private BattleState battle;
        private GameDatabase db;
        private TMP_FontAsset font;
        private Action onRestart;

        private RectTransform root;
        private TMP_Text roundText, turnText, infoText, logText, resultText;
        private readonly Button[] partySlots = new Button[4];
        private readonly Button[] enemySlots = new Button[4];
        private readonly Button[] skillButtons = new Button[4];
        private Button moveFront, moveBack, pass, flee, cancel;
        private readonly List<(string itemId, Button button)> itemButtons = new List<(string, Button)>();
        private GameObject resultPanel;

        private SkillDefinition selectedSkill;
        private string selectedItem;
        private bool enemyRunning;

        public void Initialize(BattleState battleState, GameDatabase database, TMP_FontAsset uiFont, Action restart)
        {
            battle = battleState; db = database; font = uiFont; onRestart = restart;
            Build();
            Refresh();
        }

        // ── 화면 구성 ─────────────────────────────

        private void Build()
        {
            root = UiFactory.CreateCanvas("Canvas_Battle");
            root.parent.SetParent(transform, false);
            var panel = UiFactory.Panel("Panel_Battle", root, Vector2.zero, Vector2.one, new Color(0.06f, 0.07f, 0.09f)).transform;

            roundText = UiFactory.Text("Txt_Round", panel, font, "", 30, new Vector2(.02f, .92f), new Vector2(.35f, .99f));
            turnText = UiFactory.Text("Txt_Turn", panel, font, "", 30, new Vector2(.35f, .92f), new Vector2(.98f, .99f),
                TextAlignmentOptions.Center);

            // 진형: 플레이어 4 3 2 1 | 1 2 3 4 몬스터
            for (int i = 0; i < 4; i++)
            {
                int rank = 4 - i;
                float x0 = .02f + i * .115f;
                partySlots[rank - 1] = UiFactory.Button($"Btn_PartySlot{rank}", panel, font, "", 21,
                    new Vector2(x0, .52f), new Vector2(x0 + .105f, .9f), () => OnSlot(BattleSide.Party, rank));
            }
            for (int i = 0; i < 4; i++)
            {
                int rank = i + 1;
                float x0 = .52f + i * .115f;
                enemySlots[i] = UiFactory.Button($"Btn_EnemySlot{rank}", panel, font, "", 21,
                    new Vector2(x0, .52f), new Vector2(x0 + .105f, .9f), () => OnSlot(BattleSide.Enemy, rank));
            }
            foreach (var slot in partySlots.Concat(enemySlots))
            {
                var colors = slot.colors;
                colors.disabledColor = Color.white; // 대상이 아닐 때도 어둡게 만들지 않는다
                slot.colors = colors;
            }
            UiFactory.Text("Txt_Vs", panel, font, "VS", 34, new Vector2(.48f, .66f), new Vector2(.52f, .76f),
                TextAlignmentOptions.Center);

            infoText = UiFactory.Text("Txt_SkillInfo", panel, font, "", 22, new Vector2(.02f, .44f), new Vector2(.6f, .51f));

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float x0 = .02f + i * .145f;
                skillButtons[i] = UiFactory.Button($"Btn_Skill{i + 1}", panel, font, "", 22,
                    new Vector2(x0, .3f), new Vector2(x0 + .135f, .43f), () => OnSkill(index));
            }
            moveFront = UiFactory.Button("Btn_MoveFront", panel, font, "◀ 앞으로", 22, new Vector2(.02f, .17f), new Vector2(.135f, .28f), () => OnMove(-1));
            moveBack = UiFactory.Button("Btn_MoveBack", panel, font, "뒤로 ▶", 22, new Vector2(.145f, .17f), new Vector2(.26f, .28f), () => OnMove(1));
            pass = UiFactory.Button("Btn_Pass", panel, font, "넘기기", 22, new Vector2(.27f, .17f), new Vector2(.385f, .28f), OnPass);
            flee = UiFactory.Button("Btn_Flee", panel, font, "도주", 22, new Vector2(.395f, .17f), new Vector2(.51f, .28f), OnFlee);
            cancel = UiFactory.Button("Btn_Cancel", panel, font, "취소", 22, new Vector2(.52f, .17f), new Vector2(.6f, .28f), ClearSelection);

            int n = 0;
            foreach (var entry in battle.Inventory.Entries.ToList())
            {
                string itemId = entry.Key;
                float x0 = .02f + n * .145f;
                var b = UiFactory.Button($"Btn_Item_{itemId}", panel, font, "", 22,
                    new Vector2(x0, .04f), new Vector2(x0 + .135f, .15f), () => OnItem(itemId));
                itemButtons.Add((itemId, b));
                n++;
            }

            var logPanel = UiFactory.Panel("Panel_Log", panel, new Vector2(.62f, .04f), new Vector2(.98f, .5f),
                new Color(0.1f, 0.11f, 0.14f, 0.85f)).transform;
            logText = UiFactory.Text("Txt_Log", logPanel, font, "", 19, new Vector2(.03f, .02f), new Vector2(.97f, .98f),
                TextAlignmentOptions.BottomLeft);
            logText.overflowMode = TextOverflowModes.Truncate;

            var result = UiFactory.Panel("Panel_BattleResult", panel, new Vector2(.3f, .3f), new Vector2(.7f, .75f),
                new Color(0.12f, 0.12f, 0.16f, 0.97f));
            resultPanel = result.gameObject;
            resultText = UiFactory.Text("Txt_Result", result.transform, font, "", 30, new Vector2(.06f, .35f),
                new Vector2(.94f, .95f), TextAlignmentOptions.Center);
            UiFactory.Button("Btn_Restart", result.transform, font, "새 전투", 26, new Vector2(.3f, .08f),
                new Vector2(.7f, .28f), () => onRestart?.Invoke());
            resultPanel.SetActive(false);
        }

        // ── 입력 ─────────────────────────────

        private void OnSkill(int index)
        {
            if (!IsPlayerTurn()) return;
            var skills = battle.CurrentSkills;
            if (index >= skills.Count) return;
            var skill = skills[index];
            if (!battle.CanUseSkill(skill, out string reason)) { infoText.text = $"{skill.name}: {reason}"; return; }

            selectedItem = null;
            selectedSkill = skill;
            var targets = battle.TargetsFor(skill);
            // 자신·아군 전체·소환처럼 고를 게 하나뿐이면 바로 사용
            if (skill.target == DataKeys.Target.Self || skill.target == DataKeys.Target.AllySlot ||
                skill.target == DataKeys.Target.AllAllies)
            {
                Execute(() => battle.UseSkill(skill, targets.First()));
                return;
            }
            infoText.text = $"{skill.name}: {skill.description} — 대상을 고르세요";
            Refresh();
        }

        private void OnItem(string itemId)
        {
            if (!IsPlayerTurn() || !battle.CanUseItem || battle.Inventory.Count(itemId) <= 0) return;
            selectedSkill = null;
            selectedItem = itemId;
            infoText.text = $"{db.GetItem(itemId).name}: 아군을 고르세요 (턴 소모 없음)";
            Refresh();
        }

        private void OnSlot(BattleSide side, int rank)
        {
            if (!IsPlayerTurn()) return;
            var target = battle.Formation.At(side, rank);
            if (target == null || !target.IsAlive) return;

            if (selectedSkill != null)
            {
                var skill = selectedSkill;
                if (!battle.TargetsFor(skill).Contains(target)) return;
                Execute(() => battle.UseSkill(skill, target));
            }
            else if (selectedItem != null && side == BattleSide.Party)
            {
                string item = selectedItem;
                Execute(() => battle.UseItem(item, target));
            }
        }

        private void OnMove(int delta)
        {
            if (IsPlayerTurn() && battle.CanMove(delta)) Execute(() => battle.Move(delta));
        }

        private void OnPass() { if (IsPlayerTurn()) Execute(battle.Pass); }
        private void OnFlee() { if (IsPlayerTurn()) Execute(battle.Flee); }

        private void ClearSelection()
        {
            selectedSkill = null; selectedItem = null; infoText.text = "";
            Refresh();
        }

        private void Execute(Action action)
        {
            try { action(); infoText.text = ""; }
            catch (InvalidOperationException e) { infoText.text = e.Message; }
            selectedSkill = null; selectedItem = null;
            Refresh();
        }

        private bool IsPlayerTurn() => battle.Phase == BattlePhase.PlayerTurn && !enemyRunning;

        // ── 적 차례 ─────────────────────────────

        private void Update()
        {
            if (battle != null && battle.Phase == BattlePhase.EnemyTurn && !enemyRunning)
                StartCoroutine(RunEnemy());
        }

        private IEnumerator RunEnemy()
        {
            enemyRunning = true;
            Refresh();
            yield return new WaitForSeconds(EnemyTurnDelay);
            battle.RunEnemyTurn();
            enemyRunning = false;
            Refresh();
        }

        // ── 표시 ─────────────────────────────

        private void Refresh()
        {
            roundText.text = $"{battle.Round}라운드 · {(battle.IsNight ? "밤" : "낮")}";
            var actor = battle.CurrentActor;
            turnText.text = battle.Phase == BattlePhase.Ended ? "전투 종료"
                : actor == null ? "" : $"▶ {actor.Name}의 차례 ({battle.Formation.RankOf(actor)}번 칸)";

            var targets = selectedSkill != null ? battle.TargetsFor(selectedSkill)
                : selectedItem != null ? battle.Formation.Alive(BattleSide.Party).ToList()
                : new List<Combatant>();
            RefreshSlots(BattleSide.Party, partySlots, targets);
            RefreshSlots(BattleSide.Enemy, enemySlots, targets);

            bool playerTurn = IsPlayerTurn();
            var skills = battle.CurrentSkills;
            for (int i = 0; i < skillButtons.Length; i++)
            {
                var b = skillButtons[i];
                bool has = playerTurn && i < skills.Count;
                b.gameObject.SetActive(has);
                if (!has) continue;
                bool usable = battle.CanUseSkill(skills[i], out _);
                UiFactory.SetLabel(b, skills[i].name);
                b.interactable = usable;
                UiFactory.SetColor(b, skills[i] == selectedSkill ? SelectedColor : UiFactory.ButtonColor);
            }
            moveFront.interactable = playerTurn && battle.CanMove(-1);
            moveBack.interactable = playerTurn && battle.CanMove(1);
            pass.interactable = playerTurn;
            flee.interactable = playerTurn;
            cancel.interactable = selectedSkill != null || selectedItem != null;
            foreach (var (itemId, button) in itemButtons)
            {
                int count = battle.Inventory.Count(itemId);
                UiFactory.SetLabel(button, $"{db.GetItem(itemId).name} ×{count}");
                button.interactable = playerTurn && battle.CanUseItem && count > 0;
                UiFactory.SetColor(button, itemId == selectedItem ? SelectedColor : UiFactory.ButtonColor);
            }

            var lines = battle.Log.Skip(Math.Max(0, battle.Log.Count - LogLines)).Select(e => e.Message);
            logText.text = string.Join("\n", lines);

            if (battle.Phase == BattlePhase.Ended && !resultPanel.activeSelf)
            {
                resultPanel.SetActive(true);
                string outcome = battle.Outcome == BattleOutcome.Victory ? "승리"
                    : battle.Outcome == BattleOutcome.Defeat ? "전멸" : "도주";
                resultText.text = $"<size=48>{outcome}</size>\n{battle.Round}라운드 · 처치 {battle.KilledMonsterIds.Count}";
            }
        }

        private void RefreshSlots(BattleSide side, Button[] slots, List<Combatant> targets)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int rank = i + 1;
                var c = battle.Formation.At(side, rank);
                var b = slots[i];
                if (c == null)
                {
                    UiFactory.SetLabel(b, $"{rank}번\n—");
                    UiFactory.SetColor(b, DeadColor);
                    b.interactable = false;
                    continue;
                }
                UiFactory.SetLabel(b, Describe(c, rank));
                bool isTarget = targets.Contains(c);
                UiFactory.SetColor(b, !c.IsAlive ? DeadColor
                    : c == battle.CurrentActor ? ActorColor
                    : isTarget ? TargetColor : UiFactory.ButtonColor);
                b.interactable = isTarget && IsPlayerTurn();
            }
        }

        private static string Describe(Combatant c, int rank)
        {
            var sb = new StringBuilder();
            sb.Append($"{rank}번\n<b>{c.Name}</b>\nHP {c.Hp}/{c.MaxHp}");
            foreach (var e in c.Effects.All)
                sb.Append(e.IsPoison ? $"\n<color=#8f8>중독 {e.Remaining}</color>" : $"\n<size=17>{e.SourceSkillName} {e.Remaining}</size>");
            return sb.ToString();
        }
    }
}
