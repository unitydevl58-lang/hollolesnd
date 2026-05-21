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
    /// GlobalHUDManager v6 — Pastel glass MR menu.
    ///
    /// Hardcoded Layout Rules:
    ///   1. Root Panel is strictly 1100x980 pixels.
    ///   2. Main vertical layout controls both width AND height (childControlWidth = true, childControlHeight = true).
    ///   3. Every child element is assigned a strict LayoutElement defining its exact preferred size.
    ///   4. Absolutely NO dynamic auto-fitters that can cause slipping or overlapping.
    ///   5. Pastel glass palette with centered controls and crisp labels.
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

        // --- Pastel MR palette, adapted from the supplied HTML reference ---
        static readonly Color BG_PANEL      = new Color(0.984f, 0.973f, 0.992f, 0.92f); // #fbf8fd
        static readonly Color BG_HEADER     = new Color(0.961f, 0.953f, 0.973f, 0.62f); // #f5f3f8
        static readonly Color BG_SUBPANEL   = new Color(0.941f, 0.918f, 0.965f, 0.74f); // soft lavender glass
        static readonly Color BG_INPUT      = new Color(1.000f, 1.000f, 1.000f, 0.72f);
        static readonly Color BG_KEY        = new Color(1.000f, 1.000f, 1.000f, 0.86f);

        static readonly Color ACCENT_NAV      = new Color(0.957f, 0.643f, 0.573f, 1f); // #f4a492
        static readonly Color ACCENT_SHOWCASE = new Color(0.635f, 0.835f, 0.776f, 1f); // #a2d5c6
        static readonly Color ACCENT_SANDBOX  = new Color(0.635f, 0.835f, 0.776f, 1f);
        static readonly Color ACCENT_SEND     = new Color(0.957f, 0.643f, 0.573f, 1f);
        static readonly Color ACCENT_CLOSE    = new Color(1.000f, 0.478f, 0.486f, 1f);

        static readonly Color TXT_PRIMARY   = new Color(0.290f, 0.282f, 0.298f, 1f); // #4a484c
        static readonly Color TXT_SECONDARY = new Color(0.560f, 0.540f, 0.580f, 1f);
        static readonly Color TXT_SUCCESS   = new Color(0.420f, 0.860f, 0.580f, 1f);
        static readonly Color TXT_ERROR     = new Color(1.000f, 0.350f, 0.380f, 1f);

        private const float PANEL_WIDTH  = 1100f;
        private const float PANEL_HEIGHT = 1080f;
        private const float INFO_CARD_WIDTH = 320f;
        private const float INFO_CARD_HEIGHT = 280f;
        private const float INFO_CARD_GAP = 6f;
        private const float PAD = 32f;
        private const float GAP = 18f;
        private const float BTN_H = 52f;

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
            cr.localScale = Vector3.one * 0.00085f;

            // Root Panel (Strict 1100x980, Pivot 0.5, Anchor 0.5)
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

            // 6. Floating mentor/info card, exactly 6 px left of the main menu.
            BuildFeedbackCard(canvasGO.transform, uiLayer);
        }

        private void BuildHeader(Transform parent, int layer)
        {
            var header = MakePanel(parent, "Header", BG_HEADER, layer);
            
            // Header Group layout elements
            var layout = header.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 18, 8, 8);
            layout.spacing = GAP;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleRight;

            var le = header.AddComponent<LayoutElement>();
            le.preferredHeight = 66f;
            le.minHeight = 66f;

            // Title
            var title = MakeLabel(header.transform, "TitleText", "DESIGN STUDIO HUB", 18f, 40f, layer);
            title.fontStyle = FontStyles.Bold;
            title.color = TXT_PRIMARY;
            title.alignment = TextAlignmentOptions.Left;
            title.GetComponent<LayoutElement>().preferredWidth = 420f;

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(header.transform, false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1f;

            // Chat Toggle Button
            MakePillButton(header.transform, "Chat Panel", ACCENT_CLOSE, ToggleChat, 14f, 128f, 40f, layer);

            // Close Button
            MakePillButton(header.transform, "Kapat", ACCENT_NAV, HideHUD, 14f, 104f, 40f, layer);
        }

        private void BuildNavRow(Transform parent, int layer)
        {
            var navRow = MakePanel(parent, "NavRow", Color.clear, layer);
            var layout = navRow.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var le = navRow.AddComponent<LayoutElement>();
            le.preferredHeight = 72f;

            MakePillButton(navRow.transform, "Ana Menü (MainMenu)", ACCENT_NAV, () => LoadScene("MainMenu_Scene"), 15f, 320f, 64f, layer);
            MakePillButton(navRow.transform, "Sandbox Sahnesi", ACCENT_SANDBOX, () => LoadScene("Sandbox_Scene"), 15f, 320f, 64f, layer);
            MakePillButton(navRow.transform, "Showcase Sahnesi", ACCENT_SHOWCASE, () => LoadScene("Showcase_Scene"), 15f, 320f, 64f, layer);
        }

        private GameObject BuildGroup(Transform parent, string labelText, Color borderAccent, (string label, System.Action action)[] buttons, int layer)
        {
            var group = MakePanel(parent, $"Group_{labelText}", Color.clear, layer);
            
            var vLayout = group.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(0, 0, 8, 8);
            vLayout.spacing = 16;
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

            var title = MakeLabel(headerRow.transform, "Title", labelText, 18f, 30f, layer);
            title.fontStyle = FontStyles.Bold;
            title.color = borderAccent;

            // Button Grid
            var gridGO = new GameObject("Grid");
            gridGO.transform.SetParent(group.transform, false);
            var grid = gridGO.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(22f, 18f);
            
            grid.cellSize = new Vector2(300f, 52f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            int rows = Mathf.CeilToInt((float)buttons.Length / 3f);
            float gridH = rows * 52f + Mathf.Max(0, rows - 1) * 18f;

            var gridLE = gridGO.AddComponent<LayoutElement>();
            gridLE.preferredHeight = gridH;

            // Define Group Container height strictly
            var groupLE = group.AddComponent<LayoutElement>();
            groupLE.preferredHeight = 30f + 16f + gridH + 18f;

            for (int i = 0; i < buttons.Length; i++)
            {
                MakePillButton(gridGO.transform, buttons[i].label, borderAccent, buttons[i].action, 14f, 300f, 52f, layer);
            }

            return group;
        }

        private void BuildChatPanel(Transform parent, int layer)
        {
            _chatPanel = MakePanel(parent, "ChatPanel", BG_SUBPANEL, layer);
            var vLayout = _chatPanel.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(28, 28, 24, 18);
            vLayout.spacing = 12;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var cpLE = _chatPanel.AddComponent<LayoutElement>();
            cpLE.preferredHeight = 500f; // Rigid height for full-width keyboard

            // Status label
            _chatStatus = MakeLabel(_chatPanel.transform, "ChatStatus", "LLM Bağlantısı Aktif", 16f, 24f, layer);
            _chatStatus.fontStyle = FontStyles.Bold;
            _chatStatus.color = TXT_SUCCESS;

            // Input Row
            var inputRow = new GameObject("InputRow");
            inputRow.transform.SetParent(_chatPanel.transform, false);
            var irLayout = inputRow.AddComponent<HorizontalLayoutGroup>();
            irLayout.spacing = 8f;
            irLayout.childControlWidth = true;
            irLayout.childControlHeight = true;
            irLayout.childForceExpandWidth = false;
            irLayout.childForceExpandHeight = false;

            var irLE = inputRow.AddComponent<LayoutElement>();
            irLE.preferredHeight = 48f;

            var inputField = MakeInputField(inputRow.transform, "Input", layer);
            _chatInput = inputField.GetComponent<TMP_InputField>();
            _chatInput.readOnly = true;
            _chatInput.shouldHideMobileInput = true;
            _chatInput.shouldHideSoftKeyboard = true;
            _chatInput.onSubmit.AddListener(_ => SubmitChat());
            var ifLE = inputField.AddComponent<LayoutElement>();
            ifLE.flexibleWidth = 1f;
            ifLE.preferredHeight = 48f;

            MakePillButton(inputRow.transform, "Gönder", ACCENT_SEND, SubmitChat, 14f, 120f, 44f, layer);

            BuildKeyboard(_chatPanel.transform, layer);
        }

        private void BuildFeedbackCard(Transform parent, int layer)
        {
            GameObject feedbackCard = MakePanel(parent, "PromptInfoCard", BG_SUBPANEL, layer);
            RectTransform cardRect = feedbackCard.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(1f, 0.5f);
            cardRect.sizeDelta = new Vector2(INFO_CARD_WIDTH, INFO_CARD_HEIGHT);
            cardRect.anchoredPosition = new Vector2(-(PANEL_WIDTH * 0.5f + INFO_CARD_GAP), 96f);

            var layout = feedbackCard.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 18, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = MakeLabel(feedbackCard.transform, "InfoTitle", "BİLGİ", 15f, 24f, layer);
            title.fontStyle = FontStyles.Bold;
            title.color = ACCENT_SEND;
            title.alignment = TextAlignmentOptions.Left;

            var bodyGO = new GameObject("InfoBody");
            bodyGO.transform.SetParent(feedbackCard.transform, false);
            bodyGO.layer = layer;

            var bodyLE = bodyGO.AddComponent<LayoutElement>();
            bodyLE.preferredHeight = INFO_CARD_HEIGHT - 72f;
            bodyLE.flexibleHeight = 1f;

            _chatFeedback = bodyGO.AddComponent<TextMeshProUGUI>();
            _chatFeedback.fontSize = 14f;
            _chatFeedback.color = TXT_PRIMARY;
            _chatFeedback.text = "Model cevapları burada görünecek.";
            _chatFeedback.textWrappingMode = TextWrappingModes.Normal;
            _chatFeedback.overflowMode = TextOverflowModes.Ellipsis;
            _chatFeedback.alignment = TextAlignmentOptions.TopLeft;
        }

        private void BuildKeyboard(Transform parent, int layer)
        {
            var keyboard = new GameObject("Keyboard");
            keyboard.transform.SetParent(parent, false);
            keyboard.layer = layer;

            var layout = keyboard.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var le = keyboard.AddComponent<LayoutElement>();
            le.preferredHeight = 326f;

            BuildKeyboardLetterRow(keyboard.transform, "1234567890", layer);
            BuildKeyboardLetterRow(keyboard.transform, "QWERTYUIOPĞÜ", layer);
            BuildKeyboardLetterRow(keyboard.transform, "ASDFGHJKLŞİ", layer);
            BuildKeyboardLetterRow(keyboard.transform, "ZXCVBNMÖÇ", layer);
            BuildKeyboardActionRow(keyboard.transform, layer);
        }

        private void BuildKeyboardLetterRow(Transform parent, string letters, int layer)
        {
            var row = MakeKeyboardRow(parent, $"KeyboardRow_{letters}", layer, 56f);
            for (int i = 0; i < letters.Length; i++)
            {
                string letter = letters[i].ToString();
                GameObject key = MakePillButton(row.transform, letter, BG_KEY, () => AppendKeyboardText(letter), 20f, 56f, 56f, layer);
                ConfigureFlexibleKey(key, 1f, 52f);
            }
        }

        private void BuildKeyboardActionRow(Transform parent, int layer)
        {
            var row = MakeKeyboardRow(parent, "KeyboardRow_Actions", layer, 60f);
            ConfigureFlexibleKey(MakePillButton(row.transform, "Boşluk", ACCENT_NAV, () => AppendKeyboardText(" "), 17f, 210f, 60f, layer), 2.2f, 180f);
            ConfigureFlexibleKey(MakePillButton(row.transform, "Sil", ACCENT_CLOSE, BackspaceKeyboardText, 17f, 104f, 60f, layer), 1f, 96f);
            ConfigureFlexibleKey(MakePillButton(row.transform, "Temizle", ACCENT_NAV, ClearKeyboardText, 17f, 142f, 60f, layer), 1.4f, 128f);
            ConfigureFlexibleKey(MakePillButton(row.transform, "Gönder", ACCENT_SEND, SubmitChat, 17f, 142f, 60f, layer), 1.4f, 128f);
        }

        private GameObject MakeKeyboardRow(Transform parent, string name, int layer, float height)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);
            row.layer = layer;

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            return row;
        }

        private static void ConfigureFlexibleKey(GameObject key, float flexibleWidth, float minWidth)
        {
            if (key == null)
                return;

            LayoutElement layout = key.GetComponent<LayoutElement>();
            if (layout == null)
                layout = key.AddComponent<LayoutElement>();

            layout.minWidth = minWidth;
            layout.flexibleWidth = flexibleWidth;
        }

        private void AppendKeyboardText(string value)
        {
            if (_chatInput == null || string.IsNullOrEmpty(value))
                return;

            _chatInput.text = (_chatInput.text ?? string.Empty) + value;
            _chatInput.caretPosition = _chatInput.text.Length;
            _chatInput.ForceLabelUpdate();
        }

        private void BackspaceKeyboardText()
        {
            if (_chatInput == null || string.IsNullOrEmpty(_chatInput.text))
                return;

            _chatInput.text = _chatInput.text.Remove(_chatInput.text.Length - 1, 1);
            _chatInput.caretPosition = _chatInput.text.Length;
            _chatInput.ForceLabelUpdate();
        }

        private void ClearKeyboardText()
        {
            if (_chatInput == null)
                return;

            _chatInput.text = string.Empty;
            _chatInput.ForceLabelUpdate();
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
            SetChatStatus("LLM'e gönderiliyor...");
            string text = _chatInput.text;
            _chatInput.text = "";
            _chatInput.ForceLabelUpdate();
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

            if (color.a > 0.01f)
                AddSoftShadow(go, name == "MainPanel" ? 0.16f : 0.08f, name == "MainPanel" ? -10f : -4f);

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
            AddSoftShadow(go, 0.12f, -4f);

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
            tmp.color = GetReadableButtonTextColor(accent);
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
            ph.text = "Komutunuzu yazın";
            ph.fontSize = 14f;
            ph.color = new Color(0.56f, 0.54f, 0.58f, 0.82f);
            ph.fontStyle = FontStyles.Italic;
            ph.alignment = TextAlignmentOptions.Left;
            var phR = ph.rectTransform;
            phR.anchorMin = Vector2.zero;
            phR.anchorMax = Vector2.one;
            phR.sizeDelta = Vector2.zero;

            var txGO = new GameObject("Text");
            txGO.transform.SetParent(vpGO.transform, false);
            txGO.layer = layer;
            var tx = txGO.AddComponent<TextMeshProUGUI>();
            tx.fontSize = 14f;
            tx.color = TXT_PRIMARY;
            tx.alignment = TextAlignmentOptions.Left;
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

        private static Color GetReadableButtonTextColor(Color background)
        {
            float luminance = background.r * 0.2126f + background.g * 0.7152f + background.b * 0.0722f;
            return luminance > 0.74f ? TXT_PRIMARY : Color.white;
        }

        private static void AddSoftShadow(GameObject target, float alpha, float yOffset)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = new Vector2(0f, yOffset);
            shadow.useGraphicAlpha = true;
        }

        #endregion
    }
}
