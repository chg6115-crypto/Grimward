using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Grimward.Presentation.UI
{
    internal static class UiFactory
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return rect;
        }
        public static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return rect;
        }
        public static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value,
            float size, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent, min, max);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size;
            text.color = new Color(0.9f, 0.93f, 0.95f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        public static Button Button(string name, Transform parent, TMP_FontAsset font, string value,
            Vector2 min, Vector2 max, UnityEngine.Events.UnityAction clicked)
        {
            var rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.17f, 0.24f, 0.3f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.8f);
            button.colors = colors;
            var label = Text("Txt_" + name.Substring(4), rect, font, value, 19,
                new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f));
            label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(clicked);
            return button;
        }
    }
}
