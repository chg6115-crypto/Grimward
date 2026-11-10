using System.Collections.Generic;
using System.IO;
using System.Linq;
using Grimward.Presentation.Bootstrap;
using Grimward.Presentation.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Grimward.EditorTools
{
    /// <summary>M0 씬 생성과 게임 데이터 검증 메뉴.</summary>
    public static class GrimwardSetupMenu
    {
        private const string SceneFolder = "Assets/_Project/Scenes";

        private static readonly (string name, string title)[] PlaceholderScenes =
        {
            ("Title", "GRIMWARD"),
            ("Town", "마을 (M3에서 구현)"),
            ("Expedition", "탐험 (M2에서 구현)"),
        };

        [MenuItem("Grimward/M0 씬 만들기 (Title·Town·Expedition·BattleTest)")]
        public static void CreateScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(SceneFolder);
            var created = new List<string>();

            foreach (var (name, title) in PlaceholderScenes)
            {
                string path = $"{SceneFolder}/{name}.unity";
                if (File.Exists(path)) continue;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var go = new GameObject("Bootstrap");
                var boot = go.AddComponent<PlaceholderSceneBootstrap>();
                var so = new SerializedObject(boot);
                so.FindProperty("title").stringValue = title;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, path);
                created.Add(path);
            }

            string battlePath = $"{SceneFolder}/BattleTest.unity";
            if (!File.Exists(battlePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                new GameObject("BattleTestBootstrap").AddComponent<BattleTestBootstrap>();
                EditorSceneManager.SaveScene(scene, battlePath);
                created.Add(battlePath);
            }

            var order = new[] { "Title", "Town", "Expedition", "BattleTest" }
                .Select(n => $"{SceneFolder}/{n}.unity").ToList();
            var others = EditorBuildSettings.scenes.Where(s => !order.Contains(s.path));
            EditorBuildSettings.scenes = order.Select(p => new EditorBuildSettingsScene(p, true)).Concat(others).ToArray();

            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(battlePath);
            Debug.Log(created.Count == 0 ? "Grimward: 씬이 이미 모두 있습니다. Build Settings만 갱신했습니다."
                : "Grimward: 씬 생성 — " + string.Join(", ", created));
        }

        [MenuItem("Grimward/게임 데이터 검증")]
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
    }
}
