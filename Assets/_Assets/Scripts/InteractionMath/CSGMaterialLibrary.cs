using UnityEngine;

namespace HoloLensApp.Interaction.CSG
{
    /// <summary>
    /// Assigns CSG materials (Black, White, Translucent/Acetate) to boolean result meshes.
    /// </summary>
    public class CSGMaterialLibrary : MonoBehaviour
    {
        public static CSGMaterialLibrary Instance { get; private set; }

        [Header("CSG Materials")]
        public Material BlackMaterial;
        public Material WhiteMaterial;
        public Material TranslucentAcetateMaterial;

        [Header("Fallback Generation")]
        [SerializeField] private Color blackColor = Color.black;
        [SerializeField] private Color whiteColor = Color.white;
        [SerializeField] private Color acetateColor = new Color(1f, 1f, 1f, 0.35f);

        private Material _runtimeBlack;
        private Material _runtimeWhite;
        private Material _runtimeAcetate;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            EnsureRuntimeMaterials();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public Material Resolve(CSGMaterialProfile profile)
        {
            EnsureRuntimeMaterials();

            switch (profile)
            {
                case CSGMaterialProfile.White:
                    return WhiteMaterial != null ? WhiteMaterial : _runtimeWhite;
                case CSGMaterialProfile.TranslucentAcetate:
                    return TranslucentAcetateMaterial != null ? TranslucentAcetateMaterial : _runtimeAcetate;
                default:
                    return BlackMaterial != null ? BlackMaterial : _runtimeBlack;
            }
        }

        public CSGMaterialProfile ProfileForOperation(CSGOperationType operation)
        {
            switch (operation)
            {
                case CSGOperationType.Union:
                    return CSGMaterialProfile.Black;
                case CSGOperationType.Intersection:
                    return CSGMaterialProfile.White;
                case CSGOperationType.Subtraction:
                    return CSGMaterialProfile.TranslucentAcetate;
                default:
                    return CSGMaterialProfile.Black;
            }
        }

        public void ApplyProfile(Renderer renderer, CSGMaterialProfile profile)
        {
            if (renderer == null)
                return;

            Material mat = Resolve(profile);
            if (mat != null)
                renderer.sharedMaterial = mat;
        }

        private void EnsureRuntimeMaterials()
        {
            if (_runtimeBlack == null)
                _runtimeBlack = CreateLitMaterial(blackColor, opaque: true);
            if (_runtimeWhite == null)
                _runtimeWhite = CreateLitMaterial(whiteColor, opaque: true);
            if (_runtimeAcetate == null)
                _runtimeAcetate = CreateLitMaterial(acetateColor, opaque: false);
        }

        private static Material CreateLitMaterial(Color color, bool opaque)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (!opaque)
            {
                ConfigureTransparent(material);
            }

            return material;
        }

        private static void ConfigureTransparent(Material material)
        {
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
        }
    }
}
