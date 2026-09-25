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
        public bool SupportsSceneView => isActive && settings.drawInSceneView && settings.gridMaterial != null;

        public override void Create()
        {
            _pass?.Dispose();
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

            foreach (var grid in FPRuntimeGridPlane.Active)
                if (grid != null && grid.IsVisibleTo(renderingData.cameraData.camera))
                {
                    renderer.EnqueuePass(_pass);
                    break;
                }
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
        }

        private sealed class Pass : ScriptableRenderPass
        {
            private readonly Settings _settings;
            private static readonly ProfilingSampler Profiler = new ProfilingSampler("FP Runtime Grid");

            // cached quad mesh
            private Mesh quad;

            private sealed class DrawData
            {
                internal Matrix4x4 Matrix;
                internal MaterialPropertyBlock Properties;
            }

            private sealed class PassData
            {
                internal Mesh Quad;
                internal Material Material;
                internal List<DrawData> Draws;
            }

            // shader property IDs
            private static readonly int MinorColorID = Shader.PropertyToID("_MinorColor");
            private static readonly int MajorColorID = Shader.PropertyToID("_MajorColor");
            private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
            private static readonly int SpacingID = Shader.PropertyToID("_SpacingWorld");
            private static readonly int MajorEveryID = Shader.PropertyToID("_MajorEvery");
            private static readonly int MinorThickID = Shader.PropertyToID("_MinorThicknessPx");
            private static readonly int MajorThickID = Shader.PropertyToID("_MajorThicknessPx");

            public Pass(Settings settings) => _settings = settings;

            private Mesh GetQuad()
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "FP_GridQuad", hideFlags = HideFlags.HideAndDontSave };
                quad.vertices = new[]
                {
                    new Vector3(-0.5f, 0, -0.5f),
                    new Vector3(-0.5f, 0,  0.5f),
                    new Vector3( 0.5f, 0,  0.5f),
                    new Vector3( 0.5f, 0, -0.5f),
                };
                quad.uv = new[]
                {
                    new Vector2(0,0),
                    new Vector2(0,1),
                    new Vector2(1,1),
                    new Vector2(1,0),
                };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                quad.RecalculateBounds();
                return quad;
            }

            internal void Dispose() { CoreUtils.Destroy(quad); quad = null; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var grids = FPRuntimeGridPlane.Active;
                if (grids == null || grids.Count == 0) return;

                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>().camera;
                var draws = new List<DrawData>();
                foreach (var grid in grids)
                {
                    if (grid == null || !grid.IsVisibleTo(camera)) continue;
                    var properties = new MaterialPropertyBlock();
                    properties.SetColor(MinorColorID, grid.MinorColor);
                    properties.SetColor(MajorColorID, grid.MajorColor);
                    properties.SetFloat(OpacityID, grid.Opacity);
                    properties.SetFloat(SpacingID, grid.SpacingWorldMeters);
                    properties.SetInt(MajorEveryID, grid.MajorEveryComputed);
                    properties.SetFloat(MinorThickID, grid.MinorThicknessPx);
                    properties.SetFloat(MajorThickID, grid.MajorThicknessPx);
                    draws.Add(new DrawData {
                        Matrix = Matrix4x4.TRS(grid.transform.position, grid.transform.rotation, new Vector3(grid.ExtentsWorld.x, 1, grid.ExtentsWorld.y)),
                        Properties = properties
                    });
                }
                if (draws.Count == 0 || _settings.gridMaterial == null) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           "FP Runtime Grid",
                           out var data,
                           Profiler))
                {
                    data.Quad = GetQuad(); data.Material = _settings.gridMaterial; data.Draws = draws;
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc(static (PassData pass, RasterGraphContext ctx) =>
                    {
                        foreach (var draw in pass.Draws)
                            ctx.cmd.DrawMesh(pass.Quad, draw.Matrix, pass.Material, 0, 0, draw.Properties);
                    });
                }
            }
        }
    }
}
