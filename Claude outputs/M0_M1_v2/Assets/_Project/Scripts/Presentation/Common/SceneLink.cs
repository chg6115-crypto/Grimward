using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Grimward.Presentation.Common
{
    /// <summary>버튼을 누르면 지정한 씬으로 이동. (M0 임시 씬용)</summary>
    [RequireComponent(typeof(Button))]
    public sealed class SceneLink : MonoBehaviour
    {
        [SerializeField] private string sceneName;

        public string SceneName { get => sceneName; set => sceneName = value; }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            GetComponent<Button>().onClick.AddListener(() => SceneManager.LoadScene(sceneName));
        }
    }
}
