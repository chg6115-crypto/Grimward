using Grimward.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grimward.Presentation.Bootstrap
{
    /// <summary>M0 빈 씬(Title·Town·Expedition) 표시. 씬 이름과 다른 씬으로 가는 버튼만 있다.</summary>
    public sealed class PlaceholderSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private string title = "Grimward";
        [SerializeField] private string[] links = { "Title", "Town", "Expedition", "BattleTest" };

        private void Start()
        {
            Application.targetFrameRate = 60;
            var font = FontProvider.Resolve(uiFont);
            var root = UiFactory.CreateCanvas("Canvas_" + SceneManager.GetActiveScene().name);
            var panel = UiFactory.Panel("Panel_" + SceneManager.GetActiveScene().name, root, Vector2.zero, Vector2.one,
                new Color(0.06f, 0.07f, 0.09f)).transform;
            UiFactory.Text("Txt_SceneTitle", panel, font, title, 64, new Vector2(.1f, .6f), new Vector2(.9f, .85f),
                TextAlignmentOptions.Center);
            for (int i = 0; i < links.Length; i++)
            {
                string scene = links[i];
                float x0 = .14f + i * .185f;
                UiFactory.Button("Btn_Go" + scene, panel, font, scene, 30, new Vector2(x0, .3f), new Vector2(x0 + .165f, .42f),
                    () => SceneManager.LoadScene(scene));
            }
        }
    }
}
