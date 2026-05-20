using UnityEngine;

namespace HoloLensApp.Interaction.Snapping
{
    /// <summary>
    /// Backward-compatible alias that delegates to <see cref="GridSnapper"/>.
    /// </summary>
    [RequireComponent(typeof(GridSnapper))]
    public class GridSnappableObject : MonoBehaviour, IGridSnappable
    {
        [Header("Grid Footprint")]
        public Vector3Int CellFootprint = Vector3Int.one;

        [Range(1f, 30f)]
        public float SnapLerpSpeed = 14f;

        public bool InstantSnap = true;

        private GridSnapper _snapper;

        public GameObject OwnerGameObject => gameObject;
        public Vector3Int CurrentGridCoordinate
        {
            get => _snapper != null ? _snapper.CurrentGridCoordinate : Vector3Int.zero;
            set { if (_snapper != null) _snapper.CurrentGridCoordinate = value; }
        }

        private void Awake()
        {
            _snapper = GetComponent<GridSnapper>();
            if (_snapper == null)
                _snapper = gameObject.AddComponent<GridSnapper>();

            SyncToSnapper();
        }

        private void OnValidate()
        {
            if (_snapper == null)
                _snapper = GetComponent<GridSnapper>();

            SyncToSnapper();
        }

        public void SnapToGrid(Vector3 targetWorldPosition)
        {
            if (_snapper != null)
                _snapper.SnapToGrid(targetWorldPosition);
        }

        private void SyncToSnapper()
        {
            if (_snapper == null)
                return;

            _snapper.CellFootprint = CellFootprint;
            _snapper.SnapLerpSpeed = SnapLerpSpeed;
            _snapper.InstantSnap = InstantSnap;
        }
    }
}
