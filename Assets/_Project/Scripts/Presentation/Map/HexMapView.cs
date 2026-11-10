using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Exploration;
using Grimward.Domain.Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Grimward.Presentation.Map
{
    /// <summary>
    /// 탐험 맵을 HexTile 프리팹으로 그리는 월드 지도.
    /// 게임 규칙은 ExplorationState에 있고, 여기서는 상태를 읽어 표시만 하고 클릭을 이동 요청으로 넘긴다.
    /// </summary>
    public sealed class HexMapView : MonoBehaviour
    {
        private readonly Dictionary<HexCoord, HexTileView> tiles = new Dictionary<HexCoord, HexTileView>();
        private readonly Dictionary<int, HexTileView> nodeTiles = new Dictionary<int, HexTileView>();
        private ExplorationState state;
        private HexMapTheme theme;
        private Camera worldCamera;
        private Rect viewportArea;
        private int lastScreenWidth;
        private int lastScreenHeight;

        /// <param name="mapViewport">화면에서 지도가 차지할 영역 (0~1 뷰포트 좌표). UI 패널과 겹치지 않게 카메라를 맞춘다.</param>
        public void Initialize(ExplorationState exploration, HexMapTheme mapTheme, Camera camera,
            TMP_FontAsset font, Rect mapViewport)
        {
            state = exploration;
            theme = mapTheme;
            worldCamera = camera;
            viewportArea = mapViewport;

            SetupCamera();
            BuildTiles(font);
            BuildRoads();
            FitCamera();
        }

        private void SetupCamera()
        {
            worldCamera.orthographic = true;
            worldCamera.cullingMask = ~0;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = theme.cameraBackground;
            if (worldCamera.GetComponent<Physics2DRaycaster>() == null)
                worldCamera.gameObject.AddComponent<Physics2DRaycaster>();
        }

        private void BuildTiles(TMP_FontAsset font)
        {
            var nodeByCoord = state.Map.Nodes.ToDictionary(n => n.Coord);
            int radius = state.Map.Radius + theme.borderRings;

            foreach (var coord in HexCoord.WithinRadius(radius))
            {
                // 지도 타일은 출정 시작 시 한 번만 만든다 (프레임마다 생성·삭제하지 않음).
                var instance = Instantiate(theme.tilePrefab, transform);
                instance.transform.localPosition = AxialToWorld(coord);
                bool isNode = nodeByCoord.TryGetValue(coord, out var node);
                instance.name = isNode ? $"Btn_Node_{node.Id:00}" : $"Hex_{coord.Q}_{coord.R}";

                var view = instance.AddComponent<HexTileView>();
                view.Initialize(coord, isNode ? node.Id : -1, theme.hexRadius, OnTileClicked);
                // 화면 아래쪽 줄(R이 큰 줄)이 앞에 그려지도록
                view.SetSortingOrder(coord.R);
                AddDecorations(view, coord);

                if (isNode)
                {
                    nodeTiles[node.Id] = view;
                    if (theme.showNodeIds) view.SetLabel(font, node.Id.ToString("00"));
                }
                tiles[coord] = view;
            }
        }

        private void BuildRoads()
        {
            foreach (var edge in state.Map.Edges)
            {
                var from = state.Map.Node(edge.FromNodeId).Coord;
                var to = state.Map.Node(edge.ToNodeId).Coord;
                Vector2 dir = AxialToWorld(to) - AxialToWorld(from);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                nodeTiles[edge.FromNodeId].AddRoad(edge.Id, theme.roadSegment, angle);
                nodeTiles[edge.ToNodeId].AddRoad(edge.Id, theme.roadSegment, angle + 180f);
            }
        }

        private void AddDecorations(HexTileView view, HexCoord coord)
        {
            if (theme.decorations == null || theme.decorations.Length == 0) return;
            // 화면 장식 전용 난수. 게임 난수(IRandomSource)를 소비하지 않도록 좌표로 고정한다.
            var random = new System.Random(unchecked(coord.Q * 73856093 ^ coord.R * 19349663));
            int count = random.NextDouble() < theme.decorationChance ? 1 + random.Next(2) : 0;
            for (int i = 0; i < count; i++)
            {
                var sprite = theme.decorations[random.Next(theme.decorations.Length)];
                var offset = new Vector2((float)(random.NextDouble() - 0.5) * 0.4f,
                    (float)(random.NextDouble() - 0.5) * 0.3f - 0.05f);
                view.AddDecoration(sprite, offset);
            }
        }

        public void Refresh()
        {
            var visitedCoords = state.Map.Nodes.Where(n => n.IsVisited).Select(n => n.Coord).ToList();

            foreach (var view in tiles.Values)
            {
                bool nearVisited = visitedCoords.Any(c => c.DistanceTo(view.Coord) <= 1);

                if (view.IsNode && state.Map.Node(view.NodeId).IsDiscovered)
                {
                    var node = state.Map.Node(view.NodeId);
                    view.SetFog(false);
                    view.SetDecorationsHidden(true);
                    view.SetMarker(MarkerFor(node));
                    view.SetLabelVisible(theme.showNodeIds);
                    bool current = node.Id == state.CurrentNodeId;
                    view.SetGroundTint(current ? theme.currentTint :
                        state.CanMoveTo(node.Id) ? theme.movableTint : theme.idleTint);
                }
                else
                {
                    // 발견 전 노드는 숲으로 위장해 지도 구조가 드러나지 않게 한다.
                    view.SetFog(!nearVisited);
                    view.SetMarker(null);
                    view.SetLabelVisible(false);
                    view.SetGroundTint(theme.forestTint);
                }

                foreach (var road in view.Roads)
                {
                    var edge = state.Map.Edge(road.Key);
                    bool visible = edge.IsDiscovered && view.IsNode && state.Map.Node(view.NodeId).IsDiscovered;
                    view.SetRoad(road.Key, visible, edge.IsTraversed ? theme.traversedRoadTint : theme.untraversedRoadTint);
                }
            }
        }

        private Sprite MarkerFor(NodeData node)
        {
            if (!node.IsVisited) return theme.markerUnknown;
            switch (node.EventType)
            {
                case NodeEventType.Start: return theme.markerStart;
                case NodeEventType.Battle: return node.IsEventCompleted ? null : theme.markerBattle;
                case NodeEventType.Treasure: return node.IsEventCompleted ? null : theme.markerTreasure;
                default: return null;
            }
        }

        private void OnTileClicked(int nodeId) => state.TryMove(nodeId);

        private void LateUpdate()
        {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) FitCamera();
        }

        /// <summary>활성 맵 전체가 지정한 뷰포트 영역 안에 들어오도록 카메라 크기와 위치를 맞춘다.</summary>
        private void FitCamera()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            float r = theme.hexRadius;
            int radius = state.Map.Radius;
            float halfWidth = Mathf.Sqrt(3f) * r * (radius + 0.5f);
            float halfHeight = 1.5f * r * radius + r;

            float aspect = worldCamera.aspect;
            float sizeByHeight = halfHeight / viewportArea.height;
            float sizeByWidth = halfWidth / (viewportArea.width * aspect);
            worldCamera.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth) * 1.08f;

            float worldHeight = worldCamera.orthographicSize * 2f;
            float worldWidth = worldHeight * aspect;
            var center = viewportArea.center;
            worldCamera.transform.position = new Vector3(
                (0.5f - center.x) * worldWidth,
                (0.5f - center.y) * worldHeight,
                worldCamera.transform.position.z);
        }

        /// <summary>pointy-top 축 좌표 → 월드 좌표. R이 커질수록 아래로 내려간다.</summary>
        private Vector2 AxialToWorld(HexCoord coord) =>
            new Vector2(Mathf.Sqrt(3f) * theme.hexRadius * (coord.Q + coord.R * 0.5f),
                -1.5f * theme.hexRadius * coord.R);
    }
}
