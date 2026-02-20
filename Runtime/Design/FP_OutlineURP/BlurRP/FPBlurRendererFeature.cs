namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;
    using UnityEngine.Rendering.Universal;

    /// <summary>
    /// This code was originally based on the works of the Unity-Technologies GitHub 'Per-Object_Outline_RenderGraph_RenderFeature_Example'
    /// https://github.com/Unity-Technologies/Per-Object_Outline_RenderGraph_RendererFeature_Example
    /// This has been modified for additional usage associated with FuzzPhyte Packages
    /// </summary>
    public class FPBlurRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private BlurSettings settings;
        [SerializeField] private Shader shader;
        private Material material;
        private FPBlurRenderPass blurRenderPass;

        public override void Create()
        {
            if (shader == null)
            {
                return;
            }
            material = new Material(shader);
            blurRenderPass = new FPBlurRenderPass(material, settings);

            blurRenderPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                renderer.EnqueuePass(blurRenderPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
#else
                Destroy(material);
#endif
        }
    }

    [Serializable]
    public class BlurSettings
    {
        [Range(0, 0.4f)] public float horizontalBlur;
        [Range(0, 0.4f)] public float verticalBlur;
        [Range(0, 50)] public int outlineThicknessPx;
        [Range(0, 50)] public int outlineBlurPx;
    }
}