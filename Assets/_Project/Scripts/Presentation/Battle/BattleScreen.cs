using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Data;
using Grimward.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [Tooltip("24시간 시계 (Panel_Top 왼쪽). 비어 있으면 표시하지 않는다")]
        [SerializeField] private ClockView clock;

        [Header("연출")]
        [SerializeField] private float enemyTurnDelay = 0.6f;
        [Header("자동 진행 (A 키로 켜고 끄기)")]
        [Tooltip("파티 행동 사이 대기(초). 연출 속도 ×2면 절반")]
        [SerializeField] private float autoActionDelay = 0.5f;
        [Tooltip("전투가 끝난 뒤 다음 전투까지 대기(초)")]
        [SerializeField] private float autoRestartDelay = 2f;
        [SerializeField] private int logLines = 9;
        [SerializeField] private FloatingTextLayer floatingText;
        [SerializeField] private Button speedButton;
        [SerializeField] private TMP_Text speedLabel;
        [Tooltip("연출 한 단계의 기본 시간(초). 속도 ×2면 절반")]
        [SerializeField] private float stepTime = 0.3f;
        [SerializeField] private Color damageColor = new Color(1f, 0.92f, 0.85f);
        [SerializeField] private Color critColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color missColor = new Color(0.7f, 0.7f, 0.72f);
        [SerializeField] private Color healColor = new Color(0.45f, 0.9f, 0.45f);
        [SerializeField] private Color buffColor = new Color(0.5f, 0.75f, 1f);
        [SerializeField] private Color debuffColor = new Color(1f, 0.55f, 0.5f);
        [SerializeField] private Color injuryColor = new Color(1f, 0.4f, 0.35f);
        private const string SpeedPrefKey = "grimward.battleSpeed";

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
        private bool playing;
        private float speed = 1f;
        private readonly Dictionary<CombatantSlotView, (int count, float time)> textStack =
            new Dictionary<CombatantSlotView, (int, float)>();

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

        /// <summary>에디터 UI 빌더 전용: 연출(떠오르는 글자 · 속도 버튼).</summary>
        public void AssignEffects(FloatingTextLayer layer, Button speed, TMP_Text speedText)
        {
            floatingText = layer; speedButton = speed; speedLabel = speedText;
        }

        public bool HasEffects => floatingText != null;

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
            speed = PlayerPrefs.GetFloat(SpeedPrefKey, 1f) >= 2f ? 2f : 1f;
            if (speedButton != null)
                speedButton.onClick.AddListener(() =>
                {
                    speed = speed >= 2f ? 1f : 2f;
                    PlayerPrefs.SetFloat(SpeedPrefKey, speed);
                    UpdateSpeedLabel();
                });
            UpdateSpeedLabel();
            restartButton.onClick.AddListener(() => onRestart?.Invoke());
        }

        public void AssignClock(ClockView clockView) => clock = clockView;

        /// <param name="time">출정 중이면 지금 시각 (시계 표시). 없으면 시계를 숨긴다.</param>
        /// <param name="continueLabel">결과창 버튼 글자 (예: 「지도로」). 없으면 그대로.</param>
        public void Show(BattleState battleState, GameDatabase database, Action restart, Data.SpriteCatalog spriteCatalog = null,
            Domain.Core.GameTime time = null, string continueLabel = null)
        {
            if (clock != null)
            {
                clock.gameObject.SetActive(time != null);
                if (time != null) clock.Set(time, database.Balance, false);
            }
            if (continueLabel != null)
            {
                var label = restartButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = continueLabel;
            }
            StopAllCoroutines();
            battle = battleState; db = database; onRestart = restart; sprites = spriteCatalog;
            shownHero = null; inspected = null; hoveredButton = -1; selectedSkill = -1; selectedItem = null; enemyRunning = false;
            playing = false; textStack.Clear(); autoTimer = 0f;
            resultPanel.SetActive(false);
            infoText.text = "";
            Refresh();
        }

        // ── 입력 ─────────────────────────────

        private bool CanInput => battle != null && battle.Phase == BattlePhase.PlayerTurn && !enemyRunning && !playing;

        private void OnSkill(int index)
        {
            if (!CanInput || shownHero != battle.CurrentActor) return;
            if (selectedSkill == index) { ClearSelection(); return; }
            var skill = battle.CurrentSkills[index];
            if (!battle.CanUseSkill(skill, out string reason)) { infoText.text = $"{skill.name}: {reason}"; return; }

            selectedItem = null;
            if (skill.target == DataKeys.Target.AllySlot) // 소환(빈 칸)은 고를 대상이 없다
            {
                Execute(() => battle.UseSkill(skill, battle.TargetsFor(skill).First()));
                return;
            }
            // 자신·아군 전체 기술도 아군 칸을 눌러야 발동 (실수 방지)
            selectedSkill = index;
            string how = skill.target == DataKeys.Target.Self ? "자신을 눌러 사용"
                : skill.target == DataKeys.Target.AllAllies ? "아군 아무나 눌러 아군 전체에 사용"
                : "대상을 고르세요";
            infoText.text = $"{skill.name} — {how} (같은 기술·단축키를 다시 누르면 취소)";
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
            int from = battle.Log.Count;
            selectedSkill = -1; selectedItem = null;
            try { action(); infoText.text = ""; }
            catch (InvalidOperationException e) { infoText.text = e.Message; Refresh(); return; }
            StartCoroutine(Playback(from));
        }

        // ── 적 차례 ─────────────────────────────

        private static readonly Key[] SkillKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
        private static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5 };

        /// <summary>단축키 1~4 = 기술 칸, 5 = 이동. 같은 키를 다시 누르면 선택 취소.</summary>
        private void HandleHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null || !CanInput) return;
            for (int i = 0; i < SkillKeys.Length; i++)
            {
                if (!kb[SkillKeys[i]].wasPressedThisFrame && !kb[NumpadKeys[i]].wasPressedThisFrame) continue;
                if (shownHero != battle.CurrentActor) { shownHero = battle.CurrentActor; Refresh(); }
                heroPanel.PressButton(i);
                return;
            }
        }

        // ── 자동 진행 ─────────────────────────────

        private bool autoPlay;
        private AutoPartyAI.Focus autoFocus;
        private float autoTimer;

        public bool AutoPlay => autoPlay;

        /// <summary>자동 진행: 파티도 AI가 조작하고, 전투가 끝나면 다음 전투(onRestart)로 넘어간다.</summary>
        public void SetAutoPlay(bool on, AutoPartyAI.Focus focus)
        {
            autoPlay = on; autoFocus = focus; autoTimer = 0f;
            if (battle != null) { ClearSelection(); Refresh(false); }
        }

        private void TickAuto()
        {
            if (battle == null || playing || enemyRunning) return;
            if (battle.Phase == BattlePhase.PlayerTurn)
            {
                autoTimer += Time.deltaTime;
                if (autoTimer < autoActionDelay / speed) return;
                autoTimer = 0f;
                shownHero = battle.CurrentActor;
                if (battle.Round > 60) { Execute(battle.Flee); return; } // 교착 상태 방지
                var action = AutoPartyAI.Decide(battle, autoFocus);
                Execute(() =>
                {
                    try { AutoPartyAI.Apply(battle, action); }
                    catch (InvalidOperationException) { battle.Pass(); } // 판단이 어긋나면 넘기기 (멈춤 방지)
                });
            }
            else if (battle.Phase == BattlePhase.Ended && resultPanel.activeSelf)
            {
                autoTimer += Time.deltaTime;
                if (autoTimer < autoRestartDelay / speed) return;
                autoTimer = 0f;
                onRestart?.Invoke();
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Key.A].wasPressedThisFrame) SetAutoPlay(!autoPlay, autoFocus);
            if (autoPlay) TickAuto();
            else HandleHotkeys();
            if (battle != null && battle.Phase == BattlePhase.EnemyTurn && !enemyRunning && !playing)
                StartCoroutine(RunEnemy());
        }

        private IEnumerator RunEnemy()
        {
            enemyRunning = true;
            Refresh();
            yield return new WaitForSeconds(enemyTurnDelay / speed);
            int from = battle.Log.Count;
            battle.RunEnemyTurn();
            yield return Playback(from);
            enemyRunning = false;
            Refresh();
        }

        // ── 연출: 행동 하나가 남긴 기록을 순서대로 보여 준다 ─────────────────────────────

        private IEnumerator Playback(int from)
        {
            playing = true;
            Refresh(bindSlots: false);   // 칸은 행동 전 모습 그대로 두고 기록을 따라 바꾼다
            for (int i = from; i < battle.Log.Count; i++)
            {
                var e = battle.Log[i];
                float wait = PlayEvent(e);
                if (wait > 0f) yield return new WaitForSeconds(wait / speed);
            }
            playing = false;
            Refresh();
        }

        /// <summary>기록 한 줄의 연출을 시작하고, 다음 기록까지 기다릴 시간(속도 ×1 기준)을 돌려준다.</summary>
        private float PlayEvent(BattleEvent e)
        {
            float t = stepTime / speed;
            var target = FindSlot(e.Target);
            switch (e.Kind)
            {
                case BattleEventKind.SkillUsed:
                {
                    var actor = FindSlot(e.Actor);
                    if (actor == null) return 0f;
                    actor.PlayLunge(Dir(e.Actor), t * 0.8f);
                    return stepTime * 0.6f;
                }
                case BattleEventKind.Damaged:
                    if (target == null) return 0f;
                    target.UpdateHp(e.Target);
                    target.PlayHit(t);
                    Float(target, e.Critical ? $"치명타! {e.Amount}" : $"{e.Amount}", e.Critical ? critColor : damageColor,
                        e.Critical ? 1.35f : 1.15f);
                    return stepTime;
                case BattleEventKind.Missed:
                    if (target == null) return 0f;
                    target.PlayDodge(Dir(e.Target), t);
                    Float(target, "회피", missColor);
                    return stepTime;
                case BattleEventKind.Healed:
                    if (target == null) return 0f;
                    target.UpdateHp(e.Target);
                    Float(target, $"+{e.Amount}", healColor, 1.1f);
                    return stepTime * 0.7f;
                case BattleEventKind.PoisonTicked:
                case BattleEventKind.BleedTicked:
                case BattleEventKind.BurnTicked:
                    if (target == null) return 0f;
                    target.UpdateHp(e.Target);
                    target.PlayHit(t * 0.7f);
                    Float(target, (e.Kind == BattleEventKind.PoisonTicked ? "중독 " : e.Kind == BattleEventKind.BurnTicked ? "화상 " : "출혈 ") + e.Amount,
                        e.Kind == BattleEventKind.PoisonTicked ? healColor : e.Kind == BattleEventKind.BurnTicked ? critColor : injuryColor, 0.95f);
                    return stepTime * 0.8f;
                case BattleEventKind.StatusResisted:
                    if (target != null) Float(target, e.Message.EndsWith("면역") ? "면역" : "저항", missColor, 0.9f);
                    return stepTime * 0.5f;
                case BattleEventKind.Stunned:
                    if (target != null) Float(target, "기절", critColor, 1.05f);
                    return stepTime;
                case BattleEventKind.EffectApplied:
                {
                    if (target == null) return 0f;
                    string label = EffectLabel(e.Message);
                    Float(target, label, IsDebuff(label) ? debuffColor : buffColor, 0.85f);
                    return stepTime * 0.35f;
                }
                case BattleEventKind.Injured:
                    if (target == null) return 0f;
                    Float(target, (e.Message.Contains("악화") ? "악화! " : "부상! ") + Between(e.Message, '「', '」'), injuryColor, 1.05f);
                    return stepTime;
                case BattleEventKind.InjuryTreated:
                    if (target != null) Float(target, "치료", healColor, 0.9f);
                    return stepTime * 0.5f;
                case BattleEventKind.PoisonCured:
                    if (target != null) Float(target, "해독", healColor, 0.9f);
                    return stepTime * 0.5f;
                case BattleEventKind.BleedStopped:
                    if (target != null) Float(target, "지혈", healColor, 0.9f);
                    return stepTime * 0.5f;
                case BattleEventKind.DisplaceResisted:
                    if (target != null) Float(target, "저항", missColor, 0.95f);
                    return stepTime * 0.6f;
                case BattleEventKind.Died:
                    if (target == null) return 0f;
                    target.PlayDeath(t * 1.2f);
                    return stepTime * 1.2f;
                case BattleEventKind.Stole:
                    if (target != null) Float(target, "약탈! " + Between(e.Message, '「', '」'), injuryColor, 1.1f);
                    bag.Bind(battle.Inventory, db, false, null);
                    return stepTime;
                case BattleEventKind.EscapeDelayed:
                    if (target != null) Float(target, "도주 지연", buffColor, 0.95f);
                    return stepTime * 0.6f;
                case BattleEventKind.Escaped:
                    if (target == null) return 0f;
                    Float(target, "도주!", injuryColor, 1.1f);
                    target.PlayDeath(t * 1.2f);
                    return stepTime * 1.2f;
                case BattleEventKind.ItemRecovered:
                    if (target != null) Float(target, "되찾음! " + Between(e.Message, '「', '」'), healColor, 1.05f);
                    bag.Bind(battle.Inventory, db, false, null);
                    return stepTime;
                case BattleEventKind.Displaced:
                case BattleEventKind.Moved:
                case BattleEventKind.Summoned:
                    // 자리가 바뀌었으니 칸을 지금 진형으로 다시 그린다
                    BindSlots(BattleSide.Party, partySlots, new List<Combatant>());
                    BindSlots(BattleSide.Enemy, enemySlots, new List<Combatant>());
                    return stepTime * 0.8f;
                default:
                    return 0f;
            }
        }

        private void Float(CombatantSlotView slot, string text, Color color, float scale = 1f)
        {
            if (floatingText == null || slot == null) return;
            // 같은 칸에 연달아 뜨면 위로 쌓는다
            int stack = 0;
            if (textStack.TryGetValue(slot, out var prev) && Time.time - prev.time < 0.8f / speed) stack = prev.count + 1;
            textStack[slot] = (stack, Time.time);
            floatingText.Show(text, color, slot.BodyRect, scale, stack % 4, speed);
        }

        private CombatantSlotView FindSlot(Combatant c)
        {
            if (c == null) return null;
            foreach (var slot in partySlots.Concat(enemySlots))
                if (slot.gameObject.activeSelf && slot.Bound == c) return slot;
            return null;
        }

        private static float Dir(Combatant c) => c.Side == BattleSide.Party ? 1f : -1f;

        /// <summary>"  이름: 피해 +20% (2라운드)" → "피해 +20%"</summary>
        private static string EffectLabel(string message)
        {
            int colon = message.IndexOf(": ", StringComparison.Ordinal);
            string label = colon >= 0 ? message.Substring(colon + 2) : message.Trim();
            int paren = label.LastIndexOf(" (", StringComparison.Ordinal);
            return paren > 0 ? label.Substring(0, paren) : label;
        }

        /// <summary>수치가 음수면 디버프. 단 「받는 피해 −」는 이로운 효과.</summary>
        private static bool IsDebuff(string label)
        {
            if (label.StartsWith("기절") || label.StartsWith("화상") || label.StartsWith("출혈") || label.StartsWith("중독")) return true;
            bool negative = label.Contains("-") || label.Contains("−");
            return label.StartsWith("받는 피해") ? !negative : negative;
        }

        private static string Between(string s, char open, char close)
        {
            int a = s.IndexOf(open), b = s.LastIndexOf(close);
            return a >= 0 && b > a ? s.Substring(a + 1, b - a - 1) : "";
        }

        private void UpdateSpeedLabel()
        {
            if (speedLabel != null) speedLabel.text = speed >= 2f ? "속도 ×2" : "속도 ×1";
        }

        // ── 표시 ─────────────────────────────

        private void Refresh() => Refresh(true);

        private void Refresh(bool bindSlots)
        {
            if (battle == null) return;
            var actor = battle.CurrentActor;
            if (actor != null && actor.Side == BattleSide.Party) shownHero = actor;
            if (shownHero == null || !battle.Formation.Side(BattleSide.Party).Contains(shownHero))
                shownHero = battle.Formation.Alive(BattleSide.Party).FirstOrDefault() ?? shownHero;
            if (inspected != null && !inspected.IsAlive) inspected = null;

            roundText.text = $"{battle.Round}";
            turnText.text = (autoPlay ? "<color=#eb4>[자동]</color> " : "") + (battle.Phase == BattlePhase.Ended ? "전투 종료"
                : actor == null ? "" : $"{(battle.IsNight ? "밤 · " : "")}{actor.Name}의 차례");
            fleeButton.interactable = CanInput;

            if (bindSlots)
            {
                var targets = CurrentTargets();
                BindSlots(BattleSide.Party, partySlots, targets);
                BindSlots(BattleSide.Enemy, enemySlots, targets);
            }

            heroPanel.Bind(shownHero, db, battle, CanInput && shownHero == actor, selectedSkill);

            bool showInfo = inspected != null;
            infoPanel.gameObject.SetActive(showInfo);
            bagLogGroup.SetActive(!showInfo);
            if (showInfo) infoPanel.Bind(inspected, db, battle.Formation.RankOf(inspected));
            bag.Bind(battle.Inventory, db, CanInput && battle.CanUseItem, selectedItem);
            logText.text = string.Join("\n", battle.Log.Skip(Math.Max(0, battle.Log.Count - logLines)).Select(e => e.Message));

            if (battle.Phase == BattlePhase.Ended && !playing && !resultPanel.activeSelf)
            {
                resultPanel.SetActive(true);
                string outcome = battle.Outcome == BattleOutcome.Victory ? "승리"
                    : battle.Outcome == BattleOutcome.Defeat ? "전멸" : "도주";
                string fallen = string.Join("\n", battle.Log.Where(l => l.Kind == BattleEventKind.KnockedDown || l.Kind == BattleEventKind.Escaped)
                    .Select(l => $"<size=80%><color=#e77>{l.Message}</color></size>"));
                resultText.text = $"<size=150%>{outcome}</size>\n{battle.Round}라운드 · 처치 {battle.KilledMonsterIds.Count}" +
                                  (fallen.Length > 0 ? "\n" + fallen : "");
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
                    hoverText = BattleText.SkillTooltip(skill, inspected.Level, db, inspected.Side == BattleSide.Enemy, inspected);
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
            else if (index < shownHero.Skills.Count) text = BattleText.SkillTooltip(shownHero.Skills[index], shownHero.Level, db, false, shownHero);
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
