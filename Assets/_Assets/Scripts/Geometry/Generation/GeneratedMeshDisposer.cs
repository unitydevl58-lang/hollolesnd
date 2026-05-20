using UnityEngine;

/// <summary>
/// Owns procedural meshes so they are destroyed with their generated GameObject.
/// </summary>
public sealed class GeneratedMeshDisposer : MonoBehaviour
{
    private Mesh ownedMesh;

    public void Initialize(Mesh mesh)
    {
        ownedMesh = mesh;
    }

    private void OnDestroy()
    {
        if (ownedMesh == null)
            return;

        if (Application.isPlaying)
            Destroy(ownedMesh);
        else
            DestroyImmediate(ownedMesh);
    }
}
