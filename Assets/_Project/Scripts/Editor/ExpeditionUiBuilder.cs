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
    /// 1) 전투 화면 프리팹: BattleTest 씬의 Panel_Battle(손으로 고친 배치 그대로)에 시계를 붙이고 프리팹으로 저장 → 두 씬이 공유.
    /// 2) 탐험 화면: Expedition 씬에 탐험 기본 화면 + 지도 오버레이(육각 칸 61개를 미리 배치) + 전투 패널(프리팹) + 테스트 부트스트랩을 만든다.
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

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAssetAndConnect(screen.gameObject, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Grimward: 전투 화면을 프리팹으로 저장했습니다 ({PrefabPath}). 시계(Img_Clock)를 Panel_Top 왼쪽에 붙였습니다.");
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
            var explore = BuildExploreScreen(safe);
            var map = BuildMapOverlay(safe);

            var battleGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, safe);
            var battleRect = (RectTransform)battleGo.transform;
            battleRect.anchorMin = Vector2.zero; battleRect.anchorMax = Vector2.one;
            battleRect.offsetMin = battleRect.offsetMax = Vector2.zero;
            battleGo.SetActive(false);

            var boot = new GameObject("ExpeditionTestBootstrap").AddComponent<ExpeditionTestBootstrap>();
            boot.Assign(explore, map, battleGo.GetComponent<BattleScreen>());
            EditorUtility.SetDirty(boot);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("Grimward: Expedition 씬에 탐험 화면 · 지도 오버레이 · 전투 패널(프리팹)을 만들었습니다.");
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

        // 탐험 기본 화면: 가운데 무대(2단계 전까지는 장소 이름) + 왼쪽 위 시계 + 오른쪽 패널
        private static ExploreScreen BuildExploreScreen(Transform safe)
        {
            var panel = UiBuild.Image("Panel_Explore", safe, Vector2.zero, Vector2.one, new Color(0.06f, 0.08f, 0.06f)).transform;

            var top = UiBuild.Image("Panel_ExploreTop", panel, new Vector2(0, .9f), new Vector2(.66f, 1), new Color(0, 0, 0, .35f)).transform;
            var clock = BuildClock(top, 92f, true);
            var clockRect = (RectTransform)clock.transform;
            clockRect.anchorMin = clockRect.anchorMax = new Vector2(0f, .5f);
            clockRect.anchoredPosition = new Vector2(64f, 0f);

            var stage = UiBuild.Rect("Panel_Stage", panel, new Vector2(0, 0), new Vector2(.66f, .9f));
            var stageText = UiBuild.Text("Txt_Stage", stage, "", 56, new Vector2(.05f, .35f), new Vector2(.95f, .65f),
                TextAlignmentOptions.Center, UiBuild.Gold);

            var side = UiBuild.Image("Panel_ExploreSide", panel, new Vector2(.665f, .015f), new Vector2(.99f, .985f), UiBuild.PanelColor).transform;
            var nodeName = UiBuild.Text("Txt_NodeName", side, "", 34, new Vector2(.04f, .91f), new Vector2(.96f, .985f),
                TextAlignmentOptions.MidlineLeft, UiBuild.Gold);
            var nodeInfo = UiBuild.Text("Txt_NodeInfo", side, "", 26, new Vector2(.04f, .69f), new Vector2(.96f, .91f));
            nodeInfo.overflowMode = TextOverflowModes.Overflow;
            // 버튼 2×2 (높이 120px 이상)
            var mapBtn = UiBuild.Button("Btn_OpenMap", side, "지도", 30, new Vector2(.04f, .55f), new Vector2(.49f, .68f));
            var ret = UiBuild.Button("Btn_Return", side, "마을로 귀환", 30, new Vector2(.51f, .55f), new Vector2(.96f, .68f));
            var ev = UiBuild.Button("Btn_EventAction", side, "조사한다", 28, new Vector2(.04f, .405f), new Vector2(.49f, .535f));
            var supply = UiBuild.Button("Btn_EventSupply", side, "보급품 쓰고 조사", 26, new Vector2(.51f, .405f), new Vector2(.96f, .535f));
            UiBuild.Divider(side, new Vector2(.03f, .39f), new Vector2(.97f, .393f));
            var party = UiBuild.Text("Txt_Party", side, "", 24, new Vector2(.04f, .265f), new Vector2(.96f, .385f));
            var bag = UiBuild.Text("Txt_Bag", side, "", 22, new Vector2(.04f, .205f), new Vector2(.96f, .265f));
            var logBg = UiBuild.Image("Panel_ExploreLog", side, new Vector2(.03f, .015f), new Vector2(.97f, .2f), new Color(0, 0, 0, .35f));
            var log = UiBuild.Text("Txt_ExploreLog", logBg.transform, "", 22, new Vector2(.02f, .03f), new Vector2(.98f, .97f),
                TextAlignmentOptions.BottomLeft);

            var result = UiBuild.Image("Panel_ExpeditionResult", panel, new Vector2(.3f, .25f), new Vector2(.7f, .8f),
                new Color(0.1f, 0.09f, 0.08f, 0.97f), true);
            var resultText = UiBuild.Text("Txt_ExpeditionResult", result.transform, "", 30, new Vector2(.05f, .3f), new Vector2(.95f, .95f),
                TextAlignmentOptions.Center);
            var again = UiBuild.Button("Btn_NewExpedition", result.transform, "다시 출정", 30, new Vector2(.3f, .06f), new Vector2(.7f, .24f));
            result.gameObject.SetActive(false);

            var view = panel.gameObject.AddComponent<ExploreScreen>();
            view.Assign(clock, stageText, nodeName, nodeInfo, mapBtn, ret, ev, supply, party, bag, log, result.gameObject, resultText, again);
            EditorUtility.SetDirty(view);
            return view;
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

            var zoom = area.gameObject.AddComponent<MapZoom>();
            zoom.Assign(scroll, slider, zoomIn, zoomOut);

            var view = root.gameObject.AddComponent<MapScreen>();
            view.Assign(dimButton, close, region, travel, travelBtn, tiles.ToArray(), scroll, zoom);
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
