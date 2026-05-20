using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Controls the pacing of generated procedural showcases for MR (HoloLens).
/// Spawns dioramas organically in front of the user instead of trapping the camera.
/// </summary>
public class ShowcaseDirector : MonoBehaviour
{
    private Camera _mainCamera;
    private Coroutine _exhibitionCoroutine;
    
    // Safety reference to the current showcase root
    private GameObject _currentRoot;
    
    // Spatial Diorama Anchor
    private Transform _showcaseAnchor;

    private IEnumerator Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("[ShowcaseDirector] Main Camera not found.");
        }

        yield return null; // Wait 1 frame for Generators and Camera to fully initialize

        SpawnAbstractCity();
    }

    private void EnsureAnchorExists()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        if (_showcaseAnchor == null)
        {
            GameObject anchorObj = new GameObject("ShowcaseAnchor_Diorama");
            _showcaseAnchor = anchorObj.transform;
            _showcaseAnchor.SetParent(this.transform);

            // Add kinematic Rigidbody for compound colliders
            Rigidbody rb = anchorObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Add XRGrabInteractable for spatial manipulation
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = anchorObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
            // Optionally, we could set trackPosition and trackRotation to true, which is default.
        }

        // Position the anchor exactly 1.5 meters in front of the user's current gaze, slightly lower
        Vector3 gazeForward = _mainCamera.transform.forward;
        gazeForward.y = 0; // Keep the diorama level with the ground
        gazeForward.Normalize();

        Vector3 spawnPos = _mainCamera.transform.position + (gazeForward * 1.5f) + (Vector3.down * 0.4f);
        _showcaseAnchor.position = spawnPos;

        // Rotate to face the user
        _showcaseAnchor.rotation = Quaternion.LookRotation(_mainCamera.transform.position - spawnPos, Vector3.up);

        // Scale down the giant procedural cities into a 2-meter tabletop diorama
        _showcaseAnchor.localScale = Vector3.one * 0.05f;
    }

    /// <summary>
    /// Legacy entry point for isolated showcase triggers (from GeminiConnection)
    /// </summary>
    public void AddScene(Transform sceneRoot)
    {
        if (_exhibitionCoroutine != null)
        {
            StopCoroutine(_exhibitionCoroutine);
            _exhibitionCoroutine = null;
        }

        EnsureAnchorExists();
        
        // Attach the new scene to our spatial anchor and reset its local transform
        _currentRoot = sceneRoot.gameObject;
        _currentRoot.transform.SetParent(_showcaseAnchor, false);
        _currentRoot.transform.localPosition = Vector3.zero;
        _currentRoot.transform.localRotation = Quaternion.identity;
    }

    private void ClearCurrentScene()
    {
        // First check for our explicit root reference
        if (_currentRoot != null)
        {
            Destroy(_currentRoot);
            _currentRoot = null;
        }

        // Also clean up globally by name to be completely safe and avoid stacking
        var names = new string[] { "Abstract City", "Radial Balance", "Deconstruction Continuity", "AbstractCity_Root", "Deconstruction_Root", "RadialBalance_Root" };
        foreach(var name in names)
        {
            var go = GameObject.Find(name);
            if (go != null) Destroy(go);
        }
    }

    public void SpawnAbstractCity()
    {
        EnsureAnchorExists();
        ClearCurrentScene();
        ShowcaseGenerator.Instance.GenerateAbstractCity(_showcaseAnchor);
        if (_showcaseAnchor.childCount > 0)
        {
            _currentRoot = _showcaseAnchor.GetChild(_showcaseAnchor.childCount - 1).gameObject;
            _currentRoot.transform.localPosition = Vector3.zero;
        }
    }

    public void SpawnRadialMonument()
    {
        EnsureAnchorExists();
        ClearCurrentScene();
        ShowcaseGenerator.Instance.GenerateRadialBalance(_showcaseAnchor);
        if (_showcaseAnchor.childCount > 0)
        {
            _currentRoot = _showcaseAnchor.GetChild(_showcaseAnchor.childCount - 1).gameObject;
            _currentRoot.transform.localPosition = Vector3.zero;
        }
    }

    public void SpawnDeconstruction()
    {
        EnsureAnchorExists();
        ClearCurrentScene();
        ShowcaseGenerator.Instance.GenerateDeconstruction(_showcaseAnchor);
        if (_showcaseAnchor.childCount > 0)
        {
            _currentRoot = _showcaseAnchor.GetChild(_showcaseAnchor.childCount - 1).gameObject;
            _currentRoot.transform.localPosition = Vector3.zero;
        }
    }
}
