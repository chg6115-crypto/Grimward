using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Grimward.Presentation.Common
{
    /// <summary>마우스(포인터)가 올라가고 나갈 때 알린다. 비활성 버튼에서도 동작.</summary>
    public sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action Entered;
        public event Action Exited;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();
        private void OnDisable() => Exited?.Invoke();
    }
}
