namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.RendererUtils;

    /// <summary>
    /// Clips walls
    /// </summary>
    public class FPRuntimeCutawayGeometryFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingOpaques;
            public LayerMask targetLayers;
            public Material cutMaterial;
        }

        [SerializeField] private Settings settings;
        private Pass _pass;

        public override void Create()
        {
            _pass = new Pass(settings)
            {
                renderPassEvent = settings.passEvent
            };
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (settings.cutMaterial == null) return;
            if (FPRuntimeCutawayVolume.Active == null) return;

            renderer.EnqueuePass(_pass);
        }

        private sealed class Pass : ScriptableRenderPass
        {
            private readonly Settings _settings;
            private static readonly ProfilingSampler Profiler =
                new ProfilingSampler("FP Cutaway Geometry Pass");

            private static readonly int VC_Center = Shader.PropertyToID("_VolumeCenter");
            private static readonly int VC_Radius = Shader.PropertyToID("_SphereRadius");
            private static readonly int VC_Extents = Shader.PropertyToID("_BoxExtents");
            private static readonly int VC_UseSphere = Shader.PropertyToID("_UseSphere");

            public Pass(Settings s) => _settings = s;

            // New RenderGraph implementation
            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData)
            {
                var volume = FPRuntimeCutawayVolume.Active;
                if (volume == null) return;

                var resources = frameData.Get<UniversalResourceData>();

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           "FP Cutaway Geometry",
                           out var passData,
                           Profiler))
                {
                    passData.volume = volume;
                    passData.material = _settings.cutMaterial;

                    // Setup renderer list
                    var cameraData = frameData.Get<UniversalCameraData>();
                    var renderingData = frameData.Get<UniversalRenderingData>();

                    ShaderTagId[] shaderTags =
                    {
                    new ShaderTagId("UniversalForward"),
                    new ShaderTagId("SRPDefaultUnlit"),
                };

                    var desc = new RendererListDesc(shaderTags,
                        renderingData.cullResults,
                        cameraData.camera)
                    {
                        sortingCriteria = SortingCriteria.CommonOpaque,
                        renderQueueRange = RenderQueueRange.opaque,
                        layerMask = _settings.targetLayers,
                        overrideMaterial = _settings.cutMaterial,
                        overrideMaterialPassIndex = 0
                    };

                    // tell render graph to make this list
                    passData.rendererList = renderGraph.CreateRendererList(desc);

                    // we intend to draw it
                    builder.UseRendererList(passData.rendererList);

                    // Targets
                    builder.SetRenderAttachment(
                        resources.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(
                        resources.activeDepthTexture);

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        ExecuteCutGeometry(data, ctx);
                    });
                }
            }

            private sealed class PassData
            {
                public FPRuntimeCutawayVolume volume;
                public Material material;
                public RendererListHandle rendererList;
            }

            private void ExecuteCutGeometry(
                PassData data,
                RasterGraphContext ctx)
            {
                var mat = data.material;
                mat.SetVector(VC_Center, data.volume.Center);
                mat.SetFloat(VC_Radius, data.volume.sphereRadius);
                //mat.SetVector(VC_Extents, data.volume
                mat.SetInt(VC_UseSphere, data.volume.useSphere ? 1 : 0);

                // draw it
                ctx.cmd.DrawRendererList(data.rendererList);
            }
        }
    }
}
