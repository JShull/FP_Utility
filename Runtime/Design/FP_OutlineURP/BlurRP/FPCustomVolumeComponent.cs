namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// This code was originally based on the works of the Unity-Technologies GitHub 'Per-Object_Outline_RenderGraph_RenderFeature_Example'
    /// https://github.com/Unity-Technologies/Per-Object_Outline_RenderGraph_RendererFeature_Example
    /// This has been modified for additional usage associated with FuzzPhyte Packages
    /// </summary>
    [Serializable]
    public class FPCustomVolumeComponent : VolumeComponent
    {
        [Header("General")]
        public BoolParameter isActive = new BoolParameter(true);

        [Header("Blur (existing)")]
        public ClampedFloatParameter horizontalBlur =
            new ClampedFloatParameter(0.05f, 0f, 0.5f);

        public ClampedFloatParameter verticalBlur =
            new ClampedFloatParameter(0.05f, 0f, 0.5f);

        [Header("Outline (pixels)")]
        // Solid outline band thickness in pixels
        public ClampedIntParameter outlineThicknessPx =
            new ClampedIntParameter(2, 0, 64);

        // Soft falloff after thickness, in pixels
        public ClampedIntParameter outlineBlurPx =
            new ClampedIntParameter(2, 0, 64);

        [Header("Advanced")]
        // If true, your pass can set _MaxRadius = outlineThicknessPx + outlineBlurPx
        // If false, use outlineMaxRadiusPx directly.
        public BoolParameter outlineAutoMaxRadius =
            new BoolParameter(true);

        // Only used if outlineAutoMaxRadius == false
        public ClampedIntParameter outlineMaxRadiusPx =
            new ClampedIntParameter(4, 1, 128);
    }
}
