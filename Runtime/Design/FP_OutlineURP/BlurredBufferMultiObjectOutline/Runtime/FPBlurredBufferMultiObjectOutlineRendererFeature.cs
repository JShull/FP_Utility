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
        private readonly List<OutlineTargetGroup> _targetGroups = new List<OutlineTargetGroup>();

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

            BuildTargetGroups();

            int batchCount = 0;
            for (int i = 0; i < _targetGroups.Count; i++)
            {
                if (_targetGroups[i].Targets.Count > 0)
                    batchCount++;
            }

            bool hasManualRenderers = _targetRenderers != null && _targetRenderers.Length > 0;
            if (hasManualRenderers)
                batchCount++;

            if (batchCount == 0)
                return;

            var batches = new FPBlurredBufferMultiObjectOutlinePass.OutlineRenderBatch[batchCount];
            int batchIndex = 0;
            for (int i = 0; i < _targetGroups.Count; i++)
            {
                OutlineTargetGroup group = _targetGroups[i];
                if (group.Targets.Count == 0)
                    continue;

                batches[batchIndex] = new FPBlurredBufferMultiObjectOutlinePass.OutlineRenderBatch
                {
                    Targets = group.Targets.ToArray(),
                    DefaultOutlineColor = defaultOutlineColor,
                    DefaultAlphaMode = defaultAlphaMode,
                    DefaultAlphaCutoff = defaultAlphaCutoff,
                    DefaultMaskTexture = defaultMaskTexture,
                    Thickness = group.Thickness,
                    Blur = group.Blur,
                    MaxRadius = group.MaxRadius
                };
                batchIndex++;
            }

            if (hasManualRenderers)
            {
                batches[batchIndex] = new FPBlurredBufferMultiObjectOutlinePass.OutlineRenderBatch
                {
                    Renderers = _targetRenderers,
                    DefaultOutlineColor = defaultOutlineColor,
                    DefaultAlphaMode = defaultAlphaMode,
                    DefaultAlphaCutoff = defaultAlphaCutoff,
                    DefaultMaskTexture = defaultMaskTexture,
                    Thickness = thickness,
                    Blur = blur,
                    MaxRadius = maxRadius
                };
            }

            // Any variables you may want to update every frame should be set here.
            _outlinePass.RenderEvent = renderEvent;
            _outlinePass.DilationMaterial = resolvedDilationMaterial;
            _outlinePass.OutlineMaterial = resolvedOutlineMaterial;
            _outlinePass.Renderers = _targetRenderers;
            _outlinePass.Targets = null;
            _outlinePass.Batches = batches;
            _outlinePass.DefaultOutlineColor = defaultOutlineColor;
            _outlinePass.DefaultAlphaMode = defaultAlphaMode;
            _outlinePass.DefaultAlphaCutoff = defaultAlphaCutoff;
            _outlinePass.DefaultMaskTexture = defaultMaskTexture;
            _outlinePass.DefaultThickness = thickness;
            _outlinePass.DefaultBlur = blur;
            _outlinePass.DefaultMaxRadius = maxRadius;

            renderer.EnqueuePass(_outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_runtimeDilationMaterial);
            CoreUtils.Destroy(_runtimeOutlineMaterial);
            _runtimeDilationMaterial = null;
            _runtimeOutlineMaterial = null;
        }

        private void BuildTargetGroups()
        {
            for (int i = 0; i < _targetGroups.Count; i++)
                _targetGroups[i].Targets.Clear();

            if (useRegisteredTargets)
            {
                FPOutlineRegistry.GetActiveTargets(_activeTargets);
                for (int i = 0; i < _activeTargets.Count; i++)
                    AddTargetToGroup(_activeTargets[i]);
            }
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

        private void AddTargetToGroup(FPOutlineTarget target)
        {
            if (!target)
                return;

            ResolveDilationSettings(target, out int targetThickness, out int targetBlur, out int targetMaxRadius);

            for (int i = 0; i < _targetGroups.Count; i++)
            {
                OutlineTargetGroup group = _targetGroups[i];
                if (group.Matches(targetThickness, targetBlur, targetMaxRadius))
                {
                    group.Targets.Add(target);
                    return;
                }
            }

            var newGroup = new OutlineTargetGroup(targetThickness, targetBlur, targetMaxRadius);
            newGroup.Targets.Add(target);
            _targetGroups.Add(newGroup);
        }

        private void ResolveDilationSettings(
            FPOutlineTarget target,
            out int targetThickness,
            out int targetBlur,
            out int targetMaxRadius)
        {
            FPOutlineProfile profile = target.OutlineProfile;
            if (profile)
            {
                targetThickness = profile.Thickness;
                targetBlur = profile.Blur;
                targetMaxRadius = profile.MaxRadius;
                return;
            }

            targetThickness = thickness;
            targetBlur = blur;
            targetMaxRadius = maxRadius;
        }

        private sealed class OutlineTargetGroup
        {
            public readonly int Thickness;
            public readonly int Blur;
            public readonly int MaxRadius;
            public readonly List<FPOutlineTarget> Targets = new List<FPOutlineTarget>();

            public OutlineTargetGroup(int thickness, int blur, int maxRadius)
            {
                Thickness = thickness;
                Blur = blur;
                MaxRadius = maxRadius;
            }

            public bool Matches(int thickness, int blur, int maxRadius)
            {
                return Thickness == thickness && Blur == blur && MaxRadius == maxRadius;
            }
        }
    }
}
