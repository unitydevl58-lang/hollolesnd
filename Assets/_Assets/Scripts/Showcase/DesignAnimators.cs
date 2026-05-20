using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace ShowcaseAnimators
{
    public class RhythmAnimator : MonoBehaviour
    {
        public float frequency = 1f;
        public float amplitude = 1f;
        public float timeOffset = -1f; // Changed to public to allow external assignment
        private Vector3 _startPos;

        void Start()
        {
            _startPos = transform.localPosition;
            // Only randomize if it hasn't been manually assigned (e.g., from ShowcaseGenerator)
            if (timeOffset < 0f)
            {
                timeOffset = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        void Update()
        {
            if (MRGrabController.IsGrabbed(gameObject))
            {
                _startPos = transform.localPosition;
                return;
            }

            float yOffset = Mathf.Sin(Time.time * frequency + timeOffset) * amplitude;
            transform.localPosition = _startPos + new Vector3(0, yOffset, 0);
        }
    }

    public class RadialAnimator : MonoBehaviour
    {
        public float rotationSpeed = 10f;
        public float baseScaleAmplitude = 0.15f;
        
        private List<Transform> _children = new List<Transform>();
        private List<Vector3> _childBaseScales = new List<Vector3>();
        private List<float> _randomOffsets = new List<float>();

        void Start()
        {
            foreach (Transform child in transform)
            {
                _children.Add(child);
                _childBaseScales.Add(child.localScale);
                _randomOffsets.Add(Random.Range(0f, 100f));
            }
        }

        void Update()
        {
            // Gentle organic rotation (only if parent is not grabbed, though normally parent isn't interactable)
            if (!MRGrabController.IsGrabbed(gameObject))
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }
            
            // Asymmetrical breathing using Perlin Noise
            float time = Time.time;
            for (int i = 0; i < _children.Count; i++)
            {
                if (_children[i] != null)
                {
                    if (MRGrabController.IsGrabbed(_children[i].gameObject))
                    {
                        _childBaseScales[i] = _children[i].localScale;
                        continue;
                    }

                    float noise = Mathf.PerlinNoise(time * 0.5f, _randomOffsets[i]);
                    float scaleMultiplier = 1f + (noise - 0.5f) * baseScaleAmplitude;
                    
                    Vector3 targetScale = _childBaseScales[i] * scaleMultiplier;
                    _children[i].localScale = Vector3.Lerp(_children[i].localScale, targetScale, Time.deltaTime * 3f);
                }
            }
        }
    }

    public class DeconstructionAnimator : MonoBehaviour
    {
        public float floatSpeed = 0.5f;
        public float floatAmplitude = 0.3f;
        public float rotateSpeed = 15f;

        private struct VoxelData
        {
            public Transform Transform;
            public Vector3 BasePosition;
            public Vector3 BaseScale;
            public float NoiseOffsetX;
            public float NoiseOffsetY;
            public float NoiseOffsetZ;
            public Vector3 RotationAxis;
            public Vector3 CurrentVelocity;
        }

        private List<VoxelData> _voxels = new List<VoxelData>();

        void Start()
        {
            foreach (Transform child in transform)
            {
                _voxels.Add(new VoxelData
                {
                    Transform = child,
                    BasePosition = child.localPosition,
                    BaseScale = child.localScale,
                    NoiseOffsetX = Random.Range(0f, 100f),
                    NoiseOffsetY = Random.Range(0f, 100f),
                    NoiseOffsetZ = Random.Range(0f, 100f),
                    RotationAxis = Random.onUnitSphere
                });
            }
        }

        void Update()
        {
            float time = Time.time * floatSpeed;
            for (int i = 0; i < _voxels.Count; i++)
            {
                var v = _voxels[i];
                if (v.Transform == null) continue;

                if (MRGrabController.IsGrabbed(v.Transform.gameObject))
                {
                    v.BasePosition = v.Transform.localPosition;
                    _voxels[i] = v;
                    continue;
                }

                // 3D Perlin Noise floating
                float xOffset = (Mathf.PerlinNoise(time, v.NoiseOffsetX) - 0.5f) * floatAmplitude;
                float yOffset = (Mathf.PerlinNoise(time, v.NoiseOffsetY) - 0.5f) * floatAmplitude;
                float zOffset = (Mathf.PerlinNoise(time, v.NoiseOffsetZ) - 0.5f) * floatAmplitude;
                
                Vector3 targetPos = v.BasePosition + new Vector3(xOffset, yOffset, zOffset);

                // Tactile Disruption (Magnetic Repulsion)
                foreach (var hand in Showcase.HandDisruptionField.ActiveHands)
                {
                    if (hand == null) continue;
                    
                    // Convert hand position to the local space of the animator
                    Vector3 localHandPos = transform.InverseTransformPoint(hand.transform.position);
                    float distance = Vector3.Distance(localHandPos, v.Transform.localPosition);
                    
                    if (distance < hand.DisruptionRadius)
                    {
                        // Repel outwards from the hand
                        Vector3 repelDir = (v.Transform.localPosition - localHandPos).normalized;
                        if (repelDir == Vector3.zero) repelDir = Random.onUnitSphere;
                        
                        // Force is stronger the closer the hand is
                        float force = (1f - (distance / hand.DisruptionRadius)) * hand.RepulsionForce;
                        targetPos += repelDir * force;
                    }
                }

                v.Transform.localPosition = Vector3.SmoothDamp(v.Transform.localPosition, targetPos, ref v.CurrentVelocity, 0.5f);

                // Peaceful slow rotation organically scaled by noise
                float rotNoise = Mathf.PerlinNoise(time * 0.2f, v.NoiseOffsetX);
                v.Transform.Rotate(v.RotationAxis, rotateSpeed * rotNoise * Time.deltaTime, Space.Self);

                _voxels[i] = v; // Update struct back in list
            }
        }
    }

    public class ContrastAnimator : MonoBehaviour
    {
        public float floatSpeed = 0.5f;
        public float floatAmplitude = 0.5f;
        private Vector3 _startPos;

        void Start()
        {
            _startPos = transform.localPosition;
        }

        void Update()
        {
            if (MRGrabController.IsGrabbed(gameObject))
            {
                _startPos = transform.localPosition;
                return;
            }

            // Slow, dominant floating
            float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.localPosition = _startPos + new Vector3(0, yOffset, 0);
        }
    }
}
