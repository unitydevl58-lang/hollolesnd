using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Sends user text or speech commands to Gemini and forwards JSON geometry commands to GeometryManager.
/// </summary>
public class GeminiConnection : MonoBehaviour
{
    [Header("API Settings")]
    [Tooltip("Optional editor-only API key override. Prefer the GEMINI_API_KEY environment variable.")]
    [FormerlySerializedAs("apiKey")]
    [SerializeField] private string apiKey = "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";

    [Tooltip("Maximum time in seconds to wait for a response. Increase for local thinking models (e.g. Gemma 4, DeepSeek-R1).")]
    [FormerlySerializedAs("istekZamanAsimi")]
    [SerializeField] private float requestTimeoutSeconds = 120f;

    [Header("System References")]
    [Tooltip("Component responsible for parsing JSON and generating voxel geometry.")]
    [FormerlySerializedAs("geometryManager")]
    [SerializeField] private GeometryManager geometryManager;

    [Header("UI References")]
    [Tooltip("Input field that contains the user's text command.")]
    [FormerlySerializedAs("kullaniciGirdiAlani")]
    [SerializeField] private TMP_InputField userInputField;

    [Tooltip("Send button disabled while a request is running.")]
    [FormerlySerializedAs("gonderButonu")]
    [SerializeField] private Button sendButton;

    [Tooltip("Text field used for loading, warning, and error messages.")] 
    [FormerlySerializedAs("durumMesajiMetni")]
    [SerializeField] private TMP_Text statusMessageText;

    [Tooltip("Text field for AI mentor feedback (the Turkish explanation before the JSON). Assign 'Bilgi' from the Canvas.")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("Rate Limit Protection")]
    [Tooltip("Minimum delay between requests, in seconds.")]
    [FormerlySerializedAs("isteklerArasiMinBeklemeSn")]
    [SerializeField] private float minSecondsBetweenRequests = 3f;

    [Tooltip("Automatic retry delay for HTTP 429 responses. 0 disables automatic retry.")]
    [FormerlySerializedAs("otomatikRetryBeklarme")]
    [SerializeField] private float automaticRetryDelaySeconds = 0f;

    private const string ApiKeyEnvironmentVariable = "GEMINI_API_KEY";
    private const string LocalAiUrl = "http://localhost:1234/v1/chat/completions";

    private const int MaxAutomaticRetryAttempts = 1;
    private const float ServiceUnavailableRetryDelaySeconds = 2f;

    /// <summary>
    /// System instruction that defines the Creative Architecture Mentor persona and technical constraints.
    /// </summary>
    private const string SystemInstruction =
        "You are the 'Creative Architecture Mentor', an AI integrated into a Mixed Reality (MR) app. Your goal is to teach basic 3D design principles to verbal-learning youth by helping them build cumulative 3D structures using simple voxel primitives. (Reference: ISAS 2018 - Kasap & Turkmen)\n" +
        "\n" +
        "=== 1. SCENE MANAGEMENT (ADDITIVE MODE) ===\n" +
        "The Unity scene is ADDITIVE — each response ADDS new objects to the existing scene without erasing what's already there.\n" +
        "- OUTPUT ONLY NEW OBJECTS you are adding in this response. Do NOT repeat previously created objects.\n" +
        "- The current scene state is injected in the user message as [SCENE STATE]. Read it to know where things are.\n" +
        "- Position NEW objects relative to existing ones so they represent the chosen Wong interaction.\n" +
        "- The user can reset the scene by saying 'temizle' or 'reset' — this is handled by the app automatically.\n" +
        "\n" +
        "=== 2. SUPPORTED GEOMETRY & SHAPES ===\n" +
        "- Only THREE shapes: 'cube', 'sphere', 'cylinder'.\n" +
        "- No pyramid/cone. Simulate a roof with a rotated/scaled cube.\n" +
        "- 'subdivision' (1-10): 1=blocky, 8-10=smooth.\n" +
        "- 'rotation': Euler angles [x, y, z] in degrees. Use to tilt objects for form interactions.\n" +
        "- 'scale': MUST be an array [x, y, z]. Use non-uniform scales like [2.0, 0.1, 1.0] for table tops.\n" +
        "- UNITY AXES: Y is UP (height), X is RIGHT/LEFT (width), Z is FORWARD/BACKWARD (depth).\n" +
        "- CRITICAL CONSTRUCTION RULES for compound objects (table/chair/house):\n" +
        "  * Always use subdivision: 1 for structural parts (legs, walls, beams) to avoid visual slicing artifacts.\n" +
        "  * For a table: table top at position Y = legHeight/2 + topThickness/2. Legs centered at Y = legHeight/2.\n" +
        "  * Example table top: scale [2.0, 0.1, 1.0] at position [0, 0.55, 0]. Legs: scale [0.1, 1.0, 0.1] at Y=0.5.\n" +
        "  * For a doghouse (köpek kulübesi): Create 3 walls resting on ground (Y=0.5). Add a 2-piece pitched roof. Left roof: position [-0.32, 1.2, 0], scale [1.0, 0.1, 1.2], rotation [0, 0, 45]. Right roof: position [0.32, 1.2, 0], scale [1.0, 0.1, 1.2], rotation [0, 0, -45].\n" +
        "  * For 'touching' (union) compound objects, compute positions so surfaces are exactly flush (zero gap).\n" +
        "- Colors (English or Turkish): red/kirmizi, blue/mavi, yellow/sari, green/yesil, white/beyaz, black/siyah, orange/turuncu, purple/mor, pink/pembe, cyan, gray/gri, brown/kahverengi.\n" +
        "\n" +
        "=== 3. WONG (1969) - 8 FORM INTERACTIONS ===\n" +
        "When placing 2+ objects, always pick ONE interaction. Set 'formInteraction' in JSON and explain it in Turkish.\n" +
        "Valid values:\n" +
        "- 'detachment'  : Kopma - forms near each other but spatially separated, each keeps its boundary.\n" +
        "- 'touching'    : Temas Etme - forms share a border but don't enter each other's space.\n" +
        "- 'overlapping' : Ortusme - one form visually covers part of another (top-bottom). Use rotation+position.\n" +
        "- 'penetration' : Ice Girme - both forms fully visible, boundaries cross, no priority. Overlap positions.\n" +
        "- 'union'       : Birlesme - forms merge into one larger mass. Position them flush/adjacent.\n" +
        "- 'subtraction' : Eksilme - runtime CSG subtraction (carved mesh, acetate material).\n" +
        "- 'intersection': Kesisme - runtime CSG intersection (shared volume mesh, white material).\n" +
        "- 'coinciding'  : Denk Gelme - identical position, forms read as one. Use slightly different colors.\n" +
        "\n" +
        "=== 4. CHING (2014) - 6 DESIGN PRINCIPLES ===\n" +
        "Explain them in Turkish. DO NOT include them in JSON to save tokens.\n" +
        "- 'harmony'    / Uyum-Harmoni       -> sub: 'rhythm', 'repetition', 'continuity'\n" +
        "- 'balance'    / Denge               -> sub: 'symmetry', 'asymmetry', 'radial'\n" +
        "- 'hierarchy'  / Hiyerarsi           -> sub: 'trees', 'clusters', 'weight'\n" +
        "- 'proportion' / Proporsiyon         -> sub: 'size', 'ratio', 'parts'\n" +
        "- 'dominance'  / Hakimiyet-Vurgu     -> sub: 'form', 'color', 'emphasis_size'\n" +
        "- 'contrast'   / Benzerlik-Karsitlik -> sub: 'value', 'line', 'shape_contrast'\n" +
        "\n" +
        "=== 5. CHING (2014) - 5 ORGANIZATION SCHEMAS ===\n" +
        "Explain in Turkish. DO NOT include it in JSON to save tokens.\n" +
        "- 'central'   : Merkezi  - dominant center form surrounded by others.\n" +
        "- 'linear'    : Cizgisel - forms arranged in a line/sequence.\n" +
        "- 'radial'    : Isinsal  - forms radiating from a center point.\n" +
        "- 'clustered' : Kumeli   - forms grouped by similarity/function.\n" +
        "- 'grid'      : Gridal   - forms on a regular grid.\n" +
        "\n" +
        "=== 6. MANDATORY RESPONSE FORMAT ===\n" +
        "Your response MUST have exactly TWO parts:\n" +
        "\n" +
        "PART 1 - TURKISH MENTOR FEEDBACK (always write this FIRST, before any JSON):\n" +
        "A short encouraging Turkish paragraph that MUST name:\n" +
        "  (a) The Wong interaction chosen and WHY.\n" +
        "  (b) The Ching principle + sub-principle and what it teaches.\n" +
        "  (c) The organization schema used.\n" +
        "\n" +
        "PART 2 - JSON (after the text, inside ```json fences) — choose ONE of two formats:\n" +
        "\n" +
        "FORMAT A — ADDING new objects to the scene (user asks to create, yap, oluştur, çiz, inşa et, ekle):\n" +
        "Output a plain JSON array containing ONLY the new objects.\n" +
        "```json\n" +
        "[ { \"action\": \"create\", \"shape\": \"sphere\", \"subdivision\": 8, \"color\": \"blue\", \"position\": [0.6, 0, 0], \"scale\": [1.0, 1.0, 1.0], \"rotation\": [0,0,0], \"formInteraction\": \"detachment\" } ]\n" +
        "```\n" +
        "\n" +
        "FORMAT B — REARRANGING existing objects (user asks to make them touch, overlap, move closer, birleştir, yaklaştır, temas ettir vs):\n" +
        "Output a JSON OBJECT with \"rebuildScene\": true and ALL objects (existing + any new) at their NEW positions.\n" +
        "The app will clear the scene and rebuild it from your list.\n" +
        "```json\n" +
        "{ \"rebuildScene\": true, \"objects\": [\n" +
        "  { \"action\": \"create\", \"shape\": \"cube\",   \"subdivision\": 1, \"color\": \"white\", \"position\": [-0.3, 0, 0], \"scale\": [1.0, 1.0, 1.0], \"rotation\": [0,0,0], \"formInteraction\": \"touching\" },\n" +
        "  { \"action\": \"create\", \"shape\": \"sphere\", \"subdivision\": 8, \"color\": \"blue\",  \"position\": [0.3, 0, 0],  \"scale\": [1.0, 1.0, 1.0], \"rotation\": [0,0,0], \"formInteraction\": \"touching\" }\n" +
        "] }\n" +
        "```\n" +
        "Use the [SCENE STATE] from the user message to know existing positions when choosing FORMAT B.\n" +
        "IMPORTANT: scale [x,y,z] means EXACT SIZE IN METERS. scale [1.0,1.0,1.0] = 1m cube. scale [2.0,0.1,1.0] = 2m wide, 0.1m thick, 1m deep.\n" +
        "IMPORTANT: For 'touching', place object centers so they are exactly half their combined sizes apart. Two 1m cubes touching: centers 1.0m apart. A 1.5m leg touching a 0.1m thick table top: leg center at Y=0.75, top center at Y=1.55 (leg_half + top_half = 0.75+0.05=0.8m gap between centers... actually: leg top at Y=1.5, top bottom at Y=1.5, top center at Y=1.55).\n" +
        "IMPORTANT: Commands like 'yaklasır', 'daha yakın', 'birleştir', 'overlap', 'closer', 'move together', 'temas' — ALWAYS use FORMAT B (rebuildScene:true) with formInteraction='touching'.\n" +
        "\n" +
        "FORMAT C — SPATIAL OPERATIONS (user asks to physically fragment, parçala, patlat, divide, shatter):\n" +
        "Output a spatialop JSON block targeting the object name or 'all' to fragment the whole scene.\n" +
        "```spatialop\n" +
        "{ \"operation\": \"fragmentation\", \"targetA\": \"all\" }\n" +
        "```\n" +
        "Position objects so they PHYSICALLY represent the chosen interaction. Now wait for the user's command.";

    private bool isRequestInProgress;
    private float lastRequestTime = -999f;
    private CancellationTokenSource cancellationSource;

    // ── HUD-injected references (set by GlobalHUDManager after each scene load) ──
    private TMP_InputField _hudInputField;
    private TMP_Text       _hudStatusText;
    private TMP_Text       _hudFeedbackText;

    /// <summary>
    /// Called by GlobalHUDManager to inject its embedded UI references into this component.
    /// Overrides the Inspector-serialized fields only when non-null values are supplied.
    /// </summary>
    public void SetHUDReferences(TMP_InputField input, TMP_Text status, TMP_Text feedback)
    {
        _hudInputField   = input;
        _hudStatusText   = status;
        _hudFeedbackText = feedback;
        // Mirror into the serialized slots so existing code paths work unchanged
        if (input   != null) userInputField    = input;
        if (status  != null) statusMessageText = status;
        if (feedback != null) feedbackText     = feedback;
        Debug.Log("[GeminiConnection] HUD references wired.");
    }

    /// <summary>
    /// Entry point called by GlobalHUDManager's Send button / Enter key.
    /// Delegates to the same async pipeline used by SubmitTextInput().
    /// </summary>
    public async void SubmitFromHUD(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (isRequestInProgress)
        {
            ShowStatus("Önceki istek bitmedi. Lütfen bekleyin.", StatusType.Warning);
            return;
        }
        await ProcessPromptAsync(text);
    }

    /// <summary>
    /// PHYSICS FEEDBACK LOOP: sends spatial operation results back to the LLM
    /// (e.g., after fragmentation, CSG union) so the AI can continue its context.
    /// Mapped to: Wong – Fragmentation, Union/Birleşme, Subtraction/Eksilme callbacks.
    /// </summary>
    public async void SendPhysicsResultToLLM(string resultSummary)
    {
        if (string.IsNullOrWhiteSpace(resultSummary)) return;
        string feedbackPrompt = $"[PHYSICS RESULT] {resultSummary}\n" +
            "Briefly acknowledge in Turkish and suggest next spatial operation (Wong principle).";
        await ProcessPromptAsync(feedbackPrompt);
    }

    /// <summary>
    /// Resets serialized field defaults when the component is added or Reset is clicked in the Inspector.
    /// This ensures the timeout is 120 s even on existing scenes that serialized the old 10 s value.
    /// </summary>
    private void Reset()
    {
        requestTimeoutSeconds = 120f;
    }

    /// <summary>
    /// Initializes the UI status field.
    /// </summary>
    private void Start()
    {
        // Guard: if an old scene serialized a very short timeout, enforce a minimum so thinking
        // models (Gemma 4, DeepSeek-R1, etc.) have enough time to finish reasoning + output.
        if (requestTimeoutSeconds < 60f)
        {
            Debug.LogWarning($"[GeminiConnection] requestTimeoutSeconds ({requestTimeoutSeconds}s) is very low for a thinking model. " +
                             "Clamping to 120s. Update the Inspector field to silence this warning.");
            requestTimeoutSeconds = 120f;
        }

        ShowStatus("");
    }

    /// <summary>
    /// Cancels any in-flight request when the component is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        cancellationSource?.Cancel();
        cancellationSource?.Dispose();
    }

    /// <summary>
    /// Reads the UI input field and submits the user's command to Gemini.
    /// Bind this method to the send button in new scenes.
    /// </summary>
    public async void SubmitTextInput()
    {
        if (isRequestInProgress)
        {
            Debug.LogWarning("[GeminiConnection] A request is already running.");
            return;
        }

        string userPrompt = userInputField != null ? userInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(userPrompt))
        {
            ShowStatus("Lütfen bir komut girin.", StatusType.Warning);
            return;
        }

        await ProcessPromptAsync(userPrompt);
    }

    /// <summary>
    /// Submits speech-recognized text to Gemini.
    /// Bind this method to speech systems or keyword recognizers.
    /// </summary>
    public async void SubmitSpeechInput(string recognizedText)
    {
        if (isRequestInProgress)
        {
            Debug.LogWarning("[GeminiConnection] Speech input skipped because a request is already running.");
            return;
        }

        if (string.IsNullOrWhiteSpace(recognizedText))
            return;

        Debug.Log($"[GeminiConnection] Speech command received: \"{recognizedText}\"");
        await ProcessPromptAsync(recognizedText);
    }

    /// <summary>
    /// Backward-compatible wrapper for existing UnityEvent bindings.
    /// Prefer SubmitTextInput in new code.
    /// </summary>
    [Obsolete("Use SubmitTextInput instead.")]
    public void ArayuzdenGelenIstegiIsle()
    {
        SubmitTextInput();
    }

    /// <summary>
    /// Backward-compatible wrapper for existing speech bindings.
    /// Prefer SubmitSpeechInput in new code.
    /// </summary>
    [Obsolete("Use SubmitSpeechInput instead.")]
    public void SesDenGelenIstegiIsle(string sestenGelenMetin)
    {
        SubmitSpeechInput(sestenGelenMetin);
    }

    /// <summary>
    /// Editor test command for creating a red voxel cube.
    /// </summary>
    [ContextMenu("Test: Create Red Cube")]
    public async void TestCreateRedCube()
    {
        await ProcessPromptAsync("Create a red cube divided into three voxels per axis.");
    }

    /// <summary>
    /// Editor test command for validating cube, sphere, and cylinder generation together.
    /// </summary>
    [ContextMenu("Test: Create Multiple Objects")]
    public async void TestCreateMultipleObjects()
    {
        await ProcessPromptAsync("Create a blue cube on the left, a green sphere on the right, and a yellow cylinder in the center.");
    }

    /// <summary>
    /// Backward-compatible wrapper for the old context menu method.
    /// </summary>
    [Obsolete("Use TestCreateRedCube instead.")]
    public void TestEt()
    {
        TestCreateRedCube();
    }

    /// <summary>
    /// Backward-compatible wrapper for the old context menu method.
    /// </summary>
    [Obsolete("Use TestCreateMultipleObjects instead.")]
    public void TestCokluNesne()
    {
        TestCreateMultipleObjects();
    }

    /// <summary>
    /// Coordinates cooldown, timeout, Gemini transport, and scene application.
    /// </summary>
    private async Task ProcessPromptAsync(string userPrompt)
    {
        if (!CanStartRequest())
            return;

        string resolvedApiKey = ResolveApiKey();

        BeginLoadingState();
        lastRequestTime = Time.realtimeSinceStartup;

        try
        {
            cancellationSource = new CancellationTokenSource();
            cancellationSource.CancelAfter(TimeSpan.FromSeconds(requestTimeoutSeconds));

            string normalizedPrompt = NormalizeUserPrompt(userPrompt);

            // Handle reset commands locally — no AI call needed.
            if (IsResetCommand(normalizedPrompt))
            {
                geometryManager?.ResetScene();
                ShowStatus("Sahne temizlendi. Yeni bir tasarıma başlayabilirsiniz!");
                return;
            }

            // Handle Abstract City trigger locally
            if (IsAbstractCityCommand(normalizedPrompt))
            {
                if (ShowcaseGenerator.Instance != null && geometryManager != null)
                {
                    ShowcaseGenerator.Instance.GenerateAbstractCity(geometryManager.transform);
                    
                    // Add it to the director if it exists
                    var director = FindAnyObjectByType<ShowcaseDirector>();
                    if (director != null)
                    {
                        director.AddScene(geometryManager.transform.GetChild(geometryManager.transform.childCount - 1));
                    }
                    ShowStatus("Soyut Şehir (Abstract City) oluşturuldu!");
                }
                return;
            }

            // Handle Radial Monument trigger locally
            if (IsRadialMonumentCommand(normalizedPrompt))
            {
                if (ShowcaseGenerator.Instance != null && geometryManager != null)
                {
                    ShowcaseGenerator.Instance.GenerateRadialBalance(geometryManager.transform);
                    
                    var director = FindAnyObjectByType<ShowcaseDirector>();
                    if (director != null)
                    {
                        director.AddScene(geometryManager.transform.GetChild(geometryManager.transform.childCount - 1));
                    }
                    ShowStatus("Işınsal Denge (Radial Balance) anıtı oluşturuldu!");
                }
                return;
            }

            // Handle Deconstruction trigger locally
            if (IsDeconstructionCommand(normalizedPrompt))
            {
                if (ShowcaseGenerator.Instance != null && geometryManager != null)
                {
                    ShowcaseGenerator.Instance.GenerateDeconstruction(geometryManager.transform);
                    
                    var director = FindAnyObjectByType<ShowcaseDirector>();
                    if (director != null)
                    {
                        director.AddScene(geometryManager.transform.GetChild(geometryManager.transform.childCount - 1));
                    }
                    ShowStatus("Parçalanma ve Süreklilik (Deconstruction) anıtı oluşturuldu!");
                }
                return;
            }



            SymbolicAnalysisResult symbolicAnalysis = new KeywordSymbolicInputAnalyzer().Analyze(normalizedPrompt);
            string promptWithLocalHints = BuildPromptWithLocalHints(normalizedPrompt, symbolicAnalysis);
            string commandJson = await SendPromptToGeminiAsync(promptWithLocalHints, resolvedApiKey, cancellationSource.Token);
            if (string.IsNullOrEmpty(commandJson))
                return;

            // Strip out <think>...</think> blocks from reasoning models (e.g., DeepSeek-R1)
            int thinkStart = commandJson.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (thinkStart >= 0)
            {
                int thinkEnd = commandJson.IndexOf("</think>", thinkStart, StringComparison.OrdinalIgnoreCase);
                if (thinkEnd >= 0)
                {
                    commandJson = commandJson.Remove(thinkStart, thinkEnd + 8 - thinkStart).Trim();
                }
                else
                {
                    commandJson = commandJson.Substring(0, thinkStart).Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(commandJson))
            {
                ShowStatus("Model sadece düşünme süreci üretti, asıl cevabı veremedi.", StatusType.Warning);
                return;
            }

            Debug.Log($"[GeminiConnection] Cleaned command JSON:\n{commandJson}");

            // Truncation Detection: If the model started writing JSON but got cut off
            if (commandJson.Contains("```json") && !commandJson.EndsWith("```") && !commandJson.Contains("]\n```") && !commandJson.Contains("}\n```"))
            {
                if (commandJson.LastIndexOf("```json") > commandJson.LastIndexOf("```", commandJson.Length - 4))
                {
                    ShowStatus("Yapay zeka yanıtı yarıda kesildi! (Token limiti). Lütfen LM Studio ayarlarından Max Tokens sınırını artırın.", StatusType.Error);
                    Debug.LogWarning("[GeminiConnection] The response was truncated. LM Studio max_tokens limit reached.");
                    // We still try to apply what we can, but it will likely fail JSON parsing.
                }
            }

            ApplyGeometryCommand(commandJson, symbolicAnalysis);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[GeminiConnection] Request timed out or was cancelled.");
            ShowStatus($"Bağlantı zaman aşımı ({requestTimeoutSeconds}s). İnternet bağlantısını kontrol edin.", StatusType.Error);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GeminiConnection] Unexpected error: {exception.Message}");
            ShowStatus("Beklenmeyen bir hata oluştu.", StatusType.Error);
        }
        finally
        {
            EndLoadingState();
            cancellationSource?.Dispose();
            cancellationSource = null;
        }
    }

    private void ApplyGeometryCommand(string commandJson, SymbolicAnalysisResult symbolicAnalysis)
    {
        if (geometryManager == null)
            geometryManager = FindAnyObjectByType<GeometryManager>();

        if (geometryManager == null)
        {
            Debug.LogWarning("[GeminiConnection] GeometryManager not found in scene. Creating one dynamically.");
            GameObject geoObj = new GameObject("GeometryManager_Dynamic");
            geometryManager = geoObj.AddComponent<GeometryManager>();
        }

        geometryManager.ProcessCommandJson(commandJson, symbolicAnalysis);

        // Show the Turkish mentor feedback (the text before the JSON block) in the Bilgi panel.
        string feedback = ExtractFeedbackMessage(commandJson);
        if (!string.IsNullOrEmpty(feedback) && feedbackText != null)
            feedbackText.text = feedback;

        // ── LLM Spatial Operation Callback (SpatialFormPipeline dispatch) ──────
        // If the LLM appended a ```spatialop block, route it to SpatialFormPipeline.
        // This powers the AI-driven physics simulation pipeline.
        LLMCallbackRouter.TryDispatch(commandJson);

        ShowStatus("");
    }

    /// <summary>
    /// Extracts the mentor's Turkish explanation from the full AI response.
    /// The response format is: [Turkish text] followed by ```json ... ``.
    /// We display the Turkish text and hide the raw JSON from the user.
    /// </summary>
    private string ExtractFeedbackMessage(string fullResponse)
    {
        if (string.IsNullOrEmpty(fullResponse)) return null;

        // Find the start of the JSON block
        int jsonFence = fullResponse.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonFence < 0) jsonFence = fullResponse.IndexOf("```",  StringComparison.OrdinalIgnoreCase);
        if (jsonFence < 0) jsonFence = fullResponse.IndexOf('[');  // bare JSON array
        if (jsonFence < 0) jsonFence = fullResponse.IndexOf('{');  // bare JSON object

        string text = jsonFence > 0
            ? fullResponse.Substring(0, jsonFence).Trim()
            : fullResponse.Trim();

        // Strip markdown bold/italic markers
        text = Regex.Replace(text, @"\*{1,2}(.+?)\*{1,2}", "$1");
        text = Regex.Replace(text, @"#{1,3}\s*", "");

        return text.Length > 15 ? text : null;
    }

    /// <summary>
    /// Builds the final prompt by prepending the current scene state (so the AI sees
    /// where existing objects are) and appending local keyword hints from the analyzer.
    /// </summary>
    private string BuildPromptWithLocalHints(string normalizedPrompt, SymbolicAnalysisResult symbolicAnalysis)
    {
        StringBuilder builder = new StringBuilder(1024);

        // ── Inject current scene so the AI can position new objects correctly ──
        string sceneJson = geometryManager != null ? geometryManager.GetSceneHistoryAsJson() : string.Empty;
        if (!string.IsNullOrEmpty(sceneJson))
        {
            builder.AppendLine("[SCENE STATE — objects already in the scene (do NOT recreate these):");
            builder.AppendLine(sceneJson);
            builder.AppendLine("]");
            builder.AppendLine();
        }

        builder.AppendLine(normalizedPrompt);

        if (symbolicAnalysis == null)
            return builder.ToString();

        // ── Append local keyword hints ─────────────────────────────────────────
        bool hasAnyHint =
            symbolicAnalysis.RequestsBinaryPartition ||
            !string.IsNullOrEmpty(symbolicAnalysis.DetectedFormInteraction) ||
            !string.IsNullOrEmpty(symbolicAnalysis.DetectedDesignPrinciple) ||
            !string.IsNullOrEmpty(symbolicAnalysis.DetectedOrganizationSchema);

        if (!hasAnyHint)
            return builder.ToString();

        builder.AppendLine();
        builder.Append("[Local hints from student input:");

        if (symbolicAnalysis.RequestsBinaryPartition)
        {
            builder.Append(" binaryPartition=true;");
            builder.Append(" preferredAxis=");
            builder.Append(symbolicAnalysis.PreferredPartitionAxis.ToString().ToLowerInvariant());
            builder.Append(";");
        }

        if (!string.IsNullOrEmpty(symbolicAnalysis.DetectedFormInteraction))
        {
            builder.Append(" formInteraction=");
            builder.Append(symbolicAnalysis.DetectedFormInteraction);
            builder.Append(";");
        }

        if (!string.IsNullOrEmpty(symbolicAnalysis.DetectedDesignPrinciple))
        {
            builder.Append(" designPrinciple=");
            builder.Append(symbolicAnalysis.DetectedDesignPrinciple);
            builder.Append(";");
        }

        if (!string.IsNullOrEmpty(symbolicAnalysis.DetectedOrganizationSchema))
        {
            builder.Append(" organizationSchema=");
            builder.Append(symbolicAnalysis.DetectedOrganizationSchema);
            builder.Append(";");
        }

        builder.AppendLine(" — please honour these in your JSON and Turkish explanation.]");

        return builder.ToString();
    }

    /// <summary>
    /// Returns true when the user explicitly wants to clear the scene.
    /// These commands are handled locally without an AI call.
    /// </summary>
    private bool IsResetCommand(string normalizedPrompt)
    {
        if (string.IsNullOrWhiteSpace(normalizedPrompt))
            return false;

        string lower = normalizedPrompt.ToLowerInvariant().Trim();
        return lower.Contains("temizle") ||
               lower.Contains("sifirla") ||
               lower.Contains("sifirla") ||
               lower.Contains("resetle") ||
               lower.Contains("sahneyi sil") ||
               lower.Contains("yeniden basla") ||
               lower == "reset" ||
               lower == "clear";
    }

    /// <summary>
    /// Lightweight NLP to detect Abstract City intent.
    /// Looks for (city/kent/şehir) + (yap/kur/göster/model)
    /// </summary>
    private bool IsAbstractCityCommand(string normalizedPrompt)
    {
        if (string.IsNullOrWhiteSpace(normalizedPrompt)) return false;
        string lower = normalizedPrompt.ToLowerInvariant().Trim();
        
        bool hasCity = lower.Contains("şehir") || lower.Contains("sehir") || lower.Contains("kent") || lower.Contains("city");
        bool hasAction = lower.Contains("kur") || lower.Contains("yap") || lower.Contains("göster") || lower.Contains("model") || lower.Contains("oluştur");
        
        return (hasCity && hasAction) || lower.Contains("abstract city") || lower.Contains("soyut şehir");
    }

    /// <summary>
    /// Lightweight NLP to detect Radial Monument intent.
    /// Looks for (ışınsal/radyal/merkez) + (denge/anıt/göster)
    /// </summary>
    private bool IsRadialMonumentCommand(string normalizedPrompt)
    {
        if (string.IsNullOrWhiteSpace(normalizedPrompt)) return false;
        string lower = normalizedPrompt.ToLowerInvariant().Trim();
        
        bool hasRadial = lower.Contains("ışınsal") || lower.Contains("isinsal") || lower.Contains("radyal") || lower.Contains("radial");
        bool hasTarget = lower.Contains("denge") || lower.Contains("anıt") || lower.Contains("göster") || lower.Contains("model");
        
        return (hasRadial && hasTarget) || lower.Contains("radial balance");
    }

    /// <summary>
    /// Lightweight NLP to detect Deconstruction intent.
    /// Looks for (parça/erozyon/dağıl/kopma) + (anıt/göster/yap/model)
    /// </summary>
    private bool IsDeconstructionCommand(string normalizedPrompt)
    {
        if (string.IsNullOrWhiteSpace(normalizedPrompt)) return false;
        string lower = normalizedPrompt.ToLowerInvariant().Trim();
        
        bool hasDecon = lower.Contains("parçalan") || lower.Contains("parcalan") || lower.Contains("erozyon") || lower.Contains("dağılan") || lower.Contains("deconstruction");
        bool hasTarget = lower.Contains("anıt") || lower.Contains("göster") || lower.Contains("yap") || lower.Contains("model");
        
        return (hasDecon && hasTarget) || lower.Contains("sürekli parçalanma") || lower.Contains("surekli parcalanma");
    }

    /// <summary>
    /// Lightweight NLP to detect Exhibition Loop intent.
    /// Looks for (sergi/loop/sunum) + (başlat/baslat/göster)
    /// </summary>
    private bool IsExhibitionCommand(string normalizedPrompt)
    {
        if (string.IsNullOrWhiteSpace(normalizedPrompt)) return false;
        string lower = normalizedPrompt.ToLowerInvariant().Trim();
        
        bool hasExhibition = lower.Contains("sergi") || lower.Contains("loop") || lower.Contains("sunum") || lower.Contains("exhibition");
        bool hasAction = lower.Contains("başlat") || lower.Contains("baslat") || lower.Contains("göster") || lower.Contains("otomatik");
        
        return (hasExhibition && hasAction) || lower.Contains("start exhibition") || lower.Contains("showcase loop");
    }


    /// <summary>
    /// Sends one request to OpenAI and returns only the model's JSON text.
    /// Handles one optional retry for temporary HTTP failures.
    /// </summary>
    private async Task<string> SendPromptToGeminiAsync(
        string userPrompt,
        string resolvedApiKey,
        CancellationToken cancellationToken,
        int retryAttempt = 0)
    {
        string requestJson = JsonConvert.SerializeObject(BuildRequestBody(userPrompt));
        byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);

        using (UnityWebRequest request = new UnityWebRequest(LocalAiUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(requestBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + resolvedApiKey);

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
                return ExtractCommandJson(request.downloadHandler.text);

            if (request.responseCode == 429)
                return await HandleRateLimitAsync(request, userPrompt, resolvedApiKey, cancellationToken, retryAttempt);

            if (request.responseCode == 503)
                return await HandleServiceUnavailableAsync(request, userPrompt, resolvedApiKey, cancellationToken, retryAttempt);

            HandleHttpError(request);
            return null;
        }
    }

    /// <summary>
    /// Builds the OpenAI request body.
    /// </summary>
    private object BuildRequestBody(string userPrompt)
    {
        return new
        {
            model = "local-model",
            messages = new[]
            {
                new { role = "system", content = SystemInstruction },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.4f,
            // 4096 tokens for output: complex structures like doghouses need more room.
            max_tokens = 4096
        };
    }

    /// <summary>
    /// Waits for the server or inspector retry delay, then retries the request once.
    /// </summary>
    private async Task<string> HandleRateLimitAsync(
        UnityWebRequest request,
        string userPrompt,
        string resolvedApiKey,
        CancellationToken cancellationToken,
        int retryAttempt)
    {
        Debug.LogError($"[GeminiConnection] HTTP 429 rate limit: {request.error}");

        float retryDelaySeconds = GetRetryDelaySeconds(request);
        if (retryDelaySeconds <= 0f || retryAttempt >= MaxAutomaticRetryAttempts)
        {
            ShowStatus("API kotası doldu (429). Birkaç saniye bekleyip tekrar deneyin.", StatusType.Error);
            return null;
        }

        await WaitForRateLimitRetryAsync(retryDelaySeconds, cancellationToken);
        lastRequestTime = -999f;

        return await SendPromptToGeminiAsync(userPrompt, resolvedApiKey, cancellationToken, retryAttempt + 1);
    }

    /// <summary>
    /// Retries once when API reports temporary service unavailability.
    /// </summary>
    private async Task<string> HandleServiceUnavailableAsync(
        UnityWebRequest request,
        string userPrompt,
        string resolvedApiKey,
        CancellationToken cancellationToken,
        int retryAttempt)
    {
        Debug.LogWarning($"[OpenAIConnection] HTTP 503 service unavailable: {request.error}");

        if (retryAttempt >= MaxAutomaticRetryAttempts)
        {
            HandleHttpError(request);
            return null;
        }

        float retryDelaySeconds = Mathf.Max(ServiceUnavailableRetryDelaySeconds, GetRetryDelaySeconds(request));
        ShowStatus($"Sunucu meşgul. {retryDelaySeconds:F0}s sonra tekrar deneniyor...", StatusType.Warning);

        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
        lastRequestTime = -999f;

        return await SendPromptToGeminiAsync(userPrompt, resolvedApiKey, cancellationToken, retryAttempt + 1);
    }

    /// <summary>
    /// Chooses the larger value between Retry-After and the inspector retry delay.
    /// </summary>
    private float GetRetryDelaySeconds(UnityWebRequest request)
    {
        float retryDelaySeconds = automaticRetryDelaySeconds;
        string retryAfterHeader = request.GetResponseHeader("Retry-After");

        if (float.TryParse(retryAfterHeader, out float serverDelaySeconds))
            retryDelaySeconds = Mathf.Max(retryDelaySeconds, serverDelaySeconds);

        return retryDelaySeconds;
    }

    /// <summary>
    /// Shows a visible countdown while waiting before an automatic retry.
    /// </summary>
    private async Task WaitForRateLimitRetryAsync(float delaySeconds, CancellationToken cancellationToken)
    {
        Debug.LogWarning($"[GeminiConnection] Retrying after {delaySeconds:F0}s due to rate limit.");

        float remainingSeconds = delaySeconds;
        while (remainingSeconds > 0f)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ShowStatus($"API kotası doldu. {remainingSeconds:F0}s sonra tekrar deneniyor...", StatusType.Warning);

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            remainingSeconds -= 1f;
        }

        ShowStatus("Tekrar deneniyor...", StatusType.Info);
    }

    /// <summary>
    /// Extracts the model text from the OpenAI API response envelope.
    /// </summary>
    private string ExtractCommandJson(string rawResponse)
    {
        try
        {
            JObject response = JObject.Parse(rawResponse);
            JToken message = response["choices"]?[0]?["message"];

            // Primary: standard content field.
            string commandText = message?["content"]?.ToString();

            // Fallback: thinking/reasoning models (e.g. Gemma 4, DeepSeek-R1) may return
            // an empty 'content' and put their full output inside 'reasoning_content'.
            if (string.IsNullOrWhiteSpace(commandText))
            {
                commandText = message?["reasoning_content"]?.ToString();
                if (!string.IsNullOrWhiteSpace(commandText))
                    Debug.LogWarning("[GeminiConnection] 'content' was empty — falling back to 'reasoning_content'.");
            }

            if (string.IsNullOrWhiteSpace(commandText))
            {
                Debug.LogError($"[GeminiConnection] Response did not contain command text.\nRaw response: {rawResponse}");
                ShowStatus("Model boş yanıt döndürdü.", StatusType.Error);
                return null;
            }

            return commandText;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GeminiConnection] Response parse failed: {exception.Message}\nRaw response: {rawResponse}");
            ShowStatus("Yanıt çözümlenemedi.", StatusType.Error);
            return null;
        }
    }

    /// <summary>
    /// Converts non-429 HTTP failures into user-facing status messages.
    /// </summary>
    private void HandleHttpError(UnityWebRequest request)
    {
        Debug.LogError($"[OpenAIConnection] HTTP error {request.responseCode}: {request.error}\n{request.downloadHandler?.text}");

        string errorMessage = request.responseCode switch
        {
            400 => "Geçersiz istek (HTTP 400). Komut formatını kontrol edin.",
            401 => "API anahtarı geçersiz (HTTP 401). Lütfen Inspector'dan OpenAI (sk-...) anahtarınızı girin.",
            403 => "API anahtarı yetkisiz (HTTP 403).",
            500 => "OpenAI sunucu hatası (HTTP 500). Tekrar deneyin.",
            503 => "OpenAI şu an meşgul (HTTP 503). Bir süre bekleyin.",
            _ => $"Bağlantı hatası ({request.responseCode}): {request.error}"
        };

        ShowStatus(errorMessage, StatusType.Error);
    }

    /// <summary>
    /// Enforces local cooldown before starting a new request.
    /// </summary>
    private bool CanStartRequest()
    {
        float elapsedSeconds = Time.realtimeSinceStartup - lastRequestTime;
        if (elapsedSeconds >= minSecondsBetweenRequests)
            return true;

        float remainingSeconds = minSecondsBetweenRequests - elapsedSeconds;
        ShowStatus($"Lütfen {remainingSeconds:F1}s bekleyin...", StatusType.Warning);
        Debug.LogWarning($"[GeminiConnection] Cooldown active. {remainingSeconds:F1}s remaining.");
        return false;
    }

    /// <summary>
    /// Returns a dummy API key for local LLM or the inspector override if needed.
    /// </summary>
    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(apiKey) && !apiKey.StartsWith("sk-proj"))
            return apiKey.Trim();

        // Local LM Studio doesn't require an actual API key.
        return "lm-studio-local-key";
    }

    /// <summary>
    /// Repairs common speech or typing mistakes before the prompt reaches Gemini.
    /// </summary>
    private string NormalizeUserPrompt(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
            return userPrompt;

        return Regex.Replace(userPrompt, @"\bavi\b", "mavi", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Disables inputs and shows the loading state.
    /// </summary>
    private void BeginLoadingState()
    {
        isRequestInProgress = true;

        if (sendButton != null)
            sendButton.interactable = false;

        if (userInputField != null)
            userInputField.interactable = false;

        ShowStatus("Düşünüyor...", StatusType.Info);
    }

    /// <summary>
    /// Restores inputs and optionally shows the remaining cooldown.
    /// </summary>
    private void EndLoadingState()
    {
        isRequestInProgress = false;

        if (sendButton != null)
            sendButton.interactable = true;

        if (userInputField != null)
            userInputField.interactable = true;

        float elapsedSeconds = Time.realtimeSinceStartup - lastRequestTime;
        float remainingCooldownSeconds = minSecondsBetweenRequests - elapsedSeconds;
        if (remainingCooldownSeconds > 0.5f)
            ShowStatus($"Sonraki komut için {remainingCooldownSeconds:F0}s bekleyin.", StatusType.Warning);
    }

    private enum StatusType
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Updates the status text and color in one place.
    /// </summary>
    private void ShowStatus(string message, StatusType statusType = StatusType.Info)
    {
        if (statusMessageText == null)
            return;

        statusMessageText.text = message;
        statusMessageText.color = statusType switch
        {
            StatusType.Error => new Color(0.9f, 0.2f, 0.2f),
            StatusType.Warning => new Color(1.0f, 0.7f, 0.0f),
            _ => Color.white
        };
    }
}
