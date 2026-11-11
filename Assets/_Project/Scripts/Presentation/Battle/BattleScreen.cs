using System;
using System.Collections;
using System.Collections.Generic;
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
    /// 전투 화면 컨트롤러 (Panel_Battle). Hierarchy에 만들어 둔 뷰들을 연결해 BattleState를 표시·조작한다.
    /// 위: 진형 (왼쪽 플레이어 4 3 2 1 | 1 2 3 4 몬스터) · 좌하단: 모험가 패널 · 우하단: 가방 6×2 + 전투 기록 / 상세 정보.
    /// 기술 버튼에 포인터를 올리면(또는 기술을 고르면) 툴팁.
    /// 규칙 계산은 하지 않는다.
    /// </summary>
    public sealed class BattleScreen : MonoBehaviour
    {
        private const int MoveIndex = 4;

        [Header("상단")]
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private Button fleeButton;
        [SerializeField] private TMP_Text infoText;

        [Header("진형 (index 0 = 1번 칸)")]
        [SerializeField] private CombatantSlotView[] partySlots = new CombatantSlotView[4];
        [SerializeField] private CombatantSlotView[] enemySlots = new CombatantSlotView[4];

        [Header("하단")]
        [SerializeField] private HeroPanelView heroPanel;
        [SerializeField] private GameObject bagLogGroup;
        [SerializeField] private BagView bag;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private CombatantInfoView infoPanel;
        [SerializeField] private TooltipView tooltip;

        [Header("결과")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button restartButton;

        [Header("연출")]
        [SerializeField] private float enemyTurnDelay = 0.6f;
        [SerializeField] private int logLines = 9;

        private BattleState battle;
        private GameDatabase db;
        private Data.SpriteCatalog sprites;
        private Action onRestart;

        private Combatant shownHero;
        private Combatant inspected;
        private int hoveredButton = -1;
        // 기술 버튼 외의 툴팁 (상태 표시·상세 정보의 기술/패시브)
        private string hoverText;
        private RectTransform hoverAnchor;
        private Vector2 hoverScreen;
        private int selectedSkill = -1;   // 0~3 기술, 4 이동
        private string selectedItem;
        private bool enemyRunning;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(TMP_Text roundText, TMP_Text turnText, Button fleeButton, TMP_Text infoText,
            CombatantSlotView[] partySlots, CombatantSlotView[] enemySlots, HeroPanelView heroPanel,
            GameObject bagLogGroup, BagView bag, TMP_Text logText, CombatantInfoView infoPanel, TooltipView tooltip,
            GameObject resultPanel, TMP_Text resultText, Button restartButton)
        {
            this.roundText = roundText; this.turnText = turnText; this.fleeButton = fleeButton; this.infoText = infoText;
            this.partySlots = partySlots; this.enemySlots = enemySlots; this.heroPanel = heroPanel;
            this.bagLogGroup = bagLogGroup; this.bag = bag; this.logText = logText; this.infoPanel = infoPanel;
            this.tooltip = tooltip;
            this.resultPanel = resultPanel; this.resultText = resultText; this.restartButton = restartButton;
        }

        private void Awake()
        {
            for (int i = 0; i < 4; i++)
            {
                int rank = i + 1;
                partySlots[i].OnClicked += () => OnSlot(BattleSide.Party, rank);
                enemySlots[i].OnClicked += () => OnSlot(BattleSide.Enemy, rank);
            }
            heroPanel.OnSkill += OnSkill;
            heroPanel.OnMove += OnMove;
            heroPanel.OnPass += () => { if (CanInput) Execute(battle.Pass); };
            bag.OnItem += OnItem;
            heroPanel.OnHover += (index, rect) => { hoveredButton = index; UpdateTooltip(); };
            heroPanel.OnHoverEnd += () => { hoveredButton = -1; UpdateTooltip(); };
            infoPanel.OnClose += () => { inspected = null; Refresh(); };
            infoPanel.OnLinkHover += OnInfoLinkHover;
            infoPanel.OnLinkHoverEnd += ClearHover;
            foreach (var slot in partySlots.Concat(enemySlots))
            {
                var view = slot;
                view.OnStatusHover += rect =>
                {
                    if (view.Bound == null) return;
                    hoverText = BattleText.EffectsTooltip(view.Bound); hoverAnchor = rect; UpdateTooltip();
                };
                view.OnStatusHoverEnd += ClearHover;
            }
            fleeButton.onClick.AddListener(() => { if (CanInput) Execute(battle.Flee); });
            restartButton.onClick.AddListener(() => onRestart?.Invoke());
        }

        public void Show(BattleState battleState, GameDatabase database, Action restart, Data.SpriteCatalog spriteCatalog = null)
        {
            StopAllCoroutines();
            battle = battleState; db = database; onRestart = restart; sprites = spriteCatalog;
            shownHero = null; inspected = null; hoveredButton = -1; selectedSkill = -1; selectedItem = null; enemyRunning = false;
            resultPanel.SetActive(false);
            infoText.text = "";
            Refresh();
        }

        // ── 입력 ─────────────────────────────

        private bool CanInput => battle != null && battle.Phase == BattlePhase.PlayerTurn && !enemyRunning;

        private void OnSkill(int index)
        {
            if (!CanInput || shownHero != battle.CurrentActor) return;
            if (selectedSkill == index) { ClearSelection(); return; }
            var skill = battle.CurrentSkills[index];
            if (!battle.CanUseSkill(skill, out string reason)) { infoText.text = $"{skill.name}: {reason}"; return; }

            selectedItem = null;
            if (skill.target == DataKeys.Target.Self || skill.target == DataKeys.Target.AllySlot ||
                skill.target == DataKeys.Target.AllAllies)
            {
                Execute(() => battle.UseSkill(skill, battle.TargetsFor(skill).First()));
                return;
            }
            selectedSkill = index;
            infoText.text = $"{skill.name} — 대상을 고르세요 (같은 기술을 다시 누르면 취소)";
            Refresh();
        }

        private void OnMove()
        {
            if (!CanInput) return;
            if (selectedSkill == MoveIndex) { ClearSelection(); return; }
            selectedSkill = MoveIndex;
            selectedItem = null;
            infoText.text = "이동: 바로 옆 아군을 고르면 자리를 바꿉니다";
            Refresh();
        }

        private void OnItem(string itemId)
        {
            if (!CanInput || !battle.CanUseItem) return;
            if (selectedItem == itemId) { ClearSelection(); return; }
            selectedSkill = -1;
            selectedItem = itemId;
            var item = db.GetItem(itemId);
            infoText.text = $"{item.name}: 아군을 고르세요 (턴 소모 없음, 한 턴에 1개)";
            Refresh();
        }

        private void OnSlot(BattleSide side, int rank)
        {
            var target = battle?.Formation.At(side, rank);
            if (target == null || !target.IsAlive) return;

            if (CanInput && selectedSkill >= 0 && selectedSkill < MoveIndex)
            {
                var skill = battle.CurrentSkills[selectedSkill];
                if (battle.TargetsFor(skill).Contains(target)) { Execute(() => battle.UseSkill(skill, target)); return; }
            }
            if (CanInput && selectedSkill == MoveIndex && side == BattleSide.Party)
            {
                int delta = rank - battle.Formation.RankOf(battle.CurrentActor);
                if (Math.Abs(delta) == 1 && battle.CanMove(delta)) { Execute(() => battle.Move(delta)); return; }
            }
            if (CanInput && selectedItem != null && side == BattleSide.Party)
            {
                string item = selectedItem;
                Execute(() => battle.UseItem(item, target));
                return;
            }

            // 선택 중이 아니면 상세 정보 보기 (같은 칸을 다시 누르면 닫기)
            if (side == BattleSide.Party) shownHero = target;
            inspected = inspected == target ? null : target;
            Refresh();
        }

        private void ClearSelection()
        {
            selectedSkill = -1; selectedItem = null; infoText.text = "";
            Refresh();
        }

        private void Execute(Action action)
        {
            try { action(); infoText.text = ""; }
            catch (InvalidOperationException e) { infoText.text = e.Message; }
            selectedSkill = -1; selectedItem = null;
            Refresh();
        }

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
            yield return new WaitForSeconds(enemyTurnDelay);
            battle.RunEnemyTurn();
            enemyRunning = false;
            Refresh();
        }

        // ── 표시 ─────────────────────────────

        private void Refresh()
        {
            if (battle == null) return;
            var actor = battle.CurrentActor;
            if (actor != null && actor.Side == BattleSide.Party) shownHero = actor;
            if (shownHero == null || !battle.Formation.Side(BattleSide.Party).Contains(shownHero))
                shownHero = battle.Formation.Alive(BattleSide.Party).FirstOrDefault() ?? shownHero;
            if (inspected != null && !inspected.IsAlive) inspected = null;

            roundText.text = $"{battle.Round}";
            turnText.text = battle.Phase == BattlePhase.Ended ? "전투 종료"
                : actor == null ? "" : $"{(battle.IsNight ? "밤 · " : "")}{actor.Name}의 차례";
            fleeButton.interactable = CanInput;

            var targets = CurrentTargets();
            BindSlots(BattleSide.Party, partySlots, targets);
            BindSlots(BattleSide.Enemy, enemySlots, targets);

            heroPanel.Bind(shownHero, db, battle, CanInput && shownHero == actor, selectedSkill);

            bool showInfo = inspected != null;
            infoPanel.gameObject.SetActive(showInfo);
            bagLogGroup.SetActive(!showInfo);
            if (showInfo) infoPanel.Bind(inspected, db, battle.Formation.RankOf(inspected));
            bag.Bind(battle.Inventory, db, CanInput && battle.CanUseItem, selectedItem);
            logText.text = string.Join("\n", battle.Log.Skip(Math.Max(0, battle.Log.Count - logLines)).Select(e => e.Message));

            if (battle.Phase == BattlePhase.Ended && !resultPanel.activeSelf)
            {
                resultPanel.SetActive(true);
                string outcome = battle.Outcome == BattleOutcome.Victory ? "승리"
                    : battle.Outcome == BattleOutcome.Defeat ? "전멸" : "도주";
                resultText.text = $"<size=150%>{outcome}</size>\n{battle.Round}라운드 · 처치 {battle.KilledMonsterIds.Count}";
            }
            UpdateTooltip();
        }

        private void OnInfoLinkHover(string id, Vector2 screen)
        {
            if (inspected == null) return;
            if (id.StartsWith("skill:"))
            {
                var skill = inspected.Skills.FirstOrDefault(s => "skill:" + s.id == id);
                if (skill != null)
                    hoverText = BattleText.SkillTooltip(skill, inspected.Level, db, inspected.Side == BattleSide.Enemy);
            }
            else if (id.StartsWith("passive:"))
            {
                var p = inspected.Passives.FirstOrDefault(x => "passive:" + x.id == id);
                if (p != null) hoverText = BattleText.PassiveTooltip(p);
            }
            hoverAnchor = null; hoverScreen = screen;
            UpdateTooltip();
        }

        private void ClearHover()
        {
            hoverText = null; hoverAnchor = null;
            UpdateTooltip();
        }

        /// <summary>포인터가 올라간 대상(기술 버튼 > 상태·상세 정보), 없으면 선택 중인 기술(모바일)의 설명을 띄운다.</summary>
        private void UpdateTooltip()
        {
            if (battle == null || battle.Phase == BattlePhase.Ended) { tooltip.Hide(); return; }
            if (hoveredButton < 0 && hoverText != null)
            {
                if (hoverAnchor != null) tooltip.Show(hoverText, hoverAnchor);
                else tooltip.ShowAtScreen(hoverText, hoverScreen);
                return;
            }
            if (shownHero == null) { tooltip.Hide(); return; }
            int index = hoveredButton >= 0 ? hoveredButton : selectedSkill;
            if (index < 0) { tooltip.Hide(); return; }
            string text;
            if (index == MoveIndex) text = BattleText.MoveTooltip();
            else if (index < shownHero.Skills.Count) text = BattleText.SkillTooltip(shownHero.Skills[index], shownHero.Level, db);
            else { tooltip.Hide(); return; }
            tooltip.Show(text, heroPanel.ButtonRect(index));
        }

        private Sprite SpriteFor(Combatant c)
        {
            if (c == null || sprites == null) return null;
            return c.Side == BattleSide.Enemy ? sprites.Monster(c.DefinitionId) : sprites.Hero(c.DefinitionId);
        }

        private List<Combatant> CurrentTargets()
        {
            if (!CanInput) return new List<Combatant>();
            if (selectedSkill >= 0 && selectedSkill < MoveIndex) return battle.TargetsFor(battle.CurrentSkills[selectedSkill]);
            if (selectedSkill == MoveIndex)
            {
                int rank = battle.Formation.RankOf(battle.CurrentActor);
                return battle.Formation.Alive(BattleSide.Party)
                    .Where(c => Math.Abs(battle.Formation.RankOf(c) - rank) == 1).ToList();
            }
            if (selectedItem != null) return battle.Formation.Alive(BattleSide.Party).ToList();
            return new List<Combatant>();
        }

        private void BindSlots(BattleSide side, CombatantSlotView[] slots, List<Combatant> targets)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int rank = i + 1;
                var c = battle.Formation.At(side, rank);
                // 큰 몬스터: 첫 칸 슬롯을 차지 칸 수만큼 넓히고, 나머지 칸 슬롯은 숨긴다
                bool covered = c != null && battle.Formation.RankOf(c) != rank;
                slots[i].gameObject.SetActive(!covered);
                if (covered) continue;
                int size = c?.Size ?? 1;
                int last = Math.Min(slots.Length, rank + size - 1) - 1;
                var min = slots[i].BaseAnchorMin; var max = slots[i].BaseAnchorMax;
                if (side == BattleSide.Enemy) max.x = slots[last].BaseAnchorMax.x;   // 몬스터 칸은 오른쪽으로 늘어남
                else min.x = slots[last].BaseAnchorMin.x;                            // 파티 칸은 왼쪽으로 늘어남
                slots[i].SetSpan(min, max, size);
                var h = CombatantSlotView.Highlight.None;
                if (c != null)
                {
                    if (targets.Contains(c))
                        h = side == BattleSide.Enemy ? CombatantSlotView.Highlight.EnemyTarget : CombatantSlotView.Highlight.AllyTarget;
                    else if (c == battle.CurrentActor) h = CombatantSlotView.Highlight.Actor;
                    else if (c == inspected)
                        h = CombatantSlotView.Highlight.Inspected;
                }
                slots[i].Bind(c, h, SpriteFor(c));
            }
        }
    }
}
