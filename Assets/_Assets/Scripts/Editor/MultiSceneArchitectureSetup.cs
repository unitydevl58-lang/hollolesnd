#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using HoloLensApp.Core;
using HoloLensApp.Interaction.CSG;
using HoloLensApp.Interaction.Math;
using HoloLensApp.Interaction.Snapping;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HoloLensApp.Editor
{
    public static class MultiSceneArchitectureSetup
    {
        private const string ScenesFolder = "Assets/_Assets/Scenes";

        // Top bar: Thesis / HoloLensApp. Also under Assets (easier to find).
        [MenuItem("Thesis/Create Multi-Scene Architecture", false, 0)]
        [MenuItem("HoloLensApp/Create Multi-Scene Architecture", false, 0)]
        [MenuItem("Assets/Create/HoloLens Multi-Scene Architecture", false, 21)]
        public static void CreateMultiSceneArchitecture()
        {
            Directory.CreateDirectory(ScenesFolder);

            string xrPath = Path.Combine(ScenesFolder, "Core_XR_Setup.unity");
            string managersPath = Path.Combine(ScenesFolder, "Core_Managers.unity");
            string envPath = Path.Combine(ScenesFolder, "Environment_And_Logic.unity");

            CreateCoreXRScene(xrPath);
            CreateCoreManagersScene(managersPath);
            CreateEnvironmentScene(envPath);

            var buildScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(xrPath, true),
                new EditorBuildSettingsScene(managersPath, true),
                new EditorBuildSettingsScene(envPath, true)
            };

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path.Contains("MainMenu") || scene.path.Contains("Showcase") || scene.path.Contains("Sandbox"))
                    buildScenes.Add(scene);
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log("[HoloLensApp] Created Core_XR_Setup, Core_Managers, Environment_And_Logic and updated Build Settings.");
        }

        private static void CreateCoreXRScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject sim = new GameObject("XR Device Simulator");
            var simType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.XRDeviceSimulator, Unity.XR.Interaction.Toolkit.Samples.StarterAssets");
            if (simType != null)
                sim.AddComponent(simType);

            string prefabPath = "Assets/MRTemplateAssets/Prefabs/MR Interaction Setup.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                GameObject mr = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                mr.name = "MR Interaction Setup";
            }
            else
            {
                new GameObject("MR Interaction Setup (assign MRTemplate prefab)");
            }

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                var xrUi = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
                if (xrUi != null) es.AddComponent(xrUi);
            }

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateCoreManagersScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = new GameObject("Core_Managers");
            root.AddComponent<GameManager>();
            root.AddComponent<GeminiManager>();

            GameObject spatial = new GameObject("SpatialAlignmentManager");
            spatial.AddComponent<SpatialAlignmentManager>();

            GameObject materials = new GameObject("CSGMaterialLibrary");
            materials.AddComponent<CSGMaterialLibrary>();

            GameObject csg = new GameObject("CSGFormManager");
            csg.AddComponent<CSGFormManager>();
            csg.AddComponent<PbCSGProvider>();

            GameObject interaction = new GameObject("ShapeInteractionManager");
            interaction.AddComponent<ShapeInteractionManager>();

            GameObject gemini = new GameObject("GeminiConnection");
            gemini.AddComponent<GeminiConnection>();

            GameObject geo = new GameObject("GeometryManager");
            geo.AddComponent<GeometryManager>();

            GameObject mrGrab = new GameObject("MRGrabController");
            mrGrab.AddComponent<MRGrabController>();

            GameObject pipeline = new GameObject("SpatialFormPipeline");
            pipeline.AddComponent<SpatialFormPipeline>();

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateEnvironmentScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject envRoot = new GameObject("Environment_And_Logic");
            GameObject room = new GameObject("TestingRoom");
            room.transform.SetParent(envRoot.transform);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor_Grid";
            floor.transform.SetParent(room.transform);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);

            GameObject light = new GameObject("Directional Light");
            light.AddComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject inventory = new GameObject("FormInventory");
            inventory.transform.SetParent(envRoot.transform);

            CreateInventoryPrefabSlot(inventory.transform, "Prefab_Cube", PrimitiveType.Cube, new Vector3(-0.4f, 0.15f, 0.6f));
            CreateInventoryPrefabSlot(inventory.transform, "Prefab_Sphere", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0.6f));
            CreateInventoryPrefabSlot(inventory.transform, "Prefab_Cylinder", PrimitiveType.Cylinder, new Vector3(0.4f, 0.15f, 0.6f));

            GameObject sceneRoot = new GameObject("AI_Scene");
            sceneRoot.transform.SetParent(envRoot.transform);

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateInventoryPrefabSlot(Transform parent, string name, PrimitiveType primitive, Vector3 localPos)
        {
            GameObject slot = GameObject.CreatePrimitive(primitive);
            slot.name = name;
            slot.transform.SetParent(parent);
            slot.transform.localPosition = localPos;
            slot.transform.localScale = Vector3.one * 0.12f;
            slot.AddComponent<FormInteractable>();
            slot.AddComponent<GridSnapper>();
            Object.DestroyImmediate(slot.GetComponent<Collider>());
            slot.AddComponent<BoxCollider>();
        }
    }
}
#endif
