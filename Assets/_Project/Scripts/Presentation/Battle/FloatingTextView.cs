using System.Collections;
using TMPro;
using UnityEngine;

namespace Grimward.Presentation.Battle
{
    /// <summary>떠오르는 글자 하나 (피해·회피·회복 등). FloatingTextLayer가 Hierarchy에 미리 만든 것을 돌려 쓴다.</summary>
    public sealed class FloatingTextView : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(TMP_Text label) => text = label;

        public bool IsPlaying => gameObject.activeSelf;

        public void Play(string value, Color color, float scale, Vector3 worldPos, float yOffset, float duration, float rise)
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(Run(value, color, scale, worldPos, yOffset, duration, rise));
        }

        private IEnumerator Run(string value, Color color, float scale, Vector3 worldPos, float yOffset, float duration, float rise)
        {
            var rect = (RectTransform)transform;
            rect.position = worldPos;
            rect.anchoredPosition += new Vector2(0f, yOffset);
            var start = rect.anchoredPosition;
            text.text = value;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                rect.anchoredPosition = start + new Vector2(0f, rise * (1f - (1f - k) * (1f - k)));   // 처음엔 빠르게, 끝엔 느리게
                float pop = k < 0.12f ? Mathf.Lerp(1.45f, 1f, k / 0.12f) : 1f;                          // 처음에 톡 튀어나옴
                rect.localScale = Vector3.one * scale * pop;
                var c = color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                text.color = c;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
