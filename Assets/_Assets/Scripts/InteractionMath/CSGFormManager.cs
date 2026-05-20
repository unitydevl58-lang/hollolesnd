using System.Threading.Tasks;
using UnityEngine;
using HoloLensApp.Interaction.Math;
using HoloLensApp.Interaction.Snapping;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HoloLensApp.Interaction.CSG
{
    /// <summary>
    /// Performs runtime mesh booleans and spawns interactable forms.
    /// </summary>
    public class CSGFormManager : MonoBehaviour
    {
        public static CSGFormManager Instance { get; private set; }

        [Header("CSG")]
        [SerializeField] private CSGMaterialLibrary materialLibrary;

        private ICSGProvider _csgProvider;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _csgProvider = GetComponent<ICSGProvider>();

            if (_csgProvider == null)
                _csgProvider = gameObject.AddComponent<PbCSGProvider>();

            if (materialLibrary == null)
                materialLibrary = CSGMaterialLibrary.Instance ?? FindAnyObjectByType<CSGMaterialLibrary>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetProvider(ICSGProvider provider)
        {
            _csgProvider = provider;
        }

        public async void ProcessCSGOperation(
            GameObject objA,
            GameObject objB,
            CSGOperationType operationType,
            bool destroyOperands = true)
        {
            if (objA == null || objB == null)
            {
                Debug.LogError("[CSGFormManager] Operand objects cannot be null.");
                return;
            }

            if (_csgProvider == null)
            {
                Debug.LogError("[CSGFormManager] No ICSGProvider assigned.");
                return;
            }

            MeshFilter filterA = objA.GetComponent<MeshFilter>();
            MeshFilter filterB = objB.GetComponent<MeshFilter>();

            if (filterA == null || filterB == null || filterA.sharedMesh == null || filterB.sharedMesh == null)
            {
                Debug.LogError("[CSGFormManager] Both operands require MeshFilter + mesh.");
                return;
            }

            Vector3 spawnPosition = (objA.transform.position + objB.transform.position) * 0.5f;
            WongInteractionState preState = ShapeInteractionManager.EvaluatePairStatic(objA, objB);

            Mesh resultingMesh = await _csgProvider.PerformCSGAsync(objA, objB, operationType);
            if (resultingMesh == null)
            {
                Debug.LogWarning("[CSGFormManager] CSG returned null mesh.");
                return;
            }

            GameObject result = CreatePhysicalForm(resultingMesh, spawnPosition, operationType, preState);

            if (destroyOperands)
            {
                Destroy(objA);
                Destroy(objB);
            }

            GeminiConnection gemini = FindAnyObjectByType<GeminiConnection>();
            gemini?.SendPhysicsResultToLLM(
                $"CSG {operationType} tamamlandı. Wong durumu: {preState}. Sonuç: {result.name}");
        }

        private GameObject CreatePhysicalForm(
            Mesh mesh,
            Vector3 spawnPosition,
            CSGOperationType operationType,
            WongInteractionState wongState)
        {
            GameObject newForm = new GameObject($"CSG_{operationType}_{wongState}");
            newForm.transform.position = spawnPosition;

            MeshFilter meshFilter = newForm.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer renderer = newForm.AddComponent<MeshRenderer>();
            if (materialLibrary != null)
            {
                CSGMaterialProfile profile = materialLibrary.ProfileForOperation(operationType);
                materialLibrary.ApplyProfile(renderer, profile);
            }

            MeshCollider collider = newForm.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;

            Rigidbody rb = newForm.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            XRGrabInteractable grab = newForm.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;

            GridSnapper grid = newForm.AddComponent<GridSnapper>();
            grid.CellFootprint = Vector3Int.one;

            FormInteractable form = newForm.AddComponent<FormInteractable>();
            ShapeInteractionManager.Instance?.RegisterForm(form);

            return newForm;
        }
    }
}
