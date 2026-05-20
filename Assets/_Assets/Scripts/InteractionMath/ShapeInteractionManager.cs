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

        [Header("References")]
        [SerializeField] private CSGFormManager csgFormManager;

        private readonly List<FormInteractable> _trackedForms = new List<FormInteractable>(32);
        private readonly Dictionary<ulong, WongInteractionState> _pairStates = new Dictionary<ulong, WongInteractionState>(64);
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

        public static Bounds GetWorldBounds(GameObject obj)
        {
            Renderer renderer = obj.GetComponentInChildren<Renderer>();
            if (renderer != null)
                return renderer.bounds;

            Collider collider = obj.GetComponentInChildren<Collider>();
            if (collider != null)
                return collider.bounds;

            return new Bounds(obj.transform.position, Vector3.one * 0.1f);
        }

        private static ulong PairKey(int idA, int idB)
        {
            int low = idA < idB ? idA : idB;
            int high = idA < idB ? idB : idA;
            return ((ulong)(uint)low << 32) | (uint)high;
        }
    }
}
