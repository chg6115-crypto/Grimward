using System;
using System.IO;
using System.Linq;
using Grimward.Presentation.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Grimward.Editor
{
    // 명령줄 검증 전용. -executeMethod Grimward.Editor.M1SmokeValidation.Run
    public static class M1SmokeValidation
    {
        private const string RunningKey = "Grimward.M1Smoke.Running";
        private static int frame;
        private static double readyAt;
        private static bool failed;
        private static string output;
        private static GameBootstrap bootstrap;

        public static void Run()
        {
            M1Setup.BuildAssets();
            output = Directory.GetCurrentDirectory();
            SessionState.SetString("Grimward.M1Smoke.Output", output);
            SessionState.SetBool(RunningKey, true);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity", OpenSceneMode.Single);
            Resume();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            output = SessionState.GetString("Grimward.M1Smoke.Output", Directory.GetCurrentDirectory());
            frame = 0;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= StartWhenReady;
            if (EditorApplication.isPlaying) EditorApplication.update += Tick;
            else
            {
                readyAt = EditorApplication.timeSinceStartup + 8;
                EditorApplication.update += StartWhenReady;
            }
            Application.logMessageReceived += OnLog;
        }

        private static void StartWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { readyAt = EditorApplication.timeSinceStartup + 8; return; }
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= StartWhenReady;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (stack.Contains("UnityEditor.Search."))
                {
                    File.AppendAllText(Path.Combine(output, "EditorDiagnostics.txt"), condition + "\\n" + stack);
                    return;
                }
                failed = true;
                File.AppendAllText(Path.Combine(output, "SmokeErrors.txt"), condition + "\n" + stack + "\n");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static Button Button(string name)
        {
            var obj = GameObject.Find(name);
            Require(obj != null, "Missing UI: " + name);
            return obj.GetComponent<Button>();
        }

        private static void Click(string name)
        {
            var button = Button(name);
            Require(button.interactable, "Not interactable: " + name);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }

        private static void Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            var cameraObject = new GameObject("Panel_ValidationCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            var target = new RenderTexture(1280,720,24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
            RenderTexture.active = previous;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            frame++;
            if (frame < 30) return;
            try
            {
                if (frame == 30)
                {
                    bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
                    Require(bootstrap != null && bootstrap.State != null, "Bootstrap failed");
                    Require(EventSystem.current.GetComponent<InputSystemUIInputModule>() != null, "Input System module missing");
                    Require(Application.targetFrameRate == 60, "Frame rate");
                    Require(GameObject.Find("Btn_Node_02") == null, "Undiscovered node visible");
                    Require(Button("Btn_ManualReturn").gameObject.activeSelf, "Manual return missing");
                    Require(GameObject.Find("Txt_Time").GetComponent<TMP_Text>().text.Contains("06:00"), "Initial time");
                    Capture("M1_Initial.png");
                    Click("Btn_Node_01"); Click("Btn_Node_02");
                    Require(bootstrap.State.CurrentNodeId == 2, "Button movement failed");
                    Require(bootstrap.State.Time.MinuteOfDay == 480, "Movement time");
                    Require(GameObject.Find("Btn_ManualReturn") == null, "Manual return visibility");
                    Capture("M1_Exploration.png");
                    Click("Btn_AutoReturn");
                    Require(bootstrap.State.IsAtStart && bootstrap.State.Time.MinuteOfDay == 540, "Auto return failed");
                    Click("Btn_ManualReturn");
                    Click("Btn_Node_01"); Click("Btn_Node_03"); Click("Btn_Node_05");
                    Require(bootstrap.State.FindReturnPath() == null, "Incomplete node should block return");
                    int before = bootstrap.State.Time.MinuteOfDay;
                    Click("Btn_AutoReturn");
                    Require(bootstrap.State.Time.MinuteOfDay == before, "Blocked return changed time");
                    Capture("M1_BlockedReturn.png");
                    // 밤·날짜 변경도 UI 버튼을 통해 진행.
                    for (int i = 0; i < 40; i++) Click(i % 2 == 0 ? "Btn_Node_03" : "Btn_Node_05");
                    Require(bootstrap.State.Time.Day > 1, "Day rollover");
                    Require(GameObject.Find("Txt_Time").GetComponent<TMP_Text>().text.Contains("2일차"), "Time label stale");
                    Require(!UnityEngine.Object.FindObjectsByType<TMP_Text>().Any(t => t.isTextOverflowing && t.name != "Txt_Log"), "Text overflow");
                }
                if (frame < 40) return;
                File.WriteAllText(Path.Combine(output, "SmokeResult.txt"), failed ? "FAIL: see SmokeErrors.txt" : "PASS: Main scene, Input System UI, fog, movement, auto/manual return, blocked return, day rollover, TMP text.");
                Finish(failed ? 1 : 0);
            }
            catch (Exception exception)
            {
                File.AppendAllText(Path.Combine(output, "SmokeErrors.txt"), exception.ToString());
                File.WriteAllText(Path.Combine(output, "SmokeResult.txt"), "FAIL");
                Finish(1);
            }
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(RunningKey, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            EditorApplication.Exit(exitCode);
        }
    }
}
