using UnityEngine;
using System.Collections.Generic;

namespace Showcase
{
    public class AbstractCityGenerator : MonoBehaviour
    {
        public ShowcaseSettings Settings;
        private List<GameObject> _buildings = new List<GameObject>();
        private GameObject _cityRoot;
        private Material _cityMaterial;

        public void GenerateCity()
        {
            if (Settings == null)
            {
                Debug.LogError("AbstractCityGenerator: ShowcaseSettings is missing.");
                return;
            }

            ClearCity();

            _cityRoot = new GameObject("AbstractCity_Root");
            _cityRoot.transform.SetParent(this.transform);
            _cityRoot.transform.localPosition = Vector3.zero;

            // Use realistic architectural material
            if (Settings.BaseMaterial != null)
            {
                _cityMaterial = new Material(Settings.BaseMaterial);
            }
            else
            {
                _cityMaterial = RealisticMaterialGenerator.GenerateBaseMaterial();
            }

            float totalWidth = Settings.GridWidth * (Settings.CellSize + Settings.Spacing) - Settings.Spacing;
            float totalDepth = Settings.GridDepth * (Settings.CellSize + Settings.Spacing) - Settings.Spacing;

            Vector3 startPos = new Vector3(-totalWidth / 2f + Settings.CellSize / 2f, 0, -totalDepth / 2f + Settings.CellSize / 2f);

            float offsetX = Random.Range(0f, 1000f);
            float offsetZ = Random.Range(0f, 1000f);

            for (int x = 0; x < Settings.GridWidth; x++)
            {
                for (int z = 0; z < Settings.GridDepth; z++)
                {
                    float xPos = startPos.x + x * (Settings.CellSize + Settings.Spacing);
                    float zPos = startPos.z + z * (Settings.CellSize + Settings.Spacing);

                    float perlinValue = Mathf.PerlinNoise((x + offsetX) * Settings.PerlinScale, (z + offsetZ) * Settings.PerlinScale);
                    // Use a power curve for more dramatic height differences
                    float height = Mathf.Lerp(Settings.MinHeight, Settings.MaxHeight, Mathf.Pow(perlinValue, 2f));

                    GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    building.name = $"Building_{x}_{z}";
                    building.transform.SetParent(_cityRoot.transform);
                    
                    // Center the building's bottom at Y=0
                    building.transform.localPosition = new Vector3(xPos, height / 2f, zPos);
                    building.transform.localScale = new Vector3(Settings.CellSize, height, Settings.CellSize);

                    MeshRenderer renderer = building.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        Material vibrantMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        Color randColor = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(0.6f, 1f), 1f);
                        vibrantMat.color = randColor;
                        vibrantMat.EnableKeyword("_EMISSION");
                        vibrantMat.SetColor("_EmissionColor", randColor * 1.5f); // Hafif parlama (Glow)
                        renderer.sharedMaterial = vibrantMat;
                    }

                    // Etkileşim: Kullanıcının küpleri fırlatabilmesi için fizik ayarları
                    Rigidbody rb = building.AddComponent<Rigidbody>();
                    rb.isKinematic = false; // Fırlatılabilmesi için kinematic KAPALI olmalı
                    rb.useGravity = false;  // Uzay boşluğunda süzülmeleri için yerçekimi KAPALI
                    rb.linearDamping = 0.5f;         // Fırlattıktan sonra sonsuza kadar gitmemesi için yavaşlatıcı sürtünme
                    rb.angularDamping = 0.5f;

                    var grab = building.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
                    grab.throwOnDetach = true;
                    grab.throwVelocityScale = 2.0f; // Fırlatma hissini güçlendir

                    _buildings.Add(building);
                }
            }
        }

        public void ClearCity()
        {
            if (_cityRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_cityRoot);
                }
                else
                {
                    DestroyImmediate(_cityRoot);
                }
            }
            _buildings.Clear();
        }
    }
}
