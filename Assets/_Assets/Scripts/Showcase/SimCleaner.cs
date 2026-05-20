using UnityEngine;

namespace Showcase
{
    /// <summary>
    /// Attach to the MR Interaction Setup to aggressively destroy the gray simulation room.
    /// </summary>
    public class SimCleaner : MonoBehaviour
    {
        private void Update()
        {
            GameObject env = GameObject.Find("DefaultSimulationEnvironment(Clone)");
            if (env != null) Destroy(env);

            GameObject room = GameObject.Find("Simulated Environment");
            if (room != null) Destroy(room);
        }
    }
}
