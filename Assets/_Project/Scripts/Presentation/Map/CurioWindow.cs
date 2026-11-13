using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Map
{
    /// <summary>
    /// 골동품·채집물 상호작용 창 (다키스트 던전식). 무대 위 물건(적 1번 칸 자리)을 누르면 그 오른쪽(적 2~4번 칸 자리)에 뜬다.
    /// 이름 · 상황 설명 · [조사한다/채집한다] [아이템을 사용한다] [지나간다]. 행동하면 같은 창에 결과를 보여 주고 [닫기].
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

        /// <param name="supplyLabel">아이템 칸 글자. 칸은 항상 보이고 항상 누를 수 있다 (맞는 아이템이 없으면 「아무 일도 일어나지 않았다」).</param>
        /// <param name="hasSupply">사용하지 않음 (호환용).</param>
        public void ShowEvent(string title, string body, string actLabel, string supplyLabel, bool hasSupply)
        {
            showingResult = false;
            titleText.text = title;
            bodyText.text = body;
            actButton.gameObject.SetActive(true);
            SetLabel(actButton, actLabel);
            SetLabel(closeButton, "지나간다");
            supplyButton.gameObject.SetActive(true);
            SetLabel(supplyButton, supplyLabel ?? "아이템을 사용한다");
            supplyButton.interactable = true;
            gameObject.SetActive(true);
        }

        /// <summary>버튼은 그대로 두고 설명 아래에 한 줄을 덧붙인다 (아이템을 썼지만 아무 일도 없을 때 등).</summary>
        public void ShowNote(string note)
        {
            string body = bodyText.text;
            int cut = body.IndexOf(NoteMark, StringComparison.Ordinal);
            if (cut >= 0) body = body.Substring(0, cut);
            bodyText.text = body + NoteMark + note + "</color>";
        }

        private const string NoteMark = "\n\n<color=#e9b96e>";

        public void ShowResult(string title, string body)
        {
            showingResult = true;
            titleText.text = title;
            bodyText.text = body;
            actButton.gameObject.SetActive(false);
            supplyButton.gameObject.SetActive(false);
            SetLabel(closeButton, "닫기");
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
