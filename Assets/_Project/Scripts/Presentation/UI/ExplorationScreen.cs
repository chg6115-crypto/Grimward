using Grimward.Domain.Exploration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace Grimward.Presentation.UI
{
    public sealed class ExplorationScreen : MonoBehaviour
    {
        private ExplorationState state;
        private MapView map;
        private LogView log;
        private TMP_Text time;
        private TMP_Text returnHint;
        private Button manualReturn;
        private GameObject canvasObject;
        private GameObject eventSystemObject;

        public void Initialize(ExplorationState exploration, TMP_FontAsset font)
        {
            state = exploration;
            canvasObject = new GameObject("Panel_Exploration", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)canvasObject.transform;
            UiFactory.Panel("Panel_Background", root, Vector2.zero, Vector2.one, new Color(.055f,.075f,.09f));
            UiFactory.Text("Txt_Title", root, font, "GRIMWARD · 숲 탐험", 28, new Vector2(.03f,.9f), new Vector2(.52f,.98f));
            time = UiFactory.Text("Txt_Time", root, font, "", 25, new Vector2(.56f,.9f), new Vector2(.97f,.98f));
            var mapPanel = UiFactory.Panel("Panel_Map", root, new Vector2(.02f,.2f), new Vector2(.68f,.88f), new Color(.08f,.11f,.13f));
            map = new MapView(mapPanel, state, font);
            var logPanel = UiFactory.Panel("Panel_Log", root, new Vector2(.7f,.03f), new Vector2(.98f,.88f), new Color(.09f,.12f,.15f));
            log = new LogView(logPanel, font);
            UiFactory.Text("Txt_Help", root, font, "연결된 노드를 눌러 이동 · ? 미탐색 · 금색 현재 위치", 18, new Vector2(.03f,.15f), new Vector2(.68f,.2f));
            UiFactory.Button("Btn_AutoReturn", root, font, "자동 귀환", new Vector2(.03f,.055f), new Vector2(.18f,.135f), () => state.TryAutoReturn());
            manualReturn = UiFactory.Button("Btn_ManualReturn", root, font, "마을로 귀환", new Vector2(.2f,.055f), new Vector2(.35f,.135f), () => state.TryManualReturn());
            returnHint = UiFactory.Text("Txt_ReturnStatus", root, font, "", 17, new Vector2(.37f,.04f), new Vector2(.68f,.14f));
            if (EventSystem.current == null)
                eventSystemObject = new GameObject("Panel_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            state.Logged += log.Append;
            state.Changed += Refresh;
            log.Append("숲 탐험 시작 · 전투/보물은 M1에서 미완료로 남습니다.");
            Refresh();
        }

        private void Refresh()
        {
            time.text = $"{state.Time} ({(state.IsDay ? "낮" : "밤")})";
            map.Refresh();
            manualReturn.gameObject.SetActive(state.IsAtStart);
            var path = state.FindReturnPath();
            returnHint.text = path == null ? "귀환 경로 없음\n완료 노드·통과한 길 필요" :
                $"귀환 경로: {string.Join(" → ", path)}";
        }

        private void OnDestroy()
        {
            if (state != null) { state.Logged -= log.Append; state.Changed -= Refresh; }
            if (canvasObject != null) Destroy(canvasObject);
            if (eventSystemObject != null) Destroy(eventSystemObject);
        }
    }
}
