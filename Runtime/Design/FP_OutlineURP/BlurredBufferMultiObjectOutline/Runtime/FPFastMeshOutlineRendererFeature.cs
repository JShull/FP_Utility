namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// Fast mesh-based outline renderer feature. This is cheaper than the blurred
    /// screen-space outline because it redraws selected meshes instead of dilating a
    /// full-screen buffer.
    /// </summary>
    public class FPFastMeshOutlineRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;
        [Space, SerializeField] private Material outlineMaterial;
        [Header("Outline")]
        [SerializeField, Min(0)] private int thickness = 5;
        [SerializeField, ColorUsage(false, true)] private Color defaultOutlineColor = Color.cyan;
        [SerializeField] private FPOutlineAlphaMode defaultAlphaMode = FPOutlineAlphaMode.MeshSilhouette;
        [SerializeField, Range(0f, 1f)] private float defaultAlphaCutoff = 0.5f;
        [SerializeField] private Texture defaultMaskTexture;
        [Header("Fast Mesh Outline")]
        [SerializeField, Min(0f)] private float widthPerThicknessUnit = 0.002f;
        [SerializeField] private bool requireDepth = true;
        [SerializeField] private bool warnOnUnsupportedShader = true;
        [Header("Selection")]
        [SerializeField] private bool useRegisteredTargets = true;

        private FPFastMeshOutlinePass _outlinePass;
        private Material _runtimeOutlineMaterial;
        private readonly List<FPOutlineTarget> _activeTargets = new List<FPOutlineTarget>();

        private Renderer[] _targetRenderers;
        private bool _warnedUnsupportedShader;

        public void SetRenderers(Renderer[] targetRenderers)
        {
            _targetRenderers = targetRenderers;

            if (_outlinePass != null)
                _outlinePass.Renderers = _targetRenderers;
        }

        public override void Create()
        {
            name = "FP Fast Mesh Outliner";
            _outlinePass = new FPFastMeshOutlinePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_outlinePass == null)
                return;

            Material resolvedOutlineMaterial = ResolveOutlineMaterial();
            if (!IsSupportedMaterial(resolvedOutlineMaterial))
                return;

            FPOutlineTarget[] targets = null;
            if (useRegisteredTargets)
            {
                FPOutlineRegistry.GetActiveTargets(_activeTargets);
                if (_activeTargets.Count > 0)
                    targets = _activeTargets.ToArray();
            }

            bool hasTargets = targets != null && targets.Length > 0;
            bool hasManualRenderers = _targetRenderers != null && _targetRenderers.Length > 0;
            if (!hasTargets && !hasManualRenderers)
                return;

            _outlinePass.RenderEvent = renderEvent;
            _outlinePass.OutlineMaterial = resolvedOutlineMaterial;
            _outlinePass.Renderers = _targetRenderers;
            _outlinePass.Targets = targets;
            _outlinePass.DefaultOutlineColor = defaultOutlineColor;
            _outlinePass.DefaultAlphaMode = defaultAlphaMode;
            _outlinePass.DefaultAlphaCutoff = defaultAlphaCutoff;
            _outlinePass.DefaultMaskTexture = defaultMaskTexture;
            _outlinePass.DefaultThickness = thickness;
            _outlinePass.WidthPerThicknessUnit = widthPerThicknessUnit;
            _outlinePass.RequireDepth = requireDepth;

            renderer.EnqueuePass(_outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_runtimeOutlineMaterial);
            _runtimeOutlineMaterial = null;
        }

        private void OnValidate()
        {
            thickness = Mathf.Max(0, thickness);
            defaultAlphaCutoff = Mathf.Clamp01(defaultAlphaCutoff);
            widthPerThicknessUnit = Mathf.Max(0f, widthPerThicknessUnit);
        }

        private Material ResolveOutlineMaterial()
        {
            if (outlineMaterial)
                return outlineMaterial;

            if (!_runtimeOutlineMaterial)
                _runtimeOutlineMaterial = CoreUtils.CreateEngineMaterial("FuzzPhyte/Fast Mesh Outline");

            return _runtimeOutlineMaterial;
        }

        private bool IsSupportedMaterial(Material material)
        {
            if (!material || !material.shader || !material.shader.isSupported || material.passCount == 0)
            {
                if (warnOnUnsupportedShader && !_warnedUnsupportedShader)
                {
                    _warnedUnsupportedShader = true;
                    Debug.LogWarning(
                        "FP Fast Mesh Outliner skipped because its material/shader is unsupported in this player.",
                        this);
                }

                return false;
            }

            return true;
        }
    }
}
