using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using HoloLensApp.Interaction.CSG;
using HoloLensApp.Interaction.Snapping;

namespace HoloLensApp.Interaction.Math
{
    /// <summary>
    /// Central Wong state machine + AABB volumetrics + runtime CSG dispatch.
    /// Touching: bounds contact with zero volumetric overlap. Penetration: positive overlap volume.
    /// </summary>
    public class ShapeInteractionManager : MonoBehaviour
    {
        public static ShapeInteractionManager Instance { get; private set; }

        public event Action<GameObject, GameObject, WongInteractionState> InteractionStateChanged;

        [Header("Detection")]
        [SerializeField] private float evaluationIntervalSeconds = 0.25f;
        [SerializeField] private bool autoCreateIntersectionOnPenetration = true;
        [SerializeField] private float minAutoIntersectionVolume = 0.0005f;
        [SerializeField] private float autoIntersectionCooldownSeconds = 0.8f;

        [Header("References")]
        [SerializeField] private CSGFormManager csgFormManager;

        private readonly List<FormInteractable> _trackedForms = new List<FormInteractable>(32);
        private readonly Dictionary<ulong, WongInteractionState> _pairStates = new Dictionary<ulong, WongInteractionState>(64);
        private readonly Dictionary<ulong, float> _lastAutoIntersectionTimes = new Dictionary<ulong, float>(64);
        private float _nextEvaluationTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (csgFormManager == null)
                csgFormManager = CSGFormManager.Instance ?? FindAnyObjectByType<CSGFormManager>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable()
        {
            RefreshTrackedForms();
        }

        /// <summary>
        /// Periodic evaluation — not every frame (HoloLens-friendly).
        /// </summary>
        private void Update()
        {
            if (Time.unscaledTime < _nextEvaluationTime)
                return;

            _nextEvaluationTime = Time.unscaledTime + evaluationIntervalSeconds;
            EvaluateAllTrackedPairs();
        }

        public void RegisterForm(FormInteractable form)
        {
            if (form == null || _trackedForms.Contains(form))
                return;

            _trackedForms.Add(form);
        }

        public void UnregisterForm(FormInteractable form)
        {
            if (form == null)
                return;

            _trackedForms.Remove(form);
        }

        public void RefreshTrackedForms()
        {
            _trackedForms.Clear();
            FormInteractable[] found = FindObjectsByType<FormInteractable>();
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    _trackedForms.Add(found[i]);
            }
        }

        public void EvaluateAllTrackedPairs()
        {
            int count = _trackedForms.Count;
            for (int i = 0; i < count; i++)
            {
                FormInteractable a = _trackedForms[i];
                if (a == null)
                    continue;

                for (int j = i + 1; j < count; j++)
                {
                    FormInteractable b = _trackedForms[j];
                    if (b == null)
                        continue;

                    WongInteractionState state = EvaluatePair(a.gameObject, b.gameObject);
                    ulong key = PairKey(RuntimeHelpers.GetHashCode(a), RuntimeHelpers.GetHashCode(b));

                    if (!_pairStates.TryGetValue(key, out WongInteractionState previous) || previous != state)
                    {
                        _pairStates[key] = state;
                        ApplyVisualState(a, b, state);
                        InteractionStateChanged?.Invoke(a.gameObject, b.gameObject, state);

                        if (autoCreateIntersectionOnPenetration && state == WongInteractionState.Penetration)
                            TryCreateRuntimeIntersection(a.gameObject, b.gameObject, key);
                    }
                }
            }
        }

        public WongInteractionState EvaluatePair(GameObject objA, GameObject objB)
        {
            return EvaluatePairStatic(objA, objB);
        }

        public static WongInteractionState EvaluatePairStatic(GameObject objA, GameObject objB)
        {
            if (objA == null || objB == null)
                return WongInteractionState.Unknown;

            Bounds boundsA = GetWorldBounds(objA);
            Bounds boundsB = GetWorldBounds(objB);

            return ClassifyWongState(boundsA, boundsB);
        }

        public static WongInteractionState ClassifyWongState(Bounds a, Bounds b)
        {
            FormalInteractionState formalState = WongMathUtility.DetectInteractionState(a, b);
            switch (formalState)
            {
                case FormalInteractionState.Coinciding:
                    return WongInteractionState.Coinciding;
                case FormalInteractionState.Encapsulation:
                case FormalInteractionState.Penetration:
                    return WongInteractionState.Penetration;
                case FormalInteractionState.Touching:
                    return WongInteractionState.Touching;
                case FormalInteractionState.Detachment:
                default:
                    return WongInteractionState.Detachment;
            }
        }

        public static bool BoundsAreTouching(Bounds a, Bounds b)
        {
            return WongMathUtility.DetectInteractionState(a, b, MathUtilities.Epsilon) == FormalInteractionState.Touching;
        }

        public static float CalculateUnionVolume(Bounds a, Bounds b)
        {
            return WongMathUtility.CalculateUnionVolume(a, b);
        }

        public static float CalculateIntersectionVolume(Bounds a, Bounds b)
        {
            return WongMathUtility.CalculateIntersectionVolume(a, b);
        }

        public static float CalculateSubtractionVolume(Bounds main, Bounds subtractor)
        {
            return WongMathUtility.CalculateSubtractionVolume(main, subtractor);
        }

        public void RequestCSG(GameObject objA, GameObject objB, CSGOperationType operation)
        {
            if (csgFormManager == null)
            {
                Debug.LogError("[ShapeInteractionManager] CSGFormManager not assigned.");
                return;
            }

            csgFormManager.ProcessCSGOperation(objA, objB, operation, destroyOperands: true);
        }

        public void RequestCSGForWongState(GameObject objA, GameObject objB, WongInteractionState state)
        {
            switch (state)
            {
                case WongInteractionState.Union:
                    RequestCSG(objA, objB, CSGOperationType.Union);
                    break;
                case WongInteractionState.Intersection:
                    RequestCSG(objA, objB, CSGOperationType.Intersection);
                    break;
                case WongInteractionState.Subtraction:
                    RequestCSG(objA, objB, CSGOperationType.Subtraction);
                    break;
                default:
                    Debug.LogWarning($"[ShapeInteractionManager] State '{state}' does not map to mesh CSG.");
                    break;
            }
        }

        private void ApplyVisualState(FormInteractable a, FormInteractable b, WongInteractionState state)
        {
            a?.ApplyWongHighlight(state);
            b?.ApplyWongHighlight(state);
        }

        private void TryCreateRuntimeIntersection(GameObject objA, GameObject objB, ulong pairKey)
        {
            if (objA == null || objB == null)
                return;

            if (IsGeneratedIntersectionResult(objA) || IsGeneratedIntersectionResult(objB))
                return;

            if (_lastAutoIntersectionTimes.TryGetValue(pairKey, out float lastTime) &&
                Time.unscaledTime - lastTime < autoIntersectionCooldownSeconds)
            {
                return;
            }

            Bounds boundsA = GetWorldBounds(objA);
            Bounds boundsB = GetWorldBounds(objB);
            if (WongMathUtility.CalculateIntersectionVolume(boundsA, boundsB) < minAutoIntersectionVolume)
                return;

            GeometryManager geometryManager = FindAnyObjectByType<GeometryManager>();
            if (geometryManager == null)
                geometryManager = new GameObject("GeometryManager_Dynamic").AddComponent<GeometryManager>();

            if (geometryManager.CreateIntersectionResultFromOverlap(objA, objB))
                _lastAutoIntersectionTimes[pairKey] = Time.unscaledTime;
        }

        public static Bounds GetWorldBounds(GameObject obj)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (hasBounds)
                return bounds;

            return new Bounds(obj.transform.position, Vector3.one * 0.1f);
        }

        private static bool IsGeneratedIntersectionResult(GameObject obj)
        {
            return obj != null && obj.name.StartsWith("CSG_Intersection", StringComparison.Ordinal);
        }

        private static ulong PairKey(int idA, int idB)
        {
            int low = idA < idB ? idA : idB;
            int high = idA < idB ? idB : idA;
            return ((ulong)(uint)low << 32) | (uint)high;
        }
    }
}
