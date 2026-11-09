using System.IO;
using Grimward.Presentation.Bootstrap;
using Grimward.Presentation.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grimward.Editor
{
    public static class M1Setup
    {
        [MenuItem("Grimward/M1/Create missing scene and config")]
        public static void BuildAssets()
        {
            if (!Directory.Exists("Assets/TextMesh Pro/Resources"))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
                UnityEditor.AssetPackage.Package.Import(Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage"), false);
            }
            // 기존 사용자 씬과 설정은 덮어쓰지 않는다.
            Directory.CreateDirectory("Assets/_Project/Data");
            Directory.CreateDirectory("Assets/_Project/Scenes");
            const string configPath = "Assets/_Project/Data/BalanceConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<BalanceConfigAsset>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BalanceConfigAsset>();
                AssetDatabase.CreateAsset(config, configPath);
            }
            const string scenePath = "Assets/_Project/Scenes/Main.unity";
            if (!File.Exists(scenePath))
            {
                var active = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && active.isDirty)
                    throw new System.InvalidOperationException("현재 씬을 저장한 뒤 다시 실행하세요.");
                var mode = string.IsNullOrEmpty(active.path) ? NewSceneMode.Single : NewSceneMode.Additive;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
                var cameraObject = new GameObject("Main Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 0;
                var root = new GameObject("GameBootstrap");
                SceneManager.MoveGameObjectToScene(root, scene);
                var bootstrap = root.AddComponent<GameBootstrap>();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("balanceConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BalanceConfigAsset>(configPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, scenePath);
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Grimward M1 assets ready: " + scenePath);
        }
    }
}
