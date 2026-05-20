using UnityEngine;

namespace HoloLensApp.Sandbox
{
    public static class SandboxBaker
    {
        public static void BakeModel(GameObject rootObj)
        {
            if (rootObj == null) return;

            MeshFilter[] meshFilters = rootObj.GetComponentsInChildren<MeshFilter>();
            CombineInstance[] combine = new CombineInstance[meshFilters.Length];

            Matrix4x4 rootTransformInverse = rootObj.transform.worldToLocalMatrix;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                combine[i].mesh = meshFilters[i].sharedMesh;
                combine[i].transform = rootTransformInverse * meshFilters[i].transform.localToWorldMatrix;
                meshFilters[i].gameObject.SetActive(false); // Hide the old children
            }

            MeshFilter rootMeshFilter = rootObj.GetComponent<MeshFilter>();
            if (rootMeshFilter == null)
            {
                rootMeshFilter = rootObj.AddComponent<MeshFilter>();
            }

            MeshRenderer rootRenderer = rootObj.GetComponent<MeshRenderer>();
            if (rootRenderer == null)
            {
                rootRenderer = rootObj.AddComponent<MeshRenderer>();
                // In a robust implementation, we'd combine submeshes by material.
                // For this sandbox, we'll assign the first material found to the root.
                if (meshFilters.Length > 0 && meshFilters[0].GetComponent<Renderer>() != null)
                {
                    rootRenderer.sharedMaterial = meshFilters[0].GetComponent<Renderer>().sharedMaterial;
                }
            }

            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(combine, true, true);
            rootMeshFilter.sharedMesh = combinedMesh;

            MeshCollider rootCollider = rootObj.GetComponent<MeshCollider>();
            if (rootCollider == null)
            {
                var colliders = rootObj.GetComponentsInChildren<Collider>();
                foreach(var col in colliders) Object.Destroy(col);
                
                rootCollider = rootObj.AddComponent<MeshCollider>();
            }
            rootCollider.sharedMesh = combinedMesh;
            rootCollider.convex = true;

            rootObj.SetActive(true);

            // Destroy original children after baking to clean up hierarchy
            for (int i = rootObj.transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(rootObj.transform.GetChild(i).gameObject);
            }

            // Update materials logic for selection highlight
            SandboxObject sbObj = rootObj.GetComponent<SandboxObject>();
            if (sbObj != null) sbObj.UpdateOriginalMaterials();

            Debug.Log($"[SandboxBaker] Successfully baked {rootObj.name} into a single mesh.");
        }
    }
}
