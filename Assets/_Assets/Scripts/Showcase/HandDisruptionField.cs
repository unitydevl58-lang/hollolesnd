#pragma warning disable 0618 // Ignore XRBaseController obsolete warning for XRI 3.0 compatibility
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;

namespace Showcase
{
    public class HandDisruptionField : MonoBehaviour
    {
        public static List<HandDisruptionField> ActiveHands = new List<HandDisruptionField>();

        public float DisruptionRadius = 0.3f;
        public float RepulsionForce = 1.5f;

        [Header("Haptics")]
        public XRBaseController xrController;
        public float hapticAmplitude = 0.2f;
        public float hapticDuration = 0.1f;

        private void OnEnable()
        {
            if (!ActiveHands.Contains(this))
            {
                ActiveHands.Add(this);
            }
            if (xrController == null)
            {
                xrController = GetComponent<XRBaseController>();
            }
        }

        private void OnDisable()
        {
            if (ActiveHands.Contains(this))
            {
                ActiveHands.Remove(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // If we touch an architectural mesh (cubes, cylinders, etc. with Renderers)
            if (other.GetComponent<MeshRenderer>() != null && xrController != null)
            {
                xrController.SendHapticImpulse(hapticAmplitude, hapticDuration);
            }
        }
    }
}
