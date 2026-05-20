using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;

namespace HoloLensApp.Sandbox
{
    public class BlueprintManager : MonoBehaviour
    {
        public static BlueprintManager Instance { get; private set; }

        [Header("Blueprint Prefabs")]
        public GameObject doghousePrefab;
        public GameObject tablePrefab;

        [Header("Reward Systems")]
        public ParticleSystem confettiParticles;
        public AudioClip successSound;
        private AudioSource audioSource;

        [Header("Animation Settings")]
        public GameObject floatingTextPrefab; // Prefab with TextMeshPro
        public Material pastelMaterial;

        private GameObject currentBlueprint;
        private string currentBlueprintType;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        public void SpawnBlueprint(string type)
        {
            if (currentBlueprint != null)
            {
                Destroy(currentBlueprint);
            }

            GameObject prefabToSpawn = null;
            if (type == "Doghouse" && doghousePrefab != null) prefabToSpawn = doghousePrefab;
            else if (type == "Table" && tablePrefab != null) prefabToSpawn = tablePrefab;

            if (prefabToSpawn != null)
            {
                currentBlueprint = Instantiate(prefabToSpawn, transform.position, Quaternion.identity);
                currentBlueprintType = type;
                MakeHolographic(currentBlueprint);
            }
            else
            {
                // Fallback procedural generation for demo purposes if prefabs missing
                currentBlueprint = new GameObject($"Blueprint_{type}");
                currentBlueprint.transform.position = transform.position;
                currentBlueprintType = type;
            }
            
            Debug.Log($"[BlueprintManager] Spawned {type} blueprint.");
        }

        private void MakeHolographic(GameObject obj)
        {
            // Apply a translucent glowing material to all renderers and disable colliders
            var renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                // Assuming we use a standard material and change alpha
                Material mat = r.material;
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                Color c = mat.color;
                c.a = 0.3f;
                mat.color = c;
            }

            var colliders = obj.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;
        }

        public void CheckAssemblySuccess(GameObject builtModel)
        {
            if (currentBlueprint == null || builtModel == null) return;

            // Simple heuristic check: compare bounds volume
            Bounds builtBounds = GetTotalBounds(builtModel);
            Bounds targetBounds = GetTotalBounds(currentBlueprint);

            float volBuilt = builtBounds.size.x * builtBounds.size.y * builtBounds.size.z;
            float volTarget = targetBounds.size.x * targetBounds.size.y * targetBounds.size.z;

            // If volumes are within 20% margin
            if (Mathf.Abs(volBuilt - volTarget) / volTarget < 0.2f)
            {
                TriggerReward(builtModel);
            }
        }

        private Bounds GetTotalBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private void TriggerReward(GameObject builtModel)
        {
            Debug.Log("[BlueprintManager] Assembly Success!");
            
            if (confettiParticles != null)
            {
                confettiParticles.transform.position = builtModel.transform.position + Vector3.up;
                confettiParticles.Play();
            }

            if (successSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(successSound);
            }

            // Haptic feedback on controllers
            TriggerHaptics();

            // Destroy blueprint
            if (currentBlueprint != null)
            {
                Destroy(currentBlueprint);
            }
        }

        private void TriggerHaptics()
        {
            var devices = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.Controller, devices);
            foreach(var device in devices)
            {
                if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                {
                    device.SendHapticImpulse(0, 0.7f, 0.5f);
                }
            }
        }

        public void PlayAnimatedFormation()
        {
            if (currentBlueprintType == "Doghouse")
            {
                StartCoroutine(DoghouseAnimationRoutine());
            }
            else if (currentBlueprintType == "Table")
            {
                StartCoroutine(TableAnimationRoutine());
            }
        }

        private GameObject CreatePrimitiveVisual(PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.transform.position = pos;
            obj.transform.localScale = scale;
            Destroy(obj.GetComponent<Collider>());
            if (mat != null) obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj;
        }

        private IEnumerator SmoothMove(GameObject obj, Vector3 targetPos, float duration)
        {
            Vector3 startPos = obj.transform.position;
            float elapsed = 0;
            while(elapsed < duration)
            {
                if (obj == null) yield break;
                obj.transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0, 1, elapsed / duration));
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (obj != null) obj.transform.position = targetPos;
        }

        private void ShowFloatingText(string text, Vector3 position)
        {
            if (floatingTextPrefab == null)
            {
                Debug.Log($"[FloatingText]: {text}");
                return;
            }

            GameObject fText = Instantiate(floatingTextPrefab, position, Quaternion.identity);
            var tmp = fText.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = text;
            
            // Auto destroy after 3 seconds
            Destroy(fText, 3f);
        }

        private IEnumerator DoghouseAnimationRoutine()
        {
            if (currentBlueprint != null) currentBlueprint.SetActive(false);

            Vector3 basePos = transform.position;

            // Proper proportions and initial physics drop
            GameObject body = CreatePrimitiveVisual(PrimitiveType.Cube, basePos + new Vector3(0, 3f, 0), new Vector3(1, 1, 1), pastelMaterial);
            GameObject roof = CreatePrimitiveVisual(PrimitiveType.Cube, basePos + new Vector3(0, 5f, 0), new Vector3(1.2f, 0.5f, 1.2f), pastelMaterial);
            
            Rigidbody rbBody = body.AddComponent<Rigidbody>();
            Rigidbody rbRoof = roof.AddComponent<Rigidbody>();
            
            // Add colliders temporarily for the physical drop
            var bcBody = body.AddComponent<BoxCollider>();
            var bcRoof = roof.AddComponent<BoxCollider>();

            ShowFloatingText("1. Parçaların Fiziksel Düşüşü", basePos + new Vector3(0, 2f, 0));
            
            // Let them fall and settle for 2.5 seconds
            yield return new WaitForSeconds(2.5f);
            
            // Lock them for the boolean operations
            Destroy(rbBody);
            Destroy(rbRoof);
            Destroy(bcBody);
            Destroy(bcRoof);

            // Re-align body nicely on ground
            yield return StartCoroutine(SmoothMove(body, basePos + new Vector3(0, 0.5f, 0), 1.0f));

            // Step 1: Roof Union
            ShowFloatingText("2. Birleşme (Union)", basePos + new Vector3(0, 2f, 0));
            yield return StartCoroutine(SmoothMove(roof, body.transform.position + new Vector3(0, 0.75f, 0), 2.0f));
            
            // Wait to let user observe
            yield return new WaitForSeconds(1.5f);

            // Step 2: Door Subtraction
            GameObject door = CreatePrimitiveVisual(PrimitiveType.Cube, body.transform.position + new Vector3(0, -0.2f, -1.5f), new Vector3(0.4f, 0.6f, 0.6f), pastelMaterial);
            
            ShowFloatingText("3. Eksilme (Subtraction)", basePos + new Vector3(0, 2f, 0));
            yield return StartCoroutine(SmoothMove(door, body.transform.position + new Vector3(0, -0.2f, -0.5f), 2.0f));
            
            // Wait to let user observe intersection
            yield return new WaitForSeconds(1.5f);
            
            Destroy(door);
            Destroy(body);
            Destroy(roof);
            
            if (currentBlueprint != null)
            {
                currentBlueprint.SetActive(true);
                currentBlueprint.transform.position = basePos + new Vector3(0, 0.5f, 0); // Ensure proper height
                // Pulse animation
                currentBlueprint.transform.localScale = Vector3.one * 1.1f;
                yield return new WaitForSeconds(0.2f);
                currentBlueprint.transform.localScale = Vector3.one;

                SolidifyBlueprint(currentBlueprint);
                currentBlueprint = null; // Detach so user can spawn again
            }
        }

        private IEnumerator TableAnimationRoutine()
        {
            // Disassemble visually
            if (currentBlueprint != null) currentBlueprint.SetActive(false);

            Vector3 basePos = transform.position;

            GameObject top = CreatePrimitiveVisual(PrimitiveType.Cube, basePos + new Vector3(0, 1.5f, 0), new Vector3(1.5f, 0.1f, 1f), pastelMaterial);
            GameObject[] legs = new GameObject[4];
            for (int i=0; i<4; i++)
            {
                legs[i] = CreatePrimitiveVisual(PrimitiveType.Cylinder, basePos + new Vector3(-1f + i*0.5f, 1f, 1f), new Vector3(0.1f, 0.5f, 0.1f), pastelMaterial);
            }

            yield return new WaitForSeconds(1f);

            ShowFloatingText("1. Çoklu Birleşme (Multiple Union)", basePos + new Vector3(0, 1f, 0));
            
            Coroutine[] moves = new Coroutine[4];
            moves[0] = StartCoroutine(SmoothMove(legs[0], basePos + new Vector3(-0.6f, -0.45f, -0.4f), 1.5f));
            moves[1] = StartCoroutine(SmoothMove(legs[1], basePos + new Vector3(0.6f, -0.45f, -0.4f), 1.5f));
            moves[2] = StartCoroutine(SmoothMove(legs[2], basePos + new Vector3(-0.6f, -0.45f, 0.4f), 1.5f));
            moves[3] = StartCoroutine(SmoothMove(legs[3], basePos + new Vector3(0.6f, -0.45f, 0.4f), 1.5f));
            yield return StartCoroutine(SmoothMove(top, basePos + new Vector3(0, 0, 0), 1.5f));

            yield return new WaitForSeconds(0.5f);

            Destroy(top);
            foreach(var l in legs) Destroy(l);

            if (currentBlueprint != null)
            {
                currentBlueprint.SetActive(true);
                
                SolidifyBlueprint(currentBlueprint);
                currentBlueprint = null; // Detach from blueprint manager
            }
        }

        private void SolidifyBlueprint(GameObject obj)
        {
            // Restore solid materials
            var renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (pastelMaterial != null) r.sharedMaterial = pastelMaterial;
                else
                {
                    Material mat = r.material;
                    mat.SetFloat("_Mode", 0); // Opaque
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = -1;
                    Color c = mat.color;
                    c.a = 1f;
                    mat.color = c;
                }
            }

            // Generate colliders
            var filters = obj.GetComponentsInChildren<MeshFilter>();
            foreach (var f in filters)
            {
                var mc = f.gameObject.GetComponent<MeshCollider>();
                if (mc == null) mc = f.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = f.sharedMesh;
                mc.convex = true;
            }

            // Attach Rigidbody & XR Interaction
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb == null) rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = false; // Make it physically simulated!
            rb.useGravity = true;
            rb.mass = 5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var grab = obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab == null) grab = obj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = true;
            grab.throwVelocityScale = 2.0f;

            Debug.Log($"[BlueprintManager] Blueprint Solidified and Grabbable: {obj.name}");
        }
    }
}
