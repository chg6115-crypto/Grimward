using System;
using System.IO;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Presentation.Bootstrap;
using Grimward.Presentation.Config;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace Grimward.Editor
{
    // 검증용 복사본에서만 실행: -executeMethod Grimward.Editor.M2SmokeValidation.Run
    public static class M2SmokeValidation
    {
        private const string Key = "Grimward.M2Smoke";
        private const string ConfigPath = "Assets/_Project/Data/BalanceConfig.asset";
        private static string output;
        private static int frame;
        private static double readyAt;
        private static bool failed;
        private static GameBootstrap bootstrap;
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("검증용 복사본의 batchmode에서 실행하세요.");
            M1Setup.BuildAssets();
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-grimwardOutput");
            output = index >= 0 ? args[index+1] : Directory.GetCurrentDirectory();
            Directory.CreateDirectory(output);
            SessionState.SetString(Key+".Output",output);
            SessionState.SetString(Key+".ConfigText",File.ReadAllText(ConfigPath));
            SessionState.SetString(Key+".ConfigJson",EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<BalanceConfigAsset>(ConfigPath)));
            SessionState.SetInt(Key+".Phase",0);
            SessionState.SetBool(Key,true);
            Resume();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key,false)) return;
            output = SessionState.GetString(Key+".Output",Directory.GetCurrentDirectory());
            frame = 0;
            readyAt = EditorApplication.timeSinceStartup + 8;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }
        private static void OnLog(string message,string stack,LogType type)
        {
            if(type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if(stack.Contains("UnityEditor.Search."))
            {
                File.AppendAllText(Path.Combine(output,"EditorDiagnostics.txt"),message+"\n"+stack+"\n");
                return;
            }
            failed = true;
            File.AppendAllText(Path.Combine(output,"SmokeErrors.txt"),message+"\n"+stack+"\n");
        }
        private static void Configure(int phase)
        {
            var asset = AssetDatabase.LoadAssetAtPath<BalanceConfigAsset>(ConfigPath);
            EditorJsonUtility.FromJsonOverwrite(SessionState.GetString(Key+".ConfigJson",""),asset);
            var serialized = new SerializedObject(asset);
            void Number(string name,int value) => serialized.FindProperty("values."+name).intValue=value;
            void Ratio(string name,double value) => serialized.FindProperty("values."+name).doubleValue=value;
            if(phase > 0)
            {
                Ratio("PathEventChanceDayFirst",0);
                Ratio("PathEventChanceDayRevisit",phase == 1 ? 1 : 0);
                Ratio("MinHitChance",1);
                Ratio("MaxHitChance",1);
                if(phase == 1)
                {
                    Number("PlayerWeaponMin",100);
                    Number("PlayerWeaponMax",100);
                    Ratio("PlayerArmor",1);
                }
                else
                {
                    Number("GoblinAttackMin",100);
                    Number("GoblinAttackMax",100);
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity",OpenSceneMode.Single);
        }
        private static void Require(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message);
        }
        private static Button Button(string name)
        {
            var obj = GameObject.Find(name);
            Require(obj != null,"Missing UI: "+name);
            return obj.GetComponent<Button>();
        }
        private static void Click(string name)
        {
            var b = Button(name);
            Require(b.interactable,"Disabled button: "+name);
            ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current) { button=PointerEventData.InputButton.Left },ExecuteEvents.pointerClickHandler);
        }
        private static void WinBattles()
        {
            for(int i=0;bootstrap.State.Battle!=null && i<100;i++)
            {
                var enemy=bootstrap.State.Battle.Enemies.First(e=>e.IsAlive);
                Click($"Btn_Target_{enemy.Id:00}");
                Click("Btn_Attack");
            }
            Require(bootstrap.State.Battle==null && !bootstrap.State.IsDead,"Battles did not end in victory");
        }
        private static void CheckLocked()
        {
            Require(!Button("Btn_AutoReturn").interactable,"Return not locked");
            Require(!bootstrap.State.TryMove(1),"Domain movement not locked");
            foreach(var b in UnityEngine.Object.FindObjectsByType<Button>())
                if(b.name.StartsWith("Btn_Node_")) Require(!b.interactable,"Map input not locked");
        }
        private static void RunPhase(int phase)
        {
            bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            Require(bootstrap != null && bootstrap.State != null,"Bootstrap failed");
            Require(Application.targetFrameRate==60,"Frame rate");
            Require(EventSystem.current.GetComponent<InputSystemUIInputModule>()!=null,"Input System UI missing");
            var state=bootstrap.State;
            Require(state.Player.Fighter.Hp==32,"Initial HP");
            Require(GameObject.Find("Txt_Hp").GetComponent<TMP_Text>().text.Contains("32/32"),"HP label");
            if(phase==0)
            {
                Capture("M2_Exploration.png");
                Click("Btn_Node_01");
                Require(state.Battle?.Kind==BattleKind.Ambush,"Default seed ambush");
                CheckLocked();
                Click("Btn_Target_00");
                Click("Btn_Defend");
                Require(GameObject.Find("Txt_BattleLog").GetComponent<TMP_Text>().text.Contains("방어"),"Battle log");
                Capture("M2_Ambush.png");
                Click("Btn_Flee");
                Require(state.Battle==null && state.CurrentNodeId==0,"Ambush flee");
                Require(!state.Map.Edge(0).IsTraversed,"Flee marked new edge");
                Require(state.Time.MinuteOfDay==480,"Flee time");
                Require(state.Player.Fighter.Hp<32 && state.Player.Fighter.Hp>=1,"Flee HP");
            }
            else if(phase==1)
            {
                Click("Btn_Node_01");
                Click("Btn_Node_03");
                Require(state.Battle?.Kind==BattleKind.Node,"Node battle");
                CheckLocked();
                Click("Btn_Defend");
                Click("Btn_Target_00");
                Click("Btn_Attack");
                Capture("M2_NodeBattle.png");
                WinBattles();
                Require(state.Map.Node(3).IsEventCompleted,"Node not completed");
                Require(state.Time.MinuteOfDay==510,"Victory time");
                Click("Btn_AutoReturn");
                Require(state.IsReturning && state.Battle?.Kind==BattleKind.Ambush,"Auto return not suspended");
                Capture("M2_ReturnAmbush.png");
                WinBattles();
                Require(state.IsAtStart && !state.IsReturning,"Auto return did not resume");
                Require(state.Time.MinuteOfDay==630,"Return time");
                Require(Button("Btn_ManualReturn").interactable,"Manual return");
                Click("Btn_ManualReturn");
                Capture("M2_VictoryReturn.png");
            }
            else
            {
                Click("Btn_Node_01");
                Click("Btn_Node_03");
                Click("Btn_Defend");
                Require(state.IsDead,"Death not reached");
                Require(GameObject.Find("Panel_Death")!=null,"Death panel");
                Require(GameObject.Find("Panel_Battle")==null,"Battle remains after death");
                CheckLocked();
                Require(!state.TryAutoReturn() && !state.TryManualReturn(),"Dead return allowed");
                Require(!state.TryBattleAction(BattleAction.Flee),"Dead action allowed");
                Capture("M2_Death.png");
            }
            Require(!UnityEngine.Object.FindObjectsByType<TMP_Text>().Any(t =>
                t.isTextOverflowing && !t.name.EndsWith("Log")),"Visible UI text overflow");
        }
        private static void Tick()
        {
            try
            {
                if(EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    readyAt=EditorApplication.timeSinceStartup+8;
                    return;
                }
                int phase=SessionState.GetInt(Key+".Phase",0);
                if(!EditorApplication.isPlaying)
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode) return;
                    if(phase>=3 || failed) { Finish(!failed); return; }
                    if(EditorApplication.timeSinceStartup<readyAt) return;
                    Configure(phase);
                    frame=0;
                    EditorApplication.isPlaying=true;
                    return;
                }
                if(++frame<30) return;
                RunPhase(phase);
                SessionState.SetInt(Key+".Phase",phase+1);
                frame=0;
                readyAt=EditorApplication.timeSinceStartup+3;
                EditorApplication.isPlaying=false;
            }
            catch(Exception exception)
            {
                File.AppendAllText(Path.Combine(output,"SmokeErrors.txt"),exception+"\n");
                Finish(false);
            }
        }
        private static void Capture(string filename)
        {
            Canvas.ForceUpdateCanvases();
            var go=new GameObject("ValidationCamera");
            var camera=go.AddComponent<Camera>();
            var canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();
            var target=new RenderTexture(1280,720,24);
            camera.targetTexture=target;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;
            canvas.worldCamera=camera;
            canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous=RenderTexture.active;
            RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output,filename),image.EncodeToPNG());
            RenderTexture.active=previous;
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera=null;
            camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(go);
        }
        private static void Finish(bool pass)
        {
            SessionState.SetBool(Key,false);
            EditorApplication.update-=Tick;
            Application.logMessageReceived-=OnLog;
            var asset = AssetDatabase.LoadAssetAtPath<BalanceConfigAsset>(ConfigPath);
            EditorJsonUtility.FromJsonOverwrite(SessionState.GetString(Key+".ConfigJson",""),asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            EditorUtility.ClearDirty(asset);
            File.WriteAllText(ConfigPath,SessionState.GetString(Key+".ConfigText",""));
            AssetDatabase.ImportAsset(ConfigPath,ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            File.WriteAllText(Path.Combine(output,"SmokeResult.txt"),pass ?
                "PASS: default-seed ambush/flee, HP persistence, node victory, target selection, defend, return ambush/resume, death/input lock, Korean UI." : "FAIL: see SmokeErrors.txt");
            EditorApplication.Exit(pass?0:1);
        }
    }
}
