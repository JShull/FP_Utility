namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// This code was originally based on the works of the Unity-Technologies GitHub 'Per-Object_Outline_RenderGraph_RenderFeature_Example'
    /// https://github.com/Unity-Technologies/Per-Object_Outline_RenderGraph_RendererFeature_Example
    /// This has been modified for additional usage associated with FuzzPhyte Packages
    /// </summary>
    public class FPBlurredBufferMultiObjectOutlineRendererFeature : ScriptableRendererFeature
    {
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int BlurId = Shader.PropertyToID("_Blur");
        private static readonly int MaxRadiusId = Shader.PropertyToID("_MaxRadius");

        [SerializeField] private RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;
        [Space, SerializeField] private Material dilationMaterial;
        [SerializeField] private Material outlineMaterial;
        [Header("Outline")]
        [SerializeField, Min(0)] private int thickness = 5;
        [SerializeField, Min(0)] private int blur = 2;
        [SerializeField, Range(1, 128)] private int maxRadius = 50;
        [SerializeField, ColorUsage(false, true)] private Color defaultOutlineColor = Color.cyan;
        [SerializeField] private FPOutlineAlphaMode defaultAlphaMode = FPOutlineAlphaMode.MeshSilhouette;
        [SerializeField, Range(0f, 1f)] private float defaultAlphaCutoff = 0.5f;
        [SerializeField] private Texture defaultMaskTexture;
        [Header("Selection")]
        [SerializeField] private bool useRegisteredTargets = true;

        private FPBlurredBufferMultiObjectOutlinePass _outlinePass;
        private Material _runtimeDilationMaterial;
        private Material _runtimeOutlineMaterial;
        private readonly List<FPOutlineTarget> _activeTargets = new List<FPOutlineTarget>();

        private Renderer[] _targetRenderers;

        public void SetRenderers(Renderer[] targetRenderers)
        {
            _targetRenderers = targetRenderers;

            if (_outlinePass != null)
                _outlinePass.Renderers = _targetRenderers;
        }

        public override void Create()
        {
            name = "FP Multi-Object Outliner";
            _outlinePass = new FPBlurredBufferMultiObjectOutlinePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_outlinePass == null)
                return;

            Material resolvedDilationMaterial = ResolveDilationMaterial();
            Material resolvedOutlineMaterial = ResolveOutlineMaterial();
            if (!resolvedDilationMaterial || !resolvedOutlineMaterial)
            {
                return;
            }

            FPOutlineTarget[] targetSnapshot = null;
            if (useRegisteredTargets)
            {
                FPOutlineRegistry.GetActiveTargets(_activeTargets);
                if (_activeTargets.Count > 0)
                    targetSnapshot = _activeTargets.ToArray();
            }

            bool hasManualRenderers = _targetRenderers != null && _targetRenderers.Length > 0;
            if ((targetSnapshot == null || targetSnapshot.Length == 0) && !hasManualRenderers)
                return;

            // Any variables you may want to update every frame should be set here.
            _outlinePass.RenderEvent = renderEvent;
            _outlinePass.DilationMaterial = resolvedDilationMaterial;
            resolvedDilationMaterial.SetInteger(ThicknessId, thickness);
            resolvedDilationMaterial.SetInteger(BlurId, blur);
            resolvedDilationMaterial.SetInteger(MaxRadiusId, maxRadius);
            _outlinePass.OutlineMaterial = resolvedOutlineMaterial;
            _outlinePass.Renderers = _targetRenderers;
            _outlinePass.Targets = targetSnapshot;
            _outlinePass.DefaultOutlineColor = defaultOutlineColor;
            _outlinePass.DefaultAlphaMode = defaultAlphaMode;
            _outlinePass.DefaultAlphaCutoff = defaultAlphaCutoff;
            _outlinePass.DefaultMaskTexture = defaultMaskTexture;

            renderer.EnqueuePass(_outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_runtimeDilationMaterial);
            CoreUtils.Destroy(_runtimeOutlineMaterial);
            _runtimeDilationMaterial = null;
            _runtimeOutlineMaterial = null;
        }

        private Material ResolveDilationMaterial()
        {
            if (dilationMaterial)
                return dilationMaterial;

            if (!_runtimeDilationMaterial)
                _runtimeDilationMaterial = CoreUtils.CreateEngineMaterial("FuzzPhyte/Dilation");

            return _runtimeDilationMaterial;
        }

        private Material ResolveOutlineMaterial()
        {
            if (outlineMaterial)
                return outlineMaterial;

            if (!_runtimeOutlineMaterial)
                _runtimeOutlineMaterial = CoreUtils.CreateEngineMaterial("FuzzPhyte/Outline Color And Stencil");

            return _runtimeOutlineMaterial;
        }
    }
}
