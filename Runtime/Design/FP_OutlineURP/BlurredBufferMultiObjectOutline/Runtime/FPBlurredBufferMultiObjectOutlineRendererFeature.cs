namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// This code was originally based on the works of the Unity-Technologies GitHub 'Per-Object_Outline_RenderGraph_RenderFeature_Example'
    /// https://github.com/Unity-Technologies/Per-Object_Outline_RenderGraph_RendererFeature_Example
    /// This has been modified for additional usage associated with FuzzPhyte Packages
    /// </summary>
    public class FPBlurredBufferMultiObjectOutlineRendererFeature : ScriptableRendererFeature
    {
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");

        [SerializeField] private RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;
        [Space, SerializeField] private Material dilationMaterial;
        [SerializeField] private Material outlineMaterial;
        [SerializeField, Range(1, 60)] private int spread = 15;

        private FPBlurredBufferMultiObjectOutlinePass _outlinePass;

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

            if (!dilationMaterial ||
                !outlineMaterial ||
                _targetRenderers == null ||
                _targetRenderers.Length == 0)
            {
                return;
            }

            // Any variables you may want to update every frame should be set here.
            _outlinePass.RenderEvent = renderEvent;
            _outlinePass.DilationMaterial = dilationMaterial;
            dilationMaterial.SetInteger("_Spread", spread);
            _outlinePass.OutlineMaterial = outlineMaterial;
            _outlinePass.Renderers = _targetRenderers;

            renderer.EnqueuePass(_outlinePass);
        }
    }
}
