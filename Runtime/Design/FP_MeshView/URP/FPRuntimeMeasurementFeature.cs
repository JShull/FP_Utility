namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using UnityEngine.Rendering.RenderGraphModule;

    public sealed class FPRuntimeMeasurementFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            // For “always on top of everything”, prefer AfterRenderingTransparents.
            // If you still see transparents over it, try AfterRendering (or AfterRenderingPostProcessing depending on your URP version).
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
            public bool drawInSceneView = false;
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
            if (FPRuntimeMeasurementOverlay.Active == null) return;

            if (!settings.drawInSceneView && renderingData.cameraData.isSceneViewCamera)
                return;

            renderer.EnqueuePass(_pass);
        }

        private sealed class Pass : ScriptableRenderPass
        {
            private readonly Settings _settings;
            private static readonly ProfilingSampler Profiler = new ProfilingSampler("FP Runtime Measurement Overlay");

            private sealed class PassData
            {
                public FPRuntimeMeasurementOverlay overlay;
            }

            // Shader property IDs (avoid string lookups)
            private static readonly int FPPointsID = Shader.PropertyToID("_FPPoints");
            private static readonly int FPLinePointsID = Shader.PropertyToID("_FPLinePoints");
            private static readonly int SizeID = Shader.PropertyToID("_Size");
            private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
            private static readonly int ColorID = Shader.PropertyToID("_Color");
            private static readonly int WidthWorldID = Shader.PropertyToID("_WidthWorld");

            public Pass(Settings settings) => _settings = settings;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var overlay = FPRuntimeMeasurementOverlay.Active;
                if (overlay == null) return;

                // Don’t spend a pass if there’s nothing to draw.
                if (!overlay.HasMeasurement) return; // your overlay already tracks this :contentReference[oaicite:4]{index=4}

                var resources = frameData.Get<UniversalResourceData>();

                PassData passData;
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           "FP Runtime Measurement Overlay",
                           out passData,
                           Profiler))
                {
                    passData.overlay = overlay;

                    builder.SetRenderAttachment(resources.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        ExecuteDraws(data.overlay, ctx.cmd);
                    });
                }
            }

            private static void ExecuteDraws(FPRuntimeMeasurementOverlay overlay, RasterCommandBuffer cmd)
            {
                // Points (2 points => 12 verts)
                var pointMat = overlay.PointMat;
                if (pointMat != null && overlay.PointsBuffer != null)
                {
                    pointMat.SetBuffer(FPPointsID, overlay.PointsBuffer);
                    pointMat.SetFloat(SizeID, overlay.PointSize);
                    pointMat.SetFloat(OpacityID, overlay.PointOpacity);
                    pointMat.SetColor(ColorID, overlay.PointColor);

                    cmd.DrawProcedural(Matrix4x4.identity, pointMat, 0, MeshTopology.Triangles, 2 * 6, 1);
                }

                // Line (A/B => 1 quad => 6 verts)
                var lineMat = overlay.LineMat;
                if (lineMat != null && overlay.LineBuffer != null)
                {
                    lineMat.SetBuffer(FPLinePointsID, overlay.LineBuffer);
                    lineMat.SetFloat(WidthWorldID, overlay.LineWidthWorld);
                    lineMat.SetFloat(OpacityID, overlay.LineOpacity);
                    lineMat.SetColor(ColorID, overlay.LineColor);

                    cmd.DrawProcedural(Matrix4x4.identity, lineMat, 0, MeshTopology.Triangles, 6, 1);
                }
            }
        }
    }
}
