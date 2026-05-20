using UnityEngine;

namespace Showcase
{
    public static class CyberGridTextureGenerator
    {
        public static Texture2D GenerateCyberGridTexture(int width = 512, int height = 512, int gridCount = 8, float thickness = 0.05f)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            Color baseColor = new Color(0.02f, 0.02f, 0.08f, 1f); // Deep Midnight
            
            // Cyberpunk colors
            Color cyan = new Color(0f, 1f, 1f, 1f);
            Color pink = new Color(1f, 0.07f, 0.57f, 1f);
            Color lavender = new Color(0.9f, 0.4f, 1f, 1f);
            Color electricYellow = new Color(1f, 1f, 0f, 1f);

            Color[] colors = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;

                    float cellU = (u * gridCount) % 1.0f;
                    float cellV = (v * gridCount) % 1.0f;

                    bool isLineU = cellU < thickness || cellU > 1.0f - thickness;
                    bool isLineV = cellV < thickness || cellV > 1.0f - thickness;

                    if (isLineU || isLineV)
                    {
                        // Dynamically mix colors based on position to create vibrancy
                        float mix1 = Mathf.Sin(u * Mathf.PI * 2f) * 0.5f + 0.5f;
                        float mix2 = Mathf.Cos(v * Mathf.PI * 2f) * 0.5f + 0.5f;
                        
                        Color lineColor = Color.Lerp(cyan, pink, mix1);
                        lineColor = Color.Lerp(lineColor, lavender, mix2);
                        
                        // Add electric yellow highlights at intersections
                        if (isLineU && isLineV)
                        {
                            lineColor = Color.Lerp(lineColor, electricYellow, 0.8f);
                        }
                        
                        colors[y * width + x] = lineColor; 
                    }
                    else
                    {
                        colors[y * width + x] = baseColor;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }
    }
}
