using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HoloLensApp.Interaction.Math
{
    /// <summary>
    /// Marks a geometric form for Wong state tracking, grid snapping, and CSG operations.
    /// </summary>
    public class FormInteractable : MonoBehaviour
    {
        [Header("Form Metadata")]
        public string FormLabel;

        [SerializeField] private Color touchingHighlight = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color penetrationHighlight = new Color(0.2f, 0.75f, 1f, 1f);

        private Material _originalMaterial;
        private MeshRenderer _renderer;
        private WongInteractionState _lastState = WongInteractionState.Unknown;

        private void Awake()
        {
            _renderer = GetComponentInChildren<MeshRenderer>();
            if (_renderer != null)
                _originalMaterial = _renderer.sharedMaterial;
        }

        private void OnEnable()
        {
            if (ShapeInteractionManager.Instance != null)
                ShapeInteractionManager.Instance.RegisterForm(this);
        }

        private void OnDisable()
        {
            if (ShapeInteractionManager.Instance != null)
                ShapeInteractionManager.Instance.UnregisterForm(this);

            RestoreMaterial();
        }

        public void ApplyWongHighlight(WongInteractionState state)
        {
            if (_renderer == null || _originalMaterial == null)
                return;

            if (_lastState == state)
                return;

            _lastState = state;

            switch (state)
            {
                case WongInteractionState.Touching:
                    _renderer.material.color = touchingHighlight;
                    break;
                case WongInteractionState.Penetration:
                    _renderer.material.color = penetrationHighlight;
                    break;
                default:
                    RestoreMaterial();
                    break;
            }
        }

        private void RestoreMaterial()
        {
            if (_renderer != null && _originalMaterial != null)
                _renderer.sharedMaterial = _originalMaterial;

            _lastState = WongInteractionState.Unknown;
        }
    }
}
