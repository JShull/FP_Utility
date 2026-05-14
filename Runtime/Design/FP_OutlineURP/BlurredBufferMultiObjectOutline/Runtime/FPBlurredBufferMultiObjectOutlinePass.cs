namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.RenderGraphModule;
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// This code was originally based on the works of the Unity-Technologies GitHub 'Per-Object_Outline_RenderGraph_RenderFeature_Example'
    /// https://github.com/Unity-Technologies/Per-Object_Outline_RenderGraph_RendererFeature_Example
    /// This has been modified for additional usage associated with FuzzPhyte Packages
    /// </summary>
    public class FPBlurredBufferMultiObjectOutlinePass : ScriptableRenderPass
    {
        private const string BaseMapName = "_BaseMap";
        private const string MainTexName = "_MainTex";
        private const string DilationTex0Name = "_DilationTexture0";
        private const string DilationTex1Name = "_DilationTexture1";
        private const string DrawOutlineObjectsPassName = "DrawOutlineObjectsPass";
        private const string HorizontalPassName = "HorizontalDilationPass";
        private const string VerticalPassName = "VerticalDilationPass";

        private static readonly int BaseMapId = Shader.PropertyToID(BaseMapName);
        private static readonly int MainTexId = Shader.PropertyToID(MainTexName);
        private static readonly int OutlineColorId = Shader.PropertyToID("_FPOutlineColor");
        private static readonly int OutlineAlphaModeId = Shader.PropertyToID("_FPOutlineAlphaMode");
        private static readonly int OutlineAlphaCutoffId = Shader.PropertyToID("_FPOutlineAlphaCutoff");
        private static readonly int OutlineMaskTextureId = Shader.PropertyToID("_FPOutlineMaskTexture");
        private static readonly int OutlineMaskTextureStId = Shader.PropertyToID("_FPOutlineMaskTexture_ST");

        public RenderPassEvent RenderEvent { private get; set; }
        public Material DilationMaterial { private get; set; }
        public Material OutlineMaterial { private get; set; }
        public Renderer[] Renderers { get; set; }
        public FPOutlineTarget[] Targets { get; set; }
        public Color DefaultOutlineColor { private get; set; } = Color.cyan;
        public FPOutlineAlphaMode DefaultAlphaMode { private get; set; } = FPOutlineAlphaMode.MeshSilhouette;
        public float DefaultAlphaCutoff { private get; set; } = 0.5f;
        public Texture DefaultMaskTexture { private get; set; }

        private RenderTextureDescriptor _dilationDescriptor;

        public FPBlurredBufferMultiObjectOutlinePass()
        {
            _dilationDescriptor = new RenderTextureDescriptor(
                Screen.width,
                Screen.height,
                RenderTextureFormat.Default,
                depthBufferBits: 0);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph,
            ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            // The following line ensures that the render pass doesn't blit
            // from the back buffer.
            if (resourceData.isActiveTargetBackBuffer)
                return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // Update Settings
            renderPassEvent = RenderEvent;

            // Set the dilation texture size to be the same as the camera target size.
            // depthBufferBits must be zero for color textures, and non-zero for depth textures
            // (it determines the texture format)
            _dilationDescriptor.width = cameraData.cameraTargetDescriptor.width;
            _dilationDescriptor.height = cameraData.cameraTargetDescriptor.height;
            _dilationDescriptor.msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;

            var dilation0Handle = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                _dilationDescriptor,
                DilationTex0Name,
                clear: true);
            var dilation1Handle = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                _dilationDescriptor,
                DilationTex1Name,
                clear: true);

            var screenColorHandle = resourceData.activeColorTexture;
            var screenDepthStencilHandle = resourceData.activeDepthTexture;

            // This check is to avoid an error from the material preview in the scene
            if (!screenColorHandle.IsValid() ||
                !screenDepthStencilHandle.IsValid() ||
                !dilation0Handle.IsValid() ||
                !dilation1Handle.IsValid())
                return;

            // Draw objects-to-outline pass
            using (var builder = renderGraph.AddRasterRenderPass<RenderObjectsPassData>(DrawOutlineObjectsPassName,
                       out var passData))
            {
                // Configure pass data
                passData.Renderers = Renderers;
                passData.Targets = Targets;
                passData.Material = OutlineMaterial;
                passData.DefaultOutlineColor = DefaultOutlineColor;
                passData.DefaultAlphaMode = DefaultAlphaMode;
                passData.DefaultAlphaCutoff = DefaultAlphaCutoff;
                passData.DefaultMaskTexture = DefaultMaskTexture;

                // Draw to dilation0Handle
                builder.SetRenderAttachment(dilation0Handle, 0);
                builder.SetRenderAttachmentDepth(screenDepthStencilHandle);
                builder.AllowPassCulling(false);

                // Blit from the source color to destination color,
                // using the first shader pass.
                builder.SetRenderFunc((RenderObjectsPassData data, RasterGraphContext context) =>
                    ExecuteDrawOutlineObjects(data, context));
            }

            // Horizontal dilation pass
            using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(HorizontalPassName,
                       out var passData))
            {
                // Configure pass data
                passData.Source = dilation0Handle;
                passData.Material = DilationMaterial;

                // From dilation0Handle to dilation1Handle
                builder.UseTexture(passData.Source);
                builder.SetRenderAttachment(dilation1Handle, 0);
                builder.AllowPassCulling(false);

                // Blit from the source color to destination color,
                // using the first shader pass.
                builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                    ExecuteBlit(data, context, 0));
            }

            // Vertical dilation pass
            using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(VerticalPassName, out var passData))
            {
                // Configure pass data
                passData.Source = dilation1Handle;
                passData.Material = DilationMaterial;

                // From dilation1Handle to screenColorHandle
                builder.UseTexture(passData.Source);
                builder.SetRenderAttachment(screenColorHandle, 0);

                // Make sure we also read from the active stencil buffer,
                // which was written to in the Draw objects-to-outline pass
                // and is used here to cut out the inside of the outline.
                builder.SetRenderAttachmentDepth(screenDepthStencilHandle, AccessFlags.Read);
                builder.AllowPassCulling(false);

                // Blit from the source color to destination (camera) color,
                // using the second shader pass.
                builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                    ExecuteBlit(data, context, 1));
            }
        }

        private static void ExecuteDrawOutlineObjects(
            RenderObjectsPassData data,
            RasterGraphContext context)
        {
            DrawTargets(data, context);
            DrawManualRenderers(data, context);
        }

        private static void DrawTargets(RenderObjectsPassData data, RasterGraphContext context)
        {
            if (data.Targets == null)
                return;

            for (int i = 0; i < data.Targets.Length; i++)
            {
                FPOutlineTarget target = data.Targets[i];
                if (!target || !target.isActiveAndEnabled)
                    continue;

                DrawRendererSet(
                    context,
                    data.Material,
                    target.Renderers,
                    target.OutlineColor,
                    target.AlphaMode,
                    target.AlphaCutoff,
                    target.CustomMaskTexture);
            }
        }

        private static void DrawManualRenderers(RenderObjectsPassData data, RasterGraphContext context)
        {
            DrawRendererSet(
                context,
                data.Material,
                data.Renderers,
                data.DefaultOutlineColor,
                data.DefaultAlphaMode,
                data.DefaultAlphaCutoff,
                data.DefaultMaskTexture);
        }

        private static void DrawRendererSet(
            RasterGraphContext context,
            Material material,
            Renderer[] renderers,
            Color outlineColor,
            FPOutlineAlphaMode alphaMode,
            float alphaCutoff,
            Texture customMaskTexture)
        {
            if (renderers == null)
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
                        material,
                        outlineColor,
                        alphaMode,
                        alphaCutoff,
                        customMaskTexture,
                        sourceMaterial);
                    context.cmd.DrawRenderer(objectRenderer, material, materialIndex, 0);
                }
            }
        }

        private static void ConfigureOutlineDraw(
            Material material,
            Color outlineColor,
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

            material.SetColor(OutlineColorId, outlineColor);
            material.SetInt(OutlineAlphaModeId, (int)alphaMode);
            material.SetFloat(OutlineAlphaCutoffId, Mathf.Clamp01(alphaCutoff));
            material.SetTexture(OutlineMaskTextureId, maskTexture);
            material.SetVector(OutlineMaskTextureStId, maskTextureSt);
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

        private static void ExecuteBlit(BlitPassData data, RasterGraphContext context, int pass)
        {
            Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), data.Material, pass);
        }

        private class RenderObjectsPassData
        {
            internal Renderer[] Renderers;
            internal FPOutlineTarget[] Targets;
            internal Material Material;
            internal Color DefaultOutlineColor;
            internal FPOutlineAlphaMode DefaultAlphaMode;
            internal float DefaultAlphaCutoff;
            internal Texture DefaultMaskTexture;
        }

        private class BlitPassData
        {
            internal TextureHandle Source;
            internal Material Material;
        }
    }
}
