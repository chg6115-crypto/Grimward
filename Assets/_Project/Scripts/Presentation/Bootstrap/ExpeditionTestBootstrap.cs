using System.Collections;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Map;
using Grimward.Domain.Units;
using Grimward.Presentation.Battle;
using Grimward.Presentation.Data;
using Grimward.Presentation.Map;
using UnityEngine;

namespace Grimward.Presentation.Bootstrap
{
    /// <summary>
    /// M2 탐험 테스트 씬(Expedition)의 구성 루트. 전투 화면(Panel_Battle)을 「탐험 무대」로 쓰고, 그 위에 지도 오버레이를 띄운다.
    /// 노드 도착 → 전투가 남아 있으면 같은 무대에서 전투 → 골동품·채집물은 무대 가운데에 두고 눌러서 상호작용
    /// → 노드의 할 일이 끝나거나 골동품을 지나치면 지도가 자동으로 열린다.
    /// 마을(M3)이 생기기 전까지 파티·보급품은 Inspector 값으로 시작한다.
    /// </summary>
    public sealed class ExpeditionTestBootstrap : MonoBehaviour
    {
        [SerializeField] private BattleScreen stage;
        [SerializeField] private MapScreen mapScreen;
        [SerializeField] private string mapId = "tutorial";
        [SerializeField] private string[] partyClasses = { "swordsman", "mage" };
        [SerializeField] private int potions = 5;
        [SerializeField] private int keys = 1;
        [SerializeField] private int bandages = 0;
        [Tooltip("출발 시각 (분). 360 = 06:00")]
        [SerializeField] private int startMinute = 360;
        [Tooltip("0이면 실행할 때마다 다른 시드")]
        [SerializeField] private int seed;
        [Tooltip("노드에 도착한 뒤 전투가 시작되기까지 대기(초)")]
        [SerializeField] private float battleDelay = 0.6f;
        [Tooltip("노드의 할 일이 끝난 뒤 지도가 자동으로 열리기까지 대기(초)")]
        [SerializeField] private float mapOpenDelay = 0.5f;

        private GameDatabase db;
        private SpriteCatalog sprites;
        private Expedition expedition;
        private int runIndex;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(BattleScreen stageScreen, MapScreen map)
        {
            stage = stageScreen; mapScreen = map;
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            if (stage == null || mapScreen == null || stage.Curio == null)
            {
                Debug.LogError("ExpeditionTestBootstrap: 화면이 비었습니다. 메뉴 Grimward → 전투 화면 프리팹 갱신 → 탐험 화면 만들기를 실행하세요.");
                return;
            }
            db = GameDataLoader.Load();
            sprites = SpriteCatalog.Load();
            stage.MapRequested += OpenMap;
            stage.CurioClicked += OnCurioClicked;
            stage.Curio.ActRequested += OnCurioAct;
            stage.Curio.Closed += OnCurioClosed;
            stage.PartySwapRequested += OnPartySwap;
            stage.ItemUseRequested += OnItemUse;
            stage.OutsideItemFilter = Expedition.IsUsableOutsideBattle;
            stage.OutsideItemInstant = Expedition.IsInstantOutsideBattle;
            mapScreen.TravelRequested += OnTravel;
            mapScreen.ReturnRequested += OnReturn;
            NewExpedition();
        }

        private void NewExpedition()
        {
            StopAllCoroutines();
            int s = seed != 0 ? seed + runIndex++ : System.Environment.TickCount;
            Debug.Log($"[탐험 테스트] 시드 {s}");
            var random = new SeededRandom(s);
            var party = partyClasses.Select((id, i) =>
            {
                var def = db.GetClass(id);
                var a = new Adventurer($"{id}_{i}", def.name, def, db.Balance.defaultPerception, 3);
                a.RestoreFullHp(db.Balance);
                return a;
            }).ToList();
            var bag = new BattleInventory();
            bag.Add("potion", potions);
            bag.Add("key", keys);
            bag.Add("bandage", bandages);
            var map = new FixedMapProvider(db).GetMap(mapId, random);
            expedition = new Expedition(db, random, party, bag, map, new GameTime(1, startMinute));
            stage.gameObject.SetActive(true);
            mapScreen.gameObject.SetActive(false);
            mapScreen.Bind(expedition, db);
            ShowStage(false);
            AfterNodeChanged();
        }

        // ── 무대 ─────────────────────────────

        /// <summary>지금 노드의 무대: 파티 · 남은 골동품·채집물 (전투가 남아 있으면 전투가 먼저).</summary>
        private void ShowStage(bool animateClock = true)
        {
            var x = expedition;
            stage.ShowExplore(x.Party, x.Bag, db, sprites, x.Time, PlaceText(), animateClock);
            UpdateCurio();
        }

        /// <summary>상단 장소 줄: 장소 이름 (+ 횃불 남은 시간).</summary>
        private string PlaceText()
        {
            var x = expedition;
            string place = Expedition.NodeLabel(x.Map.Current);
            if (!x.TorchLit) return place;
            int m = x.TorchMinutesLeft;
            return $"{place}   <size=80%><color=#f3b45a>횃불 {m / 60}시간{(m % 60 > 0 ? $" {m % 60}분" : "")}</color></size>";
        }

        /// <summary>무대 가운데 물건 = 이 노드에 남은 채집·조사 이벤트 (전투 중에도 그대로 보인다).</summary>
        private void UpdateCurio()
        {
            var ev = expedition.Map.Current.PendingEvents()
                .Select(p => p.ev)
                .FirstOrDefault(e => e.kind == DataKeys.NodeEvent.Gather || e.kind == DataKeys.NodeEvent.Investigate);
            if (ev == null) { stage.SetCurio(null, null); return; }
            stage.SetCurio(mapScreen.IconFor(ev.kind), CurioTitle(ev));
        }

        private string CurioTitle(NodeEventDefinition ev) =>
            ev.kind == DataKeys.NodeEvent.Investigate ? db.GetInvestigation(ev.objectId).name : db.GetItem(ev.item).name;

        /// <summary>노드 상태가 바뀐 뒤: 출정 끝 → 결과, 전투 남음 → 전투, 할 일 없음 → 지도 자동 열기.</summary>
        private void AfterNodeChanged()
        {
            if (expedition.Phase == ExpeditionPhase.Ended) { mapScreen.Close(); stage.Curio.Hide(); ShowResult(); return; }
            if (expedition.HasForcedBattle) { StartCoroutine(BattleSoon()); return; }
            if (expedition.NextEvent().ev == null) StartCoroutine(OpenMapSoon());
        }

        private IEnumerator OpenMapSoon()
        {
            yield return new WaitForSeconds(mapOpenDelay);
            OpenMap();
        }

        private void OpenMap()
        {
            if (expedition.Phase != ExpeditionPhase.Exploring || expedition.HasForcedBattle) return;
            stage.Curio.Hide();
            mapScreen.Open();
        }

        // ── 이동 ─────────────────────────────

        private void OnTravel(string nodeId)
        {
            if (!expedition.CanTravelTo(nodeId)) return;
            mapScreen.Close();
            expedition.TravelTo(nodeId);
            ShowStage();          // 2단계: 걷기 연출. 지금은 무대를 바로 바꾼다
            AfterNodeChanged();
        }

        private void OnReturn()
        {
            if (!expedition.CanReturn) return;
            mapScreen.Close();
            expedition.ReturnToTown();
            AfterNodeChanged();
        }

        // ── 전투 ─────────────────────────────

        private IEnumerator BattleSoon()
        {
            yield return new WaitForSeconds(battleDelay);
            bool nightRaid = expedition.InRandomBattle;
            var battle = expedition.BeginBattle();
            stage.ShowBattle(battle, db, sprites, expedition.Time, OnBattleFinished,
                nightRaid ? "<color=#9ab0ff>밤 습격!</color>" : null);
        }

        private void OnBattleFinished()
        {
            expedition.FinishBattle();   // 승리: 이벤트 완료 · 도주: 직전 노드로 · 전멸: 출정 끝
            if (expedition.Phase != ExpeditionPhase.Ended) ShowStage();
            AfterNodeChanged();          // 겹친 전투가 남았으면 이어서, 할 일이 끝났으면 지도
        }

        // ── 전투 밖: 자리 바꾸기 · 아이템 ─────────────────────────────

        private void OnPartySwap(int rankA, int rankB)
        {
            if (expedition.Phase != ExpeditionPhase.Exploring) return;
            try { expedition.SwapAdjacent(rankA, rankB); }
            catch (System.InvalidOperationException e) { stage.SetInfo(e.Message); return; }
            ShowStage(false);
        }

        private void OnItemUse(string itemId, int rank)
        {
            if (expedition.Phase != ExpeditionPhase.Exploring) return;
            var target = rank > 0 ? expedition.Alive.ElementAtOrDefault(rank - 1) : null;   // 횃불은 대상 없음
            string text;
            try { text = expedition.UseItem(itemId, target); }
            catch (System.InvalidOperationException e) { stage.SetInfo(e.Message); return; }
            ShowStage(false);
            stage.SetInfo(text);
        }

        // ── 골동품·채집물 ─────────────────────────────

        private void OnCurioClicked()
        {
            if (expedition.Phase != ExpeditionPhase.Exploring || expedition.HasForcedBattle) return;
            var (_, ev) = expedition.NextEvent();
            if (ev == null) return;
            mapScreen.Close();
            if (ev.kind == DataKeys.NodeEvent.Gather)
            {
                stage.Curio.ShowEvent(CurioTitle(ev), $"{db.GetItem(ev.item).name}이(가) 자라 있다. 채집하면 {ev.amount}개를 얻는다.", "채집한다", null, false);
            }
            else if (ev.kind == DataKeys.NodeEvent.Investigate)
            {
                var obj = db.GetInvestigation(ev.objectId);
                bool hasSupplySlot = !string.IsNullOrEmpty(obj.supplyItem);
                string supplyLabel = "아이템을 사용한다";
                int count = hasSupplySlot ? expedition.Bag.Count(obj.supplyItem) : 0;
                string body = obj.situation + (hasSupplySlot
                    ? $"\n\n<size=85%><color=#cbbf9a>쓸 수 있는 아이템: {db.GetItem(obj.supplyItem).name} (보유 {count})</color></size>"
                    : "");
                stage.Curio.ShowEvent(obj.name, body, "조사한다", supplyLabel, count > 0);
            }
        }

        private void OnCurioAct(bool useSupply)
        {
            var (index, ev) = expedition.NextEvent();
            if (ev == null) return;
            if (useSupply && !CanUseSupply(ev))
            {
                // 맞는 아이템이 없거나 이 물건에는 쓸 아이템이 없다: 아무것도 소모하지 않고, 창은 그대로
                stage.Curio.ShowNote("아무 일도 일어나지 않았다.");
                return;
            }
            if (ev.kind == DataKeys.NodeEvent.Gather)
            {
                var got = expedition.Gather(index);
                stage.Curio.ShowResult(CurioTitle(ev), $"{db.GetItem(got.item).name} {got.amount}개를 얻었다.");
            }
            else if (ev.kind == DataKeys.NodeEvent.Investigate)
            {
                var o = expedition.Investigate(index, useSupply);
                string grade = o.Grade == "good" ? "<color=#8d8>좋음</color>" : o.Grade == "bad" ? "<color=#e77>나쁨</color>" : "보통";
                string gained = o.Gained.Count > 0 ? "\n\n얻은 것: " + string.Join(", ", o.Gained) : "";
                stage.Curio.ShowResult(db.GetInvestigation(ev.objectId).name, $"[{grade}] {o.Text}{gained}");
            }
            // 가방·HP가 바뀌었을 수 있으니 무대를 다시 그린다 (물건은 결과창을 닫을 때 사라진다)
            stage.ShowExplore(expedition.Party, expedition.Bag, db, sprites, expedition.Time, PlaceText());
        }

        /// <summary>이 이벤트에 맞는 아이템(조사 물건의 supplyItem)을 가지고 있는가.</summary>
        private bool CanUseSupply(NodeEventDefinition ev)
        {
            if (ev.kind != DataKeys.NodeEvent.Investigate) return false;
            var item = db.GetInvestigation(ev.objectId).supplyItem;
            return !string.IsNullOrEmpty(item) && expedition.Bag.Count(item) > 0;
        }

        /// <param name="afterResult">결과를 본 뒤 닫았으면 true, 아무것도 안 하고 지나갔으면 false.</param>
        private void OnCurioClosed(bool afterResult)
        {
            UpdateCurio();
            if (afterResult) AfterNodeChanged();   // 다음 이벤트가 있으면 그대로, 없으면 지도
            else StartCoroutine(OpenMapSoon());    // 지나간다 → 골동품은 남고 지도
        }

        // ── 결과 ─────────────────────────────

        private void ShowResult()
        {
            var x = expedition;
            string head = x.End == ExpeditionEnd.Wiped ? "<color=#e55>전멸</color>" : "귀환";
            string bag = string.Join(" · ", x.Bag.Entries.Where(e => e.Value > 0).Select(e => $"{db.GetItem(e.Key).name} {e.Value}"));
            string party = string.Join("\n", x.Party.Select(a => a.IsDead ? $"{a.Name} 사망" : $"{a.Name} HP {a.CurrentHp}"));
            stage.ShowExpeditionResult($"<size=150%>{head}</size>\n{x.Time}\n처치 {x.Killed.Count}\n{party}\n<size=85%>{bag}</size>",
                "다시 출정", NewExpedition);
        }
    }
}
