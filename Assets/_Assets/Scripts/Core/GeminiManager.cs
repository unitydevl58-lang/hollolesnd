using HoloLensApp.Interaction.CSG;
using HoloLensApp.Interaction.Math;
using TMPro;
using UnityEngine;

namespace HoloLensApp.Core
{
    /// <summary>
    /// Facade between UI/speech, Gemini API transport, geometry pipeline, and CSG operations.
    /// </summary>
    public class GeminiManager : MonoBehaviour
    {
        public static GeminiManager Instance { get; private set; }

        [SerializeField] private GeminiConnection geminiConnection;
        [SerializeField] private GeometryManager geometryManager;
        [SerializeField] private ShapeInteractionManager shapeInteractionManager;

        public GeminiConnection Connection => geminiConnection;
        public GeometryManager Geometry => geometryManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveReferences();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ResolveReferences()
        {
            if (geminiConnection == null)
                geminiConnection = FindAnyObjectByType<GeminiConnection>();

            if (geometryManager == null)
                geometryManager = FindAnyObjectByType<GeometryManager>();

            if (shapeInteractionManager == null)
                shapeInteractionManager = ShapeInteractionManager.Instance
                    ?? FindAnyObjectByType<ShapeInteractionManager>();

            if (geminiConnection != null && geometryManager != null)
            {
                // GeometryManager is assigned through GeminiConnection inspector in existing scenes.
            }
        }

        public void SetHUDReferences(TMP_InputField input, TMP_Text status, TMP_Text feedback)
        {
            if (geminiConnection != null)
                geminiConnection.SetHUDReferences(input, status, feedback);
        }

        public void SubmitText(string text)
        {
            if (geminiConnection != null)
                geminiConnection.SubmitFromHUD(text);
        }

        public void SubmitSpeech(string text)
        {
            if (geminiConnection != null)
                geminiConnection.SubmitSpeechInput(text);
        }

        public void RequestCSGOnLastTwoSceneObjects(CSGOperationType operation)
        {
            if (geometryManager == null)
            {
                Debug.LogError("[GeminiManager] GeometryManager missing.");
                return;
            }

            GameObject root = GameObject.Find("AI_Scene");
            if (root == null || root.transform.childCount < 2)
            {
                Debug.LogWarning("[GeminiManager] Need at least two generated forms for CSG.");
                return;
            }

            Transform a = root.transform.GetChild(root.transform.childCount - 2);
            Transform b = root.transform.GetChild(root.transform.childCount - 1);

            if (shapeInteractionManager != null)
                shapeInteractionManager.RequestCSG(a.gameObject, b.gameObject, operation);
            else if (CSGFormManager.Instance != null)
                CSGFormManager.Instance.ProcessCSGOperation(a.gameObject, b.gameObject, operation);
        }

        public void NotifyPhysicsResult(string summary)
        {
            geminiConnection?.SendPhysicsResultToLLM(summary);
        }
    }
}
