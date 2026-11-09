using System.Collections.Generic;
using Grimward.Domain.Exploration;
using Grimward.Domain.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Grimward.Presentation.UI
{
    public sealed class MapView
    {
        private readonly ExplorationState state;
        private readonly Dictionary<int, Button> buttons = new Dictionary<int, Button>();
        private readonly Dictionary<int, Image> lines = new Dictionary<int, Image>();
        public MapView(RectTransform parent, ExplorationState state, TMP_FontAsset font)
        {
            this.state = state;
            foreach (var edge in state.Map.Edges)
            {
                Vector2 from = Position(state.Map.Node(edge.FromNodeId).Coord);
                Vector2 to = Position(state.Map.Node(edge.ToNodeId).Coord);
                var rect = UiFactory.Rect($"Panel_Edge_{edge.Id:00}", parent, new Vector2(.5f,.5f), new Vector2(.5f,.5f));
                rect.anchoredPosition = (from + to) / 2;
                rect.sizeDelta = new Vector2(Vector2.Distance(from, to), 4);
                rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                var line = rect.gameObject.AddComponent<Image>();
                line.raycastTarget = false; lines.Add(edge.Id, line);
            }
            foreach (var node in state.Map.Nodes)
            {
                int id = node.Id;
                var button = UiFactory.Button($"Btn_Node_{id:00}", parent, font, "", new Vector2(.5f,.5f),
                    new Vector2(.5f,.5f), () => state.TryMove(id));
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = Position(node.Coord);
                rect.sizeDelta = new Vector2(112, 72);
                buttons.Add(id, button);
            }
        }
        private static Vector2 Position(HexCoord coord) =>
            new Vector2((coord.Q + coord.R * .5f) * 165, -coord.R * 100);

        public void Refresh()
        {
            foreach (var edge in state.Map.Edges)
            {
                lines[edge.Id].gameObject.SetActive(edge.IsDiscovered);
                lines[edge.Id].color = edge.IsTraversed ? new Color(.4f,.75f,.65f) : new Color(.3f,.36f,.42f);
            }
            foreach (var node in state.Map.Nodes)
            {
                var button = buttons[node.Id];
                button.gameObject.SetActive(node.IsDiscovered);
                button.interactable = state.CanMoveTo(node.Id);
                bool current = node.Id == state.CurrentNodeId;
                button.GetComponent<Image>().color = current ? new Color(.6f,.43f,.12f) :
                    node.IsEventCompleted ? new Color(.18f,.4f,.33f) : new Color(.2f,.28f,.36f);
                string kind = !node.IsVisited ? "?" : EventLabel(node.EventType);
                string status = !node.IsVisited ? "" : node.IsEventCompleted ? " · 완료" : " · 미완료";
                button.GetComponentInChildren<TMP_Text>().text = $"{(current ? "▶ " : "")}{node.Id:00} {kind}\n{status}";
            }
        }
        private static string EventLabel(NodeEventType type)
        {
            switch (type)
            {
                case NodeEventType.Start: return "시작";
                case NodeEventType.Battle: return "전투";
                case NodeEventType.Treasure: return "보물";
                default: return "빈 곳";
            }
        }
    }
}
