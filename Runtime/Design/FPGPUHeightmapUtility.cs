namespace FuzzPhyte.Utility
{
    using UnityEngine;

    /// <summary>
    /// Utilities for creating and exporting GPU-backed heightmap working textures.
    /// </summary>
    public static class FPGPUHeightmapUtility
    {
        private const string BrushShaderName = "Hidden/FuzzPhyte/HeightmapBrushStamp";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BrushTexId = Shader.PropertyToID("_BrushTex");
        private static readonly int BrushCenterId = Shader.PropertyToID("_BrushCenter");
        private static readonly int TextureSizeId = Shader.PropertyToID("_TextureSize");
        private static readonly int BrushRadiusId = Shader.PropertyToID("_BrushRadius");
        private static readonly int BrushSoftnessId = Shader.PropertyToID("_BrushSoftness");
        private static readonly int BrushStrengthId = Shader.PropertyToID("_BrushStrength");
        private static readonly int BrushSetValueId = Shader.PropertyToID("_BrushSetValue");
        private static readonly int BrushRotationRadId = Shader.PropertyToID("_BrushRotationRad");
        private static readonly int BrushModeId = Shader.PropertyToID("_BrushMode");
        private static readonly int UseBrushTexId = Shader.PropertyToID("_UseBrushTex");
        private static readonly int DebugModeId = Shader.PropertyToID("_DebugMode");

        private static Material brushStampMaterial;

        public static RenderTexture CreateWorkingCopy(Texture source, string name = "FP_GPUHeightmap")
        {
            if (source == null)
            {
                return null;
            }

            int width = source.width;
            int height = source.height;
            var renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            renderTexture.Create();

            Graphics.Blit(source, renderTexture);
            return renderTexture;
        }

        public static Texture2D ReadbackToTexture2D(RenderTexture source, bool apply = true)
        {
            if (source == null)
            {
                return null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;

            Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false)
            {
                name = $"{source.name}_Readback"
            };
            texture.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
            if (apply)
            {
                texture.Apply(false, false);
            }

            RenderTexture.active = previous;
            return texture;
        }

        public static bool ApplyBrushStroke(
            RenderTexture target,
            Texture brushMask,
            Vector2 brushCenterUv,
            float brushRadiusPixels,
            float brushSoftness,
            float brushStrength,
            float brushSetValue,
            float brushRotationDegrees,
            int brushMode,
            int debugMode = 0)
        {
            if (target == null)
            {
                return false;
            }

            Material material = GetBrushStampMaterial();
            if (material == null)
            {
                return false;
            }

            var temp = RenderTexture.GetTemporary(target.descriptor);
            temp.name = $"{target.name}_BrushTemp";
            Graphics.Blit(target, temp);

            if (debugMode == 1)
            {
                Graphics.Blit(temp, target);
                RenderTexture.ReleaseTemporary(temp);
                return true;
            }

            material.SetTexture(MainTexId, temp);
            material.SetTexture(BrushTexId, brushMask != null ? brushMask : Texture2D.whiteTexture);
            material.SetVector(BrushCenterId, new Vector4(brushCenterUv.x, brushCenterUv.y, 0f, 0f));
            material.SetVector(TextureSizeId, new Vector4(target.width, target.height, 0f, 0f));
            material.SetFloat(BrushRadiusId, Mathf.Max(1f, brushRadiusPixels));
            material.SetFloat(BrushSoftnessId, Mathf.Clamp01(brushSoftness));
            material.SetFloat(BrushStrengthId, Mathf.Clamp01(brushStrength));
            material.SetFloat(BrushSetValueId, Mathf.Clamp01(brushSetValue));
            material.SetFloat(BrushRotationRadId, brushRotationDegrees * Mathf.Deg2Rad);
            material.SetFloat(BrushModeId, brushMode);
            material.SetFloat(UseBrushTexId, brushMask != null ? 1f : 0f);
            material.SetFloat(DebugModeId, debugMode);

            bool previousSrgbWrite = GL.sRGBWrite;
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(temp, target, material, 0);
            GL.sRGBWrite = previousSrgbWrite;
            RenderTexture.ReleaseTemporary(temp);
            return true;
        }

        public static void Release(RenderTexture renderTexture)
        {
            if (renderTexture == null)
            {
                return;
            }

            if (renderTexture.IsCreated())
            {
                renderTexture.Release();
            }

            Object.DestroyImmediate(renderTexture);
        }

        public static void ReleaseResources()
        {
            if (brushStampMaterial != null)
            {
                Object.DestroyImmediate(brushStampMaterial);
                brushStampMaterial = null;
            }
        }

        private static Material GetBrushStampMaterial()
        {
            if (brushStampMaterial != null)
            {
                return brushStampMaterial;
            }

            Shader shader = Shader.Find(BrushShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[FP GPU Heightmap Utility] Could not find shader '{BrushShaderName}'.");
                return null;
            }

            brushStampMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return brushStampMaterial;
        }
    }
}
