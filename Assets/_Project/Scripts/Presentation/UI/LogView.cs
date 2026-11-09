using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Grimward.Presentation.UI
{
    public sealed class LogView
    {
        private readonly TMP_Text text;
        private readonly ScrollRect scroll;
        public LogView(RectTransform parent, TMP_FontAsset font, string title = "탐험 기록", string prefix = "")
        {
            UiFactory.Text("Txt_" + prefix + "LogTitle", parent, font, title, 23, new Vector2(.05f,.91f), new Vector2(.95f,.99f));
            var view = UiFactory.Rect("Panel_" + prefix + "LogViewport", parent, new Vector2(.04f,.03f), new Vector2(.96f,.9f));
            view.gameObject.AddComponent<RectMask2D>();
            var image = view.gameObject.AddComponent<Image>(); image.color = new Color(0,0,0,.01f);
            scroll = view.gameObject.AddComponent<ScrollRect>();
            var content = UiFactory.Rect("Panel_" + prefix + "LogContent", view, new Vector2(0,1), Vector2.one);
            content.pivot = new Vector2(.5f,1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            text = UiFactory.Text("Txt_" + prefix + "Log", content, font, "", 19, Vector2.zero, Vector2.one);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.overflowMode = TextOverflowModes.Overflow;
            text.textWrappingMode = TextWrappingModes.Normal;
            scroll.viewport = view; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25;
        }
        public void Clear() { text.text = ""; }
        public void Append(string message)
        {
            text.text += (text.text.Length == 0 ? "" : "\n\n") + message;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0;
        }
    }
}
