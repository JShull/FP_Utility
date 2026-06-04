// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using UnityEngine;

    /// <summary>
    /// Shared outline style used by FPOutlineTarget and batched by the renderer feature.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FPOutlineProfile",
        menuName = "FuzzPhyte/Utility/Outline Profile")]
    public class FPOutlineProfile : ScriptableObject
    {
        [SerializeField, ColorUsage(false, true)] private Color outlineColor = Color.cyan;
        [SerializeField] private FPOutlineAlphaMode alphaMode = FPOutlineAlphaMode.MeshSilhouette;
        [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.5f;
        [SerializeField] private Texture customMaskTexture;
        [SerializeField, Min(0)] private int thickness = 5;
        [SerializeField, Min(0)] private int blur = 2;
        [SerializeField] private bool autoMaxRadius = true;
        [SerializeField, Range(1, 128)] private int maxRadius = 50;

        public Color OutlineColor => outlineColor;
        public FPOutlineAlphaMode AlphaMode => alphaMode;
        public float AlphaCutoff => alphaCutoff;
        public Texture CustomMaskTexture => customMaskTexture;
        public int Thickness => thickness;
        public int Blur => blur;
        public bool AutoMaxRadius => autoMaxRadius;
        public int MaxRadius => maxRadius;

        private void OnValidate()
        {
            alphaCutoff = Mathf.Clamp01(alphaCutoff);
            thickness = Mathf.Max(0, thickness);
            blur = Mathf.Max(0, blur);
            maxRadius = Mathf.Max(1, maxRadius);
        }
    }
}
