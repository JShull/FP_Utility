namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using UnityEngine.Rendering.RenderGraphModule;
    /*
    Open your URP Renderer Data asset (the one referenced by your URP pipeline asset).
    Click Add Renderer Feature
    Select FPRuntimeMeshViewerFeature
    Choose AfterRenderingOpaques
    */
    public class FPRuntimeMeshViewerFeature:ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingOpaques;
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
            // Only run if the viewer exists and is active
            if (FPRuntimeMeshViewer.Active == null) return;

            // Optionally skip scene view
            if (!settings.drawInSceneView && renderingData.cameraData.isSceneViewCamera)
                return;

            renderer.EnqueuePass(_pass);
        }

        private sealed class Pass : ScriptableRenderPass
        {
            private readonly Settings _settings;
            private static readonly ProfilingSampler Profiler = new ProfilingSampler("FP Runtime Mesh Viewer");
            private sealed class PassData
            {
                public FPRuntimeMeshViewer viewer;
                public MeshViewMode mode;
            }

            public Pass(Settings settings) => _settings = settings;

            // ✅ Modern URP path
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var viewer = FPRuntimeMeshViewer.Active;
                if (viewer == null) return;

                var mode = viewer.GetMode;
                if (mode == MeshViewMode.Default) return;

                var resources = frameData.Get<UniversalResourceData>();

                PassData passData;
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                        "FP Runtime Mesh Viewer",
                        out passData,
                        Profiler))
                {
                    passData.viewer = viewer;
                    passData.mode = mode;

                    // Bind the camera targets so we actually draw into the frame
                    builder.SetRenderAttachment(resources.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture);

                    // Optional but useful while you're bringing it up
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        ExecuteDraws(data.viewer, data.mode, ctx.cmd);
                    });
                }
            }

            private static void ExecuteDraws(FPRuntimeMeshViewer viewer, MeshViewMode mode, RasterCommandBuffer cmd)
            {
                var targets = viewer.GetTargets;
                for (int i = 0; i < targets.Length; i++)
                {
                    var r = targets[i];
                    if (r == null) continue;

                    var mesh = GetMeshFromRenderer(r);
                    if (mesh == null) continue;

                    if (!viewer.TryGetCache(mesh, out var cache) || cache == null) continue;

                    var matrix = r.localToWorldMatrix;

                    switch (mode)
                    {
                        case MeshViewMode.Vertices:
                            cache.DrawVertices(cmd, matrix, viewer.VertexMat);
                            break;

                        case MeshViewMode.Wireframe:
                            cache.DrawWireframe(cmd, matrix, viewer.WireframeMat);
                            break;

                        case MeshViewMode.WireframeAndVertices:
                            cache.DrawWireframe(cmd, matrix, viewer.WireframeMat);
                            cache.DrawVertices(cmd, matrix, viewer.VertexMat);
                            break;

                        case MeshViewMode.Normals:
                            cache.DrawNormals(cmd, matrix, viewer.NormalsMat);
                            break;

                        case MeshViewMode.SurfaceWorldNormals:
                        case MeshViewMode.SurfaceUV0:
                        case MeshViewMode.SurfaceVertexColor:
                            var mat = viewer.SurfaceDebugMat;
                            if (mat != null)
                                cmd.DrawMesh(mesh, matrix, mat, 0, 0);
                            break;
                    }
                }
            }
            private static Mesh GetMeshFromRenderer(Renderer r)
            {
                if (r is MeshRenderer mr)
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    return mf ? mf.sharedMesh : null;
                }
                if (r is SkinnedMeshRenderer smr)
                {
                    return smr.sharedMesh;
                }
                return null;
            }
        }
    }
}
