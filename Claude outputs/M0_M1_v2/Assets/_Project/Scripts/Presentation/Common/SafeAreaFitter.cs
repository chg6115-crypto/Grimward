using UnityEngine;

namespace Grimward.Presentation.Common
{
    /// <summary>노치·펀치홀을 피하도록 RectTransform을 Screen.safeArea에 맞춘다.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect applied;
        private Vector2Int screenSize;

        private void Awake() => Apply();

        private void Update()
        {
            if (applied != Screen.safeArea || screenSize.x != Screen.width || screenSize.y != Screen.height) Apply();
        }

        private void Apply()
        {
            var rect = (RectTransform)transform;
            applied = Screen.safeArea;
            screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rect.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rect.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
