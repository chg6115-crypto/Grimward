using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Grimward.EditorTools
{
    /// <summary>에디터에서 Hierarchy에 uGUI를 만드는 도구. 런타임에는 쓰지 않는다.</summary>
    public static class UiBuild
    {
        public static TMP_FontAsset Font;

        public static readonly Color TextColor = new Color(0.9f, 0.88f, 0.82f);
        public static readonly Color PanelColor = new Color(0.09f, 0.085f, 0.08f, 0.97f);
        public static readonly Color ButtonColor = new Color(0.2f, 0.2f, 0.24f);
        public static readonly Color Gold = new Color(0.9f, 0.75f, 0.35f);

        public static RectTransform Canvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var safe = Rect("SafeArea", go.transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<Grimward.Presentation.Common.SafeAreaFitter>();
            return safe;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color, bool raycast = false)
        {
            var image = Rect(name, parent, min, max).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TMP_Text Text(string name, Transform parent, string value, float size, Vector2 min, Vector2 max,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Color? color = null)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.color = color ?? TextColor;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        public static Button Button(string name, Transform parent, string label, float size, Vector2 min, Vector2 max)
        {
            var image = Image(name, parent, min, max, ButtonColor, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (label != null)
            {
                string suffix = name.StartsWith("Btn_") ? name.Substring(4) : name;
                Text("Txt_" + suffix, image.transform, label, size, new Vector2(.04f, .04f), new Vector2(.96f, .96f),
                    TextAlignmentOptions.Center);
            }
            return button;
        }

        public static Image Divider(Transform parent, Vector2 min, Vector2 max) =>
            Image("Divider", parent, min, max, new Color(0.35f, 0.3f, 0.25f, 0.6f));
    }
}
