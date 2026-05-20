using System.Collections;
using UnityEngine;

/// <summary>
/// Transforms the default XR Simulation environment into a modern design studio.
/// Attach to any persistent GameObject (e.g. the scene's Manager object).
/// Has no effect on a real HoloLens device — only active inside the editor simulation.
/// </summary>
public class SimEnvironmentStylist : MonoBehaviour
{
    [Header("Floor")]
    [SerializeField] private Color floorColor    = new Color(0.10f, 0.10f, 0.12f);
    [SerializeField] private float floorSmooth   = 0.80f;
    [SerializeField] private float floorMetallic = 0.10f;

    [Header("Walls")]
    [SerializeField] private Color wallColor     = new Color(0.88f, 0.87f, 0.85f);
    [SerializeField] private float wallSmooth    = 0.25f;

    [Header("Ceiling")]
    [SerializeField] private Color ceilingColor  = new Color(0.96f, 0.96f, 0.97f);

    [Header("Accent Lights")]
    [SerializeField] private Color warmLight     = new Color(1.00f, 0.82f, 0.55f);
    [SerializeField] private Color coolLight     = new Color(0.40f, 0.70f, 1.00f);
    [SerializeField] private float lightIntensity = 0.8f;

    [Header("Work Table")]
    [SerializeField] private Color tableColor    = new Color(0.14f, 0.09f, 0.04f);
    [SerializeField] private Color tableMetalColor = new Color(0.10f, 0.35f, 0.50f);

    private void Start()
    {
        StartCoroutine(EnhanceWhenReady());
    }

    private IEnumerator EnhanceWhenReady()
    {
        yield return null;
        yield return null;

        GameObject env = GameObject.Find("DefaultSimulationEnvironment(Clone)");
        if (env != null) Destroy(env);

        GameObject box = GameObject.Find("Simulated Bounding Box");
        if (box != null) Destroy(box);

        GameObject table = GameObject.Find("Simulated Bounding Box Table");
        if (table != null) Destroy(table);

        GameObject room = GameObject.Find("Simulated Environment");
        if (room != null) Destroy(room);

        Debug.Log("[SimEnvironmentStylist] Simulation environment completely removed.");
    }

    /// <summary>
    /// Hides MeshRenderers on any built-in simulation furniture so they don't
    /// clash with the custom table we spawn (e.g. "Simulated Bounding Box Table").
    /// </summary>
    private static void HideSimulationFurniture(Transform root)
    {
        string[] toHide = {
            "Simulated Bounding Box Table",
            "Table", "Furniture", "Chair", "Desk"
        };

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in toHide)
            {
                if (child.name.ToLowerInvariant().Contains(name.ToLowerInvariant()))
                {
                    // Disable renderers rather than destroying — keeps AR simulation intact
                    foreach (MeshRenderer mr in child.GetComponentsInChildren<MeshRenderer>(true))
                        mr.enabled = false;

                    Debug.Log($"[SimEnvironmentStylist] Hidden sim object: {child.name}");
                    break;
                }
            }
        }
    }


    // ── Surface materials ─────────────────────────────────────────────────────

    private void ApplySurfaceMaterials(Transform root)
    {
        // Floor
        ApplyToNamed(root, "Floor",   MakeMat(floorColor,   floorSmooth,   floorMetallic));
        // Walls (the default env often has a single "Wall" mesh)
        ApplyToNamed(root, "Wall",    MakeMat(wallColor,    wallSmooth,    0f));
        ApplyToNamed(root, "Ceiling", MakeMat(ceilingColor, 0.10f,         0f));

        // Also tint any child MeshRenderers that don't have an explicit name match
        foreach (Transform child in root)
        {
            if (child.name.ToLowerInvariant().Contains("wall") &&
                child.name != "Wall")
                ApplyToNamed(root, child.name, MakeMat(wallColor, wallSmooth, 0f));
        }
    }

    private void ApplyToNamed(Transform root, string childName, Material mat)
    {
        Transform t = root.Find(childName);
        if (t == null) return;
        foreach (MeshRenderer mr in t.GetComponentsInChildren<MeshRenderer>())
            mr.material = mat;
    }

    // ── Lighting ──────────────────────────────────────────────────────────────

    private void AddStudioLights(Transform root)
    {
        // Replace ambient with warm studio tone
        RenderSettings.ambientLight = new Color(0.18f, 0.17f, 0.20f);

        // Four corner warm lights
        Vector3[] corners = { new Vector3( 2.5f, 2.6f,  2.5f),
                               new Vector3(-2.5f, 2.6f,  2.5f),
                               new Vector3( 2.5f, 2.6f, -2.5f),
                               new Vector3(-2.5f, 2.6f, -2.5f) };
        for (int i = 0; i < corners.Length; i++)
        {
            Color c = (i % 2 == 0) ? warmLight : Color.Lerp(warmLight, coolLight, 0.3f);
            SpawnPointLight("StudioLight_" + i, root, corners[i], c, lightIntensity, 6f);
        }

        // Cool accent light from behind (bounce fill)
        SpawnPointLight("AccentLight", root, new Vector3(0f, 1.5f, -3f), coolLight, 0.5f, 5f);
    }

    private static void SpawnPointLight(string n, Transform parent, Vector3 localPos,
                                        Color color, float intensity, float range)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light l = go.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = color;
        l.intensity = intensity;
        l.range     = range;
        l.shadows   = LightShadows.Soft;
    }

    private static void SetAmbient()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.18f, 0.22f);
    }

    // ── Work table ────────────────────────────────────────────────────────────

    private void SpawnWorkTable(Transform parent)
    {
        GameObject table = new GameObject("WorkTable");
        table.transform.SetParent(parent, false);
        table.transform.localPosition = new Vector3(0f, 0f, 1.2f);

        // Tabletop
        CreateBox(table.transform, "Top",
            new Vector3(1.40f, 0.04f, 0.70f),
            new Vector3(0f, 0.92f, 0f),
            MakeMat(tableColor, 0.6f, 0f));

        // Metal legs
        Material legMat = MakeMat(tableMetalColor, 0.75f, 0.9f);
        Vector3[] legPos = { new Vector3( 0.65f, 0f,  0.30f),
                              new Vector3(-0.65f, 0f,  0.30f),
                              new Vector3( 0.65f, 0f, -0.30f),
                              new Vector3(-0.65f, 0f, -0.30f) };
        foreach (var p in legPos)
            CreateBox(table.transform, "Leg", new Vector3(0.04f, 0.90f, 0.04f),
                      p + new Vector3(0f, 0.45f, 0f), legMat);
    }

    private static void CreateBox(Transform parent, string name,
                                   Vector3 scale, Vector3 localPos, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<BoxCollider>());
        go.transform.SetParent(parent, false);
        go.transform.localScale    = scale;
        go.transform.localPosition = localPos;
        go.GetComponent<MeshRenderer>().material = mat;
    }

    // ── Material factory ──────────────────────────────────────────────────────

    private static Material MakeMat(Color color, float smoothness, float metallic)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
        Material m = new Material(shader);
        m.SetColor("_BaseColor", color);   // URP
        m.SetColor("_Color",     color);   // Standard fallback
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic",   metallic);
        return m;
    }
}
