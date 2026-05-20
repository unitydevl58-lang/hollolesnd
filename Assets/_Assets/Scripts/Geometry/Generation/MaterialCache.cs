using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses materials by color to avoid material allocation spikes while generating scenes.
/// </summary>
public sealed class MaterialCache
{
    private readonly IColorResolver colorResolver;
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private Shader cachedShader;

    public MaterialCache(IColorResolver colorResolver)
    {
        this.colorResolver = colorResolver;
    }

    public Material GetMaterial(string colorValue)
    {
        string key = TextNormalizer.NormalizeKey(string.IsNullOrWhiteSpace(colorValue) ? "white" : colorValue);
        if (materials.TryGetValue(key, out Material existingMaterial))
            return existingMaterial;

        if (!colorResolver.TryResolve(colorValue, out Color resolvedColor))
            resolvedColor = Color.white;

        Material material = new Material(GetShader());

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", resolvedColor);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", resolvedColor);

        materials[key] = material;
        return material;
    }

    public void Dispose()
    {
        foreach (Material material in materials.Values)
        {
            if (material == null)
                continue;

            if (Application.isPlaying)
                Object.Destroy(material);
            else
                Object.DestroyImmediate(material);
        }

        materials.Clear();
    }

    private Shader GetShader()
    {
        if (cachedShader != null)
            return cachedShader;

        cachedShader = Shader.Find("Universal Render Pipeline/Lit");
        if (cachedShader == null)
            cachedShader = Shader.Find("Standard");

        return cachedShader;
    }
}
