using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Showcase
{
    /// <summary>
    /// Forces the grabbed object to instantly fly to 1 meter in front of the user's hand (XR Ray Interactor)
    /// so it doesn't stay far away and isn't affected by weird gravity/distance offsets.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class AutoPullGrab : MonoBehaviour
    {
        private XRGrabInteractable _grab;

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            if (_grab != null)
            {
                // Disabling dynamic attach ensures the object rigidly snaps to the interactor's attach point
                // rather than keeping the weird angle/distance it was grabbed at.
                _grab.useDynamicAttach = false;
                
                _grab.selectEntered.AddListener(OnGrabbed);
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            var rayInteractor = args.interactorObject as XRRayInteractor;
            if (rayInteractor != null)
            {
                // The XR Ray Interactor dynamically moves its attachTransform to the hit point (far away).
                // We override this immediately! We force the attach point to be exactly 1 meter in front of the controller.
                // Vector3.forward is the Z-axis (pointing out of the controller).
                rayInteractor.attachTransform.localPosition = new Vector3(0, 0, 1.0f);
                
                // Align the rotation perfectly with the hand so it looks natural when holding it.
                rayInteractor.attachTransform.localRotation = Quaternion.identity;
                
                Debug.Log($"[AutoPullGrab] Pulled {gameObject.name} to 1 meter in front of the hand!");
            }
        }
    }
}
