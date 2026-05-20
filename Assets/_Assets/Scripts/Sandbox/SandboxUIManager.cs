using UnityEngine;
using TMPro;
using UnityEngine.UI;


namespace HoloLensApp.Sandbox
{
    public class SandboxUIManager : MonoBehaviour
    {
        public static SandboxUIManager Instance { get; private set; }

        [Header("Spawning Settings")]
        public Transform spawnPoint;
        public Material defaultPastelMaterial;



        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }



        public void OnRunCommand(string commandText)
        {
            if (SandboxEngine.Instance != null)
            {
                SandboxEngine.Instance.ProcessCommand(commandText);
            }
        }

        private GameObject CreateBaseInteractable(string name)
        {
            GameObject obj = new GameObject(name);
            obj.transform.position = spawnPoint != null ? spawnPoint.position : Vector3.zero;

            var rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;

            return obj;
        }

        public void SpawnPrimitive(PrimitiveType type)
        {
            GameObject parent = CreateBaseInteractable(type.ToString());
            
            GameObject meshObj = GameObject.CreatePrimitive(type);
            meshObj.transform.SetParent(parent.transform, false);
            
            // Move collider to parent
            Collider childCol = meshObj.GetComponent<Collider>();
            if (childCol != null)
            {
                if (childCol is BoxCollider bc)
                {
                    var pc = parent.AddComponent<BoxCollider>();
                    pc.center = bc.center; pc.size = bc.size;
                }
                else if (childCol is SphereCollider sc)
                {
                    var pc = parent.AddComponent<SphereCollider>();
                    pc.center = sc.center; pc.radius = sc.radius;
                }
                else if (childCol is CapsuleCollider cc)
                {
                    var pc = parent.AddComponent<CapsuleCollider>();
                    pc.center = cc.center; pc.radius = cc.radius; pc.height = cc.height; pc.direction = cc.direction;
                }
                Destroy(childCol);
            }

            if (defaultPastelMaterial != null)
            {
                meshObj.GetComponent<MeshRenderer>().sharedMaterial = defaultPastelMaterial;
            }
            else
            {
                Material vibrantMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                Color randColor = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(0.6f, 1f), 1f);
                vibrantMat.color = randColor;
                vibrantMat.EnableKeyword("_EMISSION");
                vibrantMat.SetColor("_EmissionColor", randColor * 1.5f);
                meshObj.GetComponent<MeshRenderer>().sharedMaterial = vibrantMat;
            }

            // ADD XRGRABINTERACTABLE AFTER COLLIDERS ARE SET
            var grab = parent.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = true;
            grab.throwVelocityScale = 2.0f;
            parent.AddComponent<SandboxObject>().UpdateOriginalMaterials();
        }

        public void SpawnPrism()
        {
            GameObject parent = CreateBaseInteractable("TriangularPrism");

            // Create a simple triangular prism mesh
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[] {
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0, 0.5f, 0.5f), // Front
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0, 0.5f, -0.5f) // Back
            };
            mesh.triangles = new int[] {
                0, 2, 1, // Front
                3, 4, 5, // Back
                0, 1, 4, 0, 4, 3, // Bottom
                1, 2, 5, 1, 5, 4, // Right
                2, 0, 3, 2, 3, 5  // Left
            };
            mesh.RecalculateNormals();

            GameObject meshObj = new GameObject("Mesh");
            meshObj.transform.SetParent(parent.transform, false);
            
            var filter = meshObj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            
            var renderer = meshObj.AddComponent<MeshRenderer>();
            if (defaultPastelMaterial != null) 
            {
                renderer.sharedMaterial = defaultPastelMaterial;
            }
            else
            {
                Material vibrantMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                Color randColor = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(0.6f, 1f), 1f);
                vibrantMat.color = randColor;
                vibrantMat.EnableKeyword("_EMISSION");
                vibrantMat.SetColor("_EmissionColor", randColor * 1.5f);
                renderer.sharedMaterial = vibrantMat;
            }

            var collider = parent.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;

            // ADD XRGRABINTERACTABLE AFTER COLLIDERS ARE SET
            var grab = parent.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = true;
            grab.throwVelocityScale = 2.0f;
            parent.AddComponent<SandboxObject>().UpdateOriginalMaterials();
        }
    }
}
