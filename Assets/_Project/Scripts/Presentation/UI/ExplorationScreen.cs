using Grimward.Domain.Exploration;
using Grimward.Presentation.Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace Grimward.Presentation.UI
{
    public sealed class ExplorationScreen : MonoBehaviour
    {
        // 지도가 보이는 화면 영역 (뷰포트 0~1). 상단 바·하단 버튼·오른쪽 로그와 겹치지 않는 자리.
        private static readonly Vector2 MapAreaMin = new Vector2(.02f, .2f);
        private static readonly Vector2 MapAreaMax = new Vector2(.68f, .88f);

        private ExplorationState state;
        private HexMapView map;
        private LogView log;
        private BattleView battleView;
        private TMP_Text time;
        private TMP_Text hp;
        private TMP_Text returnHint;
        private Button autoReturn;
        private Button manualReturn;
        private GameObject deathPanel;
        private GameObject canvasObject;
        private GameObject eventSystemObject;

        public void Initialize(ExplorationState exploration, TMP_FontAsset font, HexMapTheme mapTheme, Camera worldCamera)
        {
            state = exploration;
            canvasObject = new GameObject("Panel_Exploration", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720);
            scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)canvasObject.transform;

            // 지도는 카메라가 그리는 월드 타일이므로 화면 전체 배경은 두지 않고, 글자 영역에만 반투명 바를 깐다.
            UiFactory.Panel("Panel_TopBar", root, new Vector2(0f,.885f), new Vector2(1f,1f), new Color(.03f,.045f,.055f,.85f));
            UiFactory.Panel("Panel_BottomBar", root, new Vector2(0f,0f), new Vector2(.69f,.2f), new Color(.03f,.045f,.055f,.85f));
            UiFactory.Text("Txt_Title", root, font, "GRIMWARD · 숲 탐험", 27, new Vector2(.03f,.9f), new Vector2(.4f,.98f));
            hp = UiFactory.Text("Txt_Hp", root, font, "", 23, new Vector2(.4f,.9f), new Vector2(.62f,.98f));
            time = UiFactory.Text("Txt_Time", root, font, "", 24, new Vector2(.65f,.9f), new Vector2(.98f,.98f));

            // 지도 영역 표시용 빈 사각형 (그림 없음, 클릭을 막지 않음)
            UiFactory.Rect("Panel_Map", root, MapAreaMin, MapAreaMax);
            map = new GameObject("HexMap").AddComponent<HexMapView>();
            map.Initialize(state, mapTheme, worldCamera, font,
                new Rect(MapAreaMin, MapAreaMax - MapAreaMin));

            var logPanel = UiFactory.Panel("Panel_Log", root, new Vector2(.7f,.03f), new Vector2(.98f,.88f), new Color(.09f,.12f,.15f,.95f));
            log = new LogView(logPanel, font);
            UiFactory.Text("Txt_Help", root, font, "길로 이어진 칸을 눌러 이동 · ? 미탐색 · 밝은 칸 현재 위치", 18, new Vector2(.03f,.15f), new Vector2(.68f,.2f));
            autoReturn = UiFactory.Button("Btn_AutoReturn", root, font, "자동 귀환", new Vector2(.03f,.055f), new Vector2(.18f,.135f), () => state.TryAutoReturn());
            manualReturn = UiFactory.Button("Btn_ManualReturn", root, font, "마을로 귀환", new Vector2(.2f,.055f), new Vector2(.35f,.135f), () => state.TryManualReturn());
            returnHint = UiFactory.Text("Txt_ReturnStatus", root, font, "", 17, new Vector2(.37f,.04f), new Vector2(.68f,.14f));
            battleView = new BattleView(root, state, font);
            var death = UiFactory.Panel("Panel_Death", root, new Vector2(.15f,.32f), new Vector2(.85f,.72f), new Color(.2f,.07f,.09f));
            death.GetComponent<Image>().raycastTarget = true;
            UiFactory.Text("Txt_Death", death, font, "사망 — M4에서 처리\n숲에서의 탐험이 끝났습니다.", 30, new Vector2(.08f,.1f), new Vector2(.92f,.9f));
            deathPanel = death.gameObject;
            if (EventSystem.current == null)
                eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            state.Logged += log.Append;
            state.Changed += Refresh;
            log.Append("숲 탐험 시작 · 전투 중 HP는 다음 전투에도 유지됩니다.");
            Refresh();
        }
        private void Refresh()
        {
            time.text = $"{state.Time} ({(state.IsDay ? "낮" : "밤")})";
            hp.text = $"검사 HP {state.Player.Fighter.Hp}/{state.Player.Fighter.MaxHp}";
            map.Refresh();
            autoReturn.interactable = state.CanExplore;
            manualReturn.gameObject.SetActive(state.IsAtStart);
            manualReturn.interactable = state.CanExplore;
            var path = state.FindReturnPath();
            returnHint.text = state.IsDead ? "사망" : state.Battle != null ?
                (state.IsReturning ? "매복 전투 중\n승리 후 자동 귀환 재개" : "전투 중") :
                path == null ? "귀환 경로 없음\n완료 노드·통과한 길 필요" :
                $"귀환 경로: {string.Join(" → ", path)}";
            battleView.Refresh();
            deathPanel.SetActive(state.IsDead);
        }
        private void OnDestroy()
        {
            if (state != null)
            {
                state.Logged -= log.Append;
                state.Changed -= Refresh;
            }
            if (canvasObject != null) Destroy(canvasObject);
            if (map != null) Destroy(map.gameObject);
            if (eventSystemObject != null) Destroy(eventSystemObject);
        }
    }
}
