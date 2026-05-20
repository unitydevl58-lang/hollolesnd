using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Showcase.UI
{
    /// <summary>
    /// GlobalHUDManager v6 — Bulletproof, non-slip, professional Dark Mode MR HUD.
    ///
    /// Hardcoded Layout Rules:
    ///   1. Root Panel is strictly 700x500 pixels.
    ///   2. Main vertical layout controls both width AND height (childControlWidth = true, childControlHeight = true).
    ///   3. Every child element is assigned a strict LayoutElement defining its exact preferred size.
    ///   4. Absolutely NO dynamic auto-fitters that can cause slipping or overlapping.
    ///   5. Premium Dark Mode charcoal grey palette with centered text and crisp labels.
    /// </summary>
    public class GlobalHUDManager : MonoBehaviour
    {
        public static GlobalHUDManager Instance { get; private set; }

        private Canvas _hudCanvas;
        private RectTransform _mainPanelRect;
        private bool _menuVisible;
        private bool _wasXRMenuPressed;

        // Chat elements
        private GameObject _chatPanel;
        private TMP_InputField _chatInput;
        private TMP_Text _chatStatus;
        private TMP_Text _chatFeedback;
        private bool _chatOpen = true;

        // Scene-conditional groups
        private GameObject _grpShowcase;
        private GameObject _grpSandbox;

        // --- Sweet, Colorful, Modern Palette ---
        static readonly Color BG_PANEL      = new Color(0.12f, 0.10f, 0.20f, 0.94f); // Glassy dark purple/navy
        static readonly Color BG_HEADER     = new Color(0.18f, 0.14f, 0.30f, 0.98f); // Rich violet header
        static readonly Color BG_SUBPANEL   = new Color(0.15f, 0.12f, 0.25f, 0.95f); // Soft violet subpanel
        static readonly Color BG_INPUT      = new Color(0.25f, 0.20f, 0.35f, 1.00f); // Bright input area

        // Vibrant Sweet Accent Colors
        static readonly Color ACCENT_NAV      = new Color(0.85f, 0.25f, 0.55f, 1f);  // Vibrant Magenta/Pink
        static readonly Color ACCENT_SHOWCASE = new Color(0.15f, 0.75f, 0.85f, 1f);  // Bright Cyan
        static readonly Color ACCENT_SANDBOX  = new Color(0.20f, 0.85f, 0.60f, 1f);  // Vibrant Mint Green
        static readonly Color ACCENT_SEND     = new Color(0.95f, 0.45f, 0.40f, 1f);  // Neon Coral
        static readonly Color ACCENT_CLOSE    = new Color(0.95f, 0.25f, 0.35f, 1f);  // Vivid Red/Pink

        static readonly Color TXT_PRIMARY   = Color.white;
        static readonly Color TXT_SECONDARY = new Color(0.85f, 0.85f, 0.95f, 1f);
        static readonly Color TXT_SUCCESS   = new Color(0.40f, 0.95f, 0.65f, 1f);
        static readonly Color TXT_ERROR     = new Color(1.00f, 0.45f, 0.45f, 1f);

        private const float PANEL_WIDTH  = 700f;
        private const float PANEL_HEIGHT = 500f;
        private const float PAD = 20f;
        private const float GAP = 15f;
        private const float BTN_H = 44f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            StartCoroutine(InitHUD());
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
                ToggleMenu();

            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                MRGrabController.TriggerGrab();

            // XR Controller Menu Button support
            bool xrPressed = false;
            var devices = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.Controller, devices);
            foreach (var d in devices)
            {
                if ((d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool s) && s) ||
                    (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out s) && s))
                {
                    xrPressed = true;
                    break;
                }
            }
            if (xrPressed && !_wasXRMenuPressed) ToggleMenu();
            _wasXRMenuPressed = xrPressed;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateSceneGroups();
            StartCoroutine(RewireLLM());
        }

        private IEnumerator InitHUD()
        {
            yield return null;
            BuildHUD();
            
            _menuVisible = false;
            if (_hudCanvas != null) _hudCanvas.gameObject.SetActive(false);

            PositionCanvasInFrontOfCamera();
            UpdateSceneGroups();
            yield return RewireLLM();
        }

        private IEnumerator RewireLLM()
        {
            yield return new WaitForSeconds(0.1f);
            var gemini = FindAnyObjectByType<GeminiConnection>();
            if (gemini != null && _chatInput != null)
            {
                gemini.SetHUDReferences(_chatInput, _chatStatus, _chatFeedback);
            }
        }

        private void PositionCanvasInFrontOfCamera()
        {
            if (_hudCanvas == null || Camera.main == null) return;
            Transform cam = Camera.main.transform;
            
            // Place exactly 1.25 meters in front of user's camera forward vector
            _hudCanvas.transform.position = cam.position + cam.forward * 1.25f;
            
            // Rotate to look directly at the user
            Vector3 lookDir = _hudCanvas.transform.position - cam.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                _hudCanvas.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            }
        }

        public void ToggleMenu()
        {
            if (_hudCanvas == null) return;
            _menuVisible = !_menuVisible;
            _hudCanvas.gameObject.SetActive(_menuVisible);

            if (_menuVisible)
            {
                PositionCanvasInFrontOfCamera();
            }
        }

        public void ShowHUD()
        {
            _menuVisible = true;
            if (_hudCanvas != null)
            {
                _hudCanvas.gameObject.SetActive(true);
                PositionCanvasInFrontOfCamera();
            }
        }

        public void HideHUD()
        {
            _menuVisible = false;
            if (_hudCanvas != null) _hudCanvas.gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        #region UI Creation (Strict Absolute Layout Architecture)

        private void BuildHUD()
        {
            if (_hudCanvas != null) return;

            int uiLayer = LayerMask.NameToLayer("UI");

            // Canvas Setup
            var canvasGO = new GameObject("GlobalHUD_Canvas");
            canvasGO.transform.SetParent(transform);
            canvasGO.layer = uiLayer;

            _hudCanvas = canvasGO.AddComponent<Canvas>();
            _hudCanvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            var xrRaycasterType = System.Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (xrRaycasterType != null) canvasGO.AddComponent(xrRaycasterType);
            else canvasGO.AddComponent<GraphicRaycaster>();

            var cr = canvasGO.GetComponent<RectTransform>();
            cr.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
            cr.localScale = Vector3.one * 0.001f;

            // Root Panel (Strict 700x500, Pivot 0.5, Anchor 0.5)
            var mainPanel = MakePanel(canvasGO.transform, "MainPanel", BG_PANEL, uiLayer);
            _mainPanelRect = mainPanel.GetComponent<RectTransform>();
            _mainPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _mainPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _mainPanelRect.pivot = new Vector2(0.5f, 0.5f);
            _mainPanelRect.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
            _mainPanelRect.anchoredPosition = Vector2.zero;

            // Strict Vertical Layout on the main panel (Control Width & Height = True)
            var mainVLayout = mainPanel.AddComponent<VerticalLayoutGroup>();
            mainVLayout.padding = new RectOffset((int)PAD, (int)PAD, (int)PAD, (int)PAD);
            mainVLayout.spacing = GAP;
            mainVLayout.childAlignment = TextAnchor.UpperCenter;
            mainVLayout.childControlWidth = true;
            mainVLayout.childControlHeight = true; // Control height natively
            mainVLayout.childForceExpandWidth = true;
            mainVLayout.childForceExpandHeight = false; // Do not force fill empty space

            // 1. Header Row
            BuildHeader(mainPanel.transform, uiLayer);

            // 2. Main Navigation Buttons Row
            BuildNavRow(mainPanel.transform, uiLayer);

            // 3. Showcase Group
            _grpShowcase = BuildGroup(mainPanel.transform, "SHOWCASE OPERATIONS", ACCENT_SHOWCASE, new[]
            {
                ("Soyut Şehir", (System.Action)(() => CallShowcase("AbstractCity"))),
                ("Işınsal Anıt", (System.Action)(() => CallShowcase("RadialMonument"))),
                ("Parçalanma", (System.Action)(() => CallShowcase("Deconstruction")))
            }, uiLayer);

            // 4. Sandbox Group
            _grpSandbox = BuildGroup(mainPanel.transform, "SANDBOX BLUEPRINTS", ACCENT_SANDBOX, new[]
            {
                ("Küp", (System.Action)(() => CallSandbox("Cube"))),
                ("Silindir", (System.Action)(() => CallSandbox("Cylinder"))),
                ("Prizma", (System.Action)(() => CallSandbox("Prism"))),
                ("Köpek Kulübesi", (System.Action)(() => CallSandbox("Doghouse"))),
                ("Masa", (System.Action)(() => CallSandbox("Table"))),
                ("Oluşumu Oynat", (System.Action)(() => CallSandbox("PlayFormation"))),
                ("Sahneyi Temizle", (System.Action)(() => FindAnyObjectByType<GeometryManager>()?.ResetScene()))
            }, uiLayer);

            // 5. LLM Chat Panel
            BuildChatPanel(mainPanel.transform, uiLayer);
        }

        private void BuildHeader(Transform parent, int layer)
        {
            var header = MakePanel(parent, "Header", BG_HEADER, layer);
            
            // Header Group layout elements
            var layout = header.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 6, 6);
            layout.spacing = GAP;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleRight;

            var le = header.AddComponent<LayoutElement>();
            le.preferredHeight = 50f;
            le.minHeight = 50f;

            // Title
            var title = MakeLabel(header.transform, "TitleText", "DESIGN STUDIO HUB", 14f, 32f, layer);
            title.fontStyle = FontStyles.Bold;
            title.color = TXT_PRIMARY;
            title.alignment = TextAlignmentOptions.Center;

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(header.transform, false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1f;

            // Chat Toggle Button
            var chatBtn = MakePillButton(header.transform, "Chat Panel", ACCENT_NAV, ToggleChat, 10f, 100f, 32f, layer);

            // Close Button
            var closeBtn = MakePillButton(header.transform, "Kapat", ACCENT_CLOSE, HideHUD, 10f, 80f, 32f, layer);
        }

        private void BuildNavRow(Transform parent, int layer)
        {
            var navRow = MakePanel(parent, "NavRow", Color.clear, layer);
            var layout = navRow.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = GAP;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var le = navRow.AddComponent<LayoutElement>();
            le.preferredHeight = BTN_H;

            MakePillButton(navRow.transform, "Ana Menü (MainMenu)", ACCENT_NAV, () => LoadScene("MainMenu_Scene"), 11f, 200f, BTN_H, layer);
            MakePillButton(navRow.transform, "Sandbox Sahnesi", ACCENT_SANDBOX, () => LoadScene("Sandbox_Scene"), 11f, 200f, BTN_H, layer);
            MakePillButton(navRow.transform, "Showcase Sahnesi", ACCENT_SHOWCASE, () => LoadScene("Showcase_Scene"), 11f, 200f, BTN_H, layer);
        }

        private GameObject BuildGroup(Transform parent, string labelText, Color borderAccent, (string label, System.Action action)[] buttons, int layer)
        {
            var group = MakePanel(parent, $"Group_{labelText}", BG_SUBPANEL, layer);
            
            var vLayout = group.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(15, 15, 10, 10);
            vLayout.spacing = 10;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            // Header Row Label
            var headerRow = new GameObject("HeaderRow");
            headerRow.transform.SetParent(group.transform, false);
            var hrLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            hrLayout.childControlWidth = true;
            hrLayout.childControlHeight = true;
            hrLayout.childForceExpandWidth = true;
            hrLayout.childForceExpandHeight = false;

            var title = MakeLabel(headerRow.transform, "Title", labelText, 11f, 20f, layer);
            title.fontStyle = FontStyles.Bold;
            title.color = borderAccent;

            // Button Grid
            var gridGO = new GameObject("Grid");
            gridGO.transform.SetParent(group.transform, false);
            var grid = gridGO.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(GAP, GAP);
            
            // Fixed width calculation: PANEL_WIDTH (700) - PAD*2 (40) - SubPanelPadding (30) = 630.
            // 3 columns: (630 - GAP*2) / 3 = 200 cell size.
            grid.cellSize = new Vector2(200f, BTN_H);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            int rows = Mathf.CeilToInt((float)buttons.Length / 3f);
            float gridH = rows * (BTN_H + GAP) - GAP;

            var gridLE = gridGO.AddComponent<LayoutElement>();
            gridLE.preferredHeight = gridH;

            // Define Group Container height strictly
            var groupLE = group.AddComponent<LayoutElement>();
            groupLE.preferredHeight = 20f + 10f + gridH + 20f; // Label + Spacing + Grid + Padding

            for (int i = 0; i < buttons.Length; i++)
            {
                MakePillButton(gridGO.transform, buttons[i].label, borderAccent, buttons[i].action, 11f, 200f, BTN_H, layer);
            }

            return group;
        }

        private void BuildChatPanel(Transform parent, int layer)
        {
            _chatPanel = MakePanel(parent, "ChatPanel", BG_SUBPANEL, layer);
            var vLayout = _chatPanel.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(15, 15, 8, 8);
            vLayout.spacing = 8;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var cpLE = _chatPanel.AddComponent<LayoutElement>();
            cpLE.preferredHeight = 136f; // Rigid height for chat container

            // Status label
            _chatStatus = MakeLabel(_chatPanel.transform, "ChatStatus", "● LLM Bağlantısı Aktif", 11f, 20f, layer);
            _chatStatus.fontStyle = FontStyles.Bold;
            _chatStatus.color = TXT_SUCCESS;

            // Input Row
            var inputRow = new GameObject("InputRow");
            inputRow.transform.SetParent(_chatPanel.transform, false);
            var irLayout = inputRow.AddComponent<HorizontalLayoutGroup>();
            irLayout.spacing = GAP;
            irLayout.childControlWidth = true;
            irLayout.childControlHeight = true;
            irLayout.childForceExpandWidth = false;
            irLayout.childForceExpandHeight = false;

            var irLE = inputRow.AddComponent<LayoutElement>();
            irLE.preferredHeight = BTN_H;

            var inputField = MakeInputField(inputRow.transform, "Input", layer);
            _chatInput = inputField.GetComponent<TMP_InputField>();
            _chatInput.onSubmit.AddListener(_ => SubmitChat());
            var ifLE = inputField.AddComponent<LayoutElement>();
            ifLE.flexibleWidth = 1f;
            ifLE.preferredHeight = BTN_H;

            var sendBtn = MakePillButton(inputRow.transform, "Gönder", ACCENT_SEND, SubmitChat, 11f, 90f, BTN_H, layer);

            // Feedback area
            var feedbackGO = new GameObject("Feedback");
            feedbackGO.transform.SetParent(_chatPanel.transform, false);
            _chatFeedback = feedbackGO.AddComponent<TextMeshProUGUI>();
            _chatFeedback.fontSize = 11f;
            _chatFeedback.color = TXT_SECONDARY;
            _chatFeedback.text = "Model cevapları bu alanda görüntülenecektir.";
            _chatFeedback.textWrappingMode = TextWrappingModes.Normal;
            _chatFeedback.alignment = TextAlignmentOptions.Center;

            var fbLE = feedbackGO.AddComponent<LayoutElement>();
            fbLE.preferredHeight = 40f;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Actions

        private void SubmitChat()
        {
            if (_chatInput == null || string.IsNullOrWhiteSpace(_chatInput.text)) return;
            var gemini = FindAnyObjectByType<GeminiConnection>();
            if (gemini == null)
            {
                SetChatStatus("GeminiConnection bulunamadı.", true);
                return;
            }
            SetChatStatus("⟳ LLM'e gönderiliyor…");
            string text = _chatInput.text;
            _chatInput.text = "";
            _chatInput.ActivateInputField();
            gemini.SubmitFromHUD(text);
        }

        public void SetChatStatus(string msg, bool isError = false)
        {
            if (_chatStatus == null) return;
            _chatStatus.text = msg;
            _chatStatus.color = isError ? TXT_ERROR : TXT_SUCCESS;
        }

        private void ToggleChat()
        {
            _chatOpen = !_chatOpen;
            _chatPanel.SetActive(_chatOpen);
        }

        private void LoadScene(string name) => SceneManager.LoadScene(name);

        private void CallShowcase(string target)
        {
            var d = FindAnyObjectByType<ShowcaseDirector>();
            if (d == null) return;
            switch (target)
            {
                case "AbstractCity": d.SpawnAbstractCity(); break;
                case "RadialMonument": d.SpawnRadialMonument(); break;
                case "Deconstruction": d.SpawnDeconstruction(); break;
            }
        }

        private void CallSandbox(string target)
        {
            var ui = HoloLensApp.Sandbox.SandboxUIManager.Instance;
            var bp = HoloLensApp.Sandbox.BlueprintManager.Instance;
            switch (target)
            {
                case "Cube": ui?.SpawnPrimitive(PrimitiveType.Cube); break;
                case "Cylinder": ui?.SpawnPrimitive(PrimitiveType.Cylinder); break;
                case "Prism": ui?.SpawnPrism(); break;
                case "Doghouse": bp?.SpawnBlueprint("Doghouse"); break;
                case "Table": bp?.SpawnBlueprint("Table"); break;
                case "PlayFormation": bp?.PlayAnimatedFormation(); break;
            }
        }

        private void UpdateSceneGroups()
        {
            string name = SceneManager.GetActiveScene().name;
            bool isShowcase = name.Contains("Showcase");
            bool isSandbox = name.Contains("Sandbox");
            if (_grpShowcase != null) _grpShowcase.SetActive(isShowcase);
            if (_grpSandbox != null) _grpSandbox.SetActive(isSandbox);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region UI Helpers

        private static GameObject MakePanel(Transform parent, string name, Color color, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<Image>();
            img.color = color;
            img.sprite = Resources.Load<Sprite>("UI/Skin/UISprite");
            img.type = Image.Type.Sliced;

            return go;
        }

        private static TMP_Text MakeLabel(Transform parent, string name, string text, float size, float height, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = TXT_PRIMARY;
            tmp.alignment = TextAlignmentOptions.Center;
            
            return tmp;
        }

        private static GameObject MakePillButton(Transform parent, string label, Color accent, System.Action onClick, float fontSize, float width, float height, int layer)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            var img = go.AddComponent<Image>();
            img.color = accent;
            img.sprite = Resources.Load<Sprite>("UI/Skin/UISprite");
            img.type = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = accent;
            cb.highlightedColor = new Color(Mathf.Min(accent.r + 0.12f, 1f), Mathf.Min(accent.g + 0.12f, 1f), Mathf.Min(accent.b + 0.12f, 1f), 1f);
            cb.pressedColor = new Color(accent.r * 0.75f, accent.g * 0.75f, accent.b * 0.75f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var lgo = new GameObject("Label");
            lgo.transform.SetParent(go.transform, false);
            lgo.layer = layer;
            var tmp = lgo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = TXT_PRIMARY;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            var lr = tmp.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.sizeDelta = Vector2.zero;

            return go;
        }

        private static GameObject MakeInputField(Transform parent, string name, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var bg = go.AddComponent<Image>();
            bg.color = BG_INPUT;
            bg.sprite = Resources.Load<Sprite>("UI/Skin/UISprite");
            bg.type = Image.Type.Sliced;

            var field = go.AddComponent<TMP_InputField>();

            var vpGO = new GameObject("Viewport");
            vpGO.transform.SetParent(go.transform, false);
            vpGO.layer = layer;
            var vpR = vpGO.AddComponent<RectTransform>();
            vpR.anchorMin = Vector2.zero;
            vpR.anchorMax = Vector2.one;
            vpR.offsetMin = new Vector2(8, 2);
            vpR.offsetMax = new Vector2(-8, -2);
            vpGO.AddComponent<RectMask2D>();

            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(vpGO.transform, false);
            phGO.layer = layer;
            var ph = phGO.AddComponent<TextMeshProUGUI>();
            ph.text = "Komutunuzu yazın…";
            ph.fontSize = 12f;
            ph.color = new Color(0.5f, 0.5f, 0.6f, 1f);
            ph.fontStyle = FontStyles.Italic;
            ph.alignment = TextAlignmentOptions.Center;
            var phR = ph.rectTransform;
            phR.anchorMin = Vector2.zero;
            phR.anchorMax = Vector2.one;
            phR.sizeDelta = Vector2.zero;

            var txGO = new GameObject("Text");
            txGO.transform.SetParent(vpGO.transform, false);
            txGO.layer = layer;
            var tx = txGO.AddComponent<TextMeshProUGUI>();
            tx.fontSize = 12f;
            tx.color = TXT_PRIMARY;
            tx.alignment = TextAlignmentOptions.Center;
            var txR = tx.rectTransform;
            txR.anchorMin = Vector2.zero;
            txR.anchorMax = Vector2.one;
            txR.sizeDelta = Vector2.zero;

            field.textViewport = vpR;
            field.textComponent = tx;
            field.placeholder = ph;
            field.lineType = TMP_InputField.LineType.SingleLine;

            return go;
        }

        #endregion
    }
}
