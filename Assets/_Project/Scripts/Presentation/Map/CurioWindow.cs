using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Map
{
    /// <summary>
    /// 골동품·채집물 상호작용 창 (다키스트 던전식). 무대 위 물건을 누르면 그 옆에 뜬다.
    /// 이름 · 상황 설명 · [조사한다/채집한다] [보급품] [X]. 행동하면 같은 창에 결과를 보여 주고, X로 닫는다.
    /// 규칙(결과 판정·보상)은 Domain(Expedition)에 있다.
    /// </summary>
    public sealed class CurioWindow : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("조사한다 / 채집한다")]
        [SerializeField] private Button actButton;
        [Tooltip("맞는 보급품을 쓰면 결과 좋음 확정. 가지고 있을 때만 누를 수 있다")]
        [SerializeField] private Button supplyButton;
        [Tooltip("지나간다 (결과를 본 뒤에는 닫기)")]
        [SerializeField] private Button closeButton;

        /// <summary>행동 요청. true = 보급품을 쓴다.</summary>
        public event Action<bool> ActRequested;
        /// <summary>창을 닫음. true = 결과를 본 뒤 닫음, false = 아무것도 하지 않고 지나감.</summary>
        public event Action<bool> Closed;

        private bool showingResult;

        public bool IsOpen => gameObject.activeSelf;

        public void Assign(TMP_Text title, TMP_Text body, Button act, Button supply, Button close)
        {
            titleText = title; bodyText = body; actButton = act; supplyButton = supply; closeButton = close;
        }

        private void Awake()
        {
            actButton.onClick.AddListener(() => ActRequested?.Invoke(false));
            supplyButton.onClick.AddListener(() => ActRequested?.Invoke(true));
            closeButton.onClick.AddListener(Close);
        }

        /// <param name="supplyLabel">보급품 칸 글자 (예: 「열쇠 사용」). null이면 보급품 칸을 숨긴다.</param>
        /// <param name="hasSupply">보급품을 가지고 있으면 true (칸 활성화).</param>
        public void ShowEvent(string title, string body, string actLabel, string supplyLabel, bool hasSupply)
        {
            showingResult = false;
            titleText.text = title;
            bodyText.text = body;
            actButton.gameObject.SetActive(true);
            SetLabel(actButton, actLabel);
            supplyButton.gameObject.SetActive(supplyLabel != null);
            if (supplyLabel != null)
            {
                SetLabel(supplyButton, supplyLabel);
                supplyButton.interactable = hasSupply;
            }
            gameObject.SetActive(true);
        }

        public void ShowResult(string title, string body)
        {
            showingResult = true;
            titleText.text = title;
            bodyText.text = body;
            actButton.gameObject.SetActive(false);
            supplyButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            Closed?.Invoke(showingResult);
        }

        /// <summary>이벤트 없이 숨긴다 (이동·전투 시작 등).</summary>
        public void Hide() => gameObject.SetActive(false);

        private static void SetLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = text;
        }
    }
}
