using UnityEngine;
using UnityEditor;
using Showcase;

public static class ShowcaseMenu
{
    [MenuItem("Showcase/Generate/Abstract City")]
    public static void GenerateAbstractCity()
    {
        // Find existing or create new root
        GameObject root = GameObject.Find("AbstractCity_Root");
        if (root == null)
        {
            root = new GameObject("AbstractCity_Root");
        }

        // Generate City
        var cityGen = root.GetComponent<AbstractCityGenerator>();
        if (cityGen == null)
        {
            cityGen = root.AddComponent<AbstractCityGenerator>();
        }

        if (cityGen.Settings == null)
        {
            cityGen.Settings = ScriptableObject.CreateInstance<ShowcaseSettings>();
        }

        cityGen.GenerateCity();
        Debug.Log("Abstract City generated successfully from Editor Menu.");
    }

    [MenuItem("Showcase/Generate/Radial Monument")]
    public static void GenerateRadialMonument()
    {
        // Delete old root if exists
        GameObject oldRoot = GameObject.Find("RadialBalance_Root");
        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject("RadialBalance_Root");

        var monumentGen = root.AddComponent<RadialMonumentGenerator>();
        monumentGen.Settings = ScriptableObject.CreateInstance<ShowcaseSettings>();
        monumentGen.GenerateMonument();
        
        var animator = root.AddComponent<ShowcaseAnimators.RadialAnimator>();
        animator.rotationSpeed = monumentGen.Settings.RadialRotationSpeed;
        animator.baseScaleAmplitude = monumentGen.Settings.RadialScaleAmplitude;

        Debug.Log("Radial Monument generated successfully from Editor Menu.");
    }

    [MenuItem("Showcase/Generate/Deconstruction Setup")]
    public static void GenerateDeconstructionSetup()
    {
        // Delete old root if exists
        GameObject oldRoot = GameObject.Find("Deconstruction_Root");
        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject("Deconstruction_Root");

        var deconGen = root.AddComponent<DeconstructionGenerator>();
        deconGen.Settings = ScriptableObject.CreateInstance<ShowcaseSettings>();
        deconGen.GenerateDeconstruction();

        Debug.Log("Deconstruction Setup generated successfully from Editor Menu.");
    }
}
