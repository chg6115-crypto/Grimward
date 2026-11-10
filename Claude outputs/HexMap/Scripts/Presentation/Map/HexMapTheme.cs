using UnityEngine;

namespace Grimward.Presentation.Map
{
    /// <summary>
    /// 육각 지도 표시용 아트·색상 설정. 게임 규칙과 무관한 화면 전용 값만 둔다.
    /// 메뉴 Grimward/Art/4 가 자동으로 만들고 Main 씬에 연결한다.
    /// </summary>
    [CreateAssetMenu(fileName = "HexMapTheme", menuName = "Grimward/Hex Map Theme")]
    public sealed class HexMapTheme : ScriptableObject
    {
        [Header("프리팹·스프라이트")]
        public GameObject tilePrefab;
        public Sprite markerUnknown;
        public Sprite markerStart;
        public Sprite markerBattle;
        public Sprite markerTreasure;
        public Sprite roadSegment;
        public Sprite[] decorations;

        [Header("배치")]
        [Tooltip("육각형 외접원 반지름 (유닛). 타일 높이 1 유닛이면 0.5")]
        public float hexRadius = 0.5f;
        [Tooltip("맵 바깥에 둘러칠 숲 배경 고리 수")]
        [Min(0)] public int borderRings = 2;
        [Range(0f, 1f)] public float decorationChance = 0.6f;
        [Tooltip("노드 번호 표시 (개발용)")]
        public bool showNodeIds = true;

        [Header("색상")]
        public Color cameraBackground = new Color(0.04f, 0.06f, 0.05f);
        public Color currentTint = new Color(1f, 0.9f, 0.6f);
        public Color movableTint = new Color(0.9f, 1f, 0.9f);
        public Color idleTint = new Color(0.75f, 0.8f, 0.75f);
        public Color forestTint = new Color(0.5f, 0.58f, 0.5f);
        public Color traversedRoadTint = Color.white;
        public Color untraversedRoadTint = new Color(1f, 1f, 1f, 0.55f);
    }
}
