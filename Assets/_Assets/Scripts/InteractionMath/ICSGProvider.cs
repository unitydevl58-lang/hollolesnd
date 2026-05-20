using System.Threading.Tasks;
using UnityEngine;

namespace HoloLensApp.Interaction.CSG
{
    /// <summary>
    /// A contract for any Constructive Solid Geometry (CSG) implementation.
    /// By using this interface, we decouple the HoloLens logic from specific 3rd party CSG libraries (like pb_CSG).
    /// </summary>
    public interface ICSGProvider
    {
        /// <summary>
        /// Performs a CSG operation on two GameObjects and returns the resulting Mesh data asynchronously.
        /// </summary>
        /// <param name="objA">The first/base GameObject.</param>
        /// <param name="objB">The second/intersecting GameObject.</param>
        /// <param name="operationType">Union, Intersection, or Subtraction.</param>
        /// <returns>A Task that resolves to the newly generated Mesh.</returns>
        Task<Mesh> PerformCSGAsync(GameObject objA, GameObject objB, CSGOperationType operationType);
    }
}
