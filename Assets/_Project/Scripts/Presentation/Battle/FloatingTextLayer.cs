using UnityEngine;

namespace Grimward.Presentation.Battle
{
    /// <summary>떠오르는 글자 묶음. Hierarchy에 미리 만든 FloatingTextView들을 돌려 쓴다 (런타임 생성 없음).</summary>
    public sealed class FloatingTextLayer : MonoBehaviour
    {
        [SerializeField] private FloatingTextView[] items;
        [SerializeField] private float duration = 1.0f;
        [SerializeField] private float rise = 70f;
        private int next;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(FloatingTextView[] views) => items = views;

        /// <summary>anchor(보통 칸의 몸통) 위쪽에 글자를 띄운다. stack은 같은 칸에 겹칠 때 위로 쌓는 순번.</summary>
        public void Show(string value, Color color, RectTransform anchor, float scale = 1f, int stack = 0, float speed = 1f)
        {
            if (items == null || items.Length == 0 || anchor == null) return;
            FloatingTextView view = null;
            for (int i = 0; i < items.Length; i++)
            {
                var candidate = items[(next + i) % items.Length];
                if (candidate.IsPlaying) continue;
                view = candidate;
                next = (next + i + 1) % items.Length;
                break;
            }
            if (view == null) { view = items[next]; next = (next + 1) % items.Length; }   // 다 쓰는 중이면 가장 오래된 것부터

            var r = anchor.rect;
            var pos = anchor.TransformPoint(new Vector3(r.center.x, r.yMin + r.height * 0.7f, 0f));
            view.Play(value, color, scale, pos, stack * 34f, duration / Mathf.Max(0.1f, speed), rise);
        }
    }
}
