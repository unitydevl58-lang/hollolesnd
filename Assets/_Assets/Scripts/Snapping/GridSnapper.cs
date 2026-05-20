using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HoloLensApp.Interaction.Snapping
{
    /// <summary>
    /// Strict 3D grid snapping for XR-grabbable geometric forms.
    /// Snaps to discrete matrix cells on release — no free-floating placement.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GridSnapper : MonoBehaviour, IGridSnappable
    {
        [Header("Grid Footprint")]
        [Tooltip("How many grid cells this object occupies along each axis.")]
        public Vector3Int CellFootprint = Vector3Int.one;

        [Header("Snap Behaviour")]
        [Tooltip("When true, teleports instantly to the cell center (strict matrix).")]
        public bool InstantSnap = true;

        [Range(1f, 30f)]
        public float SnapLerpSpeed = 14f;

        public GameObject OwnerGameObject => gameObject;
        public Vector3Int CurrentGridCoordinate { get; set; }

        private XRGrabInteractable _grabInteractable;
        private Coroutine _snapRoutine;

        private void Awake()
        {
            _grabInteractable = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (_grabInteractable == null)
                return;

            _grabInteractable.selectEntered.AddListener(OnSelectEntered);
            _grabInteractable.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                _grabInteractable.selectExited.RemoveListener(OnSelectExited);
            }

            if (SpatialAlignmentManager.Instance != null)
                SpatialAlignmentManager.Instance.ReleaseCoordinate(this);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (_snapRoutine != null)
            {
                StopCoroutine(_snapRoutine);
                _snapRoutine = null;
            }

            if (SpatialAlignmentManager.Instance != null)
                SpatialAlignmentManager.Instance.ReleaseCoordinate(this);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (SpatialAlignmentManager.Instance == null)
            {
                Debug.LogWarning("[GridSnapper] SpatialAlignmentManager missing.");
                return;
            }

            SpatialAlignmentManager.Instance.RequestSnap(this, transform.position);
        }

        public void SnapToGrid(Vector3 targetWorldPosition)
        {
            if (_snapRoutine != null)
                StopCoroutine(_snapRoutine);

            if (InstantSnap)
            {
                transform.position = targetWorldPosition;
                return;
            }

            _snapRoutine = StartCoroutine(SmoothSnapRoutine(targetWorldPosition));
        }

        private IEnumerator SmoothSnapRoutine(Vector3 targetPosition)
        {
            const float thresholdSqr = 0.000001f;

            while (Vector3.SqrMagnitude(transform.position - targetPosition) > thresholdSqr)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    targetPosition,
                    Time.deltaTime * SnapLerpSpeed);
                yield return null;
            }

            transform.position = targetPosition;
            _snapRoutine = null;
        }
    }
}
