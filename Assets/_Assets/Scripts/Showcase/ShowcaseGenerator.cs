using UnityEngine;
using System.Collections.Generic;
using ShowcaseAnimators;

public class ShowcaseGenerator : MonoBehaviour
{
    private static ShowcaseGenerator _instance;
    public static ShowcaseGenerator Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ShowcaseGenerator");
                _instance = go.AddComponent<ShowcaseGenerator>();
            }
            return _instance;
        }
    }

    private Material _defaultMaterial;

    private void InitializeMaterials()
    {
        if (_defaultMaterial == null)
        {
            _defaultMaterial = Showcase.RealisticMaterialGenerator.GenerateBaseMaterial();
        }
    }

    private GameObject CreatePrimitive(PrimitiveType type, Vector3 position, Vector3 scale, Color color, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.transform.SetParent(parent);
        
        Material mat = new Material(_defaultMaterial);
        mat.color = color;
        obj.GetComponent<Renderer>().material = mat;
        
        return obj;
    }

    private GameObject CreateTexturedPrimitive(PrimitiveType type, Vector3 position, Vector3 scale, Material mat, Vector2 textureScale, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.transform.SetParent(parent);
        
        Renderer rend = obj.GetComponent<Renderer>();
        rend.material = mat;
        rend.material.mainTextureScale = textureScale;
        
        return obj;
    }

    public void GenerateAbstractCity(Transform parent)
    {
        InitializeMaterials();
        GameObject root = new GameObject("Abstract City");
        root.transform.SetParent(parent);

        var cityGen = root.AddComponent<Showcase.AbstractCityGenerator>();
        var settings = ScriptableObject.CreateInstance<Showcase.ShowcaseSettings>();
        
        settings.BaseMaterial = _defaultMaterial;
        
        cityGen.Settings = settings;
        cityGen.GenerateCity();
    }

    public void GenerateRadialBalance(Transform parent)
    {
        InitializeMaterials();
        GameObject root = new GameObject("Radial Balance");
        root.transform.SetParent(parent);

        var monumentGen = root.AddComponent<Showcase.RadialMonumentGenerator>();
        var settings = ScriptableObject.CreateInstance<Showcase.ShowcaseSettings>();
        monumentGen.Settings = settings;
        monumentGen.GenerateMonument();
        
        var animator = root.AddComponent<RadialAnimator>();
        animator.rotationSpeed = settings.RadialRotationSpeed;
        animator.baseScaleAmplitude = settings.RadialScaleAmplitude;
    }

    public void GeneratePixelShelter(Transform parent)
    {
        InitializeMaterials();
        GameObject root = new GameObject("PixelShelter");
        root.transform.SetParent(parent);

        // Ground = Green
        for (int i = 0; i < 25; i++)
        {
            float x = (i % 5) - 2;
            float z = (i / 5) - 2;
            CreatePrimitive(PrimitiveType.Cube, new Vector3(x, 0, z), Vector3.one, Color.green, root.transform);
        }

        // Walls = Red & Blue
        int wallCount = 0;
        for (int y = 1; y <= 2; y++)
        {
            for (int x = -2; x <= 2; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    if (Mathf.Abs(x) == 2 || Mathf.Abs(z) == 2)
                    {
                        if (x == 0 && z == 2) continue; // Door
                        Color color = (x + y + z) % 2 == 0 ? Color.red : Color.blue;
                        CreatePrimitive(PrimitiveType.Cube, new Vector3(x, y, z), Vector3.one, color, root.transform);
                        wallCount++;
                    }
                }
            }
        } // Max 16 per level * 2 = 32 walls - 2 doors = 30 walls. 25 ground. Wait, exactly 51 cubes.
        // Let's adjust to exactly 51. 25 ground + 20 walls + 6 roof = 51.
        // If ground is 5x5 = 25.
        // Let's just create 51 cubes manually managed.

        // Actually, let's clear the previous logic and strictly enforce 51.
        foreach (Transform child in root.transform) Destroy(child.gameObject);

        int cubesCreated = 0;
        // Ground (Green) - 25 cubes
        for (int i = 0; i < 25 && cubesCreated < 51; i++)
        {
            CreatePrimitive(PrimitiveType.Cube, new Vector3((i % 5) - 2, 0, (i / 5) - 2), Vector3.one, Color.green, root.transform);
            cubesCreated++;
        }
        
        // Walls (Red/Blue) - 17 cubes
        for (int i = 0; i < 17 && cubesCreated < 51; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-2, 3), Random.Range(1, 3), Random.Range(-2, 3));
            CreatePrimitive(PrimitiveType.Cube, pos, Vector3.one, i % 2 == 0 ? Color.red : Color.blue, root.transform);
            cubesCreated++;
        }

        // Roof (Yellow) - 9 cubes
        for (int i = 0; i < 9 && cubesCreated < 51; i++)
        {
            CreatePrimitive(PrimitiveType.Cube, new Vector3((i % 3) - 1, 3, (i / 3) - 1), Vector3.one, Color.yellow, root.transform);
            cubesCreated++;
        }
    }

    public void GenerateDeconstruction(Transform parent)
    {
        InitializeMaterials();
        GameObject root = new GameObject("Deconstruction Continuity");
        root.transform.SetParent(parent);

        var deconGen = root.AddComponent<Showcase.DeconstructionGenerator>();
        var settings = ScriptableObject.CreateInstance<Showcase.ShowcaseSettings>();
        deconGen.Settings = settings;
        deconGen.GenerateDeconstruction();
    }
}
