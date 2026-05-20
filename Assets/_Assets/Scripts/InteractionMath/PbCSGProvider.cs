using System.Threading.Tasks;
using UnityEngine;
using Parabox.CSG;

namespace HoloLensApp.Interaction.CSG
{
    /// <summary>
    /// A concrete implementation of ICSGProvider that wraps the "pb_CSG" library.
    /// This acts as a bridge between our abstract CSGFormManager and the 3rd party tool.
    /// </summary>
    public class PbCSGProvider : MonoBehaviour, ICSGProvider
    {
        private void Awake()
        {
            // Optional: Automatically inject this provider into the manager
            // if we want to set it up automatically.
            // (Assuming we add a public setter to CSGFormManager)
        }

        public Task<Mesh> PerformCSGAsync(GameObject objA, GameObject objB, CSGOperationType operationType)
        {
            // Note: pb_CSG internally calls Unity API methods (like reading MeshFilters and Transforms)
            // which MUST run on the Main Thread. Therefore, we execute the pb_CSG operation synchronously
            // but return it as a Task to satisfy the async interface requirement and allow for future
            // background thread optimization if we switch to a job-based CSG system.
            
            Model result = null;

            try
            {
                switch (operationType)
                {
                    case CSGOperationType.Union:
                        result = Parabox.CSG.CSG.Union(objA, objB);
                        break;
                    case CSGOperationType.Intersection:
                        result = Parabox.CSG.CSG.Intersect(objA, objB);
                        break;
                    case CSGOperationType.Subtraction:
                        result = Parabox.CSG.CSG.Subtract(objA, objB);
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PbCSGProvider] CSG Operation failed: {e.Message}");
            }

            if (result != null && result.mesh != null)
            {
                return Task.FromResult(result.mesh);
            }
            else
            {
                Debug.LogWarning("[PbCSGProvider] CSG operation returned a null or empty mesh.");
                return Task.FromResult<Mesh>(null);
            }
        }
    }
}
