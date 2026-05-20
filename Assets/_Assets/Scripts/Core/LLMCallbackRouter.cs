using UnityEngine;
using Newtonsoft.Json.Linq;

/// <summary>
/// LLMCallbackRouter — receives raw LLM text that contains a
/// `spatialOp` JSON block and dispatches to SpatialFormPipeline.
///
/// Expected LLM response extension format (appended after the standard geometry JSON):
/// <code>
/// ```spatialop
/// {
///   "operation": "fragmentation",   // or union / subtraction / intersection / slicing / zoom / penetration
///   "targetA": "AI_Scene/Cube_1x1", // GameObject path or name
///   "targetB": "AI_Scene/Sphere_8x8" // optional
/// }
/// ```
/// </code>
///
/// GeminiConnection.ApplyGeometryCommand() calls TryDispatch() after parsing
/// the standard geometry JSON block.
/// </summary>
public static class LLMCallbackRouter
{
    private const string FenceOpen  = "```spatialop";
    private const string FenceClose = "```";

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans the full LLM response text for a spatialop block and, if found,
    /// dispatches the operation to SpatialFormPipeline.Instance.
    /// Returns true if a dispatch was made.
    /// </summary>
    public static bool TryDispatch(string fullLLMResponse)
    {
        if (string.IsNullOrWhiteSpace(fullLLMResponse)) return false;

        string block = ExtractSpatialOpBlock(fullLLMResponse);
        if (block == null) return false;

        try
        {
            JObject json = JObject.Parse(block);
            string op    = json["operation"]?.ToString() ?? json["op"]?.ToString() ?? "";
            string nameA = json["targetA"]?.ToString()   ?? json["objectA"]?.ToString() ?? "";
            string nameB = json["targetB"]?.ToString()   ?? json["objectB"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(op))
            {
                Debug.LogWarning("[LLMRouter] spatialop block has no 'operation' field.");
                return false;
            }

            GameObject objA = nameA.ToLowerInvariant() == "all" || nameA.ToLowerInvariant() == "scene" ? null : FindByName(nameA);
            GameObject objB = string.IsNullOrWhiteSpace(nameB) ? null : FindByName(nameB);

            var pipeline = SpatialFormPipeline.Instance;
            if (pipeline == null)
            {
                Debug.LogWarning("[LLMRouter] SpatialFormPipeline not in scene.");
                return false;
            }

            if (nameA.ToLowerInvariant() == "all" || nameA.ToLowerInvariant() == "scene")
            {
                Debug.Log($"[LLMRouter] Dispatching '{op}' on ALL objects in AI_Scene");
                GameObject aiRoot = GameObject.Find("AI_Scene");
                if (aiRoot != null)
                {
                    // Copy children to a list first because operations like fragmentation destroy them
                    var children = new System.Collections.Generic.List<GameObject>();
                    for (int i = 0; i < aiRoot.transform.childCount; i++)
                        children.Add(aiRoot.transform.GetChild(i).gameObject);

                    foreach (var child in children)
                        pipeline.ExecuteFormOperation(op, child, null);
                }
                return true;
            }

            if (objA == null)
            {
                Debug.LogWarning($"[LLMRouter] Could not find targetA: '{nameA}'");
                return false;
            }

            Debug.Log($"[LLMRouter] Dispatching '{op}' on '{objA.name}' + '{objB?.name}'");
            pipeline.ExecuteFormOperation(op, objA, objB);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LLMRouter] spatialop parse error: {e.Message}\nBlock: {block}");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region Helpers

    private static string ExtractSpatialOpBlock(string text)
    {
        int start = text.IndexOf(FenceOpen, System.StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;

        int lineEnd = text.IndexOf('\n', start);
        if (lineEnd < 0) return null;

        int blockStart = lineEnd + 1;
        int blockEnd   = text.IndexOf(FenceClose, blockStart, System.StringComparison.Ordinal);
        if (blockEnd < 0) return null;

        return text.Substring(blockStart, blockEnd - blockStart).Trim();
    }

    /// <summary>
    /// Finds a GameObject by name or path (supports "ParentName/ChildName" notation).
    /// Falls back to searching the AI_Scene hierarchy.
    /// </summary>
    private static GameObject FindByName(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath)) return null;

        // Try direct path first
        GameObject direct = GameObject.Find(nameOrPath);
        if (direct != null) return direct;

        // Search under AI_Scene root
        GameObject aiRoot = GameObject.Find("AI_Scene");
        if (aiRoot != null)
        {
            Transform found = aiRoot.transform.Find(nameOrPath);
            if (found != null) return found.gameObject;

            // Recursive partial name match
            foreach (Transform child in aiRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains(nameOrPath)) return child.gameObject;
            }
        }

        // Global partial name match
        foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
            if (obj.name.Contains(nameOrPath)) return obj;

        return null;
    }

    #endregion
}
