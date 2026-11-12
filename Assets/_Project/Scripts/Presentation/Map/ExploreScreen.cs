using System;
using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Data;
using Grimward.Domain.Map;
using Grimward.Domain.Units;
using Grimward.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Map
{
    /// <summary>
    /// 탐험 기본 화면 (uGUI). 지도 오버레이 뒤에 깔린다. 현재 노드 정보 · 조사/채집 · 귀환 · 「지도」 버튼 · 파티 · 가방 · 기록.
    /// M2 2단계에서 가운데 무대(캐릭터 걷기·전투·골동품)로 바뀐다. 지금은 가운데에 현재 장소 이름만 크게 보여 준다.
    /// </summary>
    public sealed class ExploreScreen : MonoBehaviour
    {
        [SerializeField] private ClockView clock;
        [Tooltip("가운데 무대 자리: 현재 장소 이름")]
        [SerializeField] private TMP_Text stageText;

        [Header("오른쪽 패널")]
        [SerializeField] private TMP_Text nodeNameText;
        [SerializeField] private TMP_Text nodeInfoText;
        [SerializeField] private Button mapButton;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button eventButton;
        [SerializeField] private Button supplyButton;
        [SerializeField] private TMP_Text partyText;
        [SerializeField] private TMP_Text bagText;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private int logLines = 8;

        [Header("결과")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button resultButton;

        private Expedition expedition;
        private GameDatabase db;
        private string message;

        public event Action MapRequested;
        public event Action<int, bool> EventRequested;
        public event Action ReturnRequested;
        public event Action RestartRequested;

        public void Assign(ClockView clockView, TMP_Text stage, TMP_Text nodeName, TMP_Text nodeInfo, Button map, Button ret,
            Button ev, Button supply, TMP_Text party, TMP_Text bag, TMP_Text log, GameObject result, TMP_Text resultLabel,
            Button resultBtn)
        {
            clock = clockView; stageText = stage; nodeNameText = nodeName; nodeInfoText = nodeInfo;
            mapButton = map; returnButton = ret; eventButton = ev; supplyButton = supply;
            partyText = party; bagText = bag; logText = log;
            resultPanel = result; resultText = resultLabel; resultButton = resultBtn;
        }

        private void Awake()
        {
            mapButton.onClick.AddListener(() => MapRequested?.Invoke());
            returnButton.onClick.AddListener(() => ReturnRequested?.Invoke());
            eventButton.onClick.AddListener(() => RequestEvent(false));
            supplyButton.onClick.AddListener(() => RequestEvent(true));
            resultButton.onClick.AddListener(() => RestartRequested?.Invoke());
        }

        public void Show(Expedition x, GameDatabase database)
        {
            expedition = x; db = database;
            message = null;
            resultPanel.SetActive(false);
            if (clock != null) clock.Set(x.Time, db.Balance, false);
            Refresh();
        }

        /// <summary>안내 문구 한 줄 (조사 결과 등). 다음 문구까지 노드 정보 위에 표시.</summary>
        public void SetMessage(string text) { message = text; Refresh(); }

        public void ShowResult(string text)
        {
            resultText.text = text;
            resultPanel.SetActive(true);
        }

        public void Refresh(bool animateClock = true)
        {
            if (expedition == null) return;
            var x = expedition;
            var node = x.Map.Current;
            bool exploring = x.Phase == ExpeditionPhase.Exploring;
            if (clock != null) clock.Set(x.Time, db.Balance, animateClock);
            if (stageText != null) stageText.text = Expedition.NodeLabel(node);

            nodeNameText.text = Expedition.NodeLabel(node);
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(message)) lines.Add($"<color=#eb4>{message}</color>");
            if (node.IsStart) lines.Add("출발점 · 여기서 마을로 귀환할 수 있다");
            else if (node.HasEvents)
            {
                lines.Add(x.DescribeEvents(node) + (node.Completed ? " <color=#8a8>(완료)</color>" : ""));
                foreach (var p in node.PendingEvents().Where(p => p.ev.kind == DataKeys.NodeEvent.Investigate))
                    lines.Add($"<size=85%><color=#aaa>{db.GetInvestigation(p.ev.objectId).situation}</color></size>");
            }
            else lines.Add("<color=#aaa>아무것도 없다</color>");
            nodeInfoText.text = string.Join("\n", lines);

            mapButton.interactable = exploring && !x.HasForcedBattle;
            returnButton.interactable = x.CanReturn;

            var (_, ev) = x.NextEvent();
            bool optional = exploring && ev != null && !x.HasForcedBattle &&
                            (ev.kind == DataKeys.NodeEvent.Gather || ev.kind == DataKeys.NodeEvent.Investigate);
            eventButton.gameObject.SetActive(optional);
            supplyButton.gameObject.SetActive(false);
            if (optional)
            {
                SetLabel(eventButton, ev.kind == DataKeys.NodeEvent.Gather ? x.DescribeEvent(ev) : "조사한다");
                if (ev.kind == DataKeys.NodeEvent.Investigate)
                {
                    var obj = db.GetInvestigation(ev.objectId);
                    if (!string.IsNullOrEmpty(obj.supplyItem) && x.Bag.Count(obj.supplyItem) > 0)
                    {
                        supplyButton.gameObject.SetActive(true);
                        SetLabel(supplyButton, $"{db.GetItem(obj.supplyItem).name} 쓰고 조사");
                    }
                }
            }

            partyText.text = string.Join("\n", x.Party.Select(a => a.IsDead
                ? $"<color=#777>{a.Name} — 사망</color>"
                : $"{a.Name}  HP {a.CurrentHp}/{StatFormulas.MaxHp(db.Balance, a)}" +
                  (a.Injuries.Count > 0 ? $"  <color=#e77>{string.Join("·", a.Injuries.Select(i => db.InjuryStageName(i.Id, i.Severe)))}</color>" : "") +
                  (a.KnockdownsThisExpedition > 0 ? "  <color=#e77>(전투불능 1회)</color>" : "")));
            bagText.text = "가방: " + string.Join(" · ", x.Bag.Entries.Where(e => e.Value > 0)
                .Select(e => $"{db.GetItem(e.Key).name} {e.Value}"));
            logText.text = string.Join("\n", x.Log.Skip(Math.Max(0, x.Log.Count - logLines)));
        }

        private void RequestEvent(bool useSupply)
        {
            var (index, ev) = expedition.NextEvent();
            if (ev != null) EventRequested?.Invoke(index, useSupply);
        }

        private static void SetLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = text;
        }
    }
}
