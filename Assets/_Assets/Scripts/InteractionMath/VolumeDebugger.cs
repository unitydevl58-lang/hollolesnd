using UnityEngine;

namespace HoloLensApp.Interaction.Math
{
    /// <summary>
    /// Low-frequency Wong volume probe for demos (not per-frame in production).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VolumeDebugger : MonoBehaviour
    {
        [SerializeField] private float probeIntervalSeconds = 0.5f;

        private Collider _collider;
        private float _nextProbeTime;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextProbeTime)
                return;

            _nextProbeTime = Time.unscaledTime + probeIntervalSeconds;
            ProbeOverlaps();
        }

        private void ProbeOverlaps()
        {
            Collider[] hits = Physics.OverlapBox(
                _collider.bounds.center,
                _collider.bounds.extents,
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider other = hits[i];
                if (other == _collider)
                    continue;

                Bounds otherBounds = other.bounds;
                WongInteractionState state = ShapeInteractionManager.ClassifyWongState(_collider.bounds, otherBounds);
                float intersection = ShapeInteractionManager.CalculateIntersectionVolume(_collider.bounds, otherBounds);
                float subtraction = ShapeInteractionManager.CalculateSubtractionVolume(_collider.bounds, otherBounds);

                Debug.Log($"[VolumeDebugger] {name} ↔ {other.name} | Wong={state} | ∩={intersection:F6} m³ | −={subtraction:F6} m³");
            }
        }
    }
}
