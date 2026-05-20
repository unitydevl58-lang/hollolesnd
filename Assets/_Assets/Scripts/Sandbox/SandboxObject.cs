using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

namespace HoloLensApp.Sandbox
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public class SandboxObject : MonoBehaviour
    {
        private Material[] originalMaterials;
        private MeshRenderer meshRenderer;
        
        [Header("Selection Aesthetics")]
        public Material highlightMaterial;
        public bool isSelected = false;

        private Rigidbody rb;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                originalMaterials = meshRenderer.materials;
            }

            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false; // Floating behavior but interactive
                rb.useGravity = false;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
            }

            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnSelectEntered);
            }
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            // Toggle selection or notify SandboxEngine
            if (SandboxEngine.Instance != null)
            {
                SandboxEngine.Instance.ToggleSelection(this);
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateHighlight();
        }

        private void UpdateHighlight()
        {
            if (meshRenderer == null) return;

            if (isSelected && highlightMaterial != null)
            {
                // Add highlight material as an overlay
                Material[] highlightedMats = new Material[originalMaterials.Length + 1];
                for (int i = 0; i < originalMaterials.Length; i++)
                {
                    highlightedMats[i] = originalMaterials[i];
                }
                highlightedMats[originalMaterials.Length] = highlightMaterial;
                meshRenderer.materials = highlightedMats;
            }
            else
            {
                // Revert to original
                meshRenderer.materials = originalMaterials;
            }
        }

        /// <summary>
        /// Updates the stored original materials if the mesh is rebuilt (e.g. by CSG).
        /// </summary>
        public void UpdateOriginalMaterials()
        {
            if (meshRenderer != null)
            {
                originalMaterials = meshRenderer.materials;
                if (isSelected) UpdateHighlight();
            }
        }
    }
}
