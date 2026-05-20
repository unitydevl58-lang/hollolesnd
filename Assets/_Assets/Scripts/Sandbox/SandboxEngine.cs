using UnityEngine;

namespace HoloLensApp.Sandbox
{
    public class SandboxEngine : MonoBehaviour
    {
        public static SandboxEngine Instance { get; private set; }

        [Header("Selection State")]
        public SandboxObject shapeA;
        public SandboxObject shapeB;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void ToggleSelection(SandboxObject obj)
        {
            if (obj == null) return;

            if (obj == shapeA)
            {
                shapeA.SetSelected(false);
                shapeA = null;
                return;
            }
            if (obj == shapeB)
            {
                shapeB.SetSelected(false);
                shapeB = null;
                return;
            }

            if (shapeA == null)
            {
                shapeA = obj;
                shapeA.SetSelected(true);
            }
            else if (shapeB == null)
            {
                shapeB = obj;
                shapeB.SetSelected(true);
            }
            else
            {
                // Deselect A, shift B to A, assign new to B
                shapeA.SetSelected(false);
                shapeA = shapeB;
                shapeB = obj;
                shapeB.SetSelected(true);
            }
        }

        public void ProcessCommand(string commandText)
        {
            // First check if it's a bake command
            string lower = commandText.ToLowerInvariant();
            if (lower.Contains("bake") || lower.Contains("kaydet") || lower.Contains("grup yap"))
            {
                if (shapeA != null) SandboxBaker.BakeModel(shapeA.gameObject);
                if (shapeB != null) SandboxBaker.BakeModel(shapeB.gameObject);
                
                // Also trigger blueprint check
                if (BlueprintManager.Instance != null && shapeA != null)
                {
                    BlueprintManager.Instance.CheckAssemblySuccess(shapeA.gameObject);
                }
                return;
            }

            WongOperation op = NLPCommandParser.ParseCommand(commandText);
            
            if (op == WongOperation.Unknown)
            {
                Debug.LogWarning($"[SandboxEngine] Unknown command: {commandText}");
                return;
            }

            if (shapeA == null || shapeB == null)
            {
                Debug.LogWarning("[SandboxEngine] Please select exactly TWO shapes to apply a form interaction.");
                return;
            }

            Debug.Log($"[SandboxEngine] Applying {op} to {shapeA.name} and {shapeB.name}");
            FormInteractionSolver.ApplyOperation(op, shapeA.gameObject, shapeB.gameObject);

            // Re-eval materials as CSG might have destroyed RHS or modified LHS
            if (shapeA != null && shapeA.gameObject != null) shapeA.UpdateOriginalMaterials();
            
            // Clean up missing references if CSG destroyed B
            if (shapeB == null || shapeB.gameObject == null)
            {
                shapeB = null;
            }
        }
    }
}
