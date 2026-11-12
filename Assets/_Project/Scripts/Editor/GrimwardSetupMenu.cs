using System.Collections.Generic;
using System.IO;
using System.Linq;
using Grimward.Presentation.Battle;
using Grimward.Presentation.Bootstrap;
using Grimward.Presentation.Common;
using Grimward.Presentation.Data;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Grimward.EditorTools
{
    /// <summary>
    /// M0 씬과 UI를 Hierarchy에 만들어 저장하는 메뉴. 만든 뒤에는 씬에서 직접 배치·색을 고쳐도 된다.
    /// 「다시 만들기」는 기존 Canvas를 지우고 새로 만드므로 손으로 고친 배치는 사라진다.
    /// </summary>
    public static class GrimwardSetupMenu
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string FontFile = "Assets/_Project/Art/Fonts/NotoSansKR-Regular.ttf";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/NotoSansKR SDF.asset";
        private static readonly string[] SceneOrder = { "Title", "Town", "Expedition", "BattleTest" };

        // Expedition은 「탐험 화면 만들기」 메뉴가 만든다 (M2). 여기서 다시 만들면 지도가 지워지므로 빼 둔다.
        private static readonly Dictionary<string, string> PlaceholderTitles = new Dictionary<string, string>
        {
            ["Title"] = "GRIMWARD",
            ["Town"] = "마을 (M3에서 구현)",
        };

        // ── 메뉴 ─────────────────────────────

        [MenuItem("Grimward/M0 씬 만들기 (Title·Town·Expedition·BattleTest)", priority = 1)]
        public static void CreateScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var existing = SceneOrder.Where(n => File.Exists(ScenePath(n))).ToList();
            if (existing.Count > 0 && !EditorUtility.DisplayDialog("M0 씬 만들기",
                    "이미 있는 씬: " + string.Join(", ", existing) +
                    "\n이 씬들의 UI(Canvas·Bootstrap·EventSystem)를 지우고 새로 만듭니다. 손으로 고친 배치는 사라집니다.",
                    "만들기", "취소")) return;

            UiBuild.Font = GetOrCreateFont();
            Directory.CreateDirectory(SceneFolder);

            foreach (var kv in PlaceholderTitles)
            {
                var scene = OpenOrCreate(kv.Key);
                BuildPlaceholderUi(kv.Key, kv.Value);
                EditorSceneManager.SaveScene(scene, ScenePath(kv.Key));
            }
            var battle = OpenOrCreate("BattleTest");
            BuildBattleUi();
            EditorSceneManager.SaveScene(battle, ScenePath("BattleTest"));

            RegisterBuildScenes();
            EditorSceneManager.OpenScene(ScenePath("BattleTest"));
            Debug.Log("Grimward: Title·Town·Expedition·BattleTest 씬과 UI를 만들었습니다.");
        }

        /// <summary>씬을 열거나 새로 만들고, 이전에 만든 UI 루트를 지운다 (카메라·조명은 둔다).</summary>
        private static Scene OpenOrCreate(string name)
        {
            string path = ScenePath(name);
            var scene = File.Exists(path)
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                bool ours = root.name.StartsWith("Canvas_") || root.name == "Bootstrap" ||
                            root.name == "BattleTestBootstrap" || root.name == "EventSystem";
                if (ours) Object.DestroyImmediate(root);
            }
            return scene;
        }

        [MenuItem("Grimward/전투 테스트 UI 다시 만들기 (BattleTest)", priority = 2)]
        public static void RebuildBattleUi()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath("BattleTest"))) { CreateScenes(); return; }
            if (!EditorUtility.DisplayDialog("전투 테스트 UI 다시 만들기",
                    "BattleTest 씬의 Canvas_Battle과 BattleTestBootstrap을 지우고 새로 만듭니다.\n손으로 고친 배치·Inspector 값은 사라집니다.",
                    "다시 만들기", "취소")) return;
            UiBuild.Font = GetOrCreateFont();
            var scene = EditorSceneManager.OpenScene(ScenePath("BattleTest"));
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Canvas_Battle" || root.name == "BattleTestBootstrap")
                    Object.DestroyImmediate(root);
            BuildBattleUi();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Grimward: BattleTest UI를 다시 만들었습니다.");
        }

        [MenuItem("Grimward/전투 연출 추가 (기존 BattleTest UI 유지)", priority = 3)]
        public static void AddBattleEffects()
        {
            var screen = Object.FindAnyObjectByType<BattleScreen>(FindObjectsInactive.Include);
            if (screen == null)
            {
                Debug.LogError("Grimward: 열린 씬에 BattleScreen이 없습니다. BattleTest 씬을 열고 다시 실행하세요.");
                return;
            }
            if (screen.HasEffects)
            {
                Debug.Log("Grimward: 이미 전투 연출(Panel_FloatingText)이 연결되어 있습니다.");
                return;
            }
            UiBuild.Font = GetOrCreateFont();
            var panel = screen.transform;
            var top = panel.Find("Panel_Top") ?? panel;
            BuildEffects(panel, top, screen);
            var tip = panel.Find("Panel_Tooltip");
            if (tip != null) tip.SetAsLastSibling();   // 툴팁은 항상 맨 위
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
            Debug.Log("Grimward: 떠오르는 글자(Panel_FloatingText)와 속도 버튼(Btn_Speed)을 추가했습니다. 씬을 저장하세요.");
        }

        [MenuItem("Grimward/게임 데이터 검증", priority = 20)]
        public static void ValidateData()
        {
            try
            {
                var db = GameDataLoader.Load();
                Debug.Log($"Grimward: 게임 데이터 정상 — 직업 {db.Classes.Count}, 몬스터 {db.Monsters.Count}, " +
                          $"편성 {db.Encounters.Count}, 아이템 {db.Items.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError(e.Message);
            }
        }

        // ── 공통 ─────────────────────────────

        private static string ScenePath(string name) => $"{SceneFolder}/{name}.unity";

        private static void RegisterBuildScenes()
        {
            var ours = SceneOrder.Select(ScenePath).ToList();
            var others = EditorBuildSettings.scenes.Where(s => !ours.Contains(s.path));
            EditorBuildSettings.scenes = ours.Where(File.Exists)
                .Select(p => new EditorBuildSettingsScene(p, true)).Concat(others).ToArray();
        }

        /// <summary>Noto Sans KR(OFL) TTF로 동적 TMP 폰트 에셋을 한 번 만든다.</summary>
        private static TMP_FontAsset GetOrCreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontFile);
            if (font == null) throw new FileNotFoundException("한글 폰트 파일이 없습니다: " + FontFile);

            var asset = TMP_FontAsset.CreateFontAsset(font);
            asset.name = "NotoSansKR SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ── 임시 씬 UI ─────────────────────────────

        private static void BuildPlaceholderUi(string sceneName, string title)
        {
            UiBuild.EnsureEventSystem();
            var root = UiBuild.Canvas("Canvas_" + sceneName);
            var panel = UiBuild.Image("Panel_" + sceneName, root, Vector2.zero, Vector2.one, new Color(0.06f, 0.06f, 0.07f)).transform;
            UiBuild.Text("Txt_SceneTitle", panel, title, 64, new Vector2(.1f, .6f), new Vector2(.9f, .85f),
                TextAlignmentOptions.Center, UiBuild.Gold);
            for (int i = 0; i < SceneOrder.Length; i++)
            {
                float x0 = .14f + i * .185f;
                var b = UiBuild.Button("Btn_Go" + SceneOrder[i], panel, SceneOrder[i], 30, new Vector2(x0, .3f),
                    new Vector2(x0 + .165f, .42f));
                b.gameObject.AddComponent<SceneLink>().SceneName = SceneOrder[i];
            }
        }

        // ── 전투 UI ─────────────────────────────

        private static void BuildBattleUi()
        {
            UiBuild.EnsureEventSystem();
            var safe = UiBuild.Canvas("Canvas_Battle");
            var panel = UiBuild.Image("Panel_Battle", safe, Vector2.zero, Vector2.one, new Color(0.04f, 0.04f, 0.05f)).transform;
            var screen = panel.gameObject.AddComponent<BattleScreen>();

            // 상단
            var top = UiBuild.Rect("Panel_Top", panel, new Vector2(0, .92f), new Vector2(1, 1)).transform;
            var turn = UiBuild.Text("Txt_Turn", top, "", 26, new Vector2(.02f, .1f), new Vector2(.4f, .9f),
                TextAlignmentOptions.MidlineLeft, UiBuild.Gold);
            var roundBox = UiBuild.Image("Img_RoundBox", top, new Vector2(.475f, .08f), new Vector2(.525f, .92f), UiBuild.PanelColor);
            var round = UiBuild.Text("Txt_Round", roundBox.transform, "", 32, Vector2.zero, Vector2.one,
                TextAlignmentOptions.Center, UiBuild.Gold);
            var flee = UiBuild.Button("Btn_Flee", top, "도주", 22, new Vector2(.9f, .15f), new Vector2(.98f, .85f));

            // 진형
            var field = UiBuild.Image("Panel_Field", panel, new Vector2(0, .44f), new Vector2(1, .91f),
                new Color(0.08f, 0.09f, 0.12f)).transform;
            UiBuild.Image("Img_Ground", field, new Vector2(0, 0), new Vector2(1, .14f), new Color(0.06f, 0.06f, 0.08f));
            var partySlots = new CombatantSlotView[4];
            var enemySlots = new CombatantSlotView[4];
            const float slotW = .085f, step = .095f;
            for (int rank = 1; rank <= 4; rank++)
            {
                float px = .34f - (rank - 1) * step;   // 1번 칸이 가운데 쪽
                float ex = .575f + (rank - 1) * step;
                partySlots[rank - 1] = BuildSlot($"Btn_PartySlot{rank}", field, new Vector2(px, .06f), new Vector2(px + slotW, .94f));
                enemySlots[rank - 1] = BuildSlot($"Btn_EnemySlot{rank}", field, new Vector2(ex, .06f), new Vector2(ex + slotW, .94f));
            }

            var info = UiBuild.Text("Txt_Info", panel, "", 21, new Vector2(.02f, .395f), new Vector2(.98f, .44f),
                TextAlignmentOptions.Center);

            // 좌하단 모험가 패널
            var hero = BuildHeroPanel(panel);

            // 우하단: 가방 + 기록 / 몬스터 정보
            var side = UiBuild.Image("Panel_Side", panel, new Vector2(.505f, .015f), new Vector2(.99f, .39f), UiBuild.PanelColor).transform;
            var bagLog = UiBuild.Rect("Group_BagLog", side, Vector2.zero, Vector2.one);
            var bag = BuildBag(bagLog);
            var logBg = UiBuild.Image("Panel_Log", bagLog, new Vector2(.02f, .04f), new Vector2(.98f, .44f), new Color(0, 0, 0, 0.35f));
            var log = UiBuild.Text("Txt_Log", logBg.transform, "", 16, new Vector2(.015f, .03f), new Vector2(.985f, .97f),
                TextAlignmentOptions.BottomLeft);
            var detail = BuildInfoPanel(side);
            detail.gameObject.SetActive(false);

            // 결과
            var result = UiBuild.Image("Panel_BattleResult", panel, new Vector2(.35f, .42f), new Vector2(.65f, .78f),
                new Color(0.1f, 0.09f, 0.08f, 0.97f), true);
            var resultText = UiBuild.Text("Txt_Result", result.transform, "", 34, new Vector2(.05f, .35f), new Vector2(.95f, .95f),
                TextAlignmentOptions.Center, UiBuild.Gold);
            var restart = UiBuild.Button("Btn_Restart", result.transform, "새 전투", 26, new Vector2(.3f, .08f), new Vector2(.7f, .28f));
            result.gameObject.SetActive(false);

            BuildEffects(panel, top, screen);
            var tip = BuildTooltip(panel);

            screen.Assign(round, turn, flee, info, partySlots, enemySlots, hero, bagLog.gameObject, bag, log, detail, tip,
                result.gameObject, resultText, restart);

            var boot = new GameObject("BattleTestBootstrap").AddComponent<BattleTestBootstrap>();
            boot.Assign(screen);
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(boot);
        }

        private static CombatantSlotView BuildSlot(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var hit = UiBuild.Image(name, parent, min, max, new Color(1, 1, 1, 0.001f), true);
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            var t = hit.transform;
            var frame = UiBuild.Image("Img_Frame", t, new Vector2(-.04f, .25f), new Vector2(1.04f, 1.02f), Color.clear);
            var body = UiBuild.Image("Img_Body", t, new Vector2(.06f, .28f), new Vector2(.94f, 1f), new Color(0.25f, 0.3f, 0.38f));
            var nameText = UiBuild.Text("Txt_Name", body.transform, "", 20, new Vector2(.05f, .05f), new Vector2(.95f, .95f),
                TextAlignmentOptions.Center);
            var hpBar = UiBuild.Image("Img_HpBar", t, new Vector2(.08f, .19f), new Vector2(.92f, .24f), new Color(0.12f, 0.05f, 0.05f));
            var hpFill = UiBuild.Image("Img_HpFill", hpBar.transform, Vector2.zero, Vector2.one, new Color(0.75f, 0.12f, 0.1f));
            var hpText = UiBuild.Text("Txt_Hp", t, "", 17, new Vector2(0, .09f), new Vector2(1, .19f), TextAlignmentOptions.Center);
            var status = UiBuild.Text("Txt_Status", t, "", 17, new Vector2(0, 0), new Vector2(1, .09f), TextAlignmentOptions.Center);
            status.raycastTarget = true;
            status.gameObject.AddComponent<HoverRelay>();
            var view = hit.gameObject.AddComponent<CombatantSlotView>();
            view.Assign(button, frame, body, nameText, hpFill, hpText, status);
            return view;
        }

        private static HeroPanelView BuildHeroPanel(Transform panel)
        {
            var p = UiBuild.Image("Panel_Hero", panel, new Vector2(.01f, .015f), new Vector2(.495f, .39f), UiBuild.PanelColor).transform;
            var portrait = UiBuild.Image("Img_Portrait", p, new Vector2(.02f, .62f), new Vector2(.12f, .95f), new Color(0.3f, 0.2f, 0.15f));
            var name = UiBuild.Text("Txt_HeroName", p, "", 28, new Vector2(.14f, .8f), new Vector2(.45f, .96f),
                TextAlignmentOptions.MidlineLeft, UiBuild.Gold);
            var cls = UiBuild.Text("Txt_HeroClass", p, "", 17, new Vector2(.14f, .63f), new Vector2(.45f, .8f),
                TextAlignmentOptions.MidlineLeft, new Color(0.65f, 0.63f, 0.6f));

            var skills = new Button[4];
            const float bx = .46f, bw = .083f, gap = .004f;
            for (int i = 0; i < 4; i++)
            {
                float x0 = bx + i * (bw + gap);
                skills[i] = UiBuild.Button($"Btn_Skill{i + 1}", p, "", 17, new Vector2(x0, .64f), new Vector2(x0 + bw, .95f));
                skills[i].gameObject.AddComponent<HoverRelay>();
            }
            float mx = bx + 4 * (bw + gap);
            var move = UiBuild.Button("Btn_Move", p, "이동", 17, new Vector2(mx, .64f), new Vector2(mx + bw, .95f));
            move.gameObject.AddComponent<HoverRelay>();
            var pass = UiBuild.Button("Btn_Pass", p, "넘기기", 17, new Vector2(mx + bw + gap, .64f), new Vector2(.98f, .95f));

            UiBuild.Divider(p, new Vector2(.02f, .595f), new Vector2(.98f, .6f));
            var stats = UiBuild.Text("Txt_HeroStats", p, "", 18, new Vector2(.03f, .03f), new Vector2(.4f, .58f));
            UiBuild.Divider(p, new Vector2(.42f, .05f), new Vector2(.423f, .56f));
            var effects = UiBuild.Text("Txt_HeroEffects", p, "", 17, new Vector2(.45f, .03f), new Vector2(.98f, .58f));

            var view = p.gameObject.AddComponent<HeroPanelView>();
            view.Assign(portrait, name, cls, skills, move, pass, stats, effects);
            return view;
        }

        private static BagView BuildBag(Transform parent)
        {
            var area = UiBuild.Rect("Panel_Bag", parent, new Vector2(.02f, .48f), new Vector2(.98f, .97f));
            var slots = new Button[12];
            for (int i = 0; i < 12; i++)
            {
                int col = i % 6, row = i / 6;
                var min = new Vector2(col / 6f + .005f, row == 0 ? .51f : .01f);
                var max = new Vector2((col + 1) / 6f - .005f, row == 0 ? .99f : .49f);
                var b = UiBuild.Button($"Btn_BagSlot{i + 1:00}", area, null, 0, min, max);
                UiBuild.Text("Txt_ItemName", b.transform, "", 16, new Vector2(.05f, .35f), new Vector2(.95f, .95f),
                    TextAlignmentOptions.Center);
                UiBuild.Text("Txt_ItemCount", b.transform, "", 20, new Vector2(.05f, .03f), new Vector2(.92f, .4f),
                    TextAlignmentOptions.BottomRight, UiBuild.Gold);
                slots[i] = b;
            }
            var view = area.gameObject.AddComponent<BagView>();
            view.Assign(slots);
            return view;
        }

        private static CombatantInfoView BuildInfoPanel(Transform side)
        {
            var p = UiBuild.Image("Panel_DetailInfo", side, Vector2.zero, Vector2.one, UiBuild.PanelColor).transform;
            var name = UiBuild.Text("Txt_InfoName", p, "", 26, new Vector2(.03f, .83f), new Vector2(.62f, .97f),
                TextAlignmentOptions.MidlineLeft);
            var hp = UiBuild.Text("Txt_InfoHp", p, "", 22, new Vector2(.62f, .83f), new Vector2(.88f, .97f),
                TextAlignmentOptions.MidlineRight);
            var close = UiBuild.Button("Btn_CloseInfo", p, "X", 22, new Vector2(.9f, .84f), new Vector2(.98f, .97f));
            UiBuild.Divider(p, new Vector2(.02f, .8f), new Vector2(.98f, .805f));
            var stats = UiBuild.Text("Txt_InfoStats", p, "", 16, new Vector2(.03f, .03f), new Vector2(.4f, .78f));
            UiBuild.Divider(p, new Vector2(.42f, .05f), new Vector2(.423f, .76f));
            var detail = UiBuild.Text("Txt_InfoDetail", p, "", 16, new Vector2(.45f, .03f), new Vector2(.97f, .78f));
            detail.gameObject.AddComponent<TmpLinkHover>();
            var view = p.gameObject.AddComponent<CombatantInfoView>();
            view.Assign(name, hp, stats, detail, close);
            return view;
        }

        /// <summary>떠오르는 글자 묶음(미리 12개)과 연출 속도 버튼.</summary>
        private static void BuildEffects(Transform panel, Transform top, BattleScreen screen)
        {
            var layerRect = UiBuild.Rect("Panel_FloatingText", panel, Vector2.zero, Vector2.one);
            var views = new FloatingTextView[12];
            for (int i = 0; i < views.Length; i++)
            {
                var text = UiBuild.Text($"Txt_Float{i + 1}", layerRect, "", 34, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    TextAlignmentOptions.Center);
                text.rectTransform.sizeDelta = new Vector2(320f, 60f);
                text.fontStyle = FontStyles.Bold;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                views[i] = text.gameObject.AddComponent<FloatingTextView>();
                views[i].Assign(text);
                text.gameObject.SetActive(false);
            }
            var layer = layerRect.gameObject.AddComponent<FloatingTextLayer>();
            layer.Assign(views);

            var speed = UiBuild.Button("Btn_Speed", top, "속도 ×1", 20, new Vector2(.8f, .15f), new Vector2(.885f, .85f));
            var label = speed.GetComponentInChildren<TMP_Text>();
            screen.AssignEffects(layer, speed, label);
        }

        private static TooltipView BuildTooltip(Transform panel)
        {
            var box = UiBuild.Image("Panel_Tooltip", panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Color(0.05f, 0.045f, 0.04f, 0.97f));
            var rect = box.rectTransform;
            rect.pivot = new Vector2(.5f, 0f);
            rect.sizeDelta = new Vector2(380f, 200f);
            var border = box.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.55f, 0.47f, 0.3f);
            border.effectDistance = new Vector2(2f, -2f);
            var text = UiBuild.Text("Txt_Tooltip", rect, "", 19, Vector2.zero, Vector2.one);
            text.rectTransform.offsetMin = new Vector2(14f, 14f);
            text.rectTransform.offsetMax = new Vector2(-14f, -14f);
            text.overflowMode = TextOverflowModes.Overflow;
            var view = box.gameObject.AddComponent<TooltipView>();
            view.Assign(rect, text);
            box.gameObject.SetActive(false);
            return view;
        }
    }
}
