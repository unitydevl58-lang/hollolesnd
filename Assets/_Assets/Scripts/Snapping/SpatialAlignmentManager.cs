using System.Collections.Generic;
using UnityEngine;
using HoloLensApp.Interaction.Math;

namespace HoloLensApp.Interaction.Snapping
{
    /// <summary>
    /// Strict 3D grid matrix: objects occupy discrete cells and snap to cell centers.
    /// </summary>
    public class SpatialAlignmentManager : MonoBehaviour
    {
        public static SpatialAlignmentManager Instance { get; private set; }

        [Header("Grid Matrix")]
        [Min(0.01f)]
        public float GridCellSize = 0.1f;

        public Vector3 GridOrigin = Vector3.zero;

        [Tooltip("When true, rejected occupied cells search outward in BFS order.")]
        public bool UseBreadthFirstSearch = true;

        private readonly Dictionary<Vector3Int, IGridSnappable> _occupiedCells = new Dictionary<Vector3Int, IGridSnappable>();

        private static readonly Vector3Int[] NeighborOffsets =
        {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down,
            Vector3Int.forward, Vector3Int.back
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RequestSnap(IGridSnappable snappable, Vector3 dropPosition)
        {
            if (snappable == null)
            {
                Debug.LogError("[SpatialAlignmentManager] Null snappable.");
                return;
            }

            Vector3Int footprint = GetFootprint(snappable);
            Vector3Int targetCoord = WorldToGridCoordinate(dropPosition, footprint);
            targetCoord = FindNearestAvailableCoordinate(targetCoord, footprint, snappable);

            UpdateOccupancy(snappable, targetCoord, footprint);
            snappable.SnapToGrid(GridCoordinateToWorld(targetCoord, footprint));
        }

        public void ReleaseCoordinate(IGridSnappable snappable)
        {
            if (snappable == null)
                return;

            Vector3Int footprint = GetFootprint(snappable);
            Vector3Int origin = snappable.CurrentGridCoordinate;

            for (int x = 0; x < footprint.x; x++)
            for (int y = 0; y < footprint.y; y++)
            for (int z = 0; z < footprint.z; z++)
            {
                Vector3Int cell = origin + new Vector3Int(x, y, z);
                if (_occupiedCells.TryGetValue(cell, out IGridSnappable occupier) && occupier == snappable)
                    _occupiedCells.Remove(cell);
            }
        }

        public bool IsCellOccupied(Vector3Int cell, IGridSnappable requestingObject)
        {
            if (!_occupiedCells.TryGetValue(cell, out IGridSnappable occupier))
                return false;

            return occupier != requestingObject;
        }

        private void UpdateOccupancy(IGridSnappable snappable, Vector3Int originCoord, Vector3Int footprint)
        {
            ReleaseCoordinate(snappable);
            snappable.CurrentGridCoordinate = originCoord;

            for (int x = 0; x < footprint.x; x++)
            for (int y = 0; y < footprint.y; y++)
            for (int z = 0; z < footprint.z; z++)
            {
                Vector3Int cell = originCoord + new Vector3Int(x, y, z);
                _occupiedCells[cell] = snappable;
            }
        }

        private Vector3Int FindNearestAvailableCoordinate(
            Vector3Int targetCoord,
            Vector3Int footprint,
            IGridSnappable requestingObject)
        {
            if (CanOccupy(targetCoord, footprint, requestingObject))
                return targetCoord;

            if (!UseBreadthFirstSearch)
                return FindNeighborFallback(targetCoord, footprint, requestingObject);

            Queue<Vector3Int> queue = new Queue<Vector3Int>(32);
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

            queue.Enqueue(targetCoord);
            visited.Add(targetCoord);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                if (CanOccupy(current, footprint, requestingObject))
                    return current;

                for (int i = 0; i < NeighborOffsets.Length; i++)
                {
                    Vector3Int next = current + NeighborOffsets[i];
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }

                if (visited.Count > 512)
                    break;
            }

            return targetCoord + Vector3Int.up;
        }

        private Vector3Int FindNeighborFallback(
            Vector3Int targetCoord,
            Vector3Int footprint,
            IGridSnappable requestingObject)
        {
            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                Vector3Int candidate = targetCoord + NeighborOffsets[i];
                if (CanOccupy(candidate, footprint, requestingObject))
                    return candidate;
            }

            return targetCoord + Vector3Int.up;
        }

        private bool CanOccupy(Vector3Int origin, Vector3Int footprint, IGridSnappable requestingObject)
        {
            for (int x = 0; x < footprint.x; x++)
            for (int y = 0; y < footprint.y; y++)
            for (int z = 0; z < footprint.z; z++)
            {
                Vector3Int cell = origin + new Vector3Int(x, y, z);
                if (IsCellOccupied(cell, requestingObject))
                    return false;
            }

            return true;
        }

        private Vector3Int WorldToGridCoordinate(Vector3 worldPos, Vector3Int footprint)
        {
            Vector3 local = worldPos - GridOrigin;
            float denom = Mathf.Max(GridCellSize, MathUtilities.Epsilon);

            int x = Mathf.RoundToInt(local.x / denom);
            int y = Mathf.RoundToInt(local.y / denom);
            int z = Mathf.RoundToInt(local.z / denom);

            Vector3Int cell = new Vector3Int(x, y, z);

            // Anchor multi-cell objects so their center sits in the matrix.
            cell.x -= (footprint.x - 1) / 2;
            cell.y -= (footprint.y - 1) / 2;
            cell.z -= (footprint.z - 1) / 2;

            return cell;
        }

        private Vector3 GridCoordinateToWorld(Vector3Int gridCoord, Vector3Int footprint)
        {
            float denom = Mathf.Max(GridCellSize, MathUtilities.Epsilon);

            float x = (gridCoord.x + (footprint.x - 1) * 0.5f) * denom;
            float y = (gridCoord.y + (footprint.y - 1) * 0.5f) * denom;
            float z = (gridCoord.z + (footprint.z - 1) * 0.5f) * denom;

            return GridOrigin + new Vector3(x, y, z);
        }

        private static Vector3Int GetFootprint(IGridSnappable snappable)
        {
            if (snappable is GridSnapper snapper)
                return snapper.CellFootprint;

            if (snappable is GridSnappableObject legacy)
                return legacy.CellFootprint;

            return Vector3Int.one;
        }
    }
}
