using System.IO;
using Grimward.Presentation.Battle;
using Grimward.Presentation.Bootstrap;
using Grimward.Presentation.Common;
using Grimward.Presentation.Map;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.EditorTools
{
    /// <summary>
    /// M2 탐험 화면 메뉴.
    /// 1) 전투 화면 프리팹: BattleTest 씬의 Panel_Battle(손으로 고친 배치 그대로)에 시계·탐험 무대 요소를 붙이고 프리팹으로 저장 → 두 씬이 공유.
    /// 2) 탐험 화면: Expedition 씬에 탐험 무대(전투 화면 프리팹) + 지도 오버레이(육각 칸 61개를 미리 배치) + 테스트 부트스트랩을 만든다.
    /// </summary>
    public static class ExpeditionUiBuilder
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/Panel_Battle.prefab";
        private const string HexArt = "Assets/_Project/Art/Hex/";
        private const string MapArtFolder = "Assets/_Project/Art/Map/";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/NotoSansKR SDF.asset";

        /// <summary>육각 칸 가로 폭(px, 1920×1080 기준). 칸 그림은 여백이 있어 이미지 크기는 폭 × 512/422.</summary>
        private const float HexWidth = 220f;
        private const int GridRadius = 4;

        // ── 1. 전투 화면 프리팹 ─────────────────────────────

        [MenuItem("Grimward/전투 화면 프리팹 만들기·갱신 (BattleTest → Prefab)", priority = 4)]
        public static void MakeBattlePrefab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            UiBuild.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var scene = EditorSceneManager.OpenScene($"{SceneFolder}/BattleTest.unity");
            var screen = Object.FindAnyObjectByType<BattleScreen>(FindObjectsInactive.Include);
            if (screen == null) { Debug.LogError("Grimward: BattleTest 씬에 BattleScreen이 없습니다."); return; }

            var top = screen.transform.Find("Panel_Top");
            if (top != null && top.Find("Img_Clock") == null)
            {
                var clock = BuildClock(top, 76f, false);
                var rect = (RectTransform)clock.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
                rect.anchoredPosition = new Vector2(52f, 0f);
                var turn = top.Find("Txt_Turn") as RectTransform;
                if (turn != null && turn.anchorMin.x < .05f) turn.anchorMin = new Vector2(.055f, turn.anchorMin.y);
                screen.AssignClock(clock);
                EditorUtility.SetDirty(screen);
            }

            AddStage(screen);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAssetAndConnect(screen.gameObject, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Grimward: 전투 화면을 프리팹으로 저장했습니다 ({PrefabPath}). 시계 · 지도 버튼 · 무대 골동품 · 골동품 창 · 큰 글자를 붙였습니다 (이미 있으면 그대로 둠).");
        }

        // 골동품 창: 물건(적 2번 칸) 오른쪽, 적 3·4번 칸 위
        private static readonly Vector2 CurioWindowMin = new Vector2(.76f, .47f);
        private static readonly Vector2 CurioWindowMax = new Vector2(.99f, .9f);

        private static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-5f;

        /// <summary>
        /// 무대 물건을 적 2번 칸 자리에 두고 몬스터 칸들보다 먼저(뒤에) 그린다.
        /// 손으로 옮긴 위치는 그대로 두고, 처음 만든 기본 위치(파티·적 사이)일 때만 옮긴다.
        /// </summary>
        private static void PlaceCurio(RectTransform curio, Transform field)
        {
            var slot2 = field.Find("Btn_EnemySlot2") as RectTransform;
            bool isDefault = Near(curio.anchorMin, new Vector2(.445f, .12f)) && Near(curio.anchorMax, new Vector2(.555f, .72f));
            if (slot2 != null && isDefault)
            {
                curio.anchorMin = new Vector2(slot2.anchorMin.x, .12f);
                curio.anchorMax = new Vector2(slot2.anchorMax.x, .72f);
            }
            int first = int.MaxValue;
            for (int rank = 1; rank <= 4; rank++)
                if (field.Find($"Btn_EnemySlot{rank}") is Transform t) first = Mathf.Min(first, t.GetSiblingIndex());
            if (first != int.MaxValue && curio.GetSiblingIndex() > first) curio.SetSiblingIndex(first);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var hit = FindDeep(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>
        /// 탐험 무대 요소를 전투 화면에 붙인다. 이미 있는 요소는 손으로 고친 배치 그대로 두고 연결만 다시 한다.
        /// Btn_OpenMap(Panel_Top, 도주 버튼 자리) · Btn_Curio(Panel_Field 가운데) · Txt_StageBanner · Panel_Curio(골동품 창).
        /// 전투 기록(Panel_Log)은 평소 꺼 두고 넘패드 0으로 켠다.
        /// </summary>
        private static void AddStage(BattleScreen screen)
        {
            var panel = screen.transform;
            var top = panel.Find("Panel_Top") ?? panel;
            var field = panel.Find("Panel_Field") ?? panel;

            // 지도 버튼: 도주 버튼과 같은 자리 (전투 밖에서만 보이므로 겹치지 않는다)
            var mapBtn = top.Find("Btn_OpenMap")?.GetComponent<Button>();
            if (mapBtn == null)
            {
                mapBtn = UiBuild.Button("Btn_OpenMap", top, "지도", 24, new Vector2(.9f, .15f), new Vector2(.98f, .85f));
                if (top.Find("Btn_Flee") is RectTransform flee)
                {
                    var r = (RectTransform)mapBtn.transform;
                    r.anchorMin = flee.anchorMin; r.anchorMax = flee.anchorMax;
                    r.offsetMin = flee.offsetMin; r.offsetMax = flee.offsetMax;
                }
            }

            // 무대 물건: 적 2번 칸 자리, 몬스터보다 뒤에 그린다 (모바일에서 파티·적 사이 공간이 좁으므로)
            var curio = field.Find("Btn_Curio")?.GetComponent<Button>();
            Image curioImg; TMP_Text curioName;
            if (curio == null)
            {
                var root = UiBuild.Image("Btn_Curio", field, new Vector2(.445f, .12f), new Vector2(.555f, .72f), new Color(1, 1, 1, 0), true);
                curio = root.gameObject.AddComponent<Button>();
                curio.transition = Selectable.Transition.ColorTint;
                curioImg = UiBuild.Image("Img_Curio", root.transform, new Vector2(.05f, .22f), new Vector2(.95f, 1f), Color.white);
                curioImg.preserveAspect = true;
                curio.targetGraphic = curioImg;
                curioName = UiBuild.Text("Txt_CurioName", root.transform, "", 20, new Vector2(-.3f, 0f), new Vector2(1.3f, .2f),
                    TextAlignmentOptions.Center, UiBuild.Gold);
                curioName.textWrappingMode = TextWrappingModes.NoWrap;
            }
            else
            {
                curioImg = curio.transform.Find("Img_Curio")?.GetComponent<Image>();
                curioName = curio.transform.Find("Txt_CurioName")?.GetComponent<TMP_Text>();
            }
            PlaceCurio((RectTransform)curio.transform, field);

            // 큰 글자 (승리 · 도주 · 전멸)
            var banner = field.Find("Txt_StageBanner")?.GetComponent<TMP_Text>();
            if (banner == null)
            {
                banner = UiBuild.Text("Txt_StageBanner", field, "", 84, new Vector2(.25f, .55f), new Vector2(.75f, .98f),
                    TextAlignmentOptions.Center, UiBuild.Gold);
                banner.fontStyle = FontStyles.Bold;
                banner.raycastTarget = false;
            }

            // 골동품 창: 물건 오른쪽 위 (다키스트 던전식)
            var window = panel.Find("Panel_Curio")?.GetComponent<CurioWindow>();
            if (window != null && window.transform is RectTransform wr && Near(wr.anchorMin, new Vector2(.585f, .47f)))
            {   // 이전 기본 위치(물건과 겹침)였으면 오른쪽으로 옮긴다
                wr.anchorMin = CurioWindowMin; wr.anchorMax = CurioWindowMax;
            }
            if (window == null)
            {
                var box = UiBuild.Image("Panel_Curio", panel, CurioWindowMin, CurioWindowMax,
                    new Color(0.1f, 0.09f, 0.08f, 0.97f), true).transform;
                var border = box.gameObject.AddComponent<Outline>();
                border.effectColor = new Color(.6f, .48f, .25f, 1f);
                border.effectDistance = new Vector2(3, -3);
                var title = UiBuild.Text("Txt_CurioTitle", box, "", 30, new Vector2(.05f, .8f), new Vector2(.95f, .96f),
                    TextAlignmentOptions.Center, UiBuild.Gold);
                var body = UiBuild.Text("Txt_CurioBody", box, "", 22, new Vector2(.06f, .36f), new Vector2(.94f, .79f),
                    TextAlignmentOptions.Center);
                body.overflowMode = TextOverflowModes.Overflow;
                var act = UiBuild.Button("Btn_CurioAct", box, "조사한다", 24, new Vector2(.05f, .05f), new Vector2(.4f, .32f));
                var supply = UiBuild.Button("Btn_CurioSupply", box, "보급품", 22, new Vector2(.43f, .05f), new Vector2(.75f, .32f));
                var close = UiBuild.Button("Btn_CurioClose", box, "X", 30, new Vector2(.78f, .05f), new Vector2(.95f, .32f));
                window = box.gameObject.AddComponent<CurioWindow>();
                window.Assign(title, body, act, supply, close);
                EditorUtility.SetDirty(window);
                // 떠오르는 글자·툴팁보다 아래
                var floating = panel.Find("Panel_FloatingText");
                if (floating != null) box.SetSiblingIndex(floating.GetSiblingIndex());
                box.gameObject.SetActive(false);
            }

            var log = FindDeep(panel, "Panel_Log");
            screen.AssignStage(mapBtn, curio, curioImg, curioName, window, banner, log != null ? log.gameObject : null);
            EditorUtility.SetDirty(screen);
        }

        // ── 2. 탐험 화면 ─────────────────────────────

        [MenuItem("Grimward/탐험 화면 만들기 (Expedition)", priority = 5)]
        public static void BuildExpedition()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                MakeBattlePrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) return;
            }
            string path = $"{SceneFolder}/Expedition.unity";
            if (File.Exists(path) && !EditorUtility.DisplayDialog("탐험 화면 만들기",
                    "Expedition 씬의 Canvas_Expedition과 ExpeditionTestBootstrap을 지우고 새로 만듭니다.\n손으로 고친 배치는 사라집니다.",
                    "만들기", "취소")) return;

            UiBuild.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var scene = File.Exists(path)
                ? EditorSceneManager.OpenScene(path)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Canvas_Expedition" || root.name == "ExpeditionTestBootstrap" || root.name == "Bootstrap")
                    Object.DestroyImmediate(root);

            UiBuild.EnsureEventSystem();
            var safe = UiBuild.Canvas("Canvas_Expedition");
            // 탐험 무대 = 전투 화면 프리팹. 지도 오버레이는 그 위에 그려지도록 뒤에 만든다.
            var battleGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, safe);
            var battleRect = (RectTransform)battleGo.transform;
            battleRect.anchorMin = Vector2.zero; battleRect.anchorMax = Vector2.one;
            battleRect.offsetMin = battleRect.offsetMax = Vector2.zero;
            battleGo.SetActive(false);
            var map = BuildMapOverlay(safe);

            var boot = new GameObject("ExpeditionTestBootstrap").AddComponent<ExpeditionTestBootstrap>();
            boot.Assign(battleGo.GetComponent<BattleScreen>(), map);
            EditorUtility.SetDirty(boot);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("Grimward: Expedition 씬에 탐험 무대(전투 화면 프리팹)와 지도 오버레이를 만들었습니다.");
        }

        private static Sprite Hex(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(HexArt + file);

        /// <summary>지도 임시 아트. 자동 분할(Multiple)되면 점선 길이 조각나므로 Single·가운데 피벗으로 맞춘 뒤 읽는다.</summary>
        private static Sprite MapArt(string file)
        {
            string path = MapArtFolder + file;
            if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePivot = new Vector2(.5f, .5f);
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"Grimward: 지도 아트가 없습니다 ({path}).");
            return sprite;
        }

        // 지도 오버레이: 화면 전체 어둡게(누르면 닫힘) + 가운데 창(제목 줄 · 지도 · 확대 조절)
        private static MapScreen BuildMapOverlay(Transform safe)
        {
            var root = UiBuild.Rect("Panel_MapOverlay", safe, Vector2.zero, Vector2.one);
            var dim = UiBuild.Image("Btn_MapDim", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, .72f), true);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            // 창 (1920×1080 기준 약 1540×950). 창 안을 눌러도 닫히지 않도록 창 바탕이 클릭을 받는다.
            var border = UiBuild.Image("Img_MapWindowBorder", root, new Vector2(.1f, .06f), new Vector2(.9f, .94f), new Color(.55f, .45f, .25f, .9f));
            border.rectTransform.offsetMin = new Vector2(-4, -4); border.rectTransform.offsetMax = new Vector2(4, 4);
            var window = UiBuild.Image("Panel_MapWindow", root, new Vector2(.1f, .06f), new Vector2(.9f, .94f),
                new Color(0.07f, 0.075f, 0.065f, 1f), true).transform;

            // 제목 줄: 왼쪽 구역 이름 + 예상귀환시간 / 오른쪽 이동 정보 + 이동 버튼 + X
            var header = UiBuild.Image("Panel_MapHeader", window, new Vector2(0, .89f), new Vector2(1, 1), new Color(0, 0, 0, .4f)).transform;
            var region = UiBuild.Text("Txt_MapRegion", header, "", 38, new Vector2(.02f, .05f), new Vector2(.45f, .95f),
                TextAlignmentOptions.MidlineLeft, UiBuild.Gold);
            region.textWrappingMode = TextWrappingModes.NoWrap;
            var travel = UiBuild.Text("Txt_MapTravel", header, "", 30, new Vector2(.4f, .05f), new Vector2(.76f, .95f),
                TextAlignmentOptions.MidlineRight);
            travel.textWrappingMode = TextWrappingModes.NoWrap;
            var travelBtn = UiBuild.Button("Btn_MapTravel", header, "이동", 30, new Vector2(.775f, .1f), new Vector2(.905f, .9f));
            var close = UiBuild.Button("Btn_MapClose", header, "X", 36, new Vector2(.92f, .1f), new Vector2(.99f, .9f));

            // 지도 영역: 끌어서 둘러보기, 가장자리 흐리게(RectMask2D softness)
            var areaImage = UiBuild.Image("Panel_MapArea", window, new Vector2(.005f, .008f), new Vector2(.995f, .885f), new Color(0, 0, 0, 0), true);
            var area = areaImage.rectTransform;
            var mask = area.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(70, 70);
            var grid = UiBuild.Rect("Grid_Hex", area, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            float tileSize = HexWidth * 512f / 422f;
            grid.sizeDelta = new Vector2(HexWidth * GridRadius * 2f + tileSize, HexWidth * 0.8660254f * GridRadius * 2f + tileSize);
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.content = grid;
            scroll.viewport = area;
            scroll.horizontal = true; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true; scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 0f; // 휠은 확대·축소(MapZoom)에 쓴다
            var tiles = new System.Collections.Generic.List<HexTileView>();
            foreach (var c in Grimward.Domain.Map.HexCoord.WithinRadius(GridRadius))
                tiles.Add(BuildTile(grid, c.Q, c.R));
            // 아래쪽 줄이 위에 그려지도록 r 순서로 정렬
            tiles.Sort((a, b) => a.R != b.R ? a.R.CompareTo(b.R) : a.Q.CompareTo(b.Q));
            for (int i = 0; i < tiles.Count; i++) tiles[i].transform.SetSiblingIndex(i);

            // 확대 조절 (오른쪽 아래): − 슬라이더 +
            var zoomBar = UiBuild.Image("Panel_MapZoom", window, new Vector2(.69f, .025f), new Vector2(.985f, .115f), new Color(0, 0, 0, .55f), true).transform;
            var zoomOut = UiBuild.Button("Btn_ZoomOut", zoomBar, "-", 40, new Vector2(.02f, .08f), new Vector2(.2f, .92f));
            var zoomIn = UiBuild.Button("Btn_ZoomIn", zoomBar, "+", 40, new Vector2(.8f, .08f), new Vector2(.98f, .92f));
            var slider = BuildSlider(zoomBar, new Vector2(.25f, .3f), new Vector2(.75f, .7f));

            // 마을로 귀환 (왼쪽 아래, 출발점에서만)
            var ret = UiBuild.Button("Btn_MapReturn", window, "마을로 귀환", 28, new Vector2(.015f, .025f), new Vector2(.2f, .115f));

            var zoom = area.gameObject.AddComponent<MapZoom>();
            zoom.Assign(scroll, slider, zoomIn, zoomOut);

            var view = root.gameObject.AddComponent<MapScreen>();
            view.Assign(dimButton, close, region, travel, travelBtn, tiles.ToArray(), scroll, zoom);
            view.AssignReturn(ret);
            view.AssignSprites(MapArt("map_icon_camp.png"), MapArt("map_icon_battle.png"), MapArt("map_icon_boss.png"),
                MapArt("map_icon_gather.png"), MapArt("map_icon_investigate.png"), MapArt("map_icon_treasure.png"),
                MapArt("map_road_solid.png"), MapArt("map_road_dashed.png"));
            EditorUtility.SetDirty(view);
            root.gameObject.SetActive(false);
            return view;
        }

        private static Slider BuildSlider(Transform parent, Vector2 min, Vector2 max)
        {
            var res = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            };
            var go = DefaultControls.CreateSlider(res);
            go.name = "Sld_Zoom";
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var handle = rect.Find("Handle Slide Area/Handle") as RectTransform;
            if (handle != null) handle.sizeDelta = new Vector2(44f, 0f); // 손가락으로 잡기 쉽게
            return go.GetComponent<Slider>();
        }

        private static HexTileView BuildTile(Transform grid, int q, int r)
        {
            float size = HexWidth * 512f / 422f;
            var root = UiBuild.Image($"Btn_Hex_{q}_{r}", grid, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(1, 1, 1, 0), true);
            var rect = root.rectTransform;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(HexWidth * (q + r / 2f), -HexWidth * 0.8660254f * r);
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var t = root.transform;

            Image Layer(string name, Sprite sprite, Color color, float scale = 1f)
            {
                var img = UiBuild.Image(name, t, Vector2.zero, Vector2.one, color);
                img.sprite = sprite;
                img.preserveAspect = true;
                img.rectTransform.localScale = new Vector3(scale, scale, 1f);
                return img;
            }

            var ground = Layer("Img_Ground", Hex("hex_grass.png"), Color.white);
            var fill = Layer("Img_Fill", MapArt("map_hex_fill.png"), Color.white, .96f);
            var roads = new Image[6];
            for (int d = 0; d < 6; d++)
            {
                roads[d] = Layer($"Img_Road{d}", MapArt("map_road_solid.png"), Color.white);
                roads[d].rectTransform.localEulerAngles = new Vector3(0, 0, d * 60f);
            }
            var outline = Layer("Img_Outline", MapArt("map_hex_outline.png"), Color.white, .96f);
            var tree = UiBuild.Image("Img_Tree", t, new Vector2(.25f, .3f), new Vector2(.75f, .85f), Color.white);
            tree.sprite = Hex("tree_01.png");
            tree.preserveAspect = true;
            var icon = UiBuild.Image("Img_Icon", t, new Vector2(.32f, .4f), new Vector2(.68f, .76f), Color.white);
            icon.preserveAspect = true;
            var question = UiBuild.Text("Txt_Question", t, "", 56, new Vector2(.6f, .5f), new Vector2(.82f, .82f),
                TextAlignmentOptions.Center, UiBuild.Gold);
            question.fontStyle = FontStyles.Bold;
            var player = UiBuild.Image("Img_Player", t, new Vector2(.36f, .4f), new Vector2(.64f, .68f), Color.white);
            player.sprite = MapArt("map_player.png");
            player.preserveAspect = true;
            var nameText = UiBuild.Text("Txt_HexName", t, "", 28, new Vector2(-.15f, .14f), new Vector2(1.15f, .38f),
                TextAlignmentOptions.Center);
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.overflowMode = TextOverflowModes.Overflow;
            var fog = Layer("Img_Fog", Hex("hex_fog.png"), new Color(1, 1, 1, .92f), 1.02f);

            var view = root.gameObject.AddComponent<HexTileView>();
            view.Assign(q, r, button, ground, fill, outline, tree, roads, icon, question, player, nameText, fog);
            fill.enabled = false; outline.enabled = false; icon.enabled = false; player.enabled = false; fog.enabled = false;
            foreach (var road in roads) road.enabled = false;
            return view;
        }

        /// <summary>24시간 원형 시계: 바탕(낮) · 밤 호 · 눈금 8개 · 시침 · (선택) 글자.</summary>
        private static ClockView BuildClock(Transform parent, float diameter, bool withLabel)
        {
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var root = UiBuild.Image("Img_Clock", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(.82f, .74f, .5f));
            root.sprite = knob;
            root.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            var face = UiBuild.Image("Img_DayFace", root.transform, Vector2.zero, Vector2.one, new Color(.93f, .86f, .62f));
            face.sprite = knob;
            face.rectTransform.offsetMin = new Vector2(4, 4); face.rectTransform.offsetMax = new Vector2(-4, -4);
            var night = UiBuild.Image("Img_NightArc", face.transform, Vector2.zero, Vector2.one, new Color(.13f, .16f, .38f));
            night.sprite = knob;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var tick = UiBuild.Image($"Img_Tick{i}", face.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    i % 2 == 0 ? new Color(.85f, .2f, .15f) : new Color(1f, .95f, .7f));
                tick.rectTransform.sizeDelta = new Vector2(diameter * .07f, diameter * .07f);
                tick.sprite = knob;
                tick.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (diameter * .4f - 4);
            }
            var hand = UiBuild.Image("Img_Hand", face.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(.98f, .97f, .92f));
            hand.rectTransform.pivot = new Vector2(.5f, 0f);
            hand.rectTransform.sizeDelta = new Vector2(Mathf.Max(4f, diameter * .06f), diameter * .4f);
            hand.rectTransform.anchoredPosition = Vector2.zero;
            var cap = UiBuild.Image("Img_HandCap", face.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Color(.35f, .3f, .25f));
            cap.sprite = knob;
            cap.rectTransform.sizeDelta = new Vector2(diameter * .14f, diameter * .14f);

            TMP_Text label = null;
            if (withLabel)
            {
                label = UiBuild.Text("Txt_Clock", root.transform, "", 28, new Vector2(1f, 0f), new Vector2(1f, 1f),
                    TextAlignmentOptions.MidlineLeft, UiBuild.Gold);
                label.rectTransform.offsetMin = new Vector2(16f, 0f);
                label.rectTransform.offsetMax = new Vector2(420f, 0f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
            var view = root.gameObject.AddComponent<ClockView>();
            view.Assign(hand.rectTransform, night, label);
            return view;
        }
    }
}
