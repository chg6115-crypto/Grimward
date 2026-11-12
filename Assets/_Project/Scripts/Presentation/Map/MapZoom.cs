using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Grimward.Presentation.Map
{
    /// <summary>
    /// 지도 확대·축소. 마우스 휠(포인터 기준), 두 손가락 핀치(손가락 가운데 기준), +/− 버튼과 슬라이더(창 가운데 기준).
    /// 지도 내용(ScrollRect.content)의 크기만 바꾸고, 기준점이 화면에서 제자리에 남도록 위치를 보정한다.
    /// </summary>
    public sealed class MapZoom : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Slider slider;
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 1.25f;
        [SerializeField] private float startZoom = 0.8f;
        [Tooltip("+/− 버튼 한 번")]
        [SerializeField] private float buttonStep = 0.15f;
        [Tooltip("휠 한 칸")]
        [SerializeField] private float wheelStep = 0.08f;

        private RectTransform Viewport => scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        private float zoom = 1f;
        private bool syncing;
        private bool pinching;
        private float pinchStartDistance;
        private float pinchStartZoom;
        private Camera uiCamera;

        public float Zoom => zoom;

        public void Assign(ScrollRect scrollRect, Slider zoomSlider, Button zoomIn, Button zoomOut)
        {
            scroll = scrollRect; slider = zoomSlider; zoomInButton = zoomIn; zoomOutButton = zoomOut;
        }

        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (slider != null)
            {
                slider.minValue = minZoom; slider.maxValue = maxZoom;
                slider.onValueChanged.AddListener(v => { if (!syncing) SetZoom(v, Vector2.zero); });
            }
            if (zoomInButton != null) zoomInButton.onClick.AddListener(() => SetZoom(zoom + buttonStep, Vector2.zero));
            if (zoomOutButton != null) zoomOutButton.onClick.AddListener(() => SetZoom(zoom - buttonStep, Vector2.zero));
            SetZoom(startZoom, Vector2.zero);
        }

        /// <summary>
        /// 배율을 바꾼다. pivot = 고정할 점 (뷰포트 가운데 기준 로컬 좌표, 0이면 가운데).
        /// 내용의 피벗이 가운데이므로: 새 위치 = pivot − (pivot − 위치) × 새배율/옛배율.
        /// </summary>
        public void SetZoom(float value, Vector2 pivot)
        {
            if (scroll == null || scroll.content == null) return;
            float next = Mathf.Clamp(value, minZoom, maxZoom);
            float ratio = next / zoom;
            var content = scroll.content;
            content.anchoredPosition = pivot - (pivot - content.anchoredPosition) * ratio;
            content.localScale = new Vector3(next, next, 1f);
            zoom = next;
            if (slider != null) { syncing = true; slider.value = next; syncing = false; }
        }

        private void Update()
        {
            if (scroll == null) return;
            HandleWheel();
            HandlePinch();
        }

        private void HandleWheel()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            float y = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(y, 0f)) return;
            var screen = mouse.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint(Viewport, screen, uiCamera)) return;
            SetZoom(zoom + Mathf.Sign(y) * wheelStep, ToViewport(screen));
        }

        private void HandlePinch()
        {
            var touch = Touchscreen.current;
            if (touch == null) return;
            int count = 0;
            Vector2 a = default, b = default;
            foreach (var t in touch.touches)
            {
                if (!t.press.isPressed) continue;
                if (count == 0) a = t.position.ReadValue(); else if (count == 1) b = t.position.ReadValue();
                count++;
            }
            if (count < 2)
            {
                if (pinching) { pinching = false; scroll.enabled = true; }
                return;
            }
            float distance = Vector2.Distance(a, b);
            var mid = (a + b) * 0.5f;
            if (!pinching)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(Viewport, mid, uiCamera)) return;
                pinching = true;
                pinchStartDistance = Mathf.Max(1f, distance);
                pinchStartZoom = zoom;
                scroll.StopMovement();
                scroll.enabled = false; // 두 손가락 중에는 끌기 끔
                return;
            }
            SetZoom(pinchStartZoom * distance / pinchStartDistance, ToViewport(mid));
        }

        /// <summary>화면 좌표 → 뷰포트 가운데 기준 로컬 좌표.</summary>
        private Vector2 ToViewport(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, screen, uiCamera, out var local);
            return local - Viewport.rect.center;
        }
    }
}
