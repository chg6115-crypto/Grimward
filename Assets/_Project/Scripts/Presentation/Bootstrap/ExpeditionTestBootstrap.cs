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
    /// M2 탐험 테스트 씬(Expedition)의 구성 루트. 탐험 화면 위에 지도 오버레이를 띄우고, 전투는 같은 씬의 전투 패널로 전환한다.
    /// 노드에 도착해 전투가 남아 있으면 전투부터, 그다음 조사·채집. 노드의 할 일이 다 끝나면 지도가 자동으로 열린다.
    /// 마을(M3)이 생기기 전까지 파티·보급품은 Inspector 값으로 시작한다.
    /// </summary>
    public sealed class ExpeditionTestBootstrap : MonoBehaviour
    {
        [SerializeField] private ExploreScreen exploreScreen;
        [SerializeField] private MapScreen mapScreen;
        [SerializeField] private BattleScreen battleScreen;
        [SerializeField] private string mapId = "tutorial";
        [SerializeField] private string[] partyClasses = { "swordsman", "mage" };
        [SerializeField] private int potions = 5;
        [SerializeField] private int keys = 1;
        [SerializeField] private int bandages = 0;
        [Tooltip("출발 시각 (분). 360 = 06:00")]
        [SerializeField] private int startMinute = 360;
        [Tooltip("0이면 실행할 때마다 다른 시드")]
        [SerializeField] private int seed;
        [Tooltip("노드에 도착한 뒤 전투 화면으로 넘어가기까지 대기(초)")]
        [SerializeField] private float battleDelay = 0.6f;
        [Tooltip("노드의 할 일이 끝난 뒤 지도가 자동으로 열리기까지 대기(초)")]
        [SerializeField] private float mapOpenDelay = 0.5f;

        private GameDatabase db;
        private SpriteCatalog sprites;
        private Expedition expedition;
        private int runIndex;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(ExploreScreen explore, MapScreen map, BattleScreen battle)
        {
            exploreScreen = explore; mapScreen = map; battleScreen = battle;
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            if (exploreScreen == null || mapScreen == null || battleScreen == null)
            {
                Debug.LogError("ExpeditionTestBootstrap: 화면이 비었습니다. 메뉴 Grimward → 탐험 화면 만들기를 실행하세요.");
                return;
            }
            db = GameDataLoader.Load();
            sprites = SpriteCatalog.Load();
            exploreScreen.MapRequested += OpenMap;
            exploreScreen.EventRequested += OnEvent;
            exploreScreen.ReturnRequested += OnReturn;
            exploreScreen.RestartRequested += NewExpedition;
            mapScreen.TravelRequested += OnTravel;
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
            battleScreen.gameObject.SetActive(false);
            mapScreen.gameObject.SetActive(false);
            mapScreen.Bind(expedition, db);
            exploreScreen.Show(expedition, db);
            AfterNodeChanged();
        }

        private void OpenMap()
        {
            if (expedition.Phase != ExpeditionPhase.Exploring || expedition.HasForcedBattle) return;
            mapScreen.Open();
        }

        /// <summary>노드에 남은 할 일이 없으면 잠시 뒤 지도를 자동으로 연다.</summary>
        private void AfterNodeChanged()
        {
            if (expedition.Phase == ExpeditionPhase.Ended) { mapScreen.Close(); ShowResult(); return; }
            if (expedition.HasForcedBattle) { StartCoroutine(BattleSoon()); return; }
            if (expedition.NextEvent().ev == null) StartCoroutine(OpenMapSoon());
        }

        private IEnumerator OpenMapSoon()
        {
            yield return new WaitForSeconds(mapOpenDelay);
            if (expedition.Phase == ExpeditionPhase.Exploring && !battleScreen.gameObject.activeSelf) OpenMap();
        }

        private void OnTravel(string nodeId)
        {
            if (!expedition.CanTravelTo(nodeId)) return;
            mapScreen.Close();
            expedition.TravelTo(nodeId);
            exploreScreen.SetMessage(expedition.HasForcedBattle ? "적이다!" : null);
            AfterNodeChanged();
        }

        private IEnumerator BattleSoon()
        {
            yield return new WaitForSeconds(battleDelay);
            var battle = expedition.BeginBattle();
            battleScreen.gameObject.SetActive(true);
            battleScreen.Show(battle, db, OnBattleClosed, sprites, expedition.Time, "계속");
        }

        private void OnBattleClosed()
        {
            var outcome = expedition.CurrentBattle.Outcome;
            expedition.FinishBattle();
            battleScreen.gameObject.SetActive(false);
            exploreScreen.SetMessage(outcome == BattleOutcome.Victory ? "승리" : outcome == BattleOutcome.Fled ? "도주 — 직전 장소로 물러났다" : "전멸");
            AfterNodeChanged(); // 겹친 전투가 남았으면 이어서, 할 일이 끝났으면 지도
        }

        private void OnEvent(int index, bool useSupply)
        {
            var (_, ev) = expedition.NextEvent();
            if (ev == null) return;
            if (ev.kind == DataKeys.NodeEvent.Gather)
            {
                var got = expedition.Gather(index);
                exploreScreen.SetMessage($"{db.GetItem(got.item).name} {got.amount}개를 얻었다");
            }
            else if (ev.kind == DataKeys.NodeEvent.Investigate)
            {
                var o = expedition.Investigate(index, useSupply);
                exploreScreen.SetMessage(o.Text + (o.Gained.Count > 0 ? " — " + string.Join(", ", o.Gained) : ""));
            }
            AfterNodeChanged();
        }

        private void OnReturn()
        {
            if (!expedition.CanReturn) return;
            expedition.ReturnToTown();
            exploreScreen.Refresh();
            AfterNodeChanged();
        }

        private void ShowResult()
        {
            var x = expedition;
            string head = x.End == ExpeditionEnd.Wiped ? "<color=#e55>전멸</color>" : "귀환";
            string bag = string.Join(" · ", x.Bag.Entries.Where(e => e.Value > 0).Select(e => $"{db.GetItem(e.Key).name} {e.Value}"));
            string party = string.Join("\n", x.Party.Select(a => a.IsDead ? $"{a.Name} 사망" : $"{a.Name} HP {a.CurrentHp}"));
            exploreScreen.ShowResult($"<size=150%>{head}</size>\n{x.Time}\n처치 {x.Killed.Count}\n{party}\n<size=85%>{bag}</size>");
        }
    }
}
