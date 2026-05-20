#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using HoloLensApp.Sandbox;
using UnityEngine.XR.Interaction.Toolkit;

namespace HoloLensApp.Editor
{
    public class SandboxSetupMenu
    {
        [MenuItem("Showcase/Setup Sandbox Scene")]
        public static void SetupSandboxScene()
        {
            // Prevent duplicates
            GameObject oldEngine = GameObject.Find("SandboxEngine");
            if (oldEngine != null) Object.DestroyImmediate(oldEngine);
            GameObject oldSpawn = GameObject.Find("SpawnPoint");
            if (oldSpawn != null) Object.DestroyImmediate(oldSpawn);

            // Create Core Manager
            GameObject sandboxCore = new GameObject("SandboxEngine");
            sandboxCore.AddComponent<SandboxEngine>();
            var bpManager = sandboxCore.AddComponent<BlueprintManager>();
            var uiManager = sandboxCore.AddComponent<SandboxUIManager>();

            // Setup Spawn Point
            GameObject spawnPoint = new GameObject("SpawnPoint");
            spawnPoint.transform.position = new Vector3(0, 1.2f, 1.5f);
            uiManager.spawnPoint = spawnPoint.transform;

            // Clean up legacy custom canvases
            GameObject oldCanvas = GameObject.Find("Sandbox_MainCanvas");
            if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);
            GameObject oldCanvasA = GameObject.Find("Sandbox_CanvasA_Creation");
            if (oldCanvasA != null) Object.DestroyImmediate(oldCanvasA);
            GameObject oldCanvasB = GameObject.Find("Sandbox_CanvasB_Terminal");
            if (oldCanvasB != null) Object.DestroyImmediate(oldCanvasB);

            // Inject GlobalHUDManager for pristine XR interaction
            if (Object.FindAnyObjectByType<Showcase.UI.GlobalHUDManager>() == null)
            {
                GameObject hudObj = new GameObject("GlobalHUDManager");
                hudObj.AddComponent<Showcase.UI.GlobalHUDManager>();
            }

            // Add Directional Light
            GameObject oldLight = GameObject.Find("Directional Light");
            if (oldLight != null) Object.DestroyImmediate(oldLight);
            
            GameObject dirLight = new GameObject("Directional Light");
            Light lightComp = dirLight.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            dirLight.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Set Camera to use Native Unity Skybox (Fixes black screen properly)
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.Skybox;
            }

            // Remove fake SkyDome
            GameObject oldDome = GameObject.Find("SandboxSkyDome");
            if (oldDome != null) Object.DestroyImmediate(oldDome);

            // Final Polish
            Selection.activeGameObject = sandboxCore;
            Debug.Log("Sandbox Scene Managers Generated! Restored GlobalHUDManager and native Skybox.");
        }
    }
}
#endif
