// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using UnityEngine;

    internal sealed class FPVideoUnwrapRenderer : IDisposable
    {
        private readonly Material material;
        private readonly Material resolve;
        private FPVideoFarneback flow;
        private FPVideoUnwrapSettings historySettings;
        internal void ResetTemporal() => flow?.ResetTemporal();
        internal const float FlowBandDegrees = 32, FlowVerticalDegrees = 160;
        internal RenderTexture Output { get; private set; }
        internal RenderTexture Working { get; private set; }
        internal static bool SupportsHighPrecision => SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
        internal string WorkingPrecision => Working != null ? "FP16 linear RGB" : "8-bit SDR fallback";
        internal RenderTexture FlowField => flow?.Result;
        internal FPVideoUnwrapRenderer()
        {
            var shader = Shader.Find("Hidden/FuzzPhyte/VideoUnwrap");
            if (shader == null || !shader.isSupported) throw new NotSupportedException("Video unwrap shader is missing or unsupported on this graphics device.");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var resolveShader = Shader.Find("Hidden/FuzzPhyte/VideoColorResolve");
            if (resolveShader == null || !resolveShader.isSupported) { UnityEngine.Object.DestroyImmediate(material); throw new NotSupportedException("Video color resolve shader is missing or unsupported."); }
            resolve = new Material(resolveShader) { hideFlags = HideFlags.HideAndDontSave };
        }
        internal void Render(Texture source, FPVideoUnwrapSettings settings, int width, int height,
            FPVideoCalibrationView view = FPVideoCalibrationView.Composite, float overlayMix = .5f, float differenceGain = 4,
            double sourceSeconds = double.NaN, double frameSeconds = double.NaN)
        {
            if (Output == null || Output.width != width || Output.height != height)
            {
                ReleaseOutput();
                Output = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.HideAndDontSave };
                Output.Create();
                if (SupportsHighPrecision)
                {
                    Working = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { hideFlags = HideFlags.HideAndDontSave };
                    if (!Working.Create()) { ReleaseOutput(); throw new NotSupportedException("FP16 video working texture allocation failed."); }
                }
            }
            material.SetFloat("_LinearOutput", Working != null ? 1 : 0);
            material.SetFloat("_SourceProjection", (int)settings.sourceProjection);
            material.SetFloat("_Projection", (int)settings.projection);
            material.SetFloat("_Aspect", (float)settings.width / settings.height);
            material.SetFloat("_HorizontalFov", settings.horizontalFov);
            material.SetFloat("_VerticalFov", settings.verticalFov);
            material.SetFloat("_LensFov", settings.lensFov);
            material.SetFloat("_RightLensFov", settings.independentRightLens ? settings.rightLensFov : settings.lensFov);
            material.SetFloat("_Blend", settings.blendDegrees);
            material.SetVector("_LeftCenter", settings.leftCenter);
            material.SetVector("_RightCenter", settings.rightCenter);
            material.SetVector("_LensRadius", settings.lensRadius);
            material.SetVector("_RightLensRadius", settings.independentRightLens ? settings.rightLensRadius : settings.lensRadius);
            material.SetFloat("_DiagnosticMode", (int)view);
            material.SetFloat("_OverlayMix", Mathf.Clamp01(overlayMix));
            material.SetFloat("_DifferenceGain", Mathf.Clamp(differenceGain, 1, 16));
            material.SetFloat("_DirectionalSeam", settings.useDirectionalSeam ? 1 : 0);
            material.SetFloat("_SeamWidth", settings.seamWidth);
            material.SetVector("_SeamNormal", Quaternion.Euler(0, settings.seamYaw, 0) * Vector3.forward);
            material.SetMatrix("_ViewRotation", Matrix4x4.Rotate(Quaternion.Euler(settings.rotation)));
            material.SetMatrix("_LeftRotation", Matrix4x4.Rotate(Quaternion.Inverse(Quaternion.Euler(settings.leftRotation))));
            material.SetMatrix("_RightRotation", Matrix4x4.Rotate(Quaternion.Inverse(Quaternion.Euler(settings.rightRotation))));
            FPVideoFactoryCalibration.Bind(material, settings);
            material.SetFloat("_FlipX", settings.flipX ? 1 : 0); material.SetFloat("_FlipY", settings.flipY ? 1 : 0);
            bool grade = settings.colorTuning && view == FPVideoCalibrationView.Composite &&
                (settings.colorExposure != 0 || settings.colorTemperature != 0 || settings.colorTint != 0 || settings.colorContrast != 1 || settings.colorSaturation != 1 ||
                (settings.colorCurve != FPVideoToneCurve.Neutral && settings.colorCurveStrength > 0 &&
                    (settings.colorCurve != FPVideoToneCurve.CustomGamma || settings.colorGamma != 1)));
            material.SetFloat("_GradeEnabled", grade ? 1 : 0);
            material.SetVector("_GradeParams", new Vector4(settings.colorExposure,settings.colorContrast,settings.colorSaturation,0));
            material.SetVector("_GradeBalance", new Vector4(settings.colorTemperature,settings.colorTint,0,0));
            material.SetVector("_GradeCurve", new Vector4((int)settings.colorCurve,settings.colorCurveStrength,settings.colorGamma,0));
            material.SetFloat("_FlowEnabled", 0); material.SetFloat("_FlowChartInput", 0);
            material.SetFloat("_FlowBand", FlowBandDegrees); material.SetFloat("_FlowVertical", FlowVerticalDegrees);
            material.SetFloat("_SeamYaw", settings.seamYaw);
            if (!settings.opticalFlow && flow != null) { flow.Dispose(); flow = null; }
            if (settings.opticalFlow && (view >= FPVideoCalibrationView.FlowVectors || (view == FPVideoCalibrationView.Composite && settings.flowStrength > 0)))
            {
                settings.Validate(); flow ??= new FPVideoFarneback();
                int chartWidth = Mathf.CeilToInt(settings.flowResolution * FlowBandDegrees / FlowVerticalDegrees / 8) * 8;
                flow.EnsureSize(chartWidth * 2, settings.flowResolution, 2);
                bool write = GL.sRGBWrite;
                try
                {
                    GL.sRGBWrite = false; material.SetFloat("_FlowChartInput", 1);
                    material.SetFloat("_FlowChartLens", 0); Graphics.Blit(source, flow.InputA, material);
                    material.SetFloat("_FlowChartLens", 1); Graphics.Blit(source, flow.InputB, material);
                }
                finally { GL.sRGBWrite = write; material.SetFloat("_FlowChartInput", 0); }
                flow.SolveCurrent(new Vector2(settings.flowMaxWarpDegrees * chartWidth / FlowBandDegrees,
                    settings.flowMaxWarpDegrees * settings.flowResolution / FlowVerticalDegrees), settings.flowIterations);
                if (!settings.SameFlowHistory(historySettings)) { flow.ResetTemporal(); historySettings = settings.Snapshot(); }
                if (settings.flowTemporalSmoothing)
                    flow.Smooth(sourceSeconds,frameSeconds,settings.flowSmoothingSeconds,settings.flowMinConfidence,
                        new Vector2(settings.flowTemporalRejectDegrees * chartWidth / FlowBandDegrees,
                            settings.flowTemporalRejectDegrees * settings.flowResolution / FlowVerticalDegrees));
                else flow.DisableTemporal();
                material.SetTexture("_SeamFlow", flow.Result);
                material.SetVector("_FlowAtlas", new Vector4(chartWidth * 2, settings.flowResolution, chartWidth, settings.flowMaxWarpDegrees));
                material.SetFloat("_FlowStrength", settings.flowStrength); material.SetFloat("_FlowThreshold", settings.flowMinConfidence);
                material.SetFloat("_FlowEnabled", 1);
            }
            else ResetTemporal();
            bool previous = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = Working == null && QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, Working != null ? Working : Output, material);
                if (Working != null)
                {
                    GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                    Graphics.Blit(Working, Output, resolve);
                }
            }
            finally { GL.sRGBWrite = previous; }
        }
        internal void ReadBack(Texture2D destination, bool uploadToGpu = true)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = Output;
                destination.ReadPixels(new Rect(0, 0, Output.width, Output.height), 0, 0, false);
                // Encoding consumes CPU pixels; avoid uploading that image back to the GPU just to encode it.
                if (uploadToGpu) destination.Apply(false);
            }
            finally { RenderTexture.active = previous; }
        }
        internal void ReadWorking(Texture2D destination)
        {
            if (Working == null) throw new NotSupportedException("FP16 video processing is unavailable on this graphics device.");
            if (destination == null || destination.format != TextureFormat.RGBAHalf || destination.width != Working.width || destination.height != Working.height)
                throw new ArgumentException("Working readback requires a matching, linear RGBAHalf texture.");
            var previous = RenderTexture.active;
            try { RenderTexture.active = Working; destination.ReadPixels(new Rect(0, 0, Working.width, Working.height), 0, 0, false); }
            finally { RenderTexture.active = previous; }
        }
        private void ReleaseOutput()
        {
            if (Working != null) { Working.Release(); UnityEngine.Object.DestroyImmediate(Working); Working = null; }
            if (Output != null) { Output.Release(); UnityEngine.Object.DestroyImmediate(Output); Output = null; }
        }
        public void Dispose() { flow?.Dispose(); flow = null; ReleaseOutput(); UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(resolve); }
    }
}
