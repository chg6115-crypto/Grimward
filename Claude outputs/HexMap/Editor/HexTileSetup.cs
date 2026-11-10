using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Grimward.Editor
{
    /// <summary>
    /// 육각 타일 임시 아트 세팅 도구.
    /// 메뉴: Grimward/Art/1~3 을 순서대로 실행한다.
    /// 타일 규격: pointy-top(꼭짓점이 위), 512x512 PNG, 육각형 높이 480px = 1 유닛.
    /// </summary>
    public static class HexTileSetup
    {
        private const string ArtDir = "Assets/_Project/Art/Hex";
        private const string PrefabDir = "Assets/_Project/Prefabs";
        private const string PrefabPath = PrefabDir + "/HexTile.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/HexTileTest.unity";

        // 육각형 높이 480px을 1 유닛으로 맞춘다. 모든 타일 관련 스프라이트에 같은 값을 써야 크기가 맞는다.
        private const float PixelsPerUnit = 480f;
        // 외접원 반지름(유닛). 높이 1 유닛이므로 0.5.
        private const float HexRadius = 0.5f;

        // 프리팹 안의 그리기 순서 (SortingGroup 내부)
        private const int OrderGround = 0;
        private const int OrderPath = 10;
        private const int OrderDecoration = 20;
        private const int OrderMarker = 30;
        private const int OrderFog = 40;

        [MenuItem("Grimward/Art/1. 스프라이트 임포트 설정")]
        public static void ConfigureSprites()
        {
            if (!Directory.Exists(ArtDir))
            {
                Debug.LogError($"{ArtDir} 폴더가 없습니다. PNG 파일을 먼저 넣어 주세요.");
                return;
            }

            foreach (string file in Directory.GetFiles(ArtDir, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = PivotFor(Path.GetFileNameWithoutExtension(path));
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }

            Debug.Log("Grimward: 육각 타일 스프라이트 임포트 설정 완료 (PPU 480).");
        }

        [MenuItem("Grimward/Art/2. HexTile 프리팹 생성")]
        public static void CreatePrefab()
        {
            ConfigureSprites();
            Directory.CreateDirectory(PrefabDir);

            var root = new GameObject("HexTile");
            root.AddComponent<SortingGroup>();

            CreateSprite("Ground", root.transform, LoadSprite("hex_grass"), OrderGround);

            var pathRoot = new GameObject("PathRoot");
            pathRoot.transform.SetParent(root.transform, false);

            var decorationRoot = new GameObject("DecorationRoot");
            decorationRoot.transform.SetParent(root.transform, false);

            CreateSprite("EventMarker", root.transform, LoadSprite("marker_unknown"), OrderMarker);

            var fog = CreateSprite("Fog", root.transform, LoadSprite("hex_fog"), OrderFog);
            fog.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"Grimward: {PrefabPath} 생성 완료.");
        }

        [MenuItem("Grimward/Art/3. 7칸 배치 테스트 씬 생성")]
        public static void CreateTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreatePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.11f, 0.13f);

            // 중앙 1칸 + 주변 6칸 (축 좌표 q, r)
            var tiles = new (int q, int r)[] { (0, 0), (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1) };
            var parent = new GameObject("HexTiles").transform;

            foreach (var (q, r) in tiles)
            {
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                tile.name = $"HexTile_{q}_{r}";
                tile.transform.SetParent(parent, false);
                tile.transform.localPosition = AxialToWorld(q, r);
                // 화면 아래쪽 줄(r이 큰 줄)이 앞에 그려지도록 정렬
                tile.GetComponent<SortingGroup>().sortingOrder = r;
            }

            // 마커 예시
            SetMarker(parent, 1, 0, "marker_battle");
            SetMarker(parent, 1, -1, "marker_treasure");
            SetMarker(parent, -1, 1, "marker_start");
            SetMarker(parent, 0, 0, null);   // 중앙은 마커 숨김 (나무 테스트용)

            // 길 예시: 중앙 → 동(0°), 북동(60°), 남서(240°). 이웃 칸에는 반대 방향 조각을 붙인다.
            AddRoadPair(parent, 0, 0, 1, 0, 0f);
            AddRoadPair(parent, 0, 0, 1, -1, 60f);
            AddRoadPair(parent, 0, 0, -1, 1, 240f);

            // 장식 예시: 중앙 칸에 나무 한 그루
            AddDecoration(parent, 0, 0, "tree_01", new Vector3(0.18f, -0.18f, 0f));

            // 안개 예시: 서쪽 칸
            Find(parent, -1, 0).Find("Fog").gameObject.SetActive(true);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Grimward: {ScenePath} 생성 완료. 정렬·간격·겹침을 확인하세요.");
        }

        [MenuItem("Grimward/Art/4. Main 씬에 육각 지도 연결")]
        public static void ConnectMainScene()
        {
            const string themePath = "Assets/_Project/Data/HexMapTheme.asset";
            const string mainScenePath = "Assets/_Project/Scenes/Main.unity";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreatePrefab();

            // 테마 에셋: 없으면 만들고, 있으면 비어 있는 참조만 채운다 (조정한 색·수치는 유지).
            var theme = AssetDatabase.LoadAssetAtPath<Grimward.Presentation.Map.HexMapTheme>(themePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<Grimward.Presentation.Map.HexMapTheme>();
                AssetDatabase.CreateAsset(theme, themePath);
            }
            if (theme.tilePrefab == null) theme.tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (theme.markerUnknown == null) theme.markerUnknown = LoadSprite("marker_unknown");
            if (theme.markerStart == null) theme.markerStart = LoadSprite("marker_start");
            if (theme.markerBattle == null) theme.markerBattle = LoadSprite("marker_battle");
            if (theme.markerTreasure == null) theme.markerTreasure = LoadSprite("marker_treasure");
            if (theme.roadSegment == null) theme.roadSegment = LoadSprite("hex_road_segment");
            if (theme.decorations == null || theme.decorations.Length == 0)
                theme.decorations = new[] { LoadSprite("tree_01") };
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(mainScenePath, OpenSceneMode.Single);

            var bootstrap = Object.FindAnyObjectByType<Grimward.Presentation.Bootstrap.GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("Main 씬에서 GameBootstrap을 찾지 못했습니다.");
                return;
            }

            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                camera = cameraObject.GetComponent<Camera>();
            }
            camera.gameObject.name = "Main Camera";
            camera.gameObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.cullingMask = ~0;   // 기존 M1 설정은 아무것도 그리지 않았음(0) → 모두 그리도록

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("mapTheme").objectReferenceValue = theme;
            serialized.FindProperty("worldCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Grimward: Main 씬에 육각 지도 테마와 카메라를 연결했습니다.");
        }

        /// <summary>pointy-top 축 좌표 → 월드 좌표. r이 커질수록 아래로 내려간다 (지도 UI와 같은 방향).</summary>
        public static Vector3 AxialToWorld(int q, int r) =>
            new Vector3(Mathf.Sqrt(3f) * HexRadius * (q + r * 0.5f), -1.5f * HexRadius * r, 0f);

        private static Vector2 PivotFor(string spriteName)
        {
            // 나무는 줄기 밑동(아래 중앙)이 기준점. 나머지는 중앙.
            if (spriteName.StartsWith("tree_")) return new Vector2(0.5f, 20f / 320f);
            return new Vector2(0.5f, 0.5f);
        }

        private static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
            if (sprite == null) Debug.LogError($"스프라이트를 찾을 수 없습니다: {ArtDir}/{name}.png");
            return sprite;
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

        private static Transform Find(Transform parent, int q, int r) => parent.Find($"HexTile_{q}_{r}");

        private static void SetMarker(Transform parent, int q, int r, string spriteName)
        {
            var marker = Find(parent, q, r).Find("EventMarker").GetComponent<SpriteRenderer>();
            if (spriteName == null) { marker.gameObject.SetActive(false); return; }
            marker.sprite = LoadSprite(spriteName);
        }

        private static void AddRoad(Transform parent, int q, int r, float angle)
        {
            var road = CreateSprite($"Road_{angle:0}", Find(parent, q, r).Find("PathRoot"),
                LoadSprite("hex_road_segment"), OrderPath);
            road.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void AddRoadPair(Transform parent, int q1, int r1, int q2, int r2, float angle)
        {
            AddRoad(parent, q1, r1, angle);
            AddRoad(parent, q2, r2, (angle + 180f) % 360f);
        }

        private static void AddDecoration(Transform parent, int q, int r, string spriteName, Vector3 localPosition)
        {
            var deco = CreateSprite(spriteName, Find(parent, q, r).Find("DecorationRoot"),
                LoadSprite(spriteName), OrderDecoration);
            deco.transform.localPosition = localPosition;
        }
    }
}
