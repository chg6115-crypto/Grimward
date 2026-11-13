using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Map
{
    /// <summary>
    /// 지도 육각 칸 하나 (uGUI). 에디터 메뉴가 반경 4 격자(61칸)를 미리 깔아 두고, 맵이 바뀌면 상태만 다시 지정한다.
    /// 길은 칸마다 6방향 조각(0=오른쪽부터 반시계 60°씩)을 미리 두고 켜고 끈다. 두 칸의 반쪽 길이 만나 하나의 길이 된다.
    /// </summary>
    public sealed class HexTileView : MonoBehaviour
    {
        [SerializeField] private int q;
        [SerializeField] private int r;
        [SerializeField] private Button button;
        [Tooltip("숲 바닥 (노드가 아니거나 아직 발견하지 못한 칸)")]
        [SerializeField] private Image ground;
        [Tooltip("노드 칸 채움 (방문 = 밝게, 미탐색 = 어둡게)")]
        [SerializeField] private Image fill;
        [Tooltip("노드 테두리 (이동 가능 = 금색, 그 밖 = 어두운 색)")]
        [SerializeField] private Image outline;
        [SerializeField] private Image tree;
        [SerializeField] private Image[] roads = new Image[6];
        [Tooltip("이벤트 아이콘 (전투·채집·조사·보물·출발점)")]
        [SerializeField] private Image icon;
        [Tooltip("지각으로만 안 칸의 「?」")]
        [SerializeField] private TMP_Text question;
        [Tooltip("현재 위치 (플레이어 아이콘)")]
        [SerializeField] private Image player;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image fog;

        public int Q => q;
        public int R => r;
        public event Action<HexTileView> Clicked;

        public void Assign(int q, int r, Button button, Image ground, Image fill, Image outline, Image tree, Image[] roads,
            Image icon, TMP_Text question, Image player, TMP_Text nameText, Image fog)
        {
            this.q = q; this.r = r; this.button = button; this.ground = ground; this.fill = fill; this.outline = outline;
            this.tree = tree; this.roads = roads; this.icon = icon; this.question = question; this.player = player;
            this.nameText = nameText; this.fog = fog;
        }

        /// <summary>현재 위치 아이콘만 끄고 켠다 (이동 연출 중에는 움직이는 핀이 대신한다).</summary>
        public void SetPlayerVisible(bool visible) => player.enabled = visible;

        /// <summary>노드 칸(채움·테두리) 크기 배율. 칸 사이 간격을 정한다 (1 = 이웃과 거의 붙음).</summary>
        public void SetNodeScale(float scale)
        {
            fill.rectTransform.localScale = new Vector3(scale, scale, 1f);
            outline.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        /// <summary>숲 칸 (노드가 아니거나 아직 발견하지 못한 노드). 지도 구조가 드러나지 않게 같은 모습.</summary>
        public void ShowForest(Color groundColor, bool fogged, bool hasTree)
        {
            ground.enabled = true;
            ground.color = groundColor;
            fill.enabled = false;
            outline.enabled = false;
            tree.enabled = hasTree && !fogged;
            fog.enabled = fogged;
            icon.enabled = false;
            question.text = "";
            player.enabled = false;
            nameText.text = "";
            foreach (var road in roads) road.enabled = false;
            button.interactable = false;
        }

        /// <summary>발견한 노드.</summary>
        public void ShowNode(Color fillColor, Color outlineColor, Sprite iconSprite, bool showQuestion, bool isCurrent,
            string nodeName, Color nameColor, bool clickable)
        {
            ground.enabled = false;
            fill.enabled = true;
            fill.color = fillColor;
            outline.enabled = true;
            outline.color = outlineColor;
            tree.enabled = false;
            fog.enabled = false;
            // 현재 위치는 플레이어 아이콘이 대신한다 (그 칸의 할 일은 탐험 화면에 나온다)
            icon.enabled = iconSprite != null && !isCurrent;
            icon.sprite = iconSprite;
            question.text = showQuestion && !isCurrent ? "?" : "";
            player.enabled = isCurrent;
            nameText.text = nodeName ?? "";
            nameText.color = nameColor;
            button.interactable = clickable;
        }

        /// <summary>direction 0~5 (오른쪽부터 반시계 60°). 보이지 않으면 끈다.</summary>
        public void SetRoad(int direction, bool visible, Sprite sprite, Color color)
        {
            if (direction < 0 || direction >= roads.Length || roads[direction] == null) return;
            var road = roads[direction];
            road.enabled = visible;
            if (!visible) return;
            if (sprite != null) road.sprite = sprite;
            road.color = color;
        }
    }
}
