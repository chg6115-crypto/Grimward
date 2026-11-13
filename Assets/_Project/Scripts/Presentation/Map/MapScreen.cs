using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Map;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Grimward.Presentation.Map
{
    /// <summary>
    /// 탐험 지도 오버레이 (uGUI). 탐험 무대(전투 화면) 위를 어둡게 덮고 가운데 창에 지도를 띄운다.
    /// 창 밖(어두운 바탕)이나 X를 누르면 닫힌다. 칸을 눌러 목표를 고르고, 한 번 더 누르거나 「이동」을 누르면 이동 요청.
    /// 규칙(어디까지 한 번에 갈 수 있는지, 걸리는 시간)은 Domain(Expedition)에 있다. 칸은 에디터 메뉴가 Hierarchy에 미리 만든다.
    /// </summary>
    public sealed class MapScreen : MonoBehaviour
    {
        [Header("창")]
        [Tooltip("창 밖 어두운 바탕. 누르면 닫힌다")]
        [SerializeField] private Button dimButton;
        [SerializeField] private Button closeButton;
        [Tooltip("왼쪽: 구역 이름 + 예상귀환시간")]
        [SerializeField] private TMP_Text regionText;
        [Tooltip("오른쪽: 고른 칸까지 이동 시간·도착 시각")]
        [SerializeField] private TMP_Text travelText;
        [SerializeField] private Button travelButton;
        [Tooltip("마을로 귀환 (창 왼쪽 아래). 출발점에서만 누를 수 있다")]
        [SerializeField] private Button returnButton;
        [Tooltip("출구로 (마을로 귀환 옆): 출발점까지 이동만. 귀환은 출구에서 따로")]
        [SerializeField] private Button autoReturnButton;

        [Header("지도")]
        [SerializeField] private HexTileView[] tiles;
        [Tooltip("지도 드래그 영역 (Panel_MapArea의 ScrollRect)")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private MapZoom zoom;
        [Tooltip("이동 연출용 핀 (Grid_Hex 맨 위). 지도에서 칸마다 움직인 뒤 지도가 닫힌다")]
        [SerializeField] private RectTransform travelPin;
        [Tooltip("핀이 한 칸 가는 시간(초)")]
        [SerializeField] private float pinStepSeconds = 0.35f;
        [Tooltip("도착 후 지도가 닫히기까지 대기(초)")]
        [SerializeField] private float pinArriveHold = 0.25f;
        [SerializeField] private float followSeconds = 0.35f;
        [Tooltip("노드 칸 크기 배율 = 칸 사이 간격. 0.96 → 간격 약 12px, 0.85 → 약 36px(3배), 1 → 거의 붙음 (칸 간격 220px 기준)")]
        [SerializeField, Range(0.6f, 1f)] private float nodeScale = 0.85f;

        [Header("아이콘")]
        [SerializeField] private Sprite iconStart;
        [SerializeField] private Sprite iconBattle;
        [SerializeField] private Sprite iconBoss;
        [SerializeField] private Sprite iconGather;
        [SerializeField] private Sprite iconInvestigate;
        [SerializeField] private Sprite iconTreasure;
        [SerializeField] private Sprite roadSolid;
        [SerializeField] private Sprite roadDashed;

        [Header("색")]
        [SerializeField] private Color forestColor = new Color(0.2f, 0.28f, 0.2f);
        [Tooltip("방문한 칸")]
        [SerializeField] private Color visitedFill = new Color(0.76f, 0.72f, 0.56f);
        [Tooltip("발견했지만 아직 가 보지 않은 칸")]
        [SerializeField] private Color unexploredFill = new Color(0.24f, 0.25f, 0.22f);
        [Tooltip("이동할 수 있는 칸의 테두리")]
        [SerializeField] private Color movableOutline = new Color(1f, 0.78f, 0.28f);
        [Tooltip("고른 칸의 테두리")]
        [SerializeField] private Color selectedOutline = new Color(1f, 0.97f, 0.8f);
        [Tooltip("그 밖(범위 밖) 칸의 테두리")]
        [SerializeField] private Color normalOutline = new Color(0.08f, 0.07f, 0.05f, 0.9f);
        [SerializeField] private Color roadTraveled = new Color(0.95f, 0.88f, 0.7f);
        [Tooltip("이동할 수 있는 칸으로 가는, 아직 안 가 본 길 (점선)")]
        [SerializeField] private Color roadOpen = new Color(1f, 0.9f, 0.62f);
        [Tooltip("그 밖의 안 가 본 길 (점선)")]
        [SerializeField] private Color roadDim = new Color(0.8f, 0.76f, 0.66f, 0.4f);
        [SerializeField] private Color nameColor = new Color(0.12f, 0.1f, 0.08f);
        [SerializeField] private Color dimNameColor = new Color(0.85f, 0.83f, 0.76f);
        [SerializeField, Range(0f, 1f)] private float treeChance = 0.55f;

        private Coroutine following;
        private Expedition expedition;
        private GameDatabase db;
        private string selectedId;

        public bool IsOpen => gameObject.activeSelf;
        /// <summary>목표 노드까지 이동 요청 (여러 칸 가능).</summary>
        public event Action<string> TravelRequested;
        public event Action Closed;
        public event Action ReturnRequested;
        public event Action ExitRequested;

        public void Assign(Button dim, Button close, TMP_Text region, TMP_Text travel, Button travelBtn,
            HexTileView[] tileViews, ScrollRect scrollRect, MapZoom mapZoom)
        {
            dimButton = dim; closeButton = close; regionText = region; travelText = travel; travelButton = travelBtn;
            tiles = tileViews; scroll = scrollRect; zoom = mapZoom;
        }

        public void AssignPin(RectTransform pin) => travelPin = pin;

        public void AssignReturn(Button button, Button autoReturn = null)
        {
            returnButton = button; autoReturnButton = autoReturn;
        }

        public void AssignSprites(Sprite start, Sprite battle, Sprite boss, Sprite gather, Sprite investigate, Sprite treasure,
            Sprite solid, Sprite dashed)
        {
            iconStart = start; iconBattle = battle; iconBoss = boss; iconGather = gather; iconInvestigate = investigate;
            iconTreasure = treasure; roadSolid = solid; roadDashed = dashed;
        }

        private void Awake()
        {
            foreach (var t in tiles) { t.Clicked += OnTileClicked; t.SetNodeScale(nodeScale); }
            dimButton.onClick.AddListener(() => { if (!traveling) Close(); });
            closeButton.onClick.AddListener(() => { if (!traveling) Close(); });
            travelButton.onClick.AddListener(() => { if (selectedId != null && !traveling) TravelRequested?.Invoke(selectedId); });
            if (travelPin != null) travelPin.gameObject.SetActive(false);
            if (returnButton != null) returnButton.onClick.AddListener(() => { if (!traveling && expedition != null && expedition.CanReturn) ReturnRequested?.Invoke(); });
            if (autoReturnButton != null) autoReturnButton.onClick.AddListener(() => { if (!traveling && expedition != null && expedition.CanGoToExit) ExitRequested?.Invoke(); });
        }

        public void Bind(Expedition x, GameDatabase database)
        {
            expedition = x; db = database;
            selectedId = null;
        }

        public void Open()
        {
            if (expedition == null) return;
            traveling = false;
            if (travelPin != null) travelPin.gameObject.SetActive(false);
            selectedId = null;
            gameObject.SetActive(true);
            Refresh();
            CenterOnCurrent(false);
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !traveling) Close();
        }

        // ── 이동 연출 ─────────────────────────────

        private bool traveling;
        public bool IsTraveling => traveling;

        /// <summary>
        /// 지도 위에서 핀이 from → path 순서로 한 칸씩 움직인 뒤 지도를 닫고 done을 부른다.
        /// 이동 규칙은 이미 Domain에서 끝난 상태(시간·발견·습격)이고, 화면은 연출만 한다. 연출 중에는 입력을 막는다.
        /// 핀이 없거나 지도가 닫혀 있으면 바로 done.
        /// </summary>
        public void PlayTravel(HexCoord from, IReadOnlyList<HexCoord> path, Action done)
        {
            if (travelPin == null || !IsOpen || path == null || path.Count == 0)
            {
                Close();
                done?.Invoke();
                return;
            }
            StartCoroutine(TravelRoutine(from, path, done));
        }

        private System.Collections.IEnumerator TravelRoutine(HexCoord from, IReadOnlyList<HexCoord> path, Action done)
        {
            traveling = true;
            selectedId = null;
            travelButton.interactable = false;
            var start = Tile(from);
            if (start != null) start.SetPlayerVisible(false);
            travelPin.gameObject.SetActive(true);
            travelPin.SetAsLastSibling();
            travelPin.anchoredPosition = Pos(from);
            var at = from;
            foreach (var next in path)
            {
                Vector2 a = Pos(at), b = Pos(next);
                for (float t = 0; t < pinStepSeconds; t += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0, 1, t / pinStepSeconds);
                    travelPin.anchoredPosition = Vector2.Lerp(a, b, k);
                    Follow(travelPin.anchoredPosition);
                    yield return null;
                }
                travelPin.anchoredPosition = b;
                at = next;
            }
            yield return new WaitForSeconds(pinArriveHold);
            travelPin.gameObject.SetActive(false);
            traveling = false;
            Refresh();
            Close();
            done?.Invoke();
        }

        private HexTileView Tile(HexCoord c) => tiles.FirstOrDefault(t => t.Q == c.Q && t.R == c.R);

        private Vector2 Pos(HexCoord c)
        {
            var tile = Tile(c);
            return tile != null ? ((RectTransform)tile.transform).anchoredPosition : Vector2.zero;
        }

        /// <summary>핀이 창 가운데 쪽에 머물도록 지도를 따라 움직인다.</summary>
        private void Follow(Vector2 pinPos)
        {
            if (scroll == null || scroll.content == null) return;
            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.Lerp(scroll.content.anchoredPosition,
                -pinPos * scroll.content.localScale.x, 0.2f);
        }

        /// <summary>현재 위치 칸이 창 가운데 오도록 지도를 옮긴다. 범위는 ScrollRect(Clamped)가 자른다.</summary>
        public void CenterOnCurrent(bool animate = true)
        {
            if (scroll == null || scroll.content == null || expedition == null) return;
            var c = expedition.Map.Current.Coord;
            var tile = tiles.FirstOrDefault(t => t.Q == c.Q && t.R == c.R);
            if (tile == null) return;
            var target = -((RectTransform)tile.transform).anchoredPosition * scroll.content.localScale.x;
            scroll.StopMovement();
            if (following != null) StopCoroutine(following);
            if (!animate || !isActiveAndEnabled) { scroll.content.anchoredPosition = target; return; }
            following = StartCoroutine(Slide(target));
        }

        private System.Collections.IEnumerator Slide(Vector2 target)
        {
            var from = scroll.content.anchoredPosition;
            for (float t = 0; t < followSeconds; t += Time.deltaTime)
            {
                scroll.content.anchoredPosition = Vector2.Lerp(from, target, Mathf.SmoothStep(0, 1, t / followSeconds));
                yield return null;
            }
            scroll.content.anchoredPosition = target;
            following = null;
        }

        public void Refresh()
        {
            if (expedition == null) return;
            var map = expedition.Map;
            var byCoord = map.Nodes.ToDictionary(n => (n.Coord.Q, n.Coord.R));
            var shown = map.Nodes.Where(n => n.Discovered).ToList();
            if (selectedId != null && !expedition.CanTravelTo(selectedId)) selectedId = null;
            var travelable = shown.Where(n => expedition.CanTravelTo(n.Id)).Select(n => n.Id).ToHashSet();

            foreach (var tile in tiles)
            {
                byCoord.TryGetValue((tile.Q, tile.R), out var node);
                if (node == null || !node.Discovered)
                {
                    bool nearSeen = shown.Any(n => n.Coord.DistanceTo(new HexCoord(tile.Q, tile.R)) <= 1);
                    tile.ShowForest(forestColor, !nearSeen, HasTree(tile.Q, tile.R));
                    continue;
                }
                bool current = node == map.Current;
                bool movable = travelable.Contains(node.Id);
                var outline = node.Id == selectedId ? selectedOutline : movable ? movableOutline : normalOutline;
                var icon = Icon(node, out bool question);
                tile.ShowNode(node.Visited ? visitedFill : unexploredFill, outline, icon, question, current,
                    Label(node), node.Visited ? nameColor : dimNameColor, movable);
                for (int d = 0; d < 6; d++)
                {
                    var nb = Neighbor(node.Coord, d);
                    byCoord.TryGetValue((nb.Q, nb.R), out var other);
                    var edge = other != null ? map.Edge(node.Id, other.Id) : null;
                    if (edge == null || !edge.Discovered) { tile.SetRoad(d, false, null, default); continue; }
                    if (edge.Traveled) { tile.SetRoad(d, true, roadSolid, roadTraveled); continue; }
                    // 길 양쪽 칸이 같은 색을 고르도록 대칭 조건: 한쪽이 이동 가능, 다른 쪽이 현재 위치 또는 완료한 칸
                    bool open = (travelable.Contains(node.Id) && (other == map.Current || other.Completed)) ||
                                (travelable.Contains(other.Id) && (node == map.Current || node.Completed));
                    tile.SetRoad(d, true, roadDashed, open ? roadOpen : roadDim);
                }
            }
            RefreshHeader();
        }

        /// <summary>
        /// 출발점 = 야영 아이콘. 남은 이벤트가 드러난 칸(방문 또는 지각) = 첫 이벤트 아이콘, 지각으로만 안 칸은 「?」를 붙인다.
        /// 아직 모르는 칸과 다 끝낸 칸은 아이콘 없음.
        /// </summary>
        /// <summary>이벤트 종류의 아이콘 (탐험 무대의 골동품·채집물 임시 그림으로도 쓴다).</summary>
        public Sprite IconFor(string eventKind)
        {
            switch (eventKind)
            {
                case DataKeys.NodeEvent.Battle: return iconBattle;
                case DataKeys.NodeEvent.Boss: return iconBoss;
                case DataKeys.NodeEvent.Gather: return iconGather;
                case DataKeys.NodeEvent.Investigate: return iconInvestigate;
                case DataKeys.NodeEvent.Treasure: return iconTreasure;
                default: return null;
            }
        }

        /// <summary>
        /// 칸 표시 (11-13): 안 가 본 칸 = 「?」 (탐지했으면 종류만: 전투 ⚔ / 그 밖은 골동품 아이콘).
        /// 방문한 칸 = 남은 일의 아이콘(종류별). 출발점 = 야영.
        /// </summary>
        private Sprite Icon(MapNodeState node, out bool question)
        {
            question = false;
            if (node.IsStart) return iconStart;
            if (!node.Visited)
            {
                question = true;
                if (!node.Detected || !node.HasEvents) return null;
                bool battle = node.PendingEvents().Any(p => p.ev.kind == DataKeys.NodeEvent.Battle || p.ev.kind == DataKeys.NodeEvent.Boss);
                return battle ? iconBattle : iconInvestigate;
            }
            if (!node.HasEvents || node.Completed) return null;
            return IconFor(node.PendingEvents().First().ev.kind);
        }

        /// <summary>방문한 칸만 이름을 보여 준다.</summary>
        private static string Label(MapNodeState node) => node.Visited ? Expedition.NodeLabel(node) : "";

        private void RefreshHeader()
        {
            var x = expedition;
            var def = x.Map.Definition;
            string region = string.IsNullOrEmpty(def.region) ? def.name : def.region;
            var eta = x.ReturnEstimate();
            regionText.text = $"{region}   <size=75%><color=#cbbf9a>예상귀환시간 : {(eta != null ? Clock(eta, x.Time) : "—")}</color></size>";

            if (selectedId != null)
            {
                var node = x.Map.Node(selectedId);
                var (path, minutes) = x.Route(selectedId);
                var at = x.TravelArrival(selectedId);
                string name = node.Visited ? Expedition.NodeLabel(node) : "미탐색 지역";
                travelText.text = $"{name}  <size=80%><color=#cbbf9a>{path.Count}칸 · {Duration(minutes)} → {Clock(at, x.Time)} 도착</color></size>";
            }
            else if (x.HasForcedBattle) travelText.text = "<size=80%><color=#e77>전투를 끝내야 움직일 수 있다</color></size>";
            else travelText.text = "<size=80%><color=#8a8a80>금색 테두리 칸을 눌러 목적지를 고른다</color></size>";
            travelButton.interactable = selectedId != null;
            if (returnButton != null) returnButton.interactable = x.CanReturn;
            if (autoReturnButton != null) autoReturnButton.interactable = x.CanGoToExit;
        }

        /// <summary>14시 · 14시 30분 · (다음 날) 02시.</summary>
        private static string Clock(GameTime t, GameTime now)
        {
            int h = t.MinuteOfDay / 60, m = t.MinuteOfDay % 60;
            string s = m == 0 ? $"{h:00}시" : $"{h:00}시 {m:00}분";
            return t.Day > now.Day ? "다음 날 " + s : s;
        }

        private static string Duration(int minutes)
        {
            int h = minutes / 60, m = minutes % 60;
            return h == 0 ? $"{m}분" : m == 0 ? $"{h}시간" : $"{h}시간 {m}분";
        }

        private void OnTileClicked(HexTileView tile)
        {
            var node = expedition?.Map.Nodes.FirstOrDefault(n => n.Coord.Q == tile.Q && n.Coord.R == tile.R);
            if (traveling || node == null || !expedition.CanTravelTo(node.Id)) return;
            // 이미 고른 칸을 한 번 더 누르면 이동 (모바일: 탭 → 정보, 다시 탭 → 실행)
            if (selectedId == node.Id) { TravelRequested?.Invoke(node.Id); return; }
            selectedId = node.Id;
            Refresh();
        }

        private bool HasTree(int q, int r)
        {
            // 화면 장식 전용: 좌표로 고정한 값 (게임 난수를 쓰지 않는다)
            unchecked
            {
                int h = q * 73856093 ^ r * 19349663;
                h ^= h >> 13;
                return (h & 0xFFFF) / 65535f < treeChance;
            }
        }

        private static readonly (int dq, int dr)[] Directions = { (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1) };

        /// <summary>방향 d(0=오른쪽부터 반시계 60°)의 이웃 칸. 육각 칸은 꼭짓점이 위인 배치.</summary>
        public static HexCoord Neighbor(HexCoord c, int d) => new HexCoord(c.Q + Directions[d].dq, c.R + Directions[d].dr);
    }
}
