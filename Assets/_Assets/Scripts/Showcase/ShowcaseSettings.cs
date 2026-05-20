using UnityEngine;

namespace Showcase
{
    [CreateAssetMenu(fileName = "ShowcaseSettings", menuName = "Showcase/Settings")]
    public class ShowcaseSettings : ScriptableObject
    {
        [Header("Grid Configuration")]
        [Tooltip("Number of buildings along the X axis.")]
        public int GridWidth = 20;
        
        [Tooltip("Number of buildings along the Z axis.")]
        public int GridDepth = 20;
        
        [Tooltip("Size of each building cell.")]
        public float CellSize = 1.0f;
        
        [Tooltip("Gap between buildings.")]
        public float Spacing = 0.2f;

        [Header("Height Parameters")]
        public float MinHeight = 0.2f;
        public float MaxHeight = 4.0f;
        public float PerlinScale = 0.15f;
        
        [Header("Materials")]
        [Tooltip("Base material to use for the buildings. A dynamic texture will be applied to this.")]
        public Material BaseMaterial;

        [Header("Radial Monument Parameters")]
        public float RadialRadius = 25f;
        public int RadialCount = 12;
        public float RadialMonumentHeight = 10f;
        public float RadialMonumentWidth = 3f;
        public float RadialMonumentDepth = 2f;
        
        [Header("Radial Animation Parameters")]
        public float RadialRotationSpeed = 10f;
        public float RadialScaleAmplitude = 0.2f;
        public float RadialScalePulseSpeed = 2f;

        [Header("Deconstruction Parameters")]
        public int DeconstructionStages = 5;
        public float DeconstructionStageSpacing = 12f;
        public float DeconstructionErosionIntensity = 2f;
        
        [Header("Deconstruction Animation Parameters")]
        public float DeconFloatSpeed = 0.5f;
        public float DeconFloatAmplitude = 0.3f;
        public float DeconRotateSpeed = 15f;
        public float DeconPulseSpeed = 1.5f;
        public float DeconPulseAmplitude = 0.15f;
    }
}
