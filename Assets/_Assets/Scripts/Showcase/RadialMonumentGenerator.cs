using UnityEngine;
using System.Collections.Generic;

namespace Showcase
{
    public class RadialMonumentGenerator : MonoBehaviour
    {
        public ShowcaseSettings Settings;

        private Material _baseArchitecturalMaterial;
        private Material[] _accentMaterials;
        private List<GameObject> _monuments = new List<GameObject>();

        public void GenerateMonument()
        {
            if (Settings == null)
            {
                Debug.LogError("ShowcaseSettings missing on RadialMonumentGenerator.");
                return;
            }

            // Clear previous monuments safely
            foreach (Transform child in transform)
            {
                DestroyImmediate(child.gameObject);
            }
            _monuments.Clear();

            // Generate realistic architectural materials
            _baseArchitecturalMaterial = RealisticMaterialGenerator.GenerateBaseMaterial();
            _accentMaterials = RealisticMaterialGenerator.GenerateAccentMaterials();

            // Instantiate monument blocks in a circle
            for (int i = 0; i < Settings.RadialCount; i++)
            {
                float angle = i * Mathf.PI * 2f / Settings.RadialCount;
                
                // Position at radius on the XZ plane
                Vector3 pos = new Vector3(Mathf.Cos(angle) * Settings.RadialRadius, 0f, Mathf.Sin(angle) * Settings.RadialRadius);
                
                // Parent pivot container for the monument block
                GameObject monumentPivot = new GameObject($"MonumentBlock_{i}");
                monumentPivot.transform.SetParent(this.transform);
                monumentPivot.transform.position = pos;

                // Face the center (0,0,0)
                monumentPivot.transform.LookAt(transform.position);

                // 1. Outer Shell (Base Material)
                GameObject outerShell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                outerShell.name = "OuterShell";
                outerShell.transform.SetParent(monumentPivot.transform);
                // Adjust pivot so it sits on the ground
                outerShell.transform.localPosition = new Vector3(0, Settings.RadialMonumentHeight / 2f, 0);
                outerShell.transform.localScale = new Vector3(Settings.RadialMonumentWidth, Settings.RadialMonumentHeight, Settings.RadialMonumentDepth);
                outerShell.transform.localRotation = Quaternion.identity;

                MeshRenderer outerRenderer = outerShell.GetComponent<MeshRenderer>();
                if (outerRenderer != null)
                {
                    Material vibrantOuter = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    Color randColor1 = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(0.6f, 1f), 1f);
                    vibrantOuter.color = randColor1;
                    vibrantOuter.EnableKeyword("_EMISSION");
                    vibrantOuter.SetColor("_EmissionColor", randColor1 * 1.5f);
                    outerRenderer.sharedMaterial = vibrantOuter;
                }

                // 2. Inner Face Accent (Facing center)
                GameObject innerFace = GameObject.CreatePrimitive(PrimitiveType.Cube);
                innerFace.name = "InnerAccentFace";
                innerFace.transform.SetParent(monumentPivot.transform);
                
                // Slightly thinner, same height, but protruding from the front face (Z-axis local forward is towards center due to LookAt)
                float innerWidth = Settings.RadialMonumentWidth * 0.9f;
                float innerHeight = Settings.RadialMonumentHeight; // Flush or slightly inset? Let's make it 95% height to look like an inset panel
                float innerDepth = 0.1f;
                
                // The front face is along the local +Z axis
                innerFace.transform.localPosition = new Vector3(0, innerHeight / 2f, (Settings.RadialMonumentDepth / 2f) + (innerDepth / 2f));
                innerFace.transform.localScale = new Vector3(innerWidth, innerHeight * 0.95f, innerDepth);
                innerFace.transform.localRotation = Quaternion.identity;

                MeshRenderer innerRenderer = innerFace.GetComponent<MeshRenderer>();
                if (innerRenderer != null)
                {
                    Material vibrantInner = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    Color randColor2 = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(0.6f, 1f), 1f);
                    vibrantInner.color = randColor2;
                    vibrantInner.EnableKeyword("_EMISSION");
                    vibrantInner.SetColor("_EmissionColor", randColor2 * 2f); // İçi daha parlak
                    innerRenderer.sharedMaterial = vibrantInner;
                }

                // Make the entire block grabbable and throwable
                Rigidbody rb = monumentPivot.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
                var grab = monumentPivot.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
                grab.throwOnDetach = true;
                grab.throwVelocityScale = 2.0f;

                _monuments.Add(monumentPivot);
            }

            // Ortaya harika bir parlak ışık ekle (Görsel şölen)
            GameObject centerLightObj = new GameObject("CenterGlowLight");
            centerLightObj.transform.SetParent(this.transform);
            centerLightObj.transform.localPosition = new Vector3(0, Settings.RadialMonumentHeight / 2f, 0);
            Light pLight = centerLightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = Settings.RadialRadius * 3f;
            pLight.intensity = 10f; // Çok aydınlık
            pLight.color = Color.cyan;
        }
    }
}
