using UnityEngine;

namespace HoloLensApp.Interaction.Snapping
{
    /// <summary>
    /// Represents an object that can be snapped to a 3D spatial grid.
    /// Implementing this interface allows the SpatialAlignmentManager to interact with the object.
    /// </summary>
    public interface IGridSnappable
    {
        /// <summary>
        /// The GameObject reference of the snappable entity.
        /// </summary>
        GameObject OwnerGameObject { get; }

        /// <summary>
        /// The current grid coordinate (Node) this object is occupying.
        /// </summary>
        Vector3Int CurrentGridCoordinate { get; set; }

        /// <summary>
        /// Snaps the object to the target position smoothly or instantaneously.
        /// </summary>
        /// <param name="targetWorldPosition">The exact world position to snap to.</param>
        void SnapToGrid(Vector3 targetWorldPosition);
    }
}
