#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using HoloLensApp.Core;
using HoloLensApp.Interaction.CSG;
using HoloLensApp.Interaction.Math;
using HoloLensApp.Interaction.Snapping;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HoloLensApp.Editor
{
    /// <summary>
    /// Duplicates the designed thesis scenes and injects MR/XRI/backend components into the copies.
    /// Originals are never opened for modification.
    /// </summary>
    public sealed class SceneUpgraderBuilder : EditorWindow
    {
        private const string OutputFolder = "Assets/_Assets/Scenes/UpgradedMRScenes";
        private const string ManagersRootName = "[MANAGERS]";

        private bool _overwriteExistingCopies;
        private bool _addUpgradedScenesToBuildSettings = true;
        private bool _upgradePrimitiveProps = true;

        [MenuItem("Thesis/Scene Upgrader Builder", false, 10)]
        [MenuItem("HoloLensApp/Scene Upgrader Builder", false, 10)]
        public static void OpenWindow()
        {
            GetWindow<SceneUpgraderBuilder>("Scene Upgrader").minSize = new Vector2(440f, 220f);
        }

        [MenuItem("Thesis/Build Upgraded MR Scene Copies", false, 11)]
        public static void BuildUpgradedMRSceneCopiesMenu()
        {
            BuildUpgradedMRSceneCopies(overwriteExistingCopies: false, addToBuildSettings: true, upgradePrimitiveProps: true);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Scene Upgrader Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Duplicates MainMenu, Sandbox, and Sergi/Showcase scenes into UpgradedMRScenes, then injects HoloLens/XRI setup and thesis backend managers into the copies.",
                MessageType.Info);

            _overwriteExistingCopies = EditorGUILayout.ToggleLeft("Overwrite existing upgraded copies", _overwriteExistingCopies);
            _addUpgradedScenesToBuildSettings = EditorGUILayout.ToggleLeft("Add upgraded copies to Build Settings", _addUpgradedScenesToBuildSettings);
            _upgradePrimitiveProps = EditorGUILayout.ToggleLeft("Upgrade primitive collider props in Sandbox/Sergi", _upgradePrimitiveProps);

            GUILayout.Space(12f);

            if (GUILayout.Button("Duplicate and Upgrade Scenes", GUILayout.Height(36f)))
            {
                BuildUpgradedMRSceneCopies(_overwriteExistingCopies, _addUpgradedScenesToBuildSettings, _upgradePrimitiveProps);
            }
        }

        private static void BuildUpgradedMRSceneCopies(bool overwriteExistingCopies, bool addToBuildSettings, bool upgradePrimitiveProps)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            SceneUpgradeSpec[] specs = ResolveSceneSpecs();
            if (specs == null)
                return;

            EnsureFolder(OutputFolder);

            for (int i = 0; i < specs.Length; i++)
            {
                SceneUpgradeSpec spec = specs[i];
                string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(OutputFolder, Path.GetFileName(spec.SourcePath)).Replace("\\", "/"));

                if (overwriteExistingCopies)
                    targetPath = Path.Combine(OutputFolder, Path.GetFileName(spec.SourcePath)).Replace("\\", "/");

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
                {
                    if (!overwriteExistingCopies)
                    {
                        Debug.LogWarning($"[SceneUpgraderBuilder] Skipped existing upgraded scene copy: {targetPath}");
                        spec.TargetPath = targetPath;
                        specs[i] = spec;
                        continue;
                    }

                    AssetDatabase.DeleteAsset(targetPath);
                }

                if (!AssetDatabase.CopyAsset(spec.SourcePath, targetPath))
                {
                    Debug.LogError($"[SceneUpgraderBuilder] Could not copy scene '{spec.SourcePath}' to '{targetPath}'.");
                    return;
                }

                spec.TargetPath = targetPath;
                specs[i] = spec;
            }

            AssetDatabase.Refresh();

            int upgradedInteractables = 0;
            for (int i = 0; i < specs.Length; i++)
            {
                SceneUpgradeSpec spec = specs[i];
                if (string.IsNullOrEmpty(spec.TargetPath))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(spec.TargetPath, OpenSceneMode.Single);
                EnsureUniversalXRSetup();

                if (spec.InjectGameplayBackend)
                    upgradedInteractables += EnsureGameplayBackend(upgradePrimitiveProps);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (addToBuildSettings)
                AddScenesToBuildSettings(specs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneUpgraderBuilder] Upgraded MR scene copies saved in {OutputFolder}. Primitive interactables upgraded: {upgradedInteractables}.");
            EditorUtility.DisplayDialog("Scene Upgrader Builder", $"Upgraded MR scenes saved in:\n{OutputFolder}", "OK");
        }

        private static SceneUpgradeSpec[] ResolveSceneSpecs()
        {
            SceneUpgradeSpec[] specs =
            {
                new SceneUpgradeSpec("MainMenu", ResolveScenePath(new[] { "MainMenu", "MainMenu_Scene" }, new[] { "MainMenu" }), false),
                new SceneUpgradeSpec("Sandbox", ResolveScenePath(new[] { "Sandbox", "Sandbox_Scene" }, new[] { "Sandbox" }), true),
                new SceneUpgradeSpec("Sergi", ResolveScenePath(new[] { "Sergi", "Sergi_Scene", "Showcase", "Showcase_Scene" }, new[] { "Sergi", "Showcase" }), true)
            };

            for (int i = 0; i < specs.Length; i++)
            {
                if (!string.IsNullOrEmpty(specs[i].SourcePath))
                    continue;

                Debug.LogError($"[SceneUpgraderBuilder] Could not locate source scene for '{specs[i].Label}'.");
                EditorUtility.DisplayDialog(
                    "Scene Upgrader Builder",
                    $"Could not locate the source scene for '{specs[i].Label}'. Expected MainMenu, Sandbox, and Sergi/Showcase scenes.",
                    "OK");
                return null;
            }

            return specs;
        }

        private static string ResolveScenePath(string[] exactNames, string[] fallbackContains)
        {
            string[] guids = AssetDatabase.FindAssets("t:SceneAsset");
            List<string> scenePaths = new List<string>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains("/Samples/", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains("/_Recovery/", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains("/UpgradedMRScenes/", StringComparison.OrdinalIgnoreCase))
                {
                    scenePaths.Add(path);
                }
            }

            string preferred = FindSceneByExactName(scenePaths, exactNames, requireProjectSceneFolder: true);
            if (!string.IsNullOrEmpty(preferred))
                return preferred;

            preferred = FindSceneByExactName(scenePaths, exactNames, requireProjectSceneFolder: false);
            if (!string.IsNullOrEmpty(preferred))
                return preferred;

            preferred = FindSceneByContains(scenePaths, fallbackContains, requireProjectSceneFolder: true);
            if (!string.IsNullOrEmpty(preferred))
                return preferred;

            return FindSceneByContains(scenePaths, fallbackContains, requireProjectSceneFolder: false);
        }

        private static string FindSceneByExactName(List<string> scenePaths, string[] exactNames, bool requireProjectSceneFolder)
        {
            for (int i = 0; i < exactNames.Length; i++)
            {
                for (int j = 0; j < scenePaths.Count; j++)
                {
                    string path = scenePaths[j];
                    if (requireProjectSceneFolder && !path.StartsWith("Assets/_Assets/Scenes/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.Equals(Path.GetFileNameWithoutExtension(path), exactNames[i], StringComparison.OrdinalIgnoreCase))
                        return path;
                }
            }

            return null;
        }

        private static string FindSceneByContains(List<string> scenePaths, string[] containsTokens, bool requireProjectSceneFolder)
        {
            for (int i = 0; i < containsTokens.Length; i++)
            {
                for (int j = 0; j < scenePaths.Count; j++)
                {
                    string path = scenePaths[j];
                    if (requireProjectSceneFolder && !path.StartsWith("Assets/_Assets/Scenes/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (Path.GetFileNameWithoutExtension(path).IndexOf(containsTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return path;
                }
            }

            return null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static void EnsureUniversalXRSetup()
        {
            XROrigin xrOrigin = EnsureXROrigin();
            Camera xrCamera = EnsureXRCamera(xrOrigin);
            NormalizeXROriginForSimulation(xrOrigin);

            EnsureXRInteractionManager();
            EnsureEventSystemForXRUI();
            EnsureCanvasesSupportTrackedDevices();
            EnsureXRDeviceSimulator(xrCamera);
        }

        private static XROrigin EnsureXROrigin()
        {
            XROrigin xrOrigin = FindFirstSceneObject<XROrigin>();
            if (xrOrigin != null)
                return xrOrigin;

            GameObject prefab = LoadFirstPrefab(
                "Assets/MRTemplateAssets/Prefabs/MR Interaction Setup.prefab",
                "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab",
                "Assets/Samples/XR Interaction Toolkit/3.4.1/AR Starter Assets/Prefabs/XR Origin (AR Rig).prefab");

            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    instance.name = prefab.name;
                    xrOrigin = instance.GetComponentInChildren<XROrigin>(true);
                    if (xrOrigin != null)
                        return xrOrigin;
                }
            }

            GameObject originObject = new GameObject("XR Origin (MR)");
            xrOrigin = originObject.AddComponent<XROrigin>();
            xrOrigin.Origin = originObject;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Unbounded;

            GameObject offsetObject = new GameObject("Camera Offset");
            offsetObject.transform.SetParent(originObject.transform, false);
            xrOrigin.CameraFloorOffsetObject = offsetObject;

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(offsetObject.transform, false);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            AddTrackedPoseDriverIfAvailable(cameraObject);
            xrOrigin.Camera = camera;

            return xrOrigin;
        }

        private static void NormalizeXROriginForSimulation(XROrigin xrOrigin)
        {
            if (xrOrigin == null)
                return;

            xrOrigin.CameraYOffset = 0f;
            if (xrOrigin.CameraFloorOffsetObject != null)
                xrOrigin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
        }

        private static Camera EnsureXRCamera(XROrigin xrOrigin)
        {
            Camera camera = xrOrigin != null ? xrOrigin.Camera : null;

            if (camera == null && xrOrigin != null)
                camera = xrOrigin.GetComponentInChildren<Camera>(true);

            if (camera == null)
                camera = Camera.main;

            if (camera == null && xrOrigin != null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                Transform parent = xrOrigin.CameraFloorOffsetObject != null
                    ? xrOrigin.CameraFloorOffsetObject.transform
                    : xrOrigin.transform;
                cameraObject.transform.SetParent(parent, false);
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                AddTrackedPoseDriverIfAvailable(cameraObject);
            }

            if (camera == null)
                return null;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            if (xrOrigin != null && xrOrigin.Camera == null)
                xrOrigin.Camera = camera;

            AddTrackedPoseDriverIfAvailable(camera.gameObject);
            return camera;
        }

        private static void EnsureXRInteractionManager()
        {
            if (FindFirstSceneObject<XRInteractionManager>() != null)
                return;

            GameObject manager = new GameObject("XR Interaction Manager");
            manager.AddComponent<XRInteractionManager>();
        }

        private static void EnsureEventSystemForXRUI()
        {
            EventSystem eventSystem = FindFirstSceneObject<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<XRUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }

        private static void EnsureCanvasesSupportTrackedDevices()
        {
            Canvas[] canvases = FindSceneObjects<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                    continue;

                if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static void EnsureXRDeviceSimulator(Camera xrCamera)
        {
            XRDeviceSimulator existingSimulator = FindFirstSceneObject<XRDeviceSimulator>();
            if (existingSimulator != null)
            {
                if (xrCamera != null)
                    existingSimulator.cameraTransform = xrCamera.transform;
                return;
            }

            GameObject prefab = LoadFirstPrefab(
                "Assets/_Assets/Prefabs/XR Device Simulator.prefab",
                "Assets/Samples/XR Interaction Toolkit/3.4.1/XR Device Simulator/XR Device Simulator.prefab");

            GameObject simulatorObject = null;
            if (prefab != null)
                simulatorObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (simulatorObject == null)
            {
                simulatorObject = new GameObject("XR Device Simulator");
                simulatorObject.AddComponent<XRDeviceSimulator>();
            }

            simulatorObject.name = "XR Device Simulator";
            XRDeviceSimulator simulator = simulatorObject.GetComponent<XRDeviceSimulator>();
            if (simulator != null && xrCamera != null)
                simulator.cameraTransform = xrCamera.transform;
        }

        private static int EnsureGameplayBackend(bool upgradePrimitiveProps)
        {
            GameObject managersRoot = GameObject.Find(ManagersRootName);
            if (managersRoot == null)
                managersRoot = new GameObject(ManagersRootName);

            EnsureComponent<ShapeInteractionManager>(managersRoot);
            HoloLensApp.Core.GameManager gameManager = EnsureComponent<HoloLensApp.Core.GameManager>(managersRoot);
            ConfigureGameManagerForSingleSceneUpgrade(gameManager);

            EnsureComponent<GeminiManager>(managersRoot);
            GeminiConnection geminiConnection = EnsureComponent<GeminiConnection>(managersRoot);
            EnsureComponent<SpatialAlignmentManager>(managersRoot);
            EnsureComponent<CSGMaterialLibrary>(managersRoot);
            PbCSGProvider csgProvider = EnsureComponent<PbCSGProvider>(managersRoot);
            CSGFormManager csgFormManager = EnsureComponent<CSGFormManager>(managersRoot);
            csgFormManager.SetProvider(csgProvider);

            GeometryManager geometryManager = EnsureComponent<GeometryManager>(managersRoot);
            WireGeminiConnection(geminiConnection, geometryManager);
            EnsureComponent<MRGrabController>(managersRoot);
            EnsureComponent<SpatialFormPipeline>(managersRoot);

            if (!upgradePrimitiveProps)
                return 0;

            return UpgradePrimitiveColliderProps();
        }

        private static void ConfigureGameManagerForSingleSceneUpgrade(HoloLensApp.Core.GameManager gameManager)
        {
            if (gameManager == null)
                return;

            SerializedObject serializedObject = new SerializedObject(gameManager);
            SerializedProperty autoLoadProperty = serializedObject.FindProperty("autoLoadCoreScenesOnStart");
            if (autoLoadProperty != null)
                autoLoadProperty.boolValue = false;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireGeminiConnection(GeminiConnection geminiConnection, GeometryManager geometryManager)
        {
            if (geminiConnection == null || geometryManager == null)
                return;

            SerializedObject serializedObject = new SerializedObject(geminiConnection);
            SerializedProperty geometryProperty = serializedObject.FindProperty("geometryManager");
            if (geometryProperty != null)
                geometryProperty.objectReferenceValue = geometryManager;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int UpgradePrimitiveColliderProps()
        {
            int upgradedCount = 0;
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                upgradedCount += UpgradePrimitiveColliderPropsRecursive(roots[i]);

            return upgradedCount;
        }

        private static int UpgradePrimitiveColliderPropsRecursive(GameObject gameObject)
        {
            int upgradedCount = 0;

            if (ShouldUpgradePrimitiveProp(gameObject))
            {
                if (gameObject.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody rb = gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                    rb.isKinematic = false;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                }

                XRGrabInteractable grabInteractable = EnsureComponent<XRGrabInteractable>(gameObject);
                grabInteractable.throwOnDetach = false;
                grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;

                EnsureComponent<FormInteractable>(gameObject);
                EnsureComponent<GridSnapper>(gameObject);
                upgradedCount++;
            }

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
                upgradedCount += UpgradePrimitiveColliderPropsRecursive(transform.GetChild(i).gameObject);

            return upgradedCount;
        }

        private static bool ShouldUpgradePrimitiveProp(GameObject gameObject)
        {
            if (gameObject == null || IsInsideExcludedHierarchy(gameObject))
                return false;

            if (gameObject.GetComponent<Collider>() == null)
                return false;

            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return false;

            if (gameObject.GetComponent<Renderer>() == null)
                return false;

            if (LooksLikeStaticSurface(gameObject))
                return false;

            string meshName = meshFilter.sharedMesh.name;
            return ContainsIgnoreCase(meshName, "cube") ||
                   ContainsIgnoreCase(meshName, "sphere") ||
                   ContainsIgnoreCase(meshName, "capsule") ||
                   ContainsIgnoreCase(meshName, "cylinder") ||
                   ContainsIgnoreCase(meshName, "plane") ||
                   ContainsIgnoreCase(meshName, "quad");
        }

        private static bool IsInsideExcludedHierarchy(GameObject gameObject)
        {
            Transform current = gameObject.transform;
            while (current != null)
            {
                GameObject currentObject = current.gameObject;
                if (currentObject.GetComponent<XROrigin>() != null ||
                    currentObject.GetComponent<XRInteractionManager>() != null ||
                    currentObject.GetComponent<EventSystem>() != null ||
                    currentObject.GetComponent<Canvas>() != null ||
                    currentObject.GetComponent<XRDeviceSimulator>() != null ||
                    string.Equals(currentObject.name, ManagersRootName, StringComparison.OrdinalIgnoreCase) ||
                    ContainsIgnoreCase(currentObject.name, "XR Origin") ||
                    ContainsIgnoreCase(currentObject.name, "MR Interaction Setup") ||
                    ContainsIgnoreCase(currentObject.name, "EventSystem") ||
                    ContainsIgnoreCase(currentObject.name, "XR Device Simulator") ||
                    ContainsIgnoreCase(currentObject.name, "XR Interaction Manager"))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool LooksLikeStaticSurface(GameObject gameObject)
        {
            string name = gameObject.name;
            return ContainsIgnoreCase(name, "floor") ||
                   ContainsIgnoreCase(name, "ground") ||
                   ContainsIgnoreCase(name, "wall") ||
                   ContainsIgnoreCase(name, "ceiling") ||
                   ContainsIgnoreCase(name, "terrain") ||
                   ContainsIgnoreCase(name, "sky") ||
                   ContainsIgnoreCase(name, "dome");
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
                component = gameObject.AddComponent<T>();

            return component;
        }

        private static T FindFirstSceneObject<T>() where T : UnityEngine.Object
        {
            T[] objects = FindSceneObjects<T>();
            return objects.Length > 0 ? objects[0] : null;
        }

        private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static GameObject LoadFirstPrefab(params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        private static void AddTrackedPoseDriverIfAvailable(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            Type trackedPoseDriverType = Type.GetType("UnityEngine.InputSystem.XR.TrackedPoseDriver, Unity.InputSystem");
            if (trackedPoseDriverType == null)
                trackedPoseDriverType = Type.GetType("UnityEngine.SpatialTracking.TrackedPoseDriver, UnityEngine.SpatialTracking");

            if (trackedPoseDriverType != null && gameObject.GetComponent(trackedPoseDriverType) == null)
                gameObject.AddComponent(trackedPoseDriverType);
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddScenesToBuildSettings(SceneUpgradeSpec[] specs)
        {
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = 0; i < specs.Length; i++)
            {
                string targetPath = specs[i].TargetPath;
                if (string.IsNullOrEmpty(targetPath))
                    continue;

                bool alreadyPresent = false;
                for (int j = 0; j < buildScenes.Count; j++)
                {
                    if (string.Equals(buildScenes[j].path, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        buildScenes[j].enabled = true;
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                    buildScenes.Add(new EditorBuildSettingsScene(targetPath, true));
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        private struct SceneUpgradeSpec
        {
            public readonly string Label;
            public readonly string SourcePath;
            public readonly bool InjectGameplayBackend;
            public string TargetPath;

            public SceneUpgradeSpec(string label, string sourcePath, bool injectGameplayBackend)
            {
                Label = label;
                SourcePath = sourcePath;
                InjectGameplayBackend = injectGameplayBackend;
                TargetPath = null;
            }
        }
    }
}
#endif
