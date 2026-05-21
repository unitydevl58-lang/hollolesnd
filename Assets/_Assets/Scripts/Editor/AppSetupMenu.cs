#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace Showcase.Editor
{
    public class AppSetupMenu : MonoBehaviour
    {
        private const string MainMenuScenePath = "Assets/_Assets/Scenes/MainMenu_Scene.unity";

        private static readonly Color PastelPanelColor = new Color(0.984f, 0.973f, 0.992f, 0.94f);
        private static readonly Color PastelHeaderColor = new Color(0.965f, 0.949f, 0.984f, 0.96f);
        private static readonly Color PastelMintColor = new Color(0.635f, 0.835f, 0.776f, 1f);
        private static readonly Color PastelSalmonColor = new Color(0.957f, 0.643f, 0.573f, 1f);
        private static readonly Color PastelCoralColor = new Color(1f, 0.478f, 0.486f, 1f);
        private static readonly Color PastelTextColor = new Color(0.29f, 0.282f, 0.298f, 1f);
        private static readonly Color PastelMutedTextColor = new Color(0.56f, 0.535f, 0.59f, 1f);

        [MenuItem("Showcase/Setup App Architecture")]
        public static void SetupAppArchitecture()
        {
            // 1. Rename current scene to Showcase_Scene
            Scene currentScene = EditorSceneManager.GetActiveScene();
            string currentPath = currentScene.path;
            
            if (string.IsNullOrEmpty(currentPath))
            {
                Debug.LogError("Current scene is not saved! Please save the scene first.");
                return;
            }

            string folderPath = System.IO.Path.GetDirectoryName(currentPath);
            string showcasePath = System.IO.Path.Combine(folderPath, "Showcase_Scene.unity");
            string mainMenuPath = System.IO.Path.Combine(folderPath, "MainMenu_Scene.unity");
            string sandboxPath = System.IO.Path.Combine(folderPath, "Sandbox_Scene.unity");

            // Save current as Showcase_Scene
            EditorSceneManager.SaveScene(currentScene, showcasePath);
            
            // Generate Sandbox_Scene
            Scene sandboxScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            CreateGameManager();
            EditorSceneManager.SaveScene(sandboxScene, sandboxPath);

            // Generate MainMenu_Scene
            Scene mainMenuScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            CreateGameManager();
            CreateMainMenuUI();
            
            // Inject Global HUD into the first loaded scene
            if (Object.FindAnyObjectByType<Showcase.UI.GlobalHUDManager>() == null)
            {
                GameObject hudObj = new GameObject("GlobalHUDManager");
                hudObj.AddComponent<Showcase.UI.GlobalHUDManager>();
            }

            EditorSceneManager.SaveScene(mainMenuScene, mainMenuPath);

            // 3. Add to Build Settings
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
            buildScenes.Add(new EditorBuildSettingsScene(mainMenuPath, true));
            buildScenes.Add(new EditorBuildSettingsScene(showcasePath, true));
            buildScenes.Add(new EditorBuildSettingsScene(sandboxPath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();

            Debug.Log("[AppSetup] App Architecture Setup Complete! Built MainMenu, Showcase, and Sandbox scenes.");
        }

        private static void CreateGameManager()
        {
            if (Object.FindAnyObjectByType<GameManager>() == null)
            {
                GameObject gmObj = new GameObject("GameManager");
                gmObj.AddComponent<GameManager>();
            }
        }

        private static void CreateMainMenuUI()
        {
            // Create XR Origin if possible
            GameObject xrOrigin = new GameObject("XR Origin Placeholder");
            xrOrigin.transform.position = Vector3.zero;

            // Create Canvas
            GameObject canvasObj = new GameObject("MainMenu_Canvas");
            canvasObj.transform.position = new Vector3(0, 1.2f, 1.5f); // 1.5m in front, slightly below eye level (1.6m)
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 600);
            canvasObj.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f); // Scale down to fit 1.6x1.2 meters

            canvasObj.AddComponent<CanvasScaler>();
            
            // Tracked Device Graphic Raycaster (via reflection to avoid assembly issues if namespace differs)
            System.Type raycasterType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycasterType != null)
            {
                canvasObj.AddComponent(raycasterType);
            }
            else
            {
                canvasObj.AddComponent<GraphicRaycaster>(); // Fallback
                Debug.LogWarning("TrackedDeviceGraphicRaycaster not found. Using standard GraphicRaycaster.");
            }

            // Pastel Background Panel
            GameObject panelObj = new GameObject("BackgroundPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImg = panelObj.AddComponent<Image>();
            panelImg.color = PastelPanelColor;
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            // Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(canvasObj.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "DESIGN STUDIO HUB";
            titleText.fontSize = 54;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = PastelTextColor;
            titleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 210);
            titleRect.sizeDelta = new Vector2(800, 100);

            // Controller Script
            MainMenuController controller = canvasObj.AddComponent<MainMenuController>();

            // Create Buttons
            CreateStyledButton(canvasObj.transform, "Sergiyi Başlat", new Vector2(-300, 25), () => {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(canvasObj.GetComponentInChildren<Button>().onClick, controller.LoadShowcaseScene);
            }, PastelSalmonColor);
            CreateStyledButton(canvasObj.transform, "Sandbox Modu", new Vector2(0, 25), () => {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(canvasObj.GetComponentsInChildren<Button>()[1].onClick, controller.LoadSandboxScene);
            }, PastelMintColor);
            CreateStyledButton(canvasObj.transform, "Çıkış", new Vector2(300, 25), () => {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(canvasObj.GetComponentsInChildren<Button>()[2].onClick, controller.QuitApplication);
            }, PastelCoralColor);

            StyleMainMenuCanvas(canvasObj);
        }

        private static void CreateStyledButton(Transform parent, string labelText, Vector2 anchoredPosition, System.Action onCreated, Color? overrideColor = null)
        {
            GameObject btnObj = new GameObject($"Button_{labelText}");
            btnObj.transform.SetParent(parent, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(280, 78);
            btnRect.anchoredPosition = anchoredPosition;

            Image btnImg = btnObj.AddComponent<Image>();
            Color baseColor = overrideColor ?? PastelMintColor;
            btnImg.color = baseColor; 

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = btnImg.color;
            // Slightly darker pastel for highlight
            cb.highlightedColor = new Color(baseColor.r * 0.9f, baseColor.g * 0.9f, baseColor.b * 0.9f, 1f);
            cb.pressedColor = new Color(baseColor.r * 0.8f, baseColor.g * 0.8f, baseColor.b * 0.8f, 1f);
            btn.colors = cb;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = labelText;
            tmp.fontSize = 26;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = GetReadableButtonTextColor(baseColor);
            tmp.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            onCreated?.Invoke();
        }

        [MenuItem("Showcase/Apply Pastel Main Menu Style")]
        public static void ApplyPastelMainMenuStyle()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.name.Contains("MainMenu"))
            {
                if (!System.IO.File.Exists(MainMenuScenePath))
                {
                    Debug.LogError($"[AppSetup] Main menu scene not found at {MainMenuScenePath}.");
                    return;
                }

                scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            }

            GameObject canvasObj = GameObject.Find("MainMenu_Canvas");
            if (canvasObj == null)
            {
                CreateMainMenuUI();
                canvasObj = GameObject.Find("MainMenu_Canvas");
            }

            if (canvasObj == null)
            {
                Debug.LogError("[AppSetup] Could not create or find MainMenu_Canvas.");
                return;
            }

            StyleMainMenuCanvas(canvasObj);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AppSetup] Pastel Main Menu style applied to {scene.path}.");
        }

        private static void StyleMainMenuCanvas(GameObject canvasObj)
        {
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(980f, 620f);
            }

            canvasObj.transform.position = new Vector3(0f, 1.2f, 1.5f);
            canvasObj.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 20;
            }

            GameObject panelObj = EnsureChild(canvasObj.transform, "BackgroundPanel");
            panelObj.transform.SetSiblingIndex(0);
            ConfigureImagePanel(panelObj, PastelPanelColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ConfigureShadow(panelObj, new Color(0f, 0f, 0f, 0.16f), new Vector2(0f, -12f));

            GameObject headerObj = EnsureChild(canvasObj.transform, "HeaderPanel");
            headerObj.transform.SetSiblingIndex(1);
            ConfigureImagePanel(headerObj, PastelHeaderColor, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(860f, 82f));

            TextMeshProUGUI titleText = EnsureText(canvasObj.transform, "TitleText");
            titleText.text = "DESIGN STUDIO HUB";
            titleText.fontSize = 40f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 0f;
            titleText.color = PastelTextColor;
            titleText.alignment = TextAlignmentOptions.Left;
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(-228f, -64f);
            titleRect.sizeDelta = new Vector2(420f, 64f);

            TextMeshProUGUI subtitleText = EnsureText(canvasObj.transform, "SubtitleText");
            subtitleText.text = "Ana Menü";
            subtitleText.fontSize = 24f;
            subtitleText.fontStyle = FontStyles.Bold;
            subtitleText.color = PastelMutedTextColor;
            subtitleText.alignment = TextAlignmentOptions.Center;
            RectTransform subtitleRect = subtitleText.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
            subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
            subtitleRect.anchoredPosition = new Vector2(0f, 116f);
            subtitleRect.sizeDelta = new Vector2(540f, 50f);

            ConfigureMainMenuButton(canvasObj.transform, "Button_Sandbox Modu", "Sandbox Modu", new Vector2(-300f, 10f), PastelMintColor);
            ConfigureMainMenuButton(canvasObj.transform, "Button_Sergiyi Başlat", "Sergiyi Başlat", new Vector2(0f, 10f), PastelSalmonColor);
            ConfigureMainMenuButton(canvasObj.transform, "Button_Çıkış", "Çıkış", new Vector2(300f, 10f), PastelCoralColor);
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static TextMeshProUGUI EnsureText(Transform parent, string name)
        {
            GameObject textObj = EnsureChild(parent, name);
            TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = textObj.AddComponent<TextMeshProUGUI>();
            }

            return text;
        }

        private static void ConfigureImagePanel(GameObject obj, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = obj.AddComponent<RectTransform>();
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image image = obj.GetComponent<Image>();
            if (image == null)
            {
                image = obj.AddComponent<Image>();
            }

            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void ConfigureMainMenuButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Color baseColor)
        {
            GameObject buttonObj = EnsureChild(parent, objectName);
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            if (buttonRect == null)
            {
                buttonRect = buttonObj.AddComponent<RectTransform>();
            }

            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = new Vector2(270f, 78f);

            Image image = buttonObj.GetComponent<Image>();
            if (image == null)
            {
                image = buttonObj.AddComponent<Image>();
            }

            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = baseColor;
            image.raycastTarget = true;
            ConfigureShadow(buttonObj, new Color(0f, 0f, 0f, 0.18f), new Vector2(0f, -6f));

            Button button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObj.AddComponent<Button>();
            }

            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = LightenColor(baseColor, 1.08f);
            colors.pressedColor = DarkenColor(baseColor, 0.88f);
            colors.selectedColor = LightenColor(baseColor, 1.04f);
            colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic = image;

            TextMeshProUGUI labelText = EnsureText(buttonObj.transform, "Text");
            labelText.text = label;
            labelText.fontSize = 24f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = GetReadableButtonTextColor(baseColor);
            labelText.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = labelText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
        }

        private static void ConfigureShadow(GameObject obj, Color color, Vector2 distance)
        {
            Shadow shadow = obj.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = obj.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static Color GetReadableButtonTextColor(Color baseColor)
        {
            float luminance = (baseColor.r * 0.299f) + (baseColor.g * 0.587f) + (baseColor.b * 0.114f);
            return luminance > 0.72f ? PastelTextColor : Color.white;
        }

        private static Color LightenColor(Color color, float multiplier)
        {
            return new Color(Mathf.Clamp01(color.r * multiplier), Mathf.Clamp01(color.g * multiplier), Mathf.Clamp01(color.b * multiplier), color.a);
        }

        private static Color DarkenColor(Color color, float multiplier)
        {
            return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
        }

        [MenuItem("Showcase/Inject MR Interaction Setup")]
        public static void InjectMRInteractionSetup()
        {
            // 1. Clean existing setup
            GameObject oldSetup = GameObject.Find("MR Interaction Setup");
            if (oldSetup != null) DestroyImmediate(oldSetup);
            
            GameObject oldSetupClone = GameObject.Find("MR Interaction Setup(Clone)");
            if (oldSetupClone != null) DestroyImmediate(oldSetupClone);

            GameObject placeholder = GameObject.Find("XR Origin Placeholder");
            if (placeholder != null) DestroyImmediate(placeholder);
            
            if (Camera.main != null && Camera.main.gameObject.name == "Main Camera")
            {
                DestroyImmediate(Camera.main.gameObject);
            }

            // 2. Instantiate MR Interaction Setup prefab
            string prefabPath = "Assets/MRTemplateAssets/Prefabs/MR Interaction Setup.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (prefab != null)
            {
                GameObject mrSetup = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                mrSetup.transform.position = Vector3.zero;
                
                // Remove annoying MR Template tutorial scripts that cause NullReferences
                StripTemplateScripts(mrSetup.transform);

                // Add script to destroy the gray simulation platform
                if (mrSetup.GetComponent<Showcase.SimCleaner>() == null)
                {
                    mrSetup.AddComponent<Showcase.SimCleaner>();
                }

                // Ensure HandDisruptionField is added to hands automatically
                InjectHandDisruptionFields(mrSetup.transform);
            }
            else
            {
                Debug.LogError($"Could not find MR Interaction Setup prefab at {prefabPath}!");
            }

            // 3. Create EventSystem with XRUIInputModule
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                
                // Add XRUIInputModule using reflection
                System.Type xrInputType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
                if (xrInputType != null)
                {
                    eventSystemObj.AddComponent(xrInputType);
                }
                else
                {
                    eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>(); // Fallback
                    Debug.LogWarning("XRUIInputModule not found, using StandaloneInputModule.");
                }
            }

            Debug.Log("[AppSetup] MR Interaction Setup and EventSystem successfully injected into the scene!");
        }

        private static void InjectHandDisruptionFields(Transform root)
        {
            // Try to find the Left and Right controllers in the MR setup and add the disruption field
            // The exact names depend on the MR template, typically "Left Controller" / "Right Controller" 
            // or "LeftHand" / "RightHand"
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string name = t.name.ToLower();
                if ((name.Contains("left") || name.Contains("right")) && (name.Contains("controller") || name.Contains("hand")))
                {
                    // If it has an XRBaseController, it's definitely the right object
                    if (t.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRBaseController>() != null)
                    {
                        if (t.GetComponent<HandDisruptionField>() == null)
                        {
                            var field = t.gameObject.AddComponent<HandDisruptionField>();
                            
                            // Add a trigger sphere collider for haptic intersections
                            if (t.GetComponent<SphereCollider>() == null)
                            {
                                var col = t.gameObject.AddComponent<SphereCollider>();
                                col.isTrigger = true;
                                col.radius = 0.05f; // 5cm hand size
                            }
                            
                            if (t.GetComponent<Rigidbody>() == null)
                            {
                                var rb = t.gameObject.AddComponent<Rigidbody>();
                                rb.isKinematic = true;
                                rb.useGravity = false;
                            }
                        }
                    }
                }
            }
        }

        private static void StripTemplateScripts(Transform root)
        {
            // Remove MR Template tutorial/sample managers that crash in empty scenes
            string[] toDelete = new string[] { "Goal Manager", "Object Spawner", "SpawnedObjectsManager", "AR Session" };
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (string name in toDelete)
                {
                    if (child.name == name || child.name.Contains(name))
                    {
                        DestroyImmediate(child.gameObject);
                        break;
                    }
                }
            }

            // Remove OcclusionManager script from anywhere in the prefab
            Component[] allComps = root.GetComponentsInChildren<Component>(true);
            foreach (var c in allComps)
            {
                if (c != null && c.GetType().Name == "OcclusionManager")
                {
                    DestroyImmediate(c);
                }
            }
        }

        [MenuItem("Showcase/Fix Main Menu UI")]
        public static void FixMainMenuUI()
        {
            GameObject canvasObj = GameObject.Find("MainMenu_Canvas");
            if (canvasObj == null)
            {
                Debug.LogError("MainMenu_Canvas not found!");
                return;
            }

            // 1. Change Layer to UI (Layer 5) for Canvas and ALL children
            int uiLayer = LayerMask.NameToLayer("UI");
            Transform[] allChildren = canvasObj.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                t.gameObject.layer = uiLayer;
            }

            // 2. Ensure TrackedDeviceGraphicRaycaster is present, NOT standard GraphicRaycaster
            var oldRaycaster = canvasObj.GetComponent<GraphicRaycaster>();
            if (oldRaycaster != null && oldRaycaster.GetType() == typeof(GraphicRaycaster))
            {
                DestroyImmediate(oldRaycaster);
            }

            System.Type raycasterType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycasterType != null)
            {
                if (canvasObj.GetComponent(raycasterType) == null)
                {
                    canvasObj.AddComponent(raycasterType);
                }
            }
            else
            {
                Debug.LogError("Could not find TrackedDeviceGraphicRaycaster in assembly!");
            }

            Debug.Log("[AppSetup] Main Menu Canvas fixed: Layer set to UI, TrackedDeviceGraphicRaycaster enforced.");
        }

        [MenuItem("Showcase/Inject Showcase Systems (For Showcase Scene)")]
        public static void InjectShowcaseSystems()
        {
            // Remove Main Menu UI if it accidentally exists here
            GameObject menuCanvas = GameObject.Find("MainMenu_Canvas");
            if (menuCanvas != null) DestroyImmediate(menuCanvas);

            // Add Showcase Director
            if (GameObject.FindObjectOfType<ShowcaseDirector>() == null)
            {
                GameObject directorObj = new GameObject("ShowcaseDirector");
                directorObj.AddComponent<ShowcaseDirector>();
            }

            // Add Gemini Connection
            if (GameObject.FindObjectOfType<GeminiConnection>() == null)
            {
                GameObject geminiObj = new GameObject("GeminiConnection");
                geminiObj.AddComponent<GeminiConnection>();
            }

            // Remove Old Showcase Control Canvas
            GameObject oldUi = GameObject.Find("ShowcaseControl_Canvas");
            if (oldUi != null) DestroyImmediate(oldUi);

            // Inject New Global HUD
            if (Object.FindAnyObjectByType<Showcase.UI.GlobalHUDManager>() == null)
            {
                GameObject hudObj = new GameObject("GlobalHUDManager");
                hudObj.AddComponent<Showcase.UI.GlobalHUDManager>();
            }

            Debug.Log("[AppSetup] Showcase Systems & Global HUD injected successfully!");
        }

        [MenuItem("Showcase/Rebuild UI In Current Scene")]
        public static void RebuildUIInCurrentScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            
            if (currentScene.name.Contains("MainMenu"))
            {
                GameObject oldMenu = GameObject.Find("MainMenu_Canvas");
                if (oldMenu != null) DestroyImmediate(oldMenu);
                CreateMainMenuUI();
                
                if (Object.FindAnyObjectByType<Showcase.UI.GlobalHUDManager>() == null)
                {
                    GameObject hudObj = new GameObject("GlobalHUDManager");
                    hudObj.AddComponent<Showcase.UI.GlobalHUDManager>();
                }
                
                Debug.Log("[AppSetup] Main Menu UI & Global HUD Rebuilt!");
            }
            else if (currentScene.name.Contains("Showcase"))
            {
                InjectShowcaseSystems();
                Debug.Log("[AppSetup] Showcase Systems Rebuilt!");
            }
            else
            {
                Debug.LogWarning("Please open either MainMenu_Scene or Showcase_Scene to rebuild its UI.");
            }
        }
    }
}
#endif
