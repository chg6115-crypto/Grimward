using TMPro;
using UnityEngine;

namespace Grimward.Presentation.Common
{
    /// <summary>대상 UI 위에 뜨는 설명 상자. 높이는 글 길이에 맞춘다. 레이캐스트를 막지 않는다.</summary>
    public sealed class TooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform box;
        [SerializeField] private TMP_Text text;
        [SerializeField] private float padding = 14f;
        [SerializeField] private float gap = 8f;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(RectTransform box, TMP_Text text) { this.box = box; this.text = text; }

        public void Show(string content, RectTransform anchor)
        {
            if (anchor == null) { Hide(); return; }
            gameObject.SetActive(true);
            text.text = content;
            float width = box.rect.width - padding * 2f;
            float height = text.GetPreferredValues(content, width, 0f).y + padding * 2f;
            box.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            // 대상의 위쪽 가운데에 붙인다 (pivot 0.5, 0)
            var top = anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMax, 0f));
            box.position = top;
            box.anchoredPosition += new Vector2(0f, gap);
            KeepInsideParent();
        }

        /// <summary>화면 좌표(포인터 위치) 위쪽에 띄운다.</summary>
        public void ShowAtScreen(string content, Vector2 screenPos)
        {
            gameObject.SetActive(true);
            text.text = content;
            float width = box.rect.width - padding * 2f;
            float height = text.GetPreferredValues(content, width, 0f).y + padding * 2f;
            box.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var parent = (RectTransform)box.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, null, out var local))
            {
                // 부모 기준 좌표 → 앵커(부모 가운데) 기준
                var pivotOffset = new Vector2((0.5f - parent.pivot.x) * parent.rect.width, (0.5f - parent.pivot.y) * parent.rect.height);
                box.anchoredPosition = local - pivotOffset + new Vector2(0f, gap * 2f);
            }
            KeepInsideParent();
        }

        public void Hide() => gameObject.SetActive(false);

        private void KeepInsideParent()
        {
            var parent = box.parent as RectTransform;
            if (parent == null) return;
            var p = parent.rect;
            var pos = box.anchoredPosition;
            float halfW = box.rect.width * 0.5f;
            float minX = p.xMin + halfW, maxX = p.xMax - halfW;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Min(pos.y, p.yMax - box.rect.height);
            box.anchoredPosition = pos;
        }
    }
}
