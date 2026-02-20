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
    public class FPBlurRenderPass : ScriptableRenderPass
    {
        private static readonly int horizontalBlurId = Shader.PropertyToID("_HorizontalBlur");
        private static readonly int verticalBlurId = Shader.PropertyToID("_VerticalBlur");
        private const string k_BlurTextureName = "_BlurTexture";
        private const string k_VerticalPassName = "VerticalBlurRenderPass";
        private const string k_HorizontalPassName = "HorizontalBlurRenderPass";
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int BlurId = Shader.PropertyToID("_Blur");
        static readonly int MaxRadiusId = Shader.PropertyToID("_MaxRadius");

        private static Vector4 m_ScaleBias = new Vector4(1f, 1f, 0f, 0f);

        private BlurSettings defaultSettings;
        private Material material;

        private RenderTextureDescriptor blurTextureDescriptor;

        public FPBlurRenderPass(Material material, BlurSettings defaultSettings)
        {
            this.material = material;
            this.defaultSettings = defaultSettings;

            blurTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height,
                RenderTextureFormat.Default, 0);
        }

        private void UpdateBlurSettings()
        {
            if (material == null) return;

            // Use the Volume settings or the default settings if no Volume is set.
            var volumeComponent =
                VolumeManager.instance.stack.GetComponent<FPCustomVolumeComponent>();
            float horizontalBlur = volumeComponent.horizontalBlur.overrideState ?
                volumeComponent.horizontalBlur.value : defaultSettings.horizontalBlur;
            float verticalBlur = volumeComponent.verticalBlur.overrideState ?
                volumeComponent.verticalBlur.value : defaultSettings.verticalBlur;
            int thicknessPx = volumeComponent.outlineThicknessPx.overrideState ?
                volumeComponent.outlineThicknessPx.value : defaultSettings.outlineThicknessPx;

            int blurPx = volumeComponent.outlineBlurPx.overrideState ?
                volumeComponent.outlineBlurPx.value : defaultSettings.outlineBlurPx;

            material.SetFloat(horizontalBlurId, horizontalBlur);
            material.SetFloat(verticalBlurId, verticalBlur);
            material.SetInt(ThicknessId, thicknessPx);
            material.SetInt(BlurId, blurPx);
            material.SetInt(MaxRadiusId, thicknessPx + blurPx);
        }

        private class PassData
        {
            internal TextureHandle src;
            internal Material material;
        }

        private static void ExecutePass(PassData data, RasterGraphContext context, int pass)
        {
            Blitter.BlitTexture(context.cmd, data.src, m_ScaleBias, data.material, pass);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph,
        ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // The following line ensures that the render pass doesn't blit
            // from the back buffer.
            if (resourceData.isActiveTargetBackBuffer)
                return;

            // Set the blur texture size to be the same as the camera target size.
            blurTextureDescriptor.width = cameraData.cameraTargetDescriptor.width;
            blurTextureDescriptor.height = cameraData.cameraTargetDescriptor.height;
            blurTextureDescriptor.depthBufferBits = 0;

            TextureHandle srcCamColor = resourceData.activeColorTexture;
            TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(renderGraph,
                blurTextureDescriptor, k_BlurTextureName, false);

            // Update the blur settings in the material
            UpdateBlurSettings();

            // This check is to avoid an error from the material preview in the scene
            if (!srcCamColor.IsValid() || !dst.IsValid())
                return;

            // Vertical blur pass
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_VerticalPassName,
                out var passData))
            {
                // Configure pass data
                passData.src = srcCamColor;
                passData.material = material;

                // Configure render graph input and output
                builder.UseTexture(passData.src);
                builder.SetRenderAttachment(dst, 0);

                // Blit from the camera color to the render graph texture,
                // using the first shader pass.
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context, 0));
            }

            // Horizontal blur pass
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_HorizontalPassName, out var passData))
            {
                // Configure pass data
                passData.src = dst;
                passData.material = material;

                // Use the output of the previous pass as the input
                builder.UseTexture(passData.src);

                // Use the input texture of the previous pass as the output
                builder.SetRenderAttachment(srcCamColor, 0);

                // Blit from the render graph texture to the camera color,
                // using the second shader pass.
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context, 1));
            }
        }
    }
}
