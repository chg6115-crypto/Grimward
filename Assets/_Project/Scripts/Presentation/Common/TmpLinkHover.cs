using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Grimward.Presentation.Common
{
    /// <summary>TextMeshPro의 &lt;link="id"&gt; 위에 포인터가 올라가면 알린다 (Screen Space Overlay 캔버스 기준).</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TmpLinkHover : MonoBehaviour
    {
        private TMP_Text text;
        private string current;

        /// <summary>링크 ID와 포인터 화면 좌표.</summary>
        public event Action<string, Vector2> Entered;
        public event Action Exited;

        private void Awake() => text = GetComponent<TMP_Text>();

        private void Update()
        {
            var pointer = Pointer.current;
            string id = null;
            Vector2 pos = default;
            if (pointer != null)
            {
                pos = pointer.position.ReadValue();
                int index = TMP_TextUtilities.FindIntersectingLink(text, pos, null);
                if (index >= 0 && index < text.textInfo.linkCount) id = text.textInfo.linkInfo[index].GetLinkID();
            }
            if (id == current) return;
            current = id;
            if (id == null) Exited?.Invoke();
            else Entered?.Invoke(id, pos);
        }

        private void OnDisable()
        {
            if (current == null) return;
            current = null;
            Exited?.Invoke();
        }
    }
}
