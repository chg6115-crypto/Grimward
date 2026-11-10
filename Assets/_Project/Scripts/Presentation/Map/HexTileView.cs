using System;
using System.Collections.Generic;
using Grimward.Domain.Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Grimward.Presentation.Map
{
    /// <summary>
    /// HexTile 프리팹 인스턴스 한 칸의 표시와 클릭을 담당한다.
    /// 프리팹 자식 구조: Ground / PathRoot / DecorationRoot / EventMarker / Fog
    /// </summary>
    [RequireComponent(typeof(SortingGroup))]
    public sealed class HexTileView : MonoBehaviour, IPointerClickHandler
    {
        // 프리팹 SortingGroup 내부 그리기 순서 (HexTileSetup과 같은 값)
        private const int OrderPath = 10;
        private const int OrderDecoration = 20;
        private const int OrderLabel = 35;

        private readonly Dictionary<int, SpriteRenderer> roads = new Dictionary<int, SpriteRenderer>();
        private readonly List<SpriteRenderer> decorations = new List<SpriteRenderer>();
        private SpriteRenderer ground;
        private SpriteRenderer marker;
        private GameObject fog;
        private Transform pathRoot;
        private Transform decorationRoot;
        private TextMeshPro label;
        private Action<int> clicked;

        public HexCoord Coord { get; private set; }
        public int NodeId { get; private set; } = -1;
        public bool IsNode => NodeId >= 0;
        public IReadOnlyDictionary<int, SpriteRenderer> Roads => roads;

        public void Initialize(HexCoord coord, int nodeId, float hexRadius, Action<int> onClicked)
        {
            Coord = coord;
            NodeId = nodeId;
            clicked = onClicked;

            ground = transform.Find("Ground").GetComponent<SpriteRenderer>();
            marker = transform.Find("EventMarker").GetComponent<SpriteRenderer>();
            fog = transform.Find("Fog").gameObject;
            pathRoot = transform.Find("PathRoot");
            decorationRoot = transform.Find("DecorationRoot");

            if (IsNode)
            {
                // 클릭 판정용 육각형 콜라이더 (pointy-top)
                var collider = gameObject.AddComponent<PolygonCollider2D>();
                var points = new Vector2[6];
                for (int i = 0; i < 6; i++)
                {
                    float angle = Mathf.Deg2Rad * (90f + 60f * i);
                    points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * hexRadius * 0.95f;
                }
                collider.points = points;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsNode) clicked?.Invoke(NodeId);
        }

        public void SetSortingOrder(int order) => GetComponent<SortingGroup>().sortingOrder = order;

        public void SetGroundTint(Color color) => ground.color = color;

        public void SetMarker(Sprite sprite)
        {
            marker.gameObject.SetActive(sprite != null);
            marker.sprite = sprite;
        }

        public void SetFog(bool covered)
        {
            fog.SetActive(covered);
            // 나무가 육각형 밖으로 삐져나오므로 안개 아래에서는 숨긴다.
            decorationRoot.gameObject.SetActive(!covered && decorations.Count > 0);
        }

        public void SetDecorationsHidden(bool hidden)
        {
            if (hidden) decorationRoot.gameObject.SetActive(false);
        }

        public void AddRoad(int edgeId, Sprite sprite, float angle)
        {
            var road = CreateSprite($"Road_{edgeId:00}", pathRoot, sprite, OrderPath);
            road.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            roads[edgeId] = road;
        }

        public void SetRoad(int edgeId, bool visible, Color tint)
        {
            if (!roads.TryGetValue(edgeId, out var road)) return;
            road.gameObject.SetActive(visible);
            road.color = tint;
        }

        public void AddDecoration(Sprite sprite, Vector2 localPosition)
        {
            // 같은 칸 안에서는 아래쪽(y가 작은) 장식이 앞에 오도록
            int order = OrderDecoration + Mathf.RoundToInt((0.5f - localPosition.y) * 10f);
            var deco = CreateSprite($"Deco_{decorations.Count}", decorationRoot, sprite, order);
            deco.transform.localPosition = localPosition;
            decorations.Add(deco);
        }

        public void SetLabel(TMP_FontAsset font, string text)
        {
            if (label == null)
            {
                var go = new GameObject("Txt_NodeId");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                label = go.AddComponent<TextMeshPro>();
                label.font = font;
                label.fontSize = 1.6f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(1f, 1f, 1f, 0.85f);
                label.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
                label.GetComponent<MeshRenderer>().sortingOrder = OrderLabel;
            }
            label.text = text;
        }

        public void SetLabelVisible(bool visible)
        {
            if (label != null) label.gameObject.SetActive(visible);
        }

        private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
