using UnityEngine;

namespace Showcase
{
    public static class RealisticMaterialGenerator
    {
        public static Material GenerateBaseMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            // Light architectural concrete/stone: soft warm white
            mat.SetColor("_BaseColor", new Color(0.92f, 0.90f, 0.88f)); 
            mat.SetColor("_Color", new Color(0.92f, 0.90f, 0.88f)); // Fallback
            mat.SetFloat("_Smoothness", 0.1f); // Matte finish
            return mat;
        }

        public static Material[] GenerateAccentMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            // Soft Terracotta
            Material terracotta = new Material(shader);
            terracotta.SetColor("_BaseColor", new Color(0.80f, 0.45f, 0.35f));
            terracotta.SetColor("_Color", new Color(0.80f, 0.45f, 0.35f));
            terracotta.SetFloat("_Smoothness", 0.1f);

            // Ocean Blue
            Material oceanBlue = new Material(shader);
            oceanBlue.SetColor("_BaseColor", new Color(0.35f, 0.60f, 0.75f));
            oceanBlue.SetColor("_Color", new Color(0.35f, 0.60f, 0.75f));
            oceanBlue.SetFloat("_Smoothness", 0.1f);

            // Sunlight Yellow
            Material sunlightYellow = new Material(shader);
            sunlightYellow.SetColor("_BaseColor", new Color(0.95f, 0.85f, 0.40f));
            sunlightYellow.SetColor("_Color", new Color(0.95f, 0.85f, 0.40f));
            sunlightYellow.SetFloat("_Smoothness", 0.1f);

            return new Material[] { terracotta, oceanBlue, sunlightYellow };
        }
    }
}
