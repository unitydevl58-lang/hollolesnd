using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;

/// <summary>
/// MRGrabController — Direct XRI-integrated grab physics controller.
///
/// Native Strategy:
///   1. Hooks directly to selectEntered and selectExited events of all XRGrabInteractables in the scene.
///   2. On Grab: Set isKinematic = true, disable collisions/interactions by moving to 'Ignore Raycast' layer,
///      and smoothly interpolate position/rotation to the interactor's attachTransform.
///   3. On Release: Restore original isKinematic/gravity, restore original layer, and zero velocities.
///   4. Disables throwOnDetach dynamically so XRI doesn't apply extreme velocities.
/// </summary>
public class MRGrabController : MonoBehaviour
{
    public static MRGrabController Instance { get; private set; }

    [Header("G-Key Keyboard Grab Settings")]
    [SerializeField] private float readDistance = 0.8f;
    [SerializeField] private float lerpSpeed = 10f;
    [SerializeField] private float searchRadius = 5f;

    private HashSet<XRGrabInteractable> _hookedInteractables = new HashSet<XRGrabInteractable>();

    public static bool IsGrabbed(GameObject obj)
    {
        if (Instance == null || obj == null) return false;
        var interactable = obj.GetComponent<XRGrabInteractable>();
        if (interactable == null) return false;
        return Instance._activeGrabs.ContainsKey(interactable) || interactable.isSelected;
    }

    private class GrabState
    {
        public XRGrabInteractable Interactable;
        public Rigidbody Rb;
        public bool OriginalKinematic;
        public bool OriginalGravity;
        public int OriginalLayer;
        public bool OriginalThrowOnDetach;
        public Coroutine LerpRoutine;
        public Dictionary<Collider, bool> OriginalTriggers = new Dictionary<Collider, bool>();
    }

    private Dictionary<XRGrabInteractable, GrabState> _activeGrabs = new Dictionary<XRGrabInteractable, GrabState>();
    private XRGrabInteractable _keyboardGrabbedInteractable;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private GameObject _currentVirtualTarget;
    private float _currentTargetDistance;

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Hold to Grab
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            TryGrabWithKeyboard();
        }
        
        // Release to Drop
        if (Keyboard.current.gKey.wasReleasedThisFrame)
        {
            ReleaseKeyboardGrab();
        }

        // Interactive Push/Pull with Mouse Scroll Wheel while holding
        if (_keyboardGrabbedInteractable != null && _currentVirtualTarget != null && Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Normalize scroll delta (usually +/- 120) to a smooth 0.2m step
                _currentTargetDistance += Mathf.Sign(scroll) * 0.2f;
                _currentTargetDistance = Mathf.Clamp(_currentTargetDistance, 0.4f, 15f);
                
                // Update the virtual target's depth smoothly
                _currentVirtualTarget.transform.localPosition = new Vector3(0, 0, _currentTargetDistance);
            }
        }
    }

    // Fallback for UI or external triggers (Toggle mode)
    public static void TriggerGrab()
    {
        if (Instance == null) return;
        if (Instance._keyboardGrabbedInteractable != null)
            Instance.ReleaseKeyboardGrab();
        else
            Instance.TryGrabWithKeyboard();
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region Interactive Keyboard Grab

    private void TryGrabWithKeyboard()
    {
        if (_keyboardGrabbedInteractable != null) return; // Already holding something

        Camera cam = Camera.main;
        if (cam == null) return;

        Collider[] hits = Physics.OverlapSphere(cam.transform.position, searchRadius);
        XRGrabInteractable bestInteractable = null;
        float bestDist = float.MaxValue;

        foreach (var col in hits)
        {
            if (col.isTrigger) continue;
            if (col.GetComponentInParent<Canvas>() != null) continue;

            var interactable = col.GetComponentInParent<XRGrabInteractable>() ?? col.GetComponent<XRGrabInteractable>();
            if (interactable != null && !IsGrabbed(interactable.gameObject))
            {
                Vector3 dirToObj = (interactable.transform.position - cam.transform.position).normalized;
                float dot = Vector3.Dot(cam.transform.forward, dirToObj);
                if (dot > 0.5f) // Must be somewhat in front
                {
                    float dist = Vector3.Distance(cam.transform.position, col.transform.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestInteractable = interactable;
                    }
                }
            }
        }

        if (bestInteractable != null)
        {
            _keyboardGrabbedInteractable = bestInteractable;
            
            // Dynamic safe offset calculation
            Bounds b = GetBounds(bestInteractable.gameObject);
            float extentsSize = b.extents.magnitude;
            _currentTargetDistance = Mathf.Max(readDistance, extentsSize + 0.4f);

            // Create a virtual target relative to camera forward
            _currentVirtualTarget = new GameObject("KeyboardGrabTargetHelper");
            _currentVirtualTarget.transform.SetParent(cam.transform);
            _currentVirtualTarget.transform.localPosition = new Vector3(0, 0, _currentTargetDistance);
            _currentVirtualTarget.transform.localRotation = Quaternion.identity;

            StartGrabTracking(_keyboardGrabbedInteractable, _currentVirtualTarget.transform);
        }
    }

    private void ReleaseKeyboardGrab()
    {
        if (_keyboardGrabbedInteractable != null)
        {
            var interactable = _keyboardGrabbedInteractable;
            _keyboardGrabbedInteractable = null;
            StopGrabTracking(interactable);
        }
        
        if (_currentVirtualTarget != null)
        {
            Destroy(_currentVirtualTarget);
            _currentVirtualTarget = null;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Physics Tracking Logic

    private void StartGrabTracking(XRGrabInteractable interactable, Transform attachTarget)
    {
        if (_activeGrabs.ContainsKey(interactable)) return;

        var rb = interactable.GetComponent<Rigidbody>();
        if (rb == null) return;

        var state = new GrabState
        {
            Interactable = interactable,
            Rb = rb,
            OriginalKinematic = rb.isKinematic,
            OriginalGravity = rb.useGravity,
            OriginalLayer = interactable.gameObject.layer,
            OriginalThrowOnDetach = interactable.throwOnDetach
        };

        // 1. Move to 'Ignore Raycast' layer (layer 2) to disable raycast hits and physical collision clashing
        interactable.gameObject.layer = 2;
        foreach (Transform child in interactable.transform)
        {
            child.gameObject.layer = 2;
        }

        // 2. Clear velocities BEFORE setting isKinematic to avoid Unity warnings
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // 3. Prevent XRI from throwing on release
        interactable.throwOnDetach = false;

        // 4. Set all colliders to trigger to prevent bulldozing nearby objects during pull
        var colliders = interactable.GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (c != null)
            {
                state.OriginalTriggers[c] = c.isTrigger;
                c.isTrigger = true;
            }
        }

        // 5. Smoothly override position/rotation via Lerp
        state.LerpRoutine = StartCoroutine(LerpToTargetRoutine(interactable.gameObject, attachTarget));

        _activeGrabs[interactable] = state;
        Debug.Log($"[MRGrabController] Grab Tracking Started: {interactable.name}");
    }

    private void StopGrabTracking(XRGrabInteractable interactable)
    {
        if (!_activeGrabs.TryGetValue(interactable, out var state)) return;

        if (state.LerpRoutine != null)
        {
            StopCoroutine(state.LerpRoutine);
        }

        // 1. Restore original layer
        state.Interactable.gameObject.layer = state.OriginalLayer;
        foreach (Transform child in state.Interactable.transform)
        {
            if (child != null) child.gameObject.layer = state.OriginalLayer;
        }

        // 2. Restore physical states
        state.Rb.isKinematic = state.OriginalKinematic;
        state.Rb.useGravity = state.OriginalGravity;
        
        // Only set velocity if it is non-kinematic now
        if (!state.Rb.isKinematic)
        {
            state.Rb.linearVelocity = Vector3.zero;
            state.Rb.angularVelocity = Vector3.zero;
        }

        // 3. Restore throw setting
        state.Interactable.throwOnDetach = state.OriginalThrowOnDetach;

        // 4. Restore colliders trigger state
        foreach (var kvp in state.OriginalTriggers)
        {
            if (kvp.Key != null) kvp.Key.isTrigger = kvp.Value;
        }

        _activeGrabs.Remove(interactable);
        Debug.Log($"[MRGrabController] Grab Tracking Stopped: {interactable.name}");

        // 5. Force velocities to zero on next fixed update to prevent any residual launch forces
        StartCoroutine(ClearVelocityPostRelease(state.Rb));
    }

    private IEnumerator LerpToTargetRoutine(GameObject obj, Transform target)
    {
        while (obj != null && target != null)
        {
            obj.transform.position = Vector3.Lerp(obj.transform.position, target.position, Time.deltaTime * lerpSpeed);
            obj.transform.rotation = Quaternion.Slerp(obj.transform.rotation, target.rotation, Time.deltaTime * lerpSpeed);
            yield return null;
        }
    }

    private IEnumerator ClearVelocityPostRelease(Rigidbody rb)
    {
        yield return new WaitForFixedUpdate();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private static Bounds GetBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }

        Collider[] colliders = go.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    b.Encapsulate(colliders[i].bounds);
            }
            return b;
        }

        return new Bounds(go.transform.position, Vector3.one * 0.5f);
    }

    #endregion
}
