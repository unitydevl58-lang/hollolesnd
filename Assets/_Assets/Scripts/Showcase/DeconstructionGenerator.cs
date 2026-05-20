using UnityEngine;
using System.Collections.Generic;

namespace Showcase
{
    public class DeconstructionGenerator : MonoBehaviour
    {
        public ShowcaseSettings Settings;

        private Material _baseArchitecturalMaterial;
        private Material[] _accentMaterials;

        public void GenerateDeconstruction()
        {
            if (Settings == null)
            {
                Debug.LogError("ShowcaseSettings missing on DeconstructionGenerator.");
                return;
            }

            // Clear previous stages safely
            foreach (Transform child in transform)
            {
                DestroyImmediate(child.gameObject);
            }

            // Generate realistic architectural materials
            _baseArchitecturalMaterial = RealisticMaterialGenerator.GenerateBaseMaterial();
            _accentMaterials = RealisticMaterialGenerator.GenerateAccentMaterials();

            int stages = Settings.DeconstructionStages;
            float spacing = Settings.DeconstructionStageSpacing;
            float erosionIntensity = Settings.DeconstructionErosionIntensity;

            // Start far left so the progression is centered
            float startX = -((stages - 1) * spacing) / 2f;

            // Define a 4x4x4 cube of voxels
            int grid = 4;
            float voxelSize = 1.0f;
            float offsetAmount = (grid - 1) * voxelSize / 2f;

            for (int stage = 0; stage < stages; stage++)
            {
                GameObject stageRoot = new GameObject($"Stage_{stage}");
                stageRoot.transform.SetParent(this.transform);
                stageRoot.transform.localPosition = new Vector3(startX + (stage * spacing), 0, 0);

                // To animate only the eroded stages
                GameObject animatedRoot = new GameObject("AnimatedVoxels");
                animatedRoot.transform.SetParent(stageRoot.transform);
                animatedRoot.transform.localPosition = Vector3.zero;

                for (int x = 0; x < grid; x++)
                {
                    for (int y = 0; y < grid; y++)
                    {
                        for (int z = 0; z < grid; z++)
                        {
                            bool isOuter = (x == 0 || x == grid - 1 || y == 0 || y == grid - 1 || z == 0 || z == grid - 1);
                            
                            // Base local position centered
                            Vector3 localPos = new Vector3(x * voxelSize - offsetAmount, (y * voxelSize - offsetAmount) + 3f, z * voxelSize - offsetAmount);
                            
                            // Apply erosion scattering based on the stage
                            if (stage > 0)
                            {
                                // Disconnect and scatter
                                Vector3 scatterDir = (localPos - new Vector3(0, 3f, 0)).normalized;
                                if (scatterDir == Vector3.zero) scatterDir = Random.onUnitSphere;
                                
                                // Progressively scatter more
                                float scatterDist = stage * erosionIntensity * Random.Range(0.8f, 1.2f);
                                localPos += scatterDir * scatterDist;
                            }

                            GameObject voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            voxel.name = $"Voxel_{x}_{y}_{z}";
                            
                            // Stage 0 remains static, >0 gets added to the animated root
                            if (stage > 0)
                            {
                                voxel.transform.SetParent(animatedRoot.transform);
                            }
                            else
                            {
                                voxel.transform.SetParent(stageRoot.transform);
                            }
                            
                            voxel.transform.localPosition = localPos;

                            // Scale slightly down to show disconnection if stage > 0
                            float currentScale = voxelSize;
                            if (stage > 0)
                            {
                                currentScale = voxelSize * Mathf.Lerp(1.0f, 0.4f, (float)stage / (stages - 1));
                            }
                            voxel.transform.localScale = Vector3.one * currentScale;

                            MeshRenderer renderer = voxel.GetComponent<MeshRenderer>();
                            if (renderer != null)
                            {
                                Material vibrantMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                                Color randColor = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(0.6f, 1f), 1f);
                                vibrantMat.color = randColor;
                                vibrantMat.EnableKeyword("_EMISSION");
                                vibrantMat.SetColor("_EmissionColor", randColor * 1.5f);
                                renderer.sharedMaterial = vibrantMat;
                            }

                            // Etkileşim: Fırlatılabilen parçalar
                            Rigidbody rb = voxel.AddComponent<Rigidbody>();
                            rb.isKinematic = false;
                            rb.useGravity = false;
                            rb.linearDamping = 0.5f;
                            rb.angularDamping = 0.5f;
                            var grab = voxel.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
                            grab.throwOnDetach = true;
                            grab.throwVelocityScale = 3.0f; // Parçalanan küpleri fırlatmak daha eğlenceli olsun!
                        }
                    }
                }

                // Attach animator to the scattered parts if stage > 0
                if (stage > 0)
                {
                    var animator = animatedRoot.AddComponent<ShowcaseAnimators.DeconstructionAnimator>();
                    animator.floatSpeed = Settings.DeconFloatSpeed * 2.0f; // Daha hızlı!
                    animator.floatAmplitude = Settings.DeconFloatAmplitude * stage; // More amplitude for later stages
                    animator.rotateSpeed = Settings.DeconRotateSpeed * stage * 3.0f; // 3 kat daha hızlı dönsün!
                }
            }
        }
    }
}
