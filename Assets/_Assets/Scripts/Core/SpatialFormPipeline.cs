using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using HoloLensApp.Sandbox.Csg;

/// <summary>
/// SpatialFormPipeline — AI-driven physics + mesh operation dispatcher.
///
/// Maps Wong (1969) 8 interaction principles to real-time mesh operations:
///   Intersection / Kesişme   → CSG Intersection (shared volume)
///   Subtraction  / Eksilme   → CSG Subtraction  (carving)
///   Union        / Birleşme  → CSG Union         (merged solid)
///   Penetration  / İçe Girme → Colliders overlap, no CSG (physical inter-penetration)
///   Fragmentation             → Voronoi-style random split burst
///   Scaling      / Zoom       → Animated uniform/non-uniform scale
///   Slicing      / Kesme      → Plane-cut split
///   Merging                   → AnimateTouch → CSG Union
///
/// PHYSICS FEEDBACK LOOP:
///   After any operation, calls GeminiConnection.SendPhysicsResultToLLM()
///   with world-space coordinates + result type so the LLM can continue
///   the conversation context.
/// </summary>
public class SpatialFormPipeline : MonoBehaviour
{
    public static SpatialFormPipeline Instance { get; private set; }

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration   = 0.8f;
    [SerializeField] private float fragmentBurstForce  = 3.5f;
    [SerializeField] private int   fragmentCount       = 8;

    [Header("Instantiation Visual")]
    [SerializeField] private Material ghostMaterial; // semi-transparent for build-in effect

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region Public Dispatch (called by LLM callback router)

    /// <summary>
    /// Main entry: routes an LLM spatial command to the correct physics/mesh method.
    /// <param name="operation">Wong principle name (e.g. "union", "subtraction", "fragmentation").</param>
    /// <param name="targetA">Primary GameObject.</param>
    /// <param name="targetB">Secondary GameObject (null for unary ops).</param>
    /// </summary>
    public void ExecuteFormOperation(string operation, GameObject targetA, GameObject targetB = null)
    {
        if (targetA == null) { Debug.LogWarning("[SpatialForm] targetA is null."); return; }

        string op = operation.ToLowerInvariant().Trim();

        switch (op)
        {
            // ── Wong: Intersection / Kesişme ─────────────────────────────────
            case "intersection":
            case "kesisme":
            case "kesişme":
                StartCoroutine(CSGOperation_Intersection(targetA, targetB));
                break;

            // ── Wong: Subtraction / Eksilme ──────────────────────────────────
            case "subtraction":
            case "eksilme":
                StartCoroutine(CSGOperation_Subtraction(targetA, targetB));
                break;

            // ── Wong: Union / Birleşme ───────────────────────────────────────
            case "union":
            case "birlesme":
            case "birleşme":
                StartCoroutine(CSGOperation_Union(targetA, targetB));
                break;

            // ── Wong: Penetration / İçe Girme — no CSG, just animate overlap ─
            case "penetration":
            case "ice girme":
            case "içe girme":
                StartCoroutine(AnimatePenetration(targetA, targetB));
                break;

            // ── Fragmentation / Parçalanma ────────────────────────────────────
            case "fragmentation":
            case "parcalanma":
            case "parçalanma":
                StartCoroutine(AnimateFragmentation(targetA));
                break;

            // ── Zoom / Scale ──────────────────────────────────────────────────
            case "zoom":
            case "scale":
            case "scaling":
                StartCoroutine(AnimateScaling(targetA, targetB));
                break;

            // ── Slicing / Kesme ───────────────────────────────────────────────
            case "slicing":
            case "cutting":
            case "kesme":
            case "bolme":
            case "bölme":
                StartCoroutine(AnimateSlicing(targetA));
                break;

            // ── Merging: animate touching then CSG Union ─────────────────────
            case "merging":
            case "merge":
                StartCoroutine(AnimateMerge(targetA, targetB));
                break;

            default:
                Debug.LogWarning($"[SpatialForm] Unknown operation: '{op}'");
                break;
        }
    }

    /// <summary>
    /// Animated object instantiation — build-in effect (ghosted scale-up).
    /// Called by GeometryManager / LLM to make creation visible.
    /// </summary>
    public void AnimatedInstantiate(GameObject prefabOrExisting, Vector3 worldPos, float duration = 0.7f)
    {
        StartCoroutine(BuildInEffect(prefabOrExisting, worldPos, duration));
    }

    public void FragmentAllGeneratedObjects()
    {
        GameObject aiRoot = GameObject.Find("AI_Scene");
        if (aiRoot == null || aiRoot.transform.childCount == 0)
        {
            Debug.LogWarning("[SpatialForm] AI_Scene has no generated objects to fragment.");
            return;
        }

        var children = new List<GameObject>(aiRoot.transform.childCount);
        for (int i = 0; i < aiRoot.transform.childCount; i++)
            children.Add(aiRoot.transform.GetChild(i).gameObject);

        for (int i = 0; i < children.Count; i++)
            if (children[i] != null)
                StartCoroutine(AnimateFragmentation(children[i]));
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Animated Instantiation

    /// <summary>
    /// Scales the object from zero + applies a ghosted material overlay,
    /// then fades to the real material. Creates a "materializing" MR effect.
    /// Colliders are disabled during the 1.5s transition to prevent physics explosions on spawn.
    /// </summary>
    private IEnumerator BuildInEffect(GameObject obj, Vector3 worldPos, float duration)
    {
        if (obj == null) yield break;

        obj.transform.position   = worldPos;
        Vector3 targetScale      = obj.transform.localScale;
        obj.transform.localScale = Vector3.zero;

        // 1. Disable all colliders immediately on spawn
        var colliders = obj.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = false;
        }

        // 2. Setup a temporary ghost transparent material to lerp opacity
        Renderer rend = obj.GetComponentInChildren<Renderer>();
        Material[] origMats = null;
        Material tempGhostMat = null;

        if (rend != null)
        {
            origMats = rend.materials;
            
            // Try to find a transparent URP or standard shader
            Shader ghostShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (ghostMaterial != null)
            {
                tempGhostMat = new Material(ghostMaterial);
            }
            else
            {
                tempGhostMat = new Material(ghostShader);
                // Try setting URP transparent keyword if URP Lit shader is used
                if (tempGhostMat.shader.name.Contains("Universal Render Pipeline"))
                {
                    tempGhostMat.SetFloat("_Surface", 1); // 1 = Transparent
                    tempGhostMat.SetFloat("_Blend", 0); // 0 = Alpha blend
                    tempGhostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    tempGhostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    tempGhostMat.SetInt("_ZWrite", 0);
                    tempGhostMat.DisableKeyword("_ALPHATEST_ON");
                    tempGhostMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    tempGhostMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
            }

            // Assign the temporary ghost material to the renderer
            var tempMats = new Material[origMats.Length];
            for (int i = 0; i < origMats.Length; i++)
            {
                tempMats[i] = tempGhostMat;
            }
            rend.materials = tempMats;
        }

        // 3. Lerp scale and opacity over 1.5 seconds
        float transitionDuration = 1.5f;
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            if (obj == null)
            {
                if (tempGhostMat != null)
                    Destroy(tempGhostMat);

                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            
            // Lerp scale
            obj.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
            
            // Lerp opacity of the ghost material
            if (tempGhostMat != null)
            {
                if (tempGhostMat.HasProperty("_BaseColor"))
                {
                    Color c = tempGhostMat.GetColor("_BaseColor");
                    c.a = t; // fade in from fully transparent to opaque
                    tempGhostMat.SetColor("_BaseColor", c);
                }
                else if (tempGhostMat.HasProperty("_Color"))
                {
                    Color c = tempGhostMat.GetColor("_Color");
                    c.a = t;
                    tempGhostMat.SetColor("_Color", c);
                }
            }

            yield return null;
        }

        if (obj == null)
        {
            if (tempGhostMat != null)
                Destroy(tempGhostMat);

            yield break;
        }

        // 4. Force target scale
        obj.transform.localScale = targetScale;

        // 5. Restore original opaque materials
        if (rend != null && origMats != null)
        {
            rend.materials = origMats;
        }

        // 6. Enable all colliders only when the animation completes
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = true;
        }

        if (tempGhostMat != null)
        {
            Destroy(tempGhostMat);
        }

        ReportResult("instantiation", $"Object '{obj.name}' materialized at {worldPos:F2}");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region CSG Boolean Operations (Wong: Intersection, Subtraction, Union)

    /// <summary>
    /// Wong: INTERSECTION / Kesişme — keeps only the shared volume of A ∩ B.
    /// Animates both objects pulsing toward each other before the CSG cut.
    /// </summary>
    private IEnumerator CSGOperation_Intersection(GameObject a, GameObject b)
    {
        if (b == null) { Debug.LogWarning("[SpatialForm] Intersection requires two objects."); yield break; }

        // Pre-flash both objects to signal operation
        yield return FlashObjects(new[] { a, b }, Color.cyan, 0.3f);

        try
        {
            Model result = CSG.Perform(CSG.BooleanOp.Intersection, a, b);
            if (result != null)
            {
                ApplyCSGResult(result, a, "Intersection");
                Object.Destroy(b);
                ReportResult("intersection", $"CSG Intersection applied. Result object at {a.transform.position:F2}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpatialForm] CSG Intersection failed: {e.Message}");
        }
    }

    /// <summary>
    /// Wong: SUBTRACTION / Eksilme — carves B's volume out of A.
    /// </summary>
    private IEnumerator CSGOperation_Subtraction(GameObject a, GameObject b)
    {
        if (b == null) { Debug.LogWarning("[SpatialForm] Subtraction requires two objects."); yield break; }

        yield return FlashObjects(new[] { b }, Color.red, 0.3f);

        try
        {
            Model result = CSG.Perform(CSG.BooleanOp.Subtraction, a, b);
            if (result != null)
            {
                ApplyCSGResult(result, a, "Subtraction");
                Object.Destroy(b);
                ReportResult("subtraction", $"CSG Subtraction (Eksilme) applied. Carved shape at {a.transform.position:F2}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpatialForm] CSG Subtraction failed: {e.Message}");
        }
    }

    /// <summary>
    /// Wong: UNION / Birleşme — merges A and B into one solid.
    /// Animates them sliding together before merge.
    /// </summary>
    private IEnumerator CSGOperation_Union(GameObject a, GameObject b)
    {
        if (b == null) { Debug.LogWarning("[SpatialForm] Union requires two objects."); yield break; }

        // Animate touch before merge
        Vector3 midPoint = (a.transform.position + b.transform.position) * 0.5f;
        float   elapsed  = 0f;
        Vector3 startA   = a.transform.position;
        Vector3 startB   = b.transform.position;
        Vector3 targA    = midPoint - (midPoint - startA).normalized * 0.01f;
        Vector3 targB    = midPoint - (midPoint - startB).normalized * 0.01f;

        while (elapsed < animationDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / (animationDuration * 0.5f);
            if (a != null) a.transform.position = Vector3.Lerp(startA, targA, t);
            if (b != null) b.transform.position = Vector3.Lerp(startB, targB, t);
            yield return null;
        }

        yield return FlashObjects(new[] { a, b }, Color.green, 0.2f);

        try
        {
            Model result = CSG.Perform(CSG.BooleanOp.Union, a, b);
            if (result != null)
            {
                ApplyCSGResult(result, a, "Union");
                Object.Destroy(b);
                ReportResult("union", $"CSG Union (Birleşme) completed. Merged object at {a.transform.position:F2}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpatialForm] CSG Union failed: {e.Message}");
        }
    }

    private static void ApplyCSGResult(Model result, GameObject target, string opName)
    {
        Mesh mesh = (Mesh)result;
        mesh.name = $"CSG_{opName}";

        MeshFilter mf = target.GetComponent<MeshFilter>() ?? target.GetComponentInChildren<MeshFilter>();
        if (mf != null) mf.sharedMesh = mesh;

        // Update or replace collider
        var oldCols = target.GetComponents<Collider>();
        foreach (var c in oldCols) Object.Destroy(c);
        var mc = target.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex     = true;

        if (result.materials != null && result.materials.Count > 0)
        {
            var rend = target.GetComponent<MeshRenderer>() ?? target.GetComponentInChildren<MeshRenderer>();
            if (rend != null) rend.sharedMaterials = result.materials.ToArray();
        }

        var sbObj = target.GetComponent<HoloLensApp.Sandbox.SandboxObject>();
        sbObj?.UpdateOriginalMaterials();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Wong: Penetration / İçe Girme

    /// <summary>
    /// PENETRATION — both forms remain fully visible, their boundaries cross.
    /// No geometry is changed; objects simply animate into each other's space.
    /// </summary>
    private IEnumerator AnimatePenetration(GameObject a, GameObject b)
    {
        if (b == null) yield break;

        Vector3 startA = a.transform.position;
        Vector3 targetA = (a.transform.position + b.transform.position) * 0.5f
                          + (a.transform.position - b.transform.position).normalized * 0.15f;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0, 1, elapsed / animationDuration);
            if (a != null) a.transform.position = Vector3.Lerp(startA, targetA, t);
            yield return null;
        }

        // Disable collider on A during penetration to avoid physics push-out
        var cols = a.GetComponents<Collider>();
        foreach (var c in cols) c.isTrigger = true;

        ReportResult("penetration", $"Penetration (İçe Girme): '{a.name}' interpenetrates '{b?.name}' at {targetA:F2}");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Fragmentation / Parçalanma

    /// <summary>
    /// FRAGMENTATION / Parçalanma — splits the object into N random fragments
    /// that burst outward with physics velocity. Results reported to LLM.
    /// </summary>
    private IEnumerator AnimateFragmentation(GameObject source)
    {
        if (source == null) yield break;

        Bounds  bounds   = GetBounds(source);
        Vector3 center   = bounds.center;
        var     fragments = new List<GameObject>();
        Transform fragmentParent = source.transform.parent;

        // Create fragments as scaled copies
        Renderer srcRend = source.GetComponentInChildren<Renderer>();
        Material mat     = srcRend != null ? srcRend.sharedMaterial : null;

        for (int i = 0; i < fragmentCount; i++)
        {
            float scale = Random.Range(bounds.size.x * 0.2f, bounds.size.x * 0.45f);
            Vector3 offset = new Vector3(
                Random.Range(-bounds.extents.x, bounds.extents.x),
                Random.Range(-bounds.extents.y, bounds.extents.y),
                Random.Range(-bounds.extents.z, bounds.extents.z));

            GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frag.name = $"Fragment_{i}";
            frag.transform.SetParent(fragmentParent, worldPositionStays: true);
            frag.transform.position   = center + offset * 0.3f;
            frag.transform.localScale = Vector3.one * scale;
            frag.transform.rotation   = Random.rotation;

            if (mat != null)
                frag.GetComponent<Renderer>().sharedMaterial = mat;

            var rb = frag.AddComponent<Rigidbody>();
            rb.useGravity    = true;
            rb.linearDamping = 1.2f;
            rb.angularDamping = 1.5f;

            // Initial burst outward
            Vector3 burst = (center + offset - center).normalized
                            * Random.Range(fragmentBurstForce * 0.6f, fragmentBurstForce)
                            + Vector3.up * Random.Range(0.5f, 2f);
            rb.linearVelocity = burst;
            rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.Impulse);

            MakeRuntimeFragmentInteractable(frag);

            fragments.Add(frag);
        }

        // Flash + destroy source
        yield return FlashObjects(new[] { source }, Color.white, 0.15f);
        if (source != null)
            source.transform.DOKill();

        Object.Destroy(source);

        // Build result string: positions of all fragments
        var posReport = new System.Text.StringBuilder();
        posReport.Append("Fragmentation (Parçalanma) complete. Fragment positions: ");
        foreach (var f in fragments)
            if (f != null) posReport.Append($"{f.transform.position:F2} | ");

        ReportResult("fragmentation", posReport.ToString());
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Zoom / Scaling

    /// <summary>
    /// Animates the object to a new uniform scale. If targetB is provided,
    /// it is used to sample the target scale (relative scaling by B's size).
    /// </summary>
    private IEnumerator AnimateScaling(GameObject a, GameObject b)
    {
        if (a == null) yield break;

        float   targetScale  = b != null ? b.transform.localScale.magnitude : a.transform.localScale.magnitude * 1.5f;
        Vector3 startScale   = a.transform.localScale;
        Vector3 endScale     = Vector3.one * targetScale;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0, 1, elapsed / animationDuration);
            if (a != null) a.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        ReportResult("scaling", $"Scaling complete. '{a.name}' new scale: {endScale:F2}");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Slicing / Kesme (plane cut via Y-axis split)

    /// <summary>
    /// SLICING / Kesme — splits an object at its local Y midpoint, animates
    /// the two halves separating along Y. No true CSG cut; uses bounds-based
    /// child object generation for visual effect.
    /// </summary>
    private IEnumerator AnimateSlicing(GameObject source)
    {
        if (source == null) yield break;

        Bounds  bounds  = GetBounds(source);
        Vector3 center  = bounds.center;
        Renderer srcRnd = source.GetComponentInChildren<Renderer>();
        Material mat    = srcRnd != null ? srcRnd.sharedMaterial : null;

        // Top half
        GameObject topHalf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topHalf.name = "Slice_Top";
        topHalf.transform.SetParent(source.transform.parent, worldPositionStays: true);
        topHalf.transform.position   = center + Vector3.up * bounds.extents.y * 0.5f;
        topHalf.transform.localScale = new Vector3(bounds.size.x, bounds.size.y * 0.5f, bounds.size.z);
        if (mat != null) topHalf.GetComponent<Renderer>().sharedMaterial = mat;

        // Bottom half
        GameObject botHalf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        botHalf.name = "Slice_Bottom";
        botHalf.transform.SetParent(source.transform.parent, worldPositionStays: true);
        botHalf.transform.position   = center - Vector3.up * bounds.extents.y * 0.5f;
        botHalf.transform.localScale = new Vector3(bounds.size.x, bounds.size.y * 0.5f, bounds.size.z);
        if (mat != null) botHalf.GetComponent<Renderer>().sharedMaterial = mat;

        MakeRuntimeFragmentInteractable(topHalf);
        MakeRuntimeFragmentInteractable(botHalf);

        Object.Destroy(source);

        // Animate apart
        Vector3 topStart = topHalf.transform.position;
        Vector3 botStart = botHalf.transform.position;
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0, 1, elapsed / animationDuration);
            topHalf.transform.position = Vector3.Lerp(topStart, topStart + Vector3.up * 0.3f, t);
            botHalf.transform.position = Vector3.Lerp(botStart, botStart - Vector3.up * 0.3f, t);
            yield return null;
        }

        ReportResult("slicing", $"Slicing (Kesme) complete. Top: {topHalf.transform.position:F2}, Bottom: {botHalf.transform.position:F2}");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Merge (touch-animate → CSG Union)

    private IEnumerator AnimateMerge(GameObject a, GameObject b)
    {
        if (a == null || b == null) yield break;
        // First animate them touching
        yield return AnimatePenetration(a, b);
        // Then CSG Union
        yield return CSGOperation_Union(a, b);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Helpers

    private static Bounds GetBounds(GameObject go)
    {
        Renderer r = go.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds;
        Collider c = go.GetComponent<Collider>();
        if (c != null) return c.bounds;
        return new Bounds(go.transform.position, Vector3.one * 0.5f);
    }

    private static void MakeRuntimeFragmentInteractable(GameObject obj)
    {
        if (obj == null)
            return;

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = obj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        var grab = obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab == null)
            grab = obj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = true;

        if (obj.GetComponent<HoloLensApp.Interaction.Snapping.GridSnapper>() == null)
            obj.AddComponent<HoloLensApp.Interaction.Snapping.GridSnapper>();

        if (obj.GetComponent<HoloLensApp.Interaction.Math.FormInteractable>() == null)
            obj.AddComponent<HoloLensApp.Interaction.Math.FormInteractable>();
    }

    /// <summary>Flashes objects by briefly swapping to emissive material.</summary>
    private static IEnumerator FlashObjects(GameObject[] objs, Color flashColor, float duration)
    {
        var renderers  = new List<(Renderer r, Material[] orig)>();
        Material flash = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        flash.color = flashColor;
        flash.EnableKeyword("_EMISSION");
        flash.SetColor("_EmissionColor", flashColor * 2f);

        foreach (var o in objs)
        {
            if (o == null) continue;
            Renderer r = o.GetComponentInChildren<Renderer>();
            if (r == null) continue;
            Material[] orig = r.sharedMaterials;
            renderers.Add((r, orig));
            r.sharedMaterials = new[] { flash };
        }

        yield return new WaitForSeconds(duration);

        foreach (var (r, orig) in renderers)
            if (r != null) r.sharedMaterials = orig;
    }

    /// <summary>
    /// Sends physics results back to the LLM (feedback loop).
    /// Finds GeminiConnection in any scene since GlobalHUDManager is DontDestroyOnLoad.
    /// </summary>
    private static void ReportResult(string opType, string message)
    {
        Debug.Log($"[SpatialForm] Result — {opType}: {message}");
        var gemini = Object.FindAnyObjectByType<GeminiConnection>();
        gemini?.SendPhysicsResultToLLM($"[{opType.ToUpper()}] {message}");
    }

    #endregion
}
