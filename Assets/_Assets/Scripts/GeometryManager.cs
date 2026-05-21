using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;
using HoloLensApp.Interaction.Snapping;
using HoloLensApp.Interaction.CSG;
using HoloLensApp.Interaction.Math;

/// <summary>
/// Engine-friendly representation of a single object request from Gemini.
/// The parser accepts both English and legacy Turkish JSON field names.
/// </summary>
[Serializable]
public sealed class DesignCommand
{
    public string Action = "create";
    public VoxelShape Shape = VoxelShape.Cube;
    public int Subdivision = 1;
    public string ColorValue = "white";
    public Vector3 PositionOffset = Vector3.zero;
    public float Scale = 1f;
    public Vector3 ScaleVector = Vector3.one;
    public BinaryPartitionInstruction BinaryPartition = BinaryPartitionInstruction.None();

    // --- ISAS 2018 spatial fields ---
    /// <summary>Euler rotation in degrees [x, y, z] applied to the whole object group.</summary>
    public Vector3 Rotation = Vector3.zero;

    /// <summary>
    /// World-space position written back by CreateObject after first placement.
    /// Used by GetSceneHistoryAsJson() and rebuildScene so objects don't drift
    /// when the camera moves between commands.
    /// </summary>
    public Vector3 WorldPosition = Vector3.zero;

    // --- ISAS 2018 pedagogical metadata (informational; does not affect geometry) ---
    /// <summary>Wong (1969) form interaction type, e.g. "touching", "overlapping".</summary>
    public string FormInteraction = string.Empty;
    /// <summary>Ching (2014) organization schema, e.g. "linear", "radial".</summary>
    public string OrganizationSchema = string.Empty;
    /// <summary>Active Ching design principle, e.g. "harmony", "balance".</summary>
    public string DesignPrinciple = string.Empty;
    /// <summary>Active design sub-principle, e.g. "repetition", "asymmetry".</summary>
    public string SubPrinciple = string.Empty;
}

/// <summary>
/// Parses Gemini JSON commands and builds voxel-based 3D objects in the Unity scene.
/// Supported shapes are cube, sphere, and cylinder.
/// </summary>
public class GeometryManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Generated objects are parented under this scene root.")]
    [FormerlySerializedAs("sahnePaReadAdi")]
    [SerializeField] private string sceneRootName = "AI_Scene";

    [Tooltip("Distance in meters in front of the main camera where objects are spawned.")]
    [FormerlySerializedAs("kameraOnMesafesi")]
    [SerializeField] private float cameraForwardDistance = 1.5f;

    [Header("Optional Error Output")]
    [Tooltip("Optional TMP text used to display geometry parsing errors.")]
    [FormerlySerializedAs("hataMesajiMetni")]
    [SerializeField] private TMP_Text errorMessageText;

    private const float VoxelGapRatio = 0.95f;
    private const int MinSubdivision = 1;
    private const int MinCurvedSubdivision = 8;
    private const int MaxSubdivision = 10;
    private const float BaseObjectSize = 0.5f;
    private const float DefaultObjectGap = 0.2f;
    private const float DefaultPartitionGap = 0.08f;
    private const float PositionEpsilon = 0.0001f;

    private static readonly Dictionary<string, Color> ColorTable = new Dictionary<string, Color>
    {
        { "red", new Color(0.90f, 0.18f, 0.18f) },
        { "kirmizi", new Color(0.90f, 0.18f, 0.18f) },
        { "blue", new Color(0.15f, 0.47f, 0.90f) },
        { "mavi", new Color(0.15f, 0.47f, 0.90f) },
        { "avi", new Color(0.15f, 0.47f, 0.90f) },
        { "yellow", new Color(0.97f, 0.80f, 0.10f) },
        { "sari", new Color(0.97f, 0.80f, 0.10f) },
        { "green", new Color(0.18f, 0.72f, 0.30f) },
        { "yesil", new Color(0.18f, 0.72f, 0.30f) },
        { "white", Color.white },
        { "beyaz", Color.white },
        { "black", Color.black },
        { "siyah", Color.black },
        { "orange", new Color(0.97f, 0.50f, 0.10f) },
        { "turuncu", new Color(0.97f, 0.50f, 0.10f) },
        { "purple", new Color(0.58f, 0.18f, 0.85f) },
        { "mor", new Color(0.58f, 0.18f, 0.85f) },
        { "pink", new Color(0.97f, 0.45f, 0.70f) },
        { "pembe", new Color(0.97f, 0.45f, 0.70f) },
        { "cyan", new Color(0.10f, 0.80f, 0.85f) },
        { "gray", new Color(0.55f, 0.55f, 0.55f) },
        { "grey", new Color(0.55f, 0.55f, 0.55f) },
        { "gri", new Color(0.55f, 0.55f, 0.55f) },
        { "brown", new Color(0.55f, 0.30f, 0.10f) },
        { "kahverengi", new Color(0.55f, 0.30f, 0.10f) },
    };

    private readonly List<DesignCommand> lastCreatedCommands = new List<DesignCommand>();

    /// <summary>
    /// Full scene history: grows with every ProcessCommandJson call.
    /// Used to inject context into the AI prompt and to support ResetScene.
    /// </summary>
    private readonly List<DesignCommand> cumulativeHistory = new List<DesignCommand>();

    /// <summary>
    /// Set by ParseCommands when the AI response contains {"rebuildScene":true}.
    /// ProcessCommandJson reads this to clear the scene before adding the new objects.
    /// </summary>
    private bool _pendingRebuild;

    /// <summary>
    /// World-space anchor captured on the first object placement of the session.
    /// All objects (additive AND rebuildScene) use sessionAnchor + PositionOffset
    /// so the coordinate system stays consistent even if the camera moves.
    /// Reset by ResetScene().
    /// </summary>
    private Vector3 _sessionAnchor;
    private bool    _sessionAnchorSet;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Main entry point used by GeminiConnection.
    /// ADDITIVE MODE: new objects are placed into the existing scene without
    /// clearing previous objects. Call <see cref="ResetScene"/> to start fresh.
    /// </summary>
    private bool HasMeaningfulPositions(List<DesignCommand> commands)
    {
        int meaningfulCount = 0;
        foreach (DesignCommand command in commands)
        {
            if (command != null && command.PositionOffset.sqrMagnitude > 0.01f)
                meaningfulCount++;
        }
        return meaningfulCount > 0;
    }

    public void ProcessCommandJson(string jsonData, SymbolicAnalysisResult symbolicAnalysis = null)
    {
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            ShowError("[GeometryManager] Empty JSON command received.");
            return;
        }

        List<DesignCommand> commands = ParseCommands(jsonData);
        if (commands == null || commands.Count == 0)
        {
            ShowError("[GeometryManager] No valid geometry command was found.");
            return;
        }

        bool isSplitRequest = symbolicAnalysis != null && symbolicAnalysis.RequestsBinaryPartition;
        if (isSplitRequest)
        {
            if (ShouldApplyPartitionToPreviousScene(symbolicAnalysis))
                commands = CloneLastCommands();

            ApplySymbolicPartitionHints(commands, symbolicAnalysis);
        }

        bool hasMeaningfulPos = HasMeaningfulPositions(commands);

        // ── PATH 0: CSG BOOLEAN OPERATIONS ─────────────────────────────────────────
        foreach (DesignCommand command in commands)
        {
            if (command != null && IsCSGAction(command.Action))
            {
                ExecuteCSGAction(command.Action);
                return;
            }
        }

        // ── PATH 2: REBUILD — animate existing objects to new positions ──────────
        if (_pendingRebuild)
        {
            _pendingRebuild = false;
            if (!hasMeaningfulPos) EnforceInteractionSpacing(commands);

            GameObject rebuildRoot = GameObject.Find(sceneRootName) ?? new GameObject(sceneRootName);
            List<Transform> existing = GetSceneChildren(rebuildRoot);

            List<Vector3> targets = hasMeaningfulPos
                ? commands.ConvertAll(c => _sessionAnchor + c.PositionOffset)
                : CalculateInteractionTargets(existing, commands);

            cumulativeHistory.Clear();
            lastCreatedCommands.Clear();
            RememberCreatedCommands(commands);

            StartCoroutine(AnimateRearrange(existing, targets, commands, rebuildRoot));
            Debug.Log("[GeometryManager] Rearrangement animation started.");
            return;
        }

        // ── PATH 3: SPLIT (Burst Animation) ──────────────────────────────────────
        if (isSplitRequest)
        {
            if (!hasMeaningfulPos) EnforceInteractionSpacing(commands);

            GameObject partRoot = GameObject.Find(sceneRootName) ?? new GameObject(sceneRootName);
            Vector3 splitOrigin = GetSceneCentroid(partRoot);

            ClearGeneratedScene();
            cumulativeHistory.Clear();
            lastCreatedCommands.Clear();
            RememberCreatedCommands(commands);

            StartCoroutine(AnimateSplit(commands, partRoot, splitOrigin));
            Debug.Log("[GeometryManager] Split animation started.");
            return;
        }

        // ── PATH 1: NORMAL ADDITIVE — spawn-bounce new objects ───────────────────
        if (!hasMeaningfulPos) EnforceInteractionSpacing(commands);

        GameObject sceneRoot = GameObject.Find(sceneRootName) ?? new GameObject(sceneRootName);

        int createdCount = 0;
        int spawnIndex   = 0;
        foreach (DesignCommand command in commands)
        {
            if (command == null) continue;
            if (!IsCreateAction(command.Action))
            {
                Debug.LogWarning($"[GeometryManager] Unsupported action '{command.Action}' was skipped.");
                continue;
            }
            CreateObject(command, sceneRoot.transform, spawnIndex);
            spawnIndex++;
            createdCount++;
        }

        if (createdCount > 0)
            RememberCreatedCommands(commands);

        Debug.Log($"[GeometryManager] Created {createdCount} object(s).");
    }

    /// <summary>
    /// Backward-compatible wrapper for existing UnityEvent bindings.
    /// Prefer ProcessCommandJson in new code.
    /// </summary>
    [Obsolete("Use ProcessCommandJson instead.")]
    public void KomutuIsle(string jsonVerisi)
    {
        ProcessCommandJson(jsonVerisi);
    }

    /// <summary>
    /// Destroys all generated geometry and clears the scene history.
    /// Call this when the student wants to start a new design from scratch.
    /// Also accessible via Inspector right-click menu for quick testing.
    /// </summary>
    [ContextMenu("Reset Scene")]
    public void ResetScene()
    {
        ClearGeneratedScene();
        cumulativeHistory.Clear();
        lastCreatedCommands.Clear();
        _sessionAnchorSet = false;   // Next object will capture a fresh camera position
        Debug.Log("[GeometryManager] Scene reset. History and anchor cleared.");
    }

    /// <summary>
    /// Returns the current scene as a compact JSON string suitable for injecting into the AI prompt.
    /// Returns an empty string when the scene is empty.
    /// </summary>
    public string GetSceneHistoryAsJson()
    {
        if (cumulativeHistory.Count == 0)
            return string.Empty;

        var list = new System.Collections.Generic.List<object>(cumulativeHistory.Count);
        foreach (DesignCommand cmd in cumulativeHistory)
        {
            list.Add(new
            {
                action = cmd.Action,
                shape = cmd.Shape.ToString().ToLowerInvariant(),
                subdivision = cmd.Subdivision,
                color = cmd.ColorValue,
                // Export PositionOffset (session-anchor-relative coords in meters).
                position = new float[] { cmd.PositionOffset.x, cmd.PositionOffset.y, cmd.PositionOffset.z },
                scale = new float[] { cmd.ScaleVector.x, cmd.ScaleVector.y, cmd.ScaleVector.z },
                rotation = new float[] { cmd.Rotation.x, cmd.Rotation.y, cmd.Rotation.z },
                formInteraction = cmd.FormInteraction,
                organizationSchema = cmd.OrganizationSchema,
                designPrinciple = cmd.DesignPrinciple,
                subPrinciple = cmd.SubPrinciple
            });
        }

        return JsonConvert.SerializeObject(list);
    }

    public int GetGeneratedObjectCount()
    {
        GameObject root = GameObject.Find(sceneRootName);
        return root != null ? root.transform.childCount : 0;
    }

    public bool ExecuteSceneBooleanOperation(string action)
    {
        return ExecuteCSGAction(action);
    }

    public bool CreateIntersectionResultFromOverlap(GameObject objA, GameObject objB)
    {
        GameObject root = GameObject.Find(sceneRootName) ?? new GameObject(sceneRootName);
        return TryCreateAabbIntersection(objA, objB, root.transform, allowAutoOverlap: false);
    }

    public bool CreatePrimitiveBatch(string shapeName, string colorValue, int count)
    {
        count = Mathf.Clamp(count, 1, MaxSubdivision);
        GameObject sceneRoot = GameObject.Find(sceneRootName) ?? new GameObject(sceneRootName);

        VoxelShape shape = ResolveShape(shapeName);
        float step = BaseObjectSize + DefaultObjectGap;
        float startX = -((count - 1) * step) * 0.5f;

        List<DesignCommand> commands = new List<DesignCommand>(count);
        for (int i = 0; i < count; i++)
        {
            commands.Add(new DesignCommand
            {
                Action = "create",
                Shape = shape,
                Subdivision = shape == VoxelShape.Cube ? MinSubdivision : MinCurvedSubdivision,
                ColorValue = colorValue,
                ScaleVector = Vector3.one,
                Scale = 1f,
                PositionOffset = new Vector3(startX + i * step, 0f, 0f)
            });
        }

        for (int i = 0; i < commands.Count; i++)
            CreateObject(commands[i], sceneRoot.transform, i);

        RememberCreatedCommands(commands);
        Debug.Log($"[GeometryManager] Created {count} primitive object(s) locally.");
        return true;
    }

    public bool CreateSplitPrimitive(string shapeName, string colorValue, PartitionAxis axis)
    {
        if (!_sessionAnchorSet)
        {
            _sessionAnchor = GetCameraForwardPosition();
            _sessionAnchorSet = true;
        }

        GameObject sceneRoot = GameObject.Find(sceneRootName) ?? new GameObject(sceneRootName);
        Material material = CreateMaterial(colorValue);
        Bounds bounds = new Bounds(_sessionAnchor, Vector3.one * 0.95f);
        bool result = CreateSplitPiecesFromBounds(bounds, material, sceneRoot.transform, ResolveShape(shapeName).ToString(), axis);

        if (result)
        {
            lastCreatedCommands.Clear();
            cumulativeHistory.Clear();
        }

        return result;
    }

    public bool SplitLastGeneratedObject(PartitionAxis axis)
    {
        GameObject root = GameObject.Find(sceneRootName);
        if (root == null || root.transform.childCount == 0)
            return CreateSplitPrimitive("cube", "white", axis);

        GameObject source = root.transform.GetChild(root.transform.childCount - 1).gameObject;
        if (!TryGetObjectBounds(source, out Bounds bounds))
            return false;

        Renderer renderer = source.GetComponentInChildren<Renderer>();
        Material material = renderer != null ? renderer.sharedMaterial : CreateMaterial("white");
        string sourceName = source.name;

        source.transform.DOKill();
        bool result = CreateSplitPiecesFromBounds(bounds, material, root.transform, sourceName, axis);
        Destroy(source);
        return result;
    }


    /// <summary>
    /// Converts raw Gemini output into a list of validated DesignCommand objects.
    /// The method is intentionally tolerant because LLMs can vary field names.
    /// </summary>
    private List<DesignCommand> ParseCommands(string jsonData)
    {
        try
        {
            string cleanedJson = ExtractJsonPayload(jsonData);
            Debug.Log($"[GeometryManager] Extracted JSON ({cleanedJson.Length} chars):\n{cleanedJson.Substring(0, Mathf.Min(500, cleanedJson.Length))}");
            JToken root = JToken.Parse(cleanedJson);

            if (root.Type == JTokenType.Array)
                return ParseCommandArray((JArray)root);

            if (root.Type == JTokenType.Object)
            {
                JObject rootObject = (JObject)root;

                // Check for rebuild flag: {"rebuildScene": true, "objects": [...]}
                JToken rebuildToken = GetProperty(rootObject, "rebuildScene", "rebuild", "clearAndRebuild", "yenidenCiz");
                if (rebuildToken != null && rebuildToken.Type == JTokenType.Boolean && rebuildToken.Value<bool>())
                    _pendingRebuild = true;

                JToken wrappedCommands = GetProperty(rootObject, "commands", "objects", "items", "komutlar", "nesneler");
                if (wrappedCommands is JArray commandArray)
                    return ParseCommandArray(commandArray);

                DesignCommand singleCommand = ParseCommandObject(rootObject);
                return new List<DesignCommand> { singleCommand };
            }

            ShowError("[GeometryManager] JSON root must be an object or array.");
            return null;
        }
        catch (JsonException exception)
        {
            Debug.LogError($"[GeometryManager] JSON parse failed: {exception.Message}\nPayload: {jsonData}");
            ShowError("Geometry command format is invalid.");
            return null;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GeometryManager] Command parse failed: {exception.Message}\nPayload: {jsonData}");
            ShowError("Geometry command could not be processed.");
            return null;
        }
    }

    /// <summary>
    /// Parses each object in an array and skips entries that are not JSON objects.
    /// </summary>
    private List<DesignCommand> ParseCommandArray(JArray commandArray)
    {
        List<DesignCommand> commands = new List<DesignCommand>();

        foreach (JToken token in commandArray)
        {
            if (token.Type != JTokenType.Object)
            {
                Debug.LogWarning("[GeometryManager] Non-object command entry skipped.");
                continue;
            }

            commands.Add(ParseCommandObject((JObject)token));
        }

        return commands;
    }

    /// <summary>
    /// Maps one JSON object into the internal command model.
    /// English field names are preferred, while Turkish names remain supported.
    /// </summary>
    private DesignCommand ParseCommandObject(JObject source)
    {
        string action = ReadString(source, "action", "command", "komut");
        string shapeName = ReadString(source, "shape", "type", "primitive", "sekil", "şekil");
        VoxelShape shape = ResolveShape(shapeName);

        int defaultSubdivision = shape == VoxelShape.Cube ? MinSubdivision : MinCurvedSubdivision;

        JToken scaleToken = GetProperty(source, "scale", "size", "olcek", "ölçek");
        Vector3 scaleVec = Vector3.one;
        float scaleFloat = 1f;

        if (scaleToken != null)
        {
            if (scaleToken.Type == JTokenType.Array)
            {
                scaleVec = ReadVector3(source, "scale", "size", "olcek", "ölçek");
                scaleFloat = scaleVec.x; // fallback for uniform scaling paths
            }
            else
            {
                scaleFloat = Mathf.Max(0.01f, scaleToken.Value<float>());
                scaleVec = new Vector3(scaleFloat, scaleFloat, scaleFloat);
            }
        }

        DesignCommand cmd = new DesignCommand
        {
            Action = string.IsNullOrWhiteSpace(action) ? "create" : action,
            Shape = shape,
            Subdivision = ReadInt(source, defaultSubdivision, "subdivision", "divisions", "segments", "bolunme", "bölünme"),
            ColorValue = ReadString(source, "color", "colour", "renk") ?? "white",
            PositionOffset = ReadVector3(source, "position", "positionOffset", "offset", "konum"),
            Scale = scaleFloat,
            ScaleVector = scaleVec,
            BinaryPartition = ReadBinaryPartition(source),
            // ISAS 2018 spatial
            Rotation = ReadVector3(source, "rotation", "rotate", "euler", "donme", "dönme"),
            // ISAS 2018 pedagogical metadata
            FormInteraction = ReadString(source, "formInteraction", "interaction", "wongInteraction", "formEtkilesimi") ?? string.Empty,
            OrganizationSchema = ReadString(source, "organizationSchema", "schema", "layout", "organizasyon") ?? string.Empty,
            DesignPrinciple = ReadString(source, "designPrinciple", "principle", "tasarimIlkesi") ?? string.Empty,
            SubPrinciple = ReadString(source, "subPrinciple", "altIlke") ?? string.Empty,
        };

        // Log pedagogical metadata when present so educators can trace the AI's decisions.
        if (!string.IsNullOrWhiteSpace(cmd.FormInteraction) ||
            !string.IsNullOrWhiteSpace(cmd.OrganizationSchema) ||
            !string.IsNullOrWhiteSpace(cmd.DesignPrinciple))
        {
            Debug.Log($"[GeometryManager] ISAS metadata — Interaction: {cmd.FormInteraction} | " +
                      $"Schema: {cmd.OrganizationSchema} | Principle: {cmd.DesignPrinciple} | Sub: {cmd.SubPrinciple}");
        }

        return cmd;
    }

    /// <summary>
    /// Reads an optional instruction for splitting one generated object into two visible halves.
    /// </summary>
    private BinaryPartitionInstruction ReadBinaryPartition(JObject source)
    {
        JToken token = GetProperty(source, "partition", "binaryPartition", "binarySplit", "split", "ayir", "ayır", "bol", "böl");
        JToken axisToken = GetProperty(source, "partitionAxis", "splitAxis", "axis", "eksen");

        if (token == null && axisToken == null)
            return BinaryPartitionInstruction.None();

        BinaryPartitionInstruction instruction = new BinaryPartitionInstruction
        {
            Enabled = IsPartitionEnabled(token) || axisToken != null,
            Axis = ParsePartitionAxis(axisToken?.ToString(), PartitionAxis.X),
            Gap = DefaultPartitionGap
        };

        if (token is JObject partitionObject)
        {
            string mode = ReadString(partitionObject, "mode", "type", "kind");
            instruction.Enabled = ReadBool(partitionObject, IsBinaryPartitionMode(mode), "enabled", "active");
            instruction.Axis = ParsePartitionAxis(ReadString(partitionObject, "axis", "eksen"), instruction.Axis);
            instruction.Gap = Mathf.Max(0f, ReadFloat(partitionObject, DefaultPartitionGap, "gap", "spacing", "bosluk", "boşluk"));
            instruction.FirstColor = ReadString(partitionObject, "firstColor", "leftColor", "primaryColor");
            instruction.SecondColor = ReadString(partitionObject, "secondColor", "rightColor", "secondaryColor");

            JToken colors = GetProperty(partitionObject, "colors", "colours", "renkler");
            if (colors is JArray colorArray && colorArray.Count >= 2)
            {
                instruction.FirstColor = colorArray[0]?.ToString();
                instruction.SecondColor = colorArray[1]?.ToString();
            }
        }

        return instruction.Enabled ? instruction : BinaryPartitionInstruction.None();
    }

    /// <summary>
    /// Removes optional Markdown fences and extra text around the JSON payload.
    /// </summary>
    private string ExtractJsonPayload(string rawText)
    {
        string trimmed = rawText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineBreak = trimmed.IndexOf('\n');
            int closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineBreak >= 0 && closingFence > firstLineBreak)
                trimmed = trimmed.Substring(firstLineBreak + 1, closingFence - firstLineBreak - 1).Trim();
        }

        int firstArray = trimmed.IndexOf('[');
        int firstObject = trimmed.IndexOf('{');
        int startIndex = -1;

        if (firstArray >= 0 && firstObject >= 0)
            startIndex = Mathf.Min(firstArray, firstObject);
        else if (firstArray >= 0)
            startIndex = firstArray;
        else if (firstObject >= 0)
            startIndex = firstObject;

        if (startIndex < 0)
            return trimmed;

        char startChar = trimmed[startIndex];
        char endChar = startChar == '[' ? ']' : '}';
        int endIndex = trimmed.LastIndexOf(endChar);

        return endIndex >= startIndex
            ? trimmed.Substring(startIndex, endIndex - startIndex + 1)
            : trimmed.Substring(startIndex);
    }

    /// <summary>
    /// Removes all generated children from the scene root without destroying the root itself.
    /// IMPORTANT: We must NOT call Destroy(root) here because Destroy() is deferred to
    /// end-of-frame in Unity. If we destroy the root and then call GameObject.Find() in the
    /// same frame, we still get the old (to-be-destroyed) root back. New objects would be
    /// parented to it and destroyed along with it at frame end — causing the "disappearing
    /// objects on rebuildScene" bug. Destroying only the children avoids this entirely.
    /// </summary>
    private void ClearGeneratedScene()
    {
        GameObject root = GameObject.Find(sceneRootName);
        if (root == null)
            return;

        // Kill all active DOTween animations to prevent MissingReferenceExceptions
        // when the objects they are animating are destroyed below.
        DOTween.KillAll();

        // Destroy every child. The root itself stays alive so subsequent
        // ProcessCommandJson calls can still Find() it and parent new objects to it.
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(root.transform.GetChild(i).gameObject);
        }

        Debug.Log("[GeometryManager] Previous generated scene was cleared.");
    }

    /// <summary>
    /// Creates one voxel object group at sessionAnchor + PositionOffset and
    /// immediately starts a staggered spring-bounce spawn animation.
    /// </summary>
    private void CreateObject(DesignCommand command, Transform parent, int spawnIndex = 0)
    {
        int subdivision = GetSafeSubdivision(command.Subdivision, command.Shape);
        Vector3 objectScaleVector = command.ScaleVector;
        float voxelSize = 1.0f / subdivision;

        if (!_sessionAnchorSet)
        {
            _sessionAnchor    = GetCameraForwardPosition();
            _sessionAnchorSet = true;
            Debug.Log($"[GeometryManager] Session anchor set: {_sessionAnchor}");
        }

        GameObject group = new GameObject(BuildObjectName(command, subdivision));
        group.transform.SetParent(parent, worldPositionStays: false);
        group.transform.position   = _sessionAnchor + command.PositionOffset;
        group.transform.localScale = Vector3.zero;   // hidden until animation starts

        if (command.Rotation.sqrMagnitude > 0.0001f)
            group.transform.localRotation = Quaternion.Euler(command.Rotation);

        Material material   = CreateMaterial(command.ColorValue);
        bool isPartitioned  = command.BinaryPartition != null && command.BinaryPartition.Enabled;

        if (isPartitioned)
            CreatePartitionedShape(command, group.transform, subdivision, voxelSize, material);
        else
            CreateShapeVoxels(command.Shape, group.transform, subdivision, voxelSize, material);

        group.transform.localScale = objectScaleVector;

        // Add XR Interactions for grabbing generated objects
        var rb = group.AddComponent<Rigidbody>();
        rb.isKinematic = true; // Keep kinematic during DOTween animation
        rb.useGravity = false; // No gravity! Let holograms float in AR space
        rb.mass = 5f;
        rb.linearDamping = 5f; // Add friction so they stop moving when pushed
        rb.angularDamping = 5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var grab = group.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = true;
        grab.throwVelocityScale = 2.0f;

        group.AddComponent<HoloLensApp.Interaction.Math.FormInteractable>();
        group.AddComponent<HoloLensApp.Interaction.Snapping.GridSnapper>();

        // Physics spawn: drop from 0.25m above with spring-bounce.
        float   delay      = spawnIndex * 0.12f;
        Vector3 finalPos   = group.transform.position;
        Vector3 spawnStart = finalPos + Vector3.up * 0.25f;

        if (delay > 0f)
            StartCoroutine(DelayedPhysicsSpawn(group, spawnStart, finalPos, objectScaleVector, delay));
        else
            GeometryPhysics.Spawn(group, spawnStart, finalPos, objectScaleVector);

        // ── AI-driven visual build-in: ghost material scale-up over DOTween spawn ──
        // SpatialFormPipeline.AnimatedInstantiate plays over the existing bounce.
        if (SpatialFormPipeline.Instance != null)
            SpatialFormPipeline.Instance.AnimatedInstantiate(group, finalPos, 0.6f + delay);

        // ── Record FormInteraction for pedagogical metadata logging ────────────
        if (!string.IsNullOrWhiteSpace(command.FormInteraction))
            command.WorldPosition = finalPos;

        Debug.Log($"[GeometryManager] Created {group.name}. Shape: {command.Shape}, Sub: {subdivision}, " +
                  $"Scale: {command.Scale:F2}, Rot: {command.Rotation}, Partitioned: {isPartitioned}");
    }

    private IEnumerator ReleaseKinematicAfterSpawn(Rigidbody rb, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (rb != null) rb.isKinematic = false;
    }

    /// <summary>
    /// Snaps objects so they actually physically touch when the AI tags them with
    /// a touching-intent formInteraction. Supports Turkish synonyms (temas, yaklaş,
    /// birleş) and English near-synonyms (union, proximity, adjacent, merge).
    /// </summary>
    private void EnforceInteractionSpacing(List<DesignCommand> commands)
    {
        if (commands == null || commands.Count <= 1) return;

        // Sort left-to-right
        var sorted = new List<DesignCommand>(commands);
        sorted.Sort((a, b) => a.PositionOffset.x.CompareTo(b.PositionOffset.x));

        for (int i = 1; i < sorted.Count; i++)
        {
            float r0 = sorted[i - 1].ScaleVector.x * 0.5f;
            float r1 = sorted[i].ScaleVector.x * 0.5f;

            string interaction = (sorted[i].FormInteraction ?? "").ToLowerInvariant();

            float targetDistance;
            if (interaction.Contains("touch") || interaction.Contains("temas") || interaction.Contains("dokun"))
                targetDistance = r0 + r1; // Touching
            else if (interaction.Contains("overlap") || interaction.Contains("penetrat") || 
                     interaction.Contains("union") || interaction.Contains("birleş") || 
                     interaction.Contains("içegirme") || interaction.Contains("kesiş") || interaction.Contains("örtüş"))
                targetDistance = Mathf.Max(r0, r1) * 0.8f; // Overlapping/Penetration
            else if (interaction.Contains("coincid") || interaction.Contains("denk"))
                targetDistance = 0f; // Coinciding
            else
                targetDistance = r0 + r1 + DefaultObjectGap; // Detachment / default

            float prevX = sorted[i - 1].PositionOffset.x;
            float currentX = sorted[i].PositionOffset.x;
            float sign = currentX >= prevX ? 1f : -1f;
            if (sign == 0f) sign = 1f;

            sorted[i].PositionOffset = new Vector3(
                prevX + sign * targetDistance,
                sorted[i].PositionOffset.y,
                sorted[i].PositionOffset.z);
        }

        float avgX = GetAverageX(sorted);
        foreach (var cmd in sorted) cmd.PositionOffset.x -= avgX;
    }

    /// <summary>
    /// Spring-bounce spawn: the object drops 0.25 m from above its target and
    /// scales from 0 to full size with an elastic ease. Each object in a batch
    /// is delayed by spawnIndex * 0.12 s for a cascading effect.
    /// </summary>
    /// <summary>
    /// Waits <paramref name="delay"/> seconds, then attaches a GeometryPhysics.Spawn.
    /// Used when multiple objects spawn in a staggered cascade.
    /// </summary>
    private IEnumerator DelayedPhysicsSpawn(
        GameObject obj, Vector3 from, Vector3 to, Vector3 sizeVec, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
            GeometryPhysics.Spawn(obj, from, to, sizeVec);
    }

    // ── Transition animations (physics-based) ─────────────────────────────────

    /// <summary>
    /// REARRANGE: attaches GeometryPhysics.Rearrange to each existing child so they
    /// spring toward their new positions. After the springs settle (~1 s), the old
    /// voxel meshes are destroyed and rebuilt at the settled positions.
    /// </summary>
    private IEnumerator AnimateRearrange(
        List<Transform>     existing,
        List<Vector3>       targets,
        List<DesignCommand> newCommands,
        GameObject          root)
    {
        int moveCount = Mathf.Min(existing.Count, targets.Count);
        for (int i = 0; i < moveCount; i++)
            if (existing[i] != null)
                GeometryPhysics.Rearrange(existing[i].gameObject, targets[i]);

        // Wait for springs to settle, then replace with properly-built voxel meshes.
        yield return new WaitForSeconds(1.2f);

        ClearGeneratedScene();
        yield return null;

        // Rebuild at the ACTUAL animation targets, not the AI's PositionOffset values.
        // This guarantees visual consistency between the animated end-state and the rebuilt meshes.
        for (int i = 0; i < newCommands.Count; i++)
        {
            Vector3 finalPos = i < targets.Count
                ? targets[i]
                : _sessionAnchor + newCommands[i].PositionOffset;
            CreateObjectInstant(newCommands[i], root.transform, finalPos);
        }
    }



    /// <summary>
    /// Computes touching positions centered on the current centroid of existing objects.
    /// Objects slide TOWARD each other (symmetrically) rather than to AI-chosen offsets.
    /// </summary>
    private List<Vector3> CalculateInteractionTargets(
        List<Transform> existing,
        List<DesignCommand> commands)
    {
        var sorted = new List<Transform>(existing);
        sorted.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        var sortedCmds = new List<DesignCommand>(commands);
        sortedCmds.Sort((a, b) => a.PositionOffset.x.CompareTo(b.PositionOffset.x));

        Vector3 centroid = Vector3.zero;
        foreach (var t in sorted) centroid += t.position;
        centroid /= sorted.Count;

        var targets = new List<Vector3>(sorted.Count);
        if (sorted.Count == 0) return targets;

        targets.Add(sorted[0].position); 

        for (int i = 1; i < sorted.Count; i++)
        {
            float r0 = BaseObjectSize * (i - 1 < sortedCmds.Count ? sortedCmds[i - 1].Scale : 1f) * 0.5f;
            float r1 = BaseObjectSize * (i < sortedCmds.Count ? sortedCmds[i].Scale : 1f) * 0.5f;

            string interaction = (i < sortedCmds.Count ? sortedCmds[i].FormInteraction ?? "" : "").ToLowerInvariant();

            float targetDistance;
            if (interaction.Contains("touch") || interaction.Contains("temas") || interaction.Contains("dokun"))
                targetDistance = r0 + r1;
            else if (interaction.Contains("overlap") || interaction.Contains("penetrat") || 
                     interaction.Contains("union") || interaction.Contains("birleş") || 
                     interaction.Contains("içegirme") || interaction.Contains("kesiş") || interaction.Contains("örtüş"))
                targetDistance = Mathf.Max(r0, r1) * 0.8f;
            else if (interaction.Contains("coincid") || interaction.Contains("denk"))
                targetDistance = 0f;
            else
                targetDistance = r0 + r1 + DefaultObjectGap;

            float prevX = targets[i - 1].x;
            targets.Add(new Vector3(prevX + targetDistance, sorted[i].position.y, sorted[i].position.z));
        }

        float newCentroidX = 0f;
        foreach (var t in targets) newCentroidX += t.x;
        newCentroidX /= targets.Count;

        float offset = centroid.x - newCentroidX;
        for (int i = 0; i < targets.Count; i++)
            targets[i] = new Vector3(targets[i].x + offset, targets[i].y, targets[i].z);

        return targets;
    }

    /// <summary>
    /// SPLIT: creates partitioned objects at the scene centroid (scale 0) and
    /// launches them outward with GeometryPhysics.Split (spring + initial burst
    /// velocity), producing a physically energetic separation.
    /// </summary>
    private IEnumerator AnimateSplit(
        List<DesignCommand> commands,
        GameObject          root,
        Vector3             splitOrigin)
    {
        for (int i = 0; i < commands.Count; i++)
        {
            Vector3 sizeVec = commands[i].ScaleVector;
            Vector3 target = _sessionAnchor + commands[i].PositionOffset;
            GameObject obj = CreateObjectInstant(commands[i], root.transform, splitOrigin);
            GeometryPhysics.Split(obj, splitOrigin, target, sizeVec);
        }
        yield return null; // coroutine must yield at least once
    }

    /// <summary>
    /// Builds a fully-assembled voxel object at an explicit world position
    /// without starting a spawn animation (used internally by transition coroutines).
    /// </summary>
    private GameObject CreateObjectInstant(DesignCommand command, Transform parent, Vector3 worldPos)
    {
        int subdivision = GetSafeSubdivision(command.Subdivision, command.Shape);
        Vector3 objectScaleVector = command.ScaleVector;
        float voxelSize = 1.0f / subdivision;

        GameObject group = new GameObject(BuildObjectName(command, subdivision));
        group.transform.SetParent(parent, worldPositionStays: false);
        group.transform.position   = worldPos;
        group.transform.localScale = objectScaleVector;

        if (command.Rotation.sqrMagnitude > 0.0001f)
            group.transform.localRotation = Quaternion.Euler(command.Rotation);

        Material mat    = CreateMaterial(command.ColorValue);
        bool partitioned = command.BinaryPartition != null && command.BinaryPartition.Enabled;

        if (partitioned) CreatePartitionedShape(command, group.transform, subdivision, voxelSize, mat);
        else             CreateShapeVoxels(command.Shape, group.transform, subdivision, voxelSize, mat);

        return group;
    }

    private static List<Transform> GetSceneChildren(GameObject root)
    {
        var list = new List<Transform>(root.transform.childCount);
        for (int i = 0; i < root.transform.childCount; i++)
            list.Add(root.transform.GetChild(i));
        return list;
    }

    private Vector3 GetSceneCentroid(GameObject root)
    {
        if (root == null || root.transform.childCount == 0)
            return _sessionAnchorSet ? _sessionAnchor : GetCameraForwardPosition();
        Vector3 sum = Vector3.zero;
        foreach (Transform child in root.transform) sum += child.position;
        return sum / root.transform.childCount;
    }

    /// <summary>Smooth ease in-out cubic: gliding/sliding feel.</summary>
    private static float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    /// <summary>Ease-out with overshoot: energetic burst/split feel.</summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>
    /// Applies locally detected split intent when the model response did not include a partition object.
    /// </summary>
    private void ApplySymbolicPartitionHints(List<DesignCommand> commands, SymbolicAnalysisResult symbolicAnalysis)
    {
        if (commands == null || symbolicAnalysis == null || !symbolicAnalysis.RequestsBinaryPartition)
            return;

        // If the AI successfully returned multiple distinct objects for a split request,
        // do not force binary partitioning on each individual object.
        if (commands.Count > 1)
            return;

        foreach (DesignCommand command in commands)
        {
            if (command == null)
                continue;

            if (command.BinaryPartition != null && command.BinaryPartition.Enabled)
                continue;

            command.BinaryPartition = new BinaryPartitionInstruction
            {
                Enabled = true,
                Axis = symbolicAnalysis.PreferredPartitionAxis,
                Gap = DefaultPartitionGap
            };
        }
    }

    /// <summary>
    /// Uses the previous generated object when the user says only "split it" without naming a new shape.
    /// </summary>
    private bool ShouldApplyPartitionToPreviousScene(SymbolicAnalysisResult symbolicAnalysis)
    {
        return symbolicAnalysis != null
            && symbolicAnalysis.RequestsBinaryPartition
            && !HasShapeSymbol(symbolicAnalysis)
            && lastCreatedCommands.Count > 0;
    }

    private bool HasShapeSymbol(SymbolicAnalysisResult symbolicAnalysis)
    {
        for (int index = 0; index < symbolicAnalysis.Symbols.Count; index++)
        {
            if (symbolicAnalysis.Symbols[index].Type == CommandSymbolType.Shape)
                return true;
        }

        return false;
    }

    private List<DesignCommand> CloneLastCommands()
    {
        List<DesignCommand> clones = new List<DesignCommand>(lastCreatedCommands.Count);

        foreach (DesignCommand command in lastCreatedCommands)
            clones.Add(CloneCommand(command));

        return clones;
    }

    private void RememberCreatedCommands(List<DesignCommand> commands)
    {
        // lastCreatedCommands: always reflects the LATEST batch (for partition feature).
        lastCreatedCommands.Clear();

        foreach (DesignCommand command in commands)
        {
            if (command == null || !IsCreateAction(command.Action))
                continue;

            DesignCommand clone = CloneCommand(command);
            lastCreatedCommands.Add(clone);

            // cumulativeHistory: grows permanently until ResetScene() is called.
            // This is what GetSceneHistoryAsJson() exports to the AI prompt.
            cumulativeHistory.Add(CloneCommand(command));
        }
    }

    private DesignCommand CloneCommand(DesignCommand source)
    {
        return new DesignCommand
        {
            Action = source.Action,
            Shape = source.Shape,
            Subdivision = source.Subdivision,
            ColorValue = source.ColorValue,
            PositionOffset = source.PositionOffset,
            Scale = source.Scale,
            BinaryPartition = CloneBinaryPartition(source.BinaryPartition),
            // ISAS 2018 fields
            Rotation = source.Rotation,
            FormInteraction = source.FormInteraction,
            OrganizationSchema = source.OrganizationSchema,
            DesignPrinciple = source.DesignPrinciple,
            SubPrinciple = source.SubPrinciple,
        };
    }

    private BinaryPartitionInstruction CloneBinaryPartition(BinaryPartitionInstruction source)
    {
        if (source == null)
            return BinaryPartitionInstruction.None();

        return new BinaryPartitionInstruction
        {
            Enabled = source.Enabled,
            Axis = source.Axis,
            Gap = source.Gap,
            FirstColor = source.FirstColor,
            SecondColor = source.SecondColor
        };
    }

    /// <summary>
    /// Builds two child objects, each containing one spatial half of the requested voxel shape.
    /// </summary>
    private void CreatePartitionedShape(
        DesignCommand command,
        Transform parent,
        int subdivision,
        float voxelSize,
        Material defaultMaterial)
    {
        BinaryPartitionInstruction instruction = command.BinaryPartition;
        float gap = instruction.Gap > 0f ? instruction.Gap : DefaultPartitionGap;
        Vector3 axisOffset = GetPartitionAxisVector(instruction.Axis) * (gap * 0.5f);

        GameObject firstHalf = new GameObject(parent.name + "_FirstHalf");
        firstHalf.transform.SetParent(parent, worldPositionStays: false);
        firstHalf.transform.localPosition = -axisOffset;

        GameObject secondHalf = new GameObject(parent.name + "_SecondHalf");
        secondHalf.transform.SetParent(parent, worldPositionStays: false);
        secondHalf.transform.localPosition = axisOffset;

        Material firstMaterial = string.IsNullOrWhiteSpace(instruction.FirstColor)
            ? defaultMaterial
            : CreateMaterial(instruction.FirstColor);
        Material secondMaterial = string.IsNullOrWhiteSpace(instruction.SecondColor)
            ? defaultMaterial
            : CreateMaterial(instruction.SecondColor);

        CreateShapeVoxels(
            command.Shape,
            firstHalf.transform,
            subdivision,
            voxelSize,
            firstMaterial,
            (x, y, z) => IsInPartitionHalf(x, y, z, subdivision, instruction.Axis, firstHalf: true));

        CreateShapeVoxels(
            command.Shape,
            secondHalf.transform,
            subdivision,
            voxelSize,
            secondMaterial,
            (x, y, z) => IsInPartitionHalf(x, y, z, subdivision, instruction.Axis, firstHalf: false));
    }

    /// <summary>
    /// Routes voxel generation by shape, optionally filtering coordinates for partitioned objects.
    /// </summary>
    private void CreateShapeVoxels(
        VoxelShape shape,
        Transform parent,
        int subdivision,
        float voxelSize,
        Material material,
        Func<int, int, int, bool> includeCoordinate = null)
    {
        switch (shape)
        {
            case VoxelShape.Sphere:
                CreateVoxelSphere(parent, subdivision, voxelSize, material, includeCoordinate);
                break;
            case VoxelShape.Cylinder:
                CreateVoxelCylinder(parent, subdivision, voxelSize, material, includeCoordinate);
                break;
            default:
                CreateVoxelCube(parent, subdivision, voxelSize, material, includeCoordinate);
                break;
        }
    }



    /// <summary>
    /// Computes the average X offset for keeping the whole generated scene centered.
    /// </summary>
    private float GetAverageX(List<DesignCommand> commands)
    {
        if (commands.Count == 0)
            return 0f;

        float totalX = 0f;
        int count = 0;

        foreach (DesignCommand command in commands)
        {
            if (command == null)
                continue;

            totalX += command.PositionOffset.x;
            count++;
        }

        return count == 0 ? 0f : totalX / count;
    }

    /// <summary>
    /// Keeps subdivision values inside performance-friendly bounds.
    /// Curved shapes get a higher minimum so they do not visually collapse into cubes.
    /// </summary>
    private int GetSafeSubdivision(int requestedSubdivision, VoxelShape shape)
    {
        int requested = requestedSubdivision > 0 ? requestedSubdivision : MinSubdivision;
        int safeSubdivision = Mathf.Clamp(requested, MinSubdivision, MaxSubdivision);

        if (shape != VoxelShape.Cube)
            safeSubdivision = Mathf.Max(safeSubdivision, MinCurvedSubdivision);

        return safeSubdivision;
    }

    /// <summary>
    /// Builds a filled N x N x N voxel cube.
    /// </summary>
    private void CreateVoxelCube(
        Transform parent,
        int subdivision,
        float voxelSize,
        Material material,
        Func<int, int, int, bool> includeCoordinate = null)
    {
        for (int x = 0; x < subdivision; x++)
        for (int y = 0; y < subdivision; y++)
        for (int z = 0; z < subdivision; z++)
        {
            if (includeCoordinate != null && !includeCoordinate(x, y, z))
                continue;

            CreateVoxel(parent, material, GetCenteredVoxelPosition(x, y, z, subdivision, voxelSize), voxelSize);
        }
    }

    /// <summary>
    /// Builds a sphere by keeping voxel centers that fall inside a unit sphere.
    /// </summary>
    private void CreateVoxelSphere(
        Transform parent,
        int subdivision,
        float voxelSize,
        Material material,
        Func<int, int, int, bool> includeCoordinate = null)
    {
        for (int x = 0; x < subdivision; x++)
        for (int y = 0; y < subdivision; y++)
        for (int z = 0; z < subdivision; z++)
        {
            if (includeCoordinate != null && !includeCoordinate(x, y, z))
                continue;

            Vector3 normalizedCenter = GetNormalizedVoxelCenter(x, y, z, subdivision);
            if (normalizedCenter.sqrMagnitude > 1f)
                continue;

            CreateVoxel(parent, material, GetCenteredVoxelPosition(x, y, z, subdivision, voxelSize), voxelSize);
        }
    }

    /// <summary>
    /// Builds a vertical cylinder by applying a circular XZ mask through the full Y height.
    /// </summary>
    private void CreateVoxelCylinder(
        Transform parent,
        int subdivision,
        float voxelSize,
        Material material,
        Func<int, int, int, bool> includeCoordinate = null)
    {
        for (int x = 0; x < subdivision; x++)
        for (int y = 0; y < subdivision; y++)
        for (int z = 0; z < subdivision; z++)
        {
            if (includeCoordinate != null && !includeCoordinate(x, y, z))
                continue;

            Vector3 normalizedCenter = GetNormalizedVoxelCenter(x, y, z, subdivision);
            float radialDistanceSquared = normalizedCenter.x * normalizedCenter.x + normalizedCenter.z * normalizedCenter.z;
            if (radialDistanceSquared > 1f)
                continue;

            CreateVoxel(parent, material, GetCenteredVoxelPosition(x, y, z, subdivision, voxelSize), voxelSize);
        }
    }

    /// <summary>
    /// Determines which half owns a voxel coordinate for a binary split.
    /// </summary>
    private bool IsInPartitionHalf(int x, int y, int z, int subdivision, PartitionAxis axis, bool firstHalf)
    {
        int midpoint = Mathf.Max(1, subdivision / 2);
        int axisValue = GetPartitionAxisValue(x, y, z, axis);
        bool coordinateIsInFirstHalf = axisValue < midpoint;

        return firstHalf == coordinateIsInFirstHalf;
    }

    private int GetPartitionAxisValue(int x, int y, int z, PartitionAxis axis)
    {
        switch (axis)
        {
            case PartitionAxis.Y:
                return y;
            case PartitionAxis.Z:
                return z;
            default:
                return x;
        }
    }

    private Vector3 GetPartitionAxisVector(PartitionAxis axis)
    {
        switch (axis)
        {
            case PartitionAxis.Y:
                return Vector3.up;
            case PartitionAxis.Z:
                return Vector3.forward;
            default:
                return Vector3.right;
        }
    }

    /// <summary>
    /// Creates one cube primitive, assigns material, and removes its collider for better runtime performance.
    /// </summary>
    private void CreateVoxel(Transform parent, Material material, Vector3 localPosition, float voxelSize)
    {
        GameObject voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        voxel.transform.SetParent(parent, worldPositionStays: false);
        voxel.transform.localPosition = localPosition;
        voxel.transform.localScale = Vector3.one * voxelSize * VoxelGapRatio;

        MeshRenderer renderer = voxel.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        // DO NOT DESTROY THE COLLIDER! We need it for CSG and XR Grabbing.
        // Collider collider = voxel.GetComponent<Collider>();
        // if (collider != null)
        //    Destroy(collider);
        
        // ── XR Interaction & Snapping Additions ──
        // Add physics and interaction to EACH voxel so the user can grab them individually!
        var rb = voxel.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false; // Floating in space
        rb.mass = 1f;
        rb.linearDamping = 5f;
        rb.angularDamping = 5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var grab = voxel.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = true;
        grab.throwVelocityScale = 2.0f;

        // Grid snap is applied at the parent group level (GridSnapper), not per-voxel.
    }

    /// <summary>
    /// Centers voxel coordinates around the parent origin instead of growing only in positive axes.
    /// </summary>
    private Vector3 GetCenteredVoxelPosition(int x, int y, int z, int subdivision, float voxelSize)
    {
        float firstCenter = -((subdivision - 1) * voxelSize) * 0.5f;
        return new Vector3(
            firstCenter + x * voxelSize,
            firstCenter + y * voxelSize,
            firstCenter + z * voxelSize);
    }

    /// <summary>
    /// Returns voxel center coordinates normalized to the -1..1 range for shape masks.
    /// </summary>
    private Vector3 GetNormalizedVoxelCenter(int x, int y, int z, int subdivision)
    {
        float scale = 2f / subdivision;
        return new Vector3(
            (x + 0.5f) * scale - 1f,
            (y + 0.5f) * scale - 1f,
            (z + 0.5f) * scale - 1f);
    }

    /// <summary>
    /// Finds a stable spawn point in front of the main camera.
    /// </summary>
    private Vector3 GetCameraForwardPosition()
    {
        if (Camera.main == null)
        {
            Debug.LogWarning("[GeometryManager] Main camera was not found. Using world forward as fallback.");
            return Vector3.forward * cameraForwardDistance;
        }

        Transform cameraTransform = Camera.main.transform;
        return cameraTransform.position + cameraTransform.forward * cameraForwardDistance;
    }

    /// <summary>
    /// Creates a URP-compatible material and applies the resolved color to common color properties.
    /// </summary>
    private Material CreateMaterial(string colorValue)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[GeometryManager] URP/Lit shader was not found. Falling back to Standard.");
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        Color resolvedColor = ResolveColor(colorValue);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", resolvedColor);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", resolvedColor);

        return material;
    }

    /// <summary>
    /// Resolves Turkish or English color names and #RRGGBB values to a Unity Color.
    /// </summary>
    private Color ResolveColor(string colorValue)
    {
        if (string.IsNullOrWhiteSpace(colorValue))
            return Color.white;

        string normalized = NormalizeText(colorValue);
        if (ColorTable.TryGetValue(normalized, out Color tableColor))
            return tableColor;

        string hexCandidate = colorValue.Trim();
        if (!hexCandidate.StartsWith("#", StringComparison.Ordinal))
            hexCandidate = "#" + hexCandidate;

        if (ColorUtility.TryParseHtmlString(hexCandidate, out Color htmlColor))
            return htmlColor;

        Debug.LogWarning($"[GeometryManager] Unknown color '{colorValue}'. Falling back to white.");
        return Color.white;
    }

    /// <summary>
    /// Converts AI-provided shape aliases into the supported shape enum.
    /// </summary>
    private VoxelShape ResolveShape(string shapeName)
    {
        string normalized = NormalizeText(shapeName);
        switch (normalized)
        {
            case "cube":
            case "box":
            case "kup":
            case "kupu":
                return VoxelShape.Cube;

            case "sphere":
            case "ball":
            case "kure":
            case "kureyi":
                return VoxelShape.Sphere;

            case "cylinder":
            case "silindir":
            case "silindiri":
                return VoxelShape.Cylinder;

            default:
                if (!string.IsNullOrWhiteSpace(shapeName))
                    Debug.LogWarning($"[GeometryManager] Unknown shape '{shapeName}'. Falling back to cube.");
                return VoxelShape.Cube;
        }
    }

    /// <summary>
    /// Checks whether the command asks for object creation.
    /// </summary>
    private bool IsCreateAction(string action)
    {
        string normalized = NormalizeText(action);
        return string.IsNullOrEmpty(normalized)
            || normalized == "create"
            || normalized == "build"
            || normalized == "make"
            || normalized == "spawn"
            || normalized == "olustur";
    }

    private bool IsCSGAction(string action)
    {
        string normalized = NormalizeText(action);
        return normalized == "intersect" || normalized == "intersection" || normalized == "kes" || normalized == "kesisim" || normalized == "kesisme" ||
               normalized == "union" || normalized == "birlesme" || normalized == "birlestir" || normalized == "merge" || normalized == "merging" ||
               normalized == "subtract" || normalized == "subtraction" || normalized == "cikar" || normalized == "fark";
    }

    private bool ExecuteCSGAction(string action)
    {
        GameObject root = GameObject.Find(sceneRootName);
        if (root == null || root.transform.childCount < 2)
        {
            ShowError("Sahne'de islem yapacak en az 2 obje bulunamadi!");
            return false;
        }

        GameObject objA = root.transform.GetChild(root.transform.childCount - 2).gameObject;
        GameObject objB = root.transform.GetChild(root.transform.childCount - 1).gameObject;

        CSGOperationType op = CSGOperationType.Subtraction;
        string normalized = NormalizeText(action);
        if (normalized == "intersect" || normalized == "intersection" || normalized == "kes" || normalized == "kesisim" || normalized == "kesisme") op = CSGOperationType.Intersection;
        if (normalized == "union" || normalized == "birlesme" || normalized == "birlestir" || normalized == "merge" || normalized == "merging") op = CSGOperationType.Union;

        if (op == CSGOperationType.Intersection && TryCreateAabbIntersection(objA, objB, root.transform, allowAutoOverlap: true))
        {
            Debug.Log("[GeometryManager] AABB intersection result created.");
            return true;
        }

        if (ShapeInteractionManager.Instance != null)
        {
            ShapeInteractionManager.Instance.RequestCSG(objA, objB, op);
            Debug.Log($"[GeometryManager] CSG Operation {op} dispatched via ShapeInteractionManager.");
            return true;
        }
        else if (CSGFormManager.Instance != null)
        {
            CSGFormManager.Instance.ProcessCSGOperation(objA, objB, op);
            Debug.Log($"[GeometryManager] CSG Operation {op} started via CSGFormManager.");
            return true;
        }
        else
        {
            Debug.LogError("[GeometryManager] No CSG pipeline available (ShapeInteractionManager / CSGFormManager).");
            return false;
        }
    }

    private bool TryCreateAabbIntersection(GameObject objA, GameObject objB, Transform parent, bool allowAutoOverlap)
    {
        if (!TryPrepareIntersectionBounds(objA, objB, out Bounds aBounds, out Bounds bBounds, allowAutoOverlap))
        {
            ShowError("[GeometryManager] Kesisim icin ortak hacim olusturulamadi.");
            return false;
        }

        float minX = Mathf.Max(aBounds.min.x, bBounds.min.x);
        float minY = Mathf.Max(aBounds.min.y, bBounds.min.y);
        float minZ = Mathf.Max(aBounds.min.z, bBounds.min.z);
        float maxX = Mathf.Min(aBounds.max.x, bBounds.max.x);
        float maxY = Mathf.Min(aBounds.max.y, bBounds.max.y);
        float maxZ = Mathf.Min(aBounds.max.z, bBounds.max.z);

        Vector3 size = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
        if (size.x <= Mathf.Epsilon || size.y <= Mathf.Epsilon || size.z <= Mathf.Epsilon)
            return false;

        GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
        result.name = $"CSG_Intersection_AABB_{parent.childCount}";
        result.transform.SetParent(parent, worldPositionStays: true);
        result.transform.position = new Vector3(minX + size.x * 0.5f, minY + size.y * 0.5f, minZ + size.z * 0.5f);
        result.transform.localScale = size;

        Renderer renderer = result.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateMaterial("white");

        MakeGeneratedResultInteractable(result);

        GeminiConnection gemini = FindAnyObjectByType<GeminiConnection>();
        gemini?.SendPhysicsResultToLLM($"CSG Intersection tamamlandi. Ortak hacim: {WongMathUtility.CalculateIntersectionVolume(aBounds, bBounds):F3} m3.");

        return true;
    }

    private bool CreateSplitPiecesFromBounds(Bounds bounds, Material material, Transform parent, string baseName, PartitionAxis axis)
    {
        Vector3 size = bounds.size;
        if (size.x <= Mathf.Epsilon || size.y <= Mathf.Epsilon || size.z <= Mathf.Epsilon)
            size = Vector3.one * 0.95f;

        Vector3 halfSize = size;
        Vector3 axisVector = Vector3.right;
        float axisSize = size.x;

        switch (axis)
        {
            case PartitionAxis.Y:
                halfSize.y *= 0.5f;
                axisVector = Vector3.up;
                axisSize = size.y;
                break;
            case PartitionAxis.Z:
                halfSize.z *= 0.5f;
                axisVector = Vector3.forward;
                axisSize = size.z;
                break;
            default:
                halfSize.x *= 0.5f;
                break;
        }

        float gap = Mathf.Max(DefaultPartitionGap, 0.04f);
        float offset = axisSize * 0.25f + gap * 0.5f;

        CreateSplitPiece(parent, $"{baseName}_Part_A", bounds.center, bounds.center - axisVector * offset, halfSize, material);
        CreateSplitPiece(parent, $"{baseName}_Part_B", bounds.center, bounds.center + axisVector * offset, halfSize, material);

        Debug.Log($"[GeometryManager] Split '{baseName}' into two separate root objects.");
        return true;
    }

    private void CreateSplitPiece(Transform parent, string name, Vector3 startPosition, Vector3 targetPosition, Vector3 size, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(parent, worldPositionStays: true);
        piece.transform.position = startPosition;
        piece.transform.localScale = size;

        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        MakeGeneratedResultInteractable(piece);
        piece.transform.DOMove(targetPosition, 0.45f)
            .SetEase(Ease.OutCubic)
            .SetLink(piece, LinkBehaviour.KillOnDestroy);
    }

    private bool TryPrepareIntersectionBounds(GameObject objA, GameObject objB, out Bounds aBounds, out Bounds bBounds, bool allowAutoOverlap)
    {
        bool hasA = TryGetObjectBounds(objA, out aBounds);
        bool hasB = TryGetObjectBounds(objB, out bBounds);
        if (!hasA || !hasB)
            return false;

        if (WongMathUtility.CalculateIntersectionVolume(aBounds, bBounds) > Mathf.Epsilon)
            return true;

        if (!allowAutoOverlap)
            return false;

        float xOffset = Mathf.Max(0.02f, Mathf.Min(aBounds.extents.x, bBounds.extents.x) * 0.5f);
        Vector3 desiredCenter = aBounds.center + Vector3.right * xOffset;
        objB.transform.position += desiredCenter - bBounds.center;

        return TryGetObjectBounds(objA, out aBounds)
            && TryGetObjectBounds(objB, out bBounds)
            && WongMathUtility.CalculateIntersectionVolume(aBounds, bBounds) > Mathf.Epsilon;
    }

    private static bool TryGetObjectBounds(GameObject obj, out Bounds bounds)
    {
        bounds = new Bounds(obj != null ? obj.transform.position : Vector3.zero, Vector3.zero);
        if (obj == null)
            return false;

        bool hasBounds = false;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private static void MakeGeneratedResultInteractable(GameObject result)
    {
        Rigidbody rb = result.GetComponent<Rigidbody>();
        if (rb == null)
            rb = result.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.mass = 5f;
        rb.linearDamping = 5f;
        rb.angularDamping = 5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var grab = result.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab == null)
            grab = result.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = true;

        if (result.GetComponent<FormInteractable>() == null)
            result.AddComponent<FormInteractable>();

        if (result.GetComponent<GridSnapper>() == null)
            result.AddComponent<GridSnapper>();
    }

    private bool IsPartitionEnabled(JToken token)
    {
        if (token == null)
            return false;

        if (token.Type == JTokenType.Boolean)
            return token.Value<bool>();

        if (token.Type == JTokenType.Object)
            return true;

        return IsBinaryPartitionMode(token.ToString());
    }

    private bool IsBinaryPartitionMode(string rawMode)
    {
        string normalized = NormalizeText(rawMode);
        return normalized == "binary"
            || normalized == "split"
            || normalized == "partition"
            || normalized == "divide"
            || normalized == "half"
            || normalized == "halves"
            || normalized == "two"
            || normalized == "2"
            || normalized == "2ye"
            || normalized == "ikiye"
            || normalized == "ayir"
            || normalized == "bol";
    }

    private PartitionAxis ParsePartitionAxis(string rawAxis, PartitionAxis fallback)
    {
        string normalized = NormalizeText(rawAxis);
        switch (normalized)
        {
            case "x":
            case "horizontal":
            case "yatay":
            case "left":
            case "right":
            case "sol":
            case "sag":
                return PartitionAxis.X;

            case "y":
            case "vertical":
            case "dikey":
            case "up":
            case "down":
            case "yukari":
            case "asagi":
                return PartitionAxis.Y;

            case "z":
            case "depth":
            case "forward":
            case "back":
            case "ileri":
            case "geri":
                return PartitionAxis.Z;

            default:
                return fallback;
        }
    }

    /// <summary>
    /// Reads a string property using a case-insensitive alias list.
    /// </summary>
    private string ReadString(JObject source, params string[] aliases)
    {
        JToken token = GetProperty(source, aliases);
        return token?.Type == JTokenType.Null ? null : token?.ToString();
    }

    /// <summary>
    /// Reads an integer property and supports numbers serialized as strings.
    /// </summary>
    private int ReadInt(JObject source, int fallback, params string[] aliases)
    {
        JToken token = GetProperty(source, aliases);
        if (token == null)
            return fallback;

        if (token.Type == JTokenType.Integer)
            return token.Value<int>();

        return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    /// <summary>
    /// Reads a float property and supports values serialized as strings.
    /// </summary>
    private float ReadFloat(JObject source, float fallback, params string[] aliases)
    {
        JToken token = GetProperty(source, aliases);
        return ReadFloatToken(token, fallback);
    }

    /// <summary>
    /// Reads a boolean property and accepts common string/number forms.
    /// </summary>
    private bool ReadBool(JObject source, bool fallback, params string[] aliases)
    {
        JToken token = GetProperty(source, aliases);
        if (token == null)
            return fallback;

        if (token.Type == JTokenType.Boolean)
            return token.Value<bool>();

        if (token.Type == JTokenType.Integer)
            return token.Value<int>() != 0;

        string normalized = NormalizeText(token.ToString());
        if (normalized == "true" || normalized == "yes" || normalized == "1" || normalized == "evet")
            return true;

        if (normalized == "false" || normalized == "no" || normalized == "0" || normalized == "hayir")
            return false;

        return fallback;
    }

    /// <summary>
    /// Reads Vector3 from either an array [x,y,z] or an object {x,y,z}.
    /// </summary>
    private Vector3 ReadVector3(JObject source, params string[] aliases)
    {
        JToken token = GetProperty(source, aliases);
        if (token is JArray array && array.Count >= 3)
        {
            return new Vector3(
                ReadFloatToken(array[0], 0f),
                ReadFloatToken(array[1], 0f),
                ReadFloatToken(array[2], 0f));
        }

        if (token is JObject vectorObject)
        {
            return new Vector3(
                ReadFloatToken(GetProperty(vectorObject, "x"), 0f),
                ReadFloatToken(GetProperty(vectorObject, "y"), 0f),
                ReadFloatToken(GetProperty(vectorObject, "z"), 0f));
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Converts a JToken into a float using invariant culture.
    /// </summary>
    private float ReadFloatToken(JToken token, float fallback)
    {
        if (token == null)
            return fallback;

        if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            return token.Value<float>();

        return float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    /// <summary>
    /// Gets a property by any alias without requiring exact casing.
    /// </summary>
    private JToken GetProperty(JObject source, params string[] aliases)
    {
        foreach (JProperty property in source.Properties())
        {
            foreach (string alias in aliases)
            {
                if (string.Equals(property.Name, alias, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Normalizes Turkish characters, spaces, and separators for reliable matching.
    /// </summary>
    private string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .Replace("İ", "I")
            .Replace("ı", "i")
            .Replace("ş", "s")
            .Replace("Ş", "S")
            .Replace("ğ", "g")
            .Replace("Ğ", "G")
            .Replace("ü", "u")
            .Replace("Ü", "U")
            .Replace("ö", "o")
            .Replace("Ö", "O")
            .Replace("ç", "c")
            .Replace("Ç", "C")
            .Replace("'", string.Empty)
            .Replace("’", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    /// <summary>
    /// Builds a readable object name for the generated scene hierarchy.
    /// </summary>
    private string BuildObjectName(DesignCommand command, int subdivision)
    {
        string colorName = string.IsNullOrWhiteSpace(command.ColorValue)
            ? "white"
            : NormalizeText(command.ColorValue).Replace("#", "hex");

        return $"{command.Shape}_{colorName}_{subdivision}x";
    }

    /// <summary>
    /// Logs errors and mirrors them into the optional TMP output field.
    /// </summary>
    private void ShowError(string message)
    {
        Debug.LogError(message);

        if (errorMessageText != null)
            errorMessageText.text = message;
    }
}
