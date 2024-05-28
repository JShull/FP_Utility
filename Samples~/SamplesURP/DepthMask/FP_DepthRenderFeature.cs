using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FuzzPhyte.Utility.Samples.Renderer
{
    public class FP_DepthRenderFeature : ScriptableRendererFeature
    {
        class DepthMaskRenderPass : ScriptableRenderPass
        {
            private Material depthMaskMaterial;
            private RTHandle source;
            private RTHandle tempTexture;

            public DepthMaskRenderPass(Material material)
            {
                this.depthMaskMaterial = material;
                tempTexture = RTHandles.Alloc("_TemporaryDepthMaskTexture", name: "_TemporaryDepthMaskTexture");
            }

            public void Setup(RTHandle source)
            {
                this.source = source;
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                tempTexture = RTHandles.Alloc(cameraTextureDescriptor);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("DepthMaskPass");

                Blitter.BlitCameraTexture(cmd, source, tempTexture, depthMaskMaterial, 0);
                Blitter.BlitCameraTexture(cmd, tempTexture, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                RTHandles.Release(tempTexture);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (tempTexture != null)
                {
                    RTHandles.Release(tempTexture);
                    tempTexture = null;
                }
            }
        }

        [System.Serializable]
        public class DepthMaskSettings
        {
            public Material depthMaskMaterial = null;
        }

        public DepthMaskSettings settings = new DepthMaskSettings();
        DepthMaskRenderPass depthMaskPass;

        public override void Create()
        {
            depthMaskPass = new DepthMaskRenderPass(settings.depthMaskMaterial)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.depthMaskMaterial == null)
            {
                Debug.LogWarningFormat("Missing Depth Mask Material. {0} render pass will not execute. Check for missing reference in the assigned renderer.", GetType().Name);
                return;
            }
            depthMaskPass.Setup(renderer.cameraColorTargetHandle);
            renderer.EnqueuePass(depthMaskPass);
        }

    }
}