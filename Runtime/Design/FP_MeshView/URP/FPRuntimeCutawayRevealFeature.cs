namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.RendererUtils;

    public sealed class FPRuntimeCutawayRevealFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
            public LayerMask revealLayer;
            public Material revealMaterial;
        }

        [SerializeField] private Settings settings = new Settings();
        private Pass _pass;

        public override void Create()
        {
            _pass = new Pass(settings)
            {
                renderPassEvent = settings.passEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.revealMaterial == null) return;
            if (FPRuntimeCutawayVolume.Active == null) return;

            renderer.EnqueuePass(_pass);
        }

        private sealed class Pass : ScriptableRenderPass
        {
            private readonly Settings _settings;

            private static readonly ProfilingSampler Profiler =
                new ProfilingSampler("FP Cutaway Reveal Pass");

            private static readonly int CenterID = Shader.PropertyToID("_VolumeCenter");
            private static readonly int RadiusID = Shader.PropertyToID("_SphereRadius");
            private static readonly int ExtentsID = Shader.PropertyToID("_BoxExtents");
            private static readonly int UseSphereID = Shader.PropertyToID("_UseSphere");

            public Pass(Settings settings) => _settings = settings;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var volume = FPRuntimeCutawayVolume.Active;
                if (volume == null) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("FP Cutaway Reveal", out var passData, Profiler))
                {
                    var cameraData = frameData.Get<UniversalCameraData>();
                    var renderingData = frameData.Get<UniversalRenderingData>();

                    // ShaderTag setup
                    ShaderTagId[] shaderTags =
                    {
                        new ShaderTagId("UniversalForward"),
                        new ShaderTagId("SRPDefaultUnlit")
                    };

                    // Build RendererListDesc
                    var desc = new RendererListDesc(shaderTags, renderingData.cullResults, cameraData.camera)
                    {
                        sortingCriteria = SortingCriteria.CommonTransparent,
                        renderQueueRange = RenderQueueRange.all,
                        layerMask = _settings.revealLayer,
                        overrideMaterial = _settings.revealMaterial,
                        overrideMaterialPassIndex = 0
                    };

                    // **Create list via renderGraph**
                    passData.rendererList = renderGraph.CreateRendererList(desc);

                    // Store volume + material
                    passData.volume = volume;
                    passData.material = _settings.revealMaterial;

                    // Register that this pass “uses” that renderer list
                    builder.UseRendererList(passData.rendererList);

                    // Set target textures
                    var resources = frameData.Get<UniversalResourceData>();
                    builder.SetRenderAttachment(resources.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture);

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        ExecuteReveal(data, ctx);
                    });
                }
            }

            private sealed class PassData
            {
                public FPRuntimeCutawayVolume volume;
                public Material material;
                public RendererListHandle rendererList;
            }
            private void ExecuteReveal(PassData data, RasterGraphContext ctx)
            {
                // Set volume shader params
                data.material.SetVector(CenterID, data.volume.Center);
                data.material.SetFloat(RadiusID, data.volume.sphereRadius);
                data.material.SetVector(ExtentsID, data.volume.boxExtents);
                data.material.SetInt(UseSphereID, data.volume.useSphere ? 1 : 0);

                // **Draw the list**
                ctx.cmd.DrawRendererList(data.rendererList);
            }


        }
    }
}
