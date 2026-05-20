using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using HoloLensApp.Interaction.CSG;
using HoloLensApp.Interaction.Math;
using HoloLensApp.Interaction.Snapping;

namespace HoloLensApp.Core
{
    /// <summary>
    /// Persistent flow controller: additive core scenes + singleton managers.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public const string SceneCoreXR = "Core_XR_Setup";
        public const string SceneCoreManagers = "Core_Managers";
        public const string SceneEnvironment = "Environment_And_Logic";

        [Header("Additive Bootstrap")]
        [FormerlySerializedAs("autoLoadThesisScenesOnStart")]
        [SerializeField] private bool autoLoadCoreScenesOnStart = false;

        [Header("Legacy Scene Names")]
        [SerializeField] private string showcaseSceneName = "Showcase_Scene";
        [SerializeField] private string sandboxSceneName = "Sandbox_Scene";
        [SerializeField] private string mainMenuSceneName = "MainMenu_Scene";

        private bool _coreScenesLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (autoLoadCoreScenesOnStart)
                StartCoroutine(EnsureCoreArchitectureLoaded());
        }

        public void LoadCoreExperience()
        {
            StartCoroutine(EnsureCoreArchitectureLoaded());
        }

        public IEnumerator EnsureCoreArchitectureLoaded()
        {
            if (_coreScenesLoaded)
                yield break;

            yield return LoadAdditiveIfMissing(SceneCoreXR);
            yield return LoadAdditiveIfMissing(SceneCoreManagers);
            yield return LoadAdditiveIfMissing(SceneEnvironment);

            CoreManagersBootstrap.BootstrapManagers();
            _coreScenesLoaded = true;
        }

        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSingleSceneAsync(sceneName));
        }

        public void LoadShowcaseScene()
        {
            LoadScene(showcaseSceneName);
        }

        public void LoadSandboxScene()
        {
            LoadScene(sandboxSceneName);
        }

        public void LoadMainMenu()
        {
            LoadScene(mainMenuSceneName);
        }

        private IEnumerator LoadSingleSceneAsync(string sceneName)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone)
                yield return null;

            yield return EnsureCoreArchitectureLoaded();
        }

        private static IEnumerator LoadAdditiveIfMissing(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
                yield break;

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[GameManager] Scene '{sceneName}' is not in Build Settings.");
                yield break;
            }

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (op != null && !op.isDone)
                yield return null;
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>
    /// Ensures manager singletons exist when Core_Managers loads.
    /// </summary>
    public class CoreManagersBootstrap : MonoBehaviour
    {
        public static void BootstrapManagers()
        {
            if (FindAnyObjectByType<CoreManagersBootstrap>() != null)
                return;

            GameObject host = new GameObject("CoreManagersBootstrap");
            host.AddComponent<CoreManagersBootstrap>();
        }

        private void Awake()
        {
            EnsureManager<SpatialAlignmentManager>("SpatialAlignmentManager");
            EnsureManager<CSGMaterialLibrary>("CSGMaterialLibrary");
            EnsureManager<CSGFormManager>("CSGFormManager", go =>
            {
                if (go.GetComponent<PbCSGProvider>() == null)
                    go.AddComponent<PbCSGProvider>();
            });
            EnsureManager<ShapeInteractionManager>("ShapeInteractionManager");
            EnsureManager<GeminiManager>("GeminiManager");
            EnsureManager<MRGrabController>("MRGrabController");
            EnsureManager<SpatialFormPipeline>("SpatialFormPipeline");
        }

        private static void EnsureManager<T>(string objectName, System.Action<GameObject> configure = null)
            where T : Component
        {
            if (FindAnyObjectByType<T>() != null)
                return;

            GameObject go = new GameObject(objectName);
            go.AddComponent<T>();
            configure?.Invoke(go);

            DontDestroyOnLoad(go);
        }
    }
}
