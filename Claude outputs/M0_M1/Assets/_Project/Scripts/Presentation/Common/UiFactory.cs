using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Grimward.Presentation.Common
{
    /// <summary>코드로 uGUI를 만드는 도구. 임시 화면용 (아트·프리팹은 나중에).</summary>
    public static class UiFactory
    {
        public static readonly Color ButtonColor = new Color(0.17f, 0.22f, 0.28f);
        public static readonly Color TextColor = new Color(0.92f, 0.93f, 0.95f);

        /// <summary>Canvas(1920×1080, Match 0.5) + 안전 영역 루트 + EventSystem.</summary>
        public static RectTransform CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var safe = Rect("SafeArea", go.transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return safe;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0.9f;
            return image;
        }

        public static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value, float size,
            Vector2 min, Vector2 max, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rect = Rect(name, parent, min, max);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size; text.color = TextColor;
            text.alignment = align; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        public static Button Button(string name, Transform parent, TMP_FontAsset font, string label, float size,
            Vector2 min, Vector2 max, UnityEngine.Events.UnityAction onClick)
        {
            var rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            button.colors = colors;
            string suffix = name.StartsWith("Btn_") ? name.Substring(4) : name;
            var text = Text("Txt_" + suffix, rect, font, label, size, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f),
                TextAlignmentOptions.Center);
            text.overflowMode = TextOverflowModes.Truncate;
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public static void SetLabel(Button button, string label) =>
            button.GetComponentInChildren<TMP_Text>().text = label;

        public static void SetColor(Button button, Color color) =>
            ((Image)button.targetGraphic).color = color;
    }
}
