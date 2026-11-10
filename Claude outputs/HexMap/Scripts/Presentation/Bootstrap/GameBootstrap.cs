using Grimward.Domain.Config;
using Grimward.Domain.Characters;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using Grimward.Domain.Map;
using Grimward.Domain.Exploration;
using Grimward.Presentation.Config;
using Grimward.Presentation.Map;
using Grimward.Presentation.UI;
using TMPro;
using UnityEngine;
namespace Grimward.Presentation.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private BalanceConfigAsset balanceConfig;
        [SerializeField] private bool useSeedOverride;
        [SerializeField] private int seed = new BalanceConfig().DefaultSeed;
        [SerializeField] private TMP_FontAsset uiFont;
        [Header("육각 지도 (메뉴 Grimward/Art/4 가 자동 연결)")]
        [SerializeField] private HexMapTheme mapTheme;
        [SerializeField] private Camera worldCamera;
        private TMP_FontAsset runtimeFont;
        public ExplorationState State { get; private set; }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            var config = balanceConfig != null ? balanceConfig.ToDomain() : new BalanceConfig();
            config.Validate();
            IRandomSource random = new SeededRandom(useSeedOverride ? seed : config.DefaultSeed);
            IMapProvider provider = new FixedMapProvider();
            var map = provider.CreateMap();
            var errors = new MapValidator(config).Validate(map);
            if (errors.Count != 0) throw new System.InvalidOperationException(string.Join("\n", errors));
            State = new ExplorationState(map, config, new TravelTimeCalculator(config),
                new FogOfWar(), new PathEventResolver(config, random), new ReturnPathFinder(config),
                new Adventurer(config), new BattleFactory(config, random, new EnemyFactory(config, random)));
            var font = uiFont;
            if (font == null)
            {
                // 데스크톱 M1용 OS 폰트. 모바일 배포 전 uiFont에 한글 TMP 에셋을 지정한다.
                foreach (string family in new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" })
                {
                    runtimeFont = TMP_FontAsset.CreateFontAsset(family, "Regular");
                    if (runtimeFont != null) break;
                }
                if (runtimeFont == null)
                    throw new System.InvalidOperationException("GameBootstrap의 Ui Font에 한글 TMP 폰트를 지정하세요.");
                font = runtimeFont;
            }
            if (mapTheme == null || mapTheme.tilePrefab == null)
                throw new System.InvalidOperationException("GameBootstrap의 Map Theme가 비어 있습니다. 메뉴 Grimward/Art/4 를 실행하세요.");
            var cameraForMap = worldCamera != null ? worldCamera : Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (cameraForMap == null)
                throw new System.InvalidOperationException("씬에 카메라가 없습니다.");
            var screen = gameObject.AddComponent<ExplorationScreen>();
            screen.Initialize(State, font, mapTheme, cameraForMap);
        }

        private void OnDestroy()
        {
            if (runtimeFont != null)
            {
                foreach (var atlas in runtimeFont.atlasTextures) if (atlas != null) Destroy(atlas);
                if (runtimeFont.material != null) Destroy(runtimeFont.material);
                Destroy(runtimeFont);
            }
        }
    }
}
