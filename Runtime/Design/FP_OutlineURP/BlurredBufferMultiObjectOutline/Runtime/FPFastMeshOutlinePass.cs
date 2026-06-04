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
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// Fast inverted-hull outline pass for selected mesh renderers.
    /// </summary>
    public class FPFastMeshOutlinePass : ScriptableRenderPass
    {
        private const string BaseMapName = "_BaseMap";
        private const string MainTexName = "_MainTex";
        private const string FastMeshOutlinePassName = "FPFastMeshOutlinePass";

        private static readonly int BaseMapId = Shader.PropertyToID(BaseMapName);
        private static readonly int MainTexId = Shader.PropertyToID(MainTexName);
        private static readonly int OutlineColorId = Shader.PropertyToID("_FPOutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_FPFastOutlineWidth");
        private static readonly int OutlineAlphaModeId = Shader.PropertyToID("_FPOutlineAlphaMode");
        private static readonly int OutlineAlphaCutoffId = Shader.PropertyToID("_FPOutlineAlphaCutoff");
        private static readonly int OutlineMaskTextureId = Shader.PropertyToID("_FPOutlineMaskTexture");
        private static readonly int OutlineMaskTextureStId = Shader.PropertyToID("_FPOutlineMaskTexture_ST");

        public RenderPassEvent RenderEvent
        {
            private get => renderPassEvent;
            set => renderPassEvent = value;
        }
        public Material OutlineMaterial { private get; set; }
        public Renderer[] Renderers { private get; set; }
        public FPOutlineTarget[] Targets { private get; set; }
        public Color DefaultOutlineColor { private get; set; } = Color.cyan;
        public FPOutlineAlphaMode DefaultAlphaMode { private get; set; } = FPOutlineAlphaMode.MeshSilhouette;
        public float DefaultAlphaCutoff { private get; set; } = 0.5f;
        public Texture DefaultMaskTexture { private get; set; }
        public int DefaultThickness { private get; set; } = 5;
        public float WidthPerThicknessUnit { private get; set; } = 0.002f;
        public bool RequireDepth { private get; set; } = true;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle screenColorHandle = resourceData.activeColorTexture;
            TextureHandle screenDepthStencilHandle = resourceData.activeDepthTexture;

            if (!screenColorHandle.IsValid())
                return;

            if (RequireDepth && !screenDepthStencilHandle.IsValid())
                return;

            if (!HasTargets() && !HasManualRenderers())
                return;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                       FastMeshOutlinePassName,
                       out var passData))
            {
                passData.Renderers = Renderers;
                passData.Targets = Targets;
                passData.Material = OutlineMaterial;
                passData.DefaultOutlineColor = DefaultOutlineColor;
                passData.DefaultAlphaMode = DefaultAlphaMode;
                passData.DefaultAlphaCutoff = DefaultAlphaCutoff;
                passData.DefaultMaskTexture = DefaultMaskTexture;
                passData.DefaultThickness = DefaultThickness;
                passData.WidthPerThicknessUnit = WidthPerThicknessUnit;

                builder.SetRenderAttachment(screenColorHandle, 0);
                if (screenDepthStencilHandle.IsValid())
                    builder.SetRenderAttachmentDepth(screenDepthStencilHandle, AccessFlags.Read);

                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    Execute(data, context));
            }
        }

        private bool HasTargets()
        {
            return Targets != null && Targets.Length > 0;
        }

        private bool HasManualRenderers()
        {
            return Renderers != null && Renderers.Length > 0;
        }

        private static void Execute(PassData data, RasterGraphContext context)
        {
            DrawTargets(data, context);
            DrawManualRenderers(data, context);
        }

        private static void DrawTargets(PassData data, RasterGraphContext context)
        {
            if (data.Targets == null)
                return;

            for (int i = 0; i < data.Targets.Length; i++)
            {
                FPOutlineTarget target = data.Targets[i];
                if (!target || !target.isActiveAndEnabled)
                    continue;

                int thickness = target.OutlineProfile ? target.OutlineProfile.Thickness : data.DefaultThickness;
                DrawRendererSet(
                    context,
                    data.Material,
                    target.Renderers,
                    target.OutlineColor,
                    target.AlphaMode,
                    target.AlphaCutoff,
                    target.CustomMaskTexture,
                    thickness,
                    data.WidthPerThicknessUnit);
            }
        }

        private static void DrawManualRenderers(PassData data, RasterGraphContext context)
        {
            DrawRendererSet(
                context,
                data.Material,
                data.Renderers,
                data.DefaultOutlineColor,
                data.DefaultAlphaMode,
                data.DefaultAlphaCutoff,
                data.DefaultMaskTexture,
                data.DefaultThickness,
                data.WidthPerThicknessUnit);
        }

        private static void DrawRendererSet(
            RasterGraphContext context,
            Material material,
            Renderer[] renderers,
            Color outlineColor,
            FPOutlineAlphaMode alphaMode,
            float alphaCutoff,
            Texture customMaskTexture,
            int thickness,
            float widthPerThicknessUnit)
        {
            if (!material || renderers == null)
                return;

            float outlineWidth = Mathf.Max(0f, thickness) * Mathf.Max(0f, widthPerThicknessUnit);
            if (outlineWidth <= 0f)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer objectRenderer = renderers[i];
                if (!objectRenderer || !objectRenderer.enabled || !objectRenderer.gameObject.activeInHierarchy)
                    continue;

                Material[] sharedMaterials = objectRenderer.sharedMaterials;
                int materialCount = sharedMaterials.Length;
                for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                {
                    Material sourceMaterial = sharedMaterials[materialIndex];
                    ConfigureOutlineDraw(
                        context,
                        material,
                        outlineColor,
                        outlineWidth,
                        alphaMode,
                        alphaCutoff,
                        customMaskTexture,
                        sourceMaterial);
                    context.cmd.DrawRenderer(objectRenderer, material, materialIndex, 0);
                }
            }
        }

        private static void ConfigureOutlineDraw(
            RasterGraphContext context,
            Material material,
            Color outlineColor,
            float outlineWidth,
            FPOutlineAlphaMode alphaMode,
            float alphaCutoff,
            Texture customMaskTexture,
            Material sourceMaterial)
        {
            Texture maskTexture = Texture2D.whiteTexture;
            Vector4 maskTextureSt = new Vector4(1f, 1f, 0f, 0f);

            if (alphaMode == FPOutlineAlphaMode.CustomMaskTexture && customMaskTexture)
            {
                maskTexture = customMaskTexture;
            }
            else if (alphaMode == FPOutlineAlphaMode.MainTextureAlpha)
            {
                ResolveMainTexture(sourceMaterial, out maskTexture, out maskTextureSt);
            }

            context.cmd.SetGlobalColor(OutlineColorId, outlineColor);
            context.cmd.SetGlobalFloat(OutlineWidthId, outlineWidth);
            context.cmd.SetGlobalInt(OutlineAlphaModeId, (int)alphaMode);
            context.cmd.SetGlobalFloat(OutlineAlphaCutoffId, Mathf.Clamp01(alphaCutoff));
            context.cmd.SetGlobalVector(OutlineMaskTextureStId, maskTextureSt);
            material.SetTexture(OutlineMaskTextureId, maskTexture);
        }

        private static void ResolveMainTexture(
            Material sourceMaterial,
            out Texture texture,
            out Vector4 textureSt)
        {
            texture = Texture2D.whiteTexture;
            textureSt = new Vector4(1f, 1f, 0f, 0f);

            if (!sourceMaterial)
                return;

            if (sourceMaterial.HasProperty(BaseMapId))
            {
                Texture sourceTexture = sourceMaterial.GetTexture(BaseMapId);
                if (sourceTexture)
                    texture = sourceTexture;

                Vector2 scale = sourceMaterial.GetTextureScale(BaseMapName);
                Vector2 offset = sourceMaterial.GetTextureOffset(BaseMapName);
                textureSt = new Vector4(scale.x, scale.y, offset.x, offset.y);
                return;
            }

            if (!sourceMaterial.HasProperty(MainTexId))
                return;

            Texture mainTexture = sourceMaterial.GetTexture(MainTexId);
            if (mainTexture)
                texture = mainTexture;

            Vector2 mainScale = sourceMaterial.GetTextureScale(MainTexName);
            Vector2 mainOffset = sourceMaterial.GetTextureOffset(MainTexName);
            textureSt = new Vector4(mainScale.x, mainScale.y, mainOffset.x, mainOffset.y);
        }

        private class PassData
        {
            internal Renderer[] Renderers;
            internal FPOutlineTarget[] Targets;
            internal Material Material;
            internal Color DefaultOutlineColor;
            internal FPOutlineAlphaMode DefaultAlphaMode;
            internal float DefaultAlphaCutoff;
            internal Texture DefaultMaskTexture;
            internal int DefaultThickness;
            internal float WidthPerThicknessUnit;
        }
    }
}
