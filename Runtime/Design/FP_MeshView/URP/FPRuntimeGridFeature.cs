namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using System.Collections.Generic;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using UnityEngine.Rendering.RenderGraphModule;

    public sealed class FPRuntimeGridFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            // If you want it behind transparents, use AfterRenderingOpaques.
            // If you want it drawn late, use AfterRenderingTransparents.
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingOpaques;
            public bool drawInSceneView = false;
            public Material gridMaterial;
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
            if (settings.gridMaterial == null) return;
            if (FPRuntimeGridPlane.Active == null || FPRuntimeGridPlane.Active.Count == 0) return;

            if (!settings.drawInSceneView && renderingData.cameraData.isSceneViewCamera)
                return;

            renderer.EnqueuePass(_pass);
        }

        private sealed class Pass : ScriptableRenderPass
        {
            private readonly Settings _settings;
            private static readonly ProfilingSampler Profiler = new ProfilingSampler("FP Runtime Grid");

            // cached quad mesh
            private static Mesh s_Quad;

            // shader property IDs
            private static readonly int MinorColorID = Shader.PropertyToID("_MinorColor");
            private static readonly int MajorColorID = Shader.PropertyToID("_MajorColor");
            private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
            private static readonly int SpacingID = Shader.PropertyToID("_SpacingWorld");
            private static readonly int MajorEveryID = Shader.PropertyToID("_MajorEvery");
            private static readonly int MinorThickID = Shader.PropertyToID("_MinorThicknessPx");
            private static readonly int MajorThickID = Shader.PropertyToID("_MajorThicknessPx");

            public Pass(Settings settings) => _settings = settings;

            private static Mesh GetQuad()
            {
                if (s_Quad != null) return s_Quad;
                s_Quad = new Mesh { name = "FP_GridQuad" };
                s_Quad.vertices = new[]
                {
                    new Vector3(-0.5f, 0, -0.5f),
                    new Vector3(-0.5f, 0,  0.5f),
                    new Vector3( 0.5f, 0,  0.5f),
                    new Vector3( 0.5f, 0, -0.5f),
                };
                s_Quad.uv = new[]
                {
                    new Vector2(0,0),
                    new Vector2(0,1),
                    new Vector2(1,1),
                    new Vector2(1,0),
                };
                s_Quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                s_Quad.RecalculateBounds();
                return s_Quad;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var grids = FPRuntimeGridPlane.Active;
                if (grids == null || grids.Count == 0) return;

                var resources = frameData.Get<UniversalResourceData>();

                using (var builder = renderGraph.AddRasterRenderPass<object>(
                           "FP Runtime Grid",
                           out _,
                           Profiler))
                {
                    builder.SetRenderAttachment(resources.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((object _, RasterGraphContext ctx) =>
                    {
                        ExecuteDraws(grids, ctx.cmd, _settings.gridMaterial);
                    });
                }
            }

            private static void ExecuteDraws(IReadOnlyList<FPRuntimeGridPlane> grids, RasterCommandBuffer cmd, Material mat)
            {
                var quad = GetQuad();

                for (int i = 0; i < grids.Count; i++)
                {
                    var g = grids[i];
                    if (g == null || !g.isActiveAndEnabled || !g.IsEnabled) continue;

                    // Set per-grid material params
                    mat.SetColor(MinorColorID, g.MinorColor);
                    mat.SetColor(MajorColorID, g.MajorColor);
                    mat.SetFloat(OpacityID, g.Opacity);
                    mat.SetFloat(SpacingID, g.SpacingWorldMeters);
                    mat.SetInt(MajorEveryID, g.MajorEveryComputed);
                    mat.SetFloat(MinorThickID, g.MinorThicknessPx);
                    mat.SetFloat(MajorThickID, g.MajorThicknessPx);

                    // Plane transform: use the object transform for plane orientation
                    // Scale quad to desired extents (x=width, z=height)
                    var t = g.transform;
                    var scale = new Vector3(g.ExtentsWorld.x, 1f, g.ExtentsWorld.y);
                    var m = Matrix4x4.TRS(t.position, t.rotation, scale);

                    cmd.DrawMesh(quad, m, mat, 0, 0);
                }
            }
        }
    }
}
