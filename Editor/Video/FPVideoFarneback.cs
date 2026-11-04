// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>Owned GPU polynomial-expansion flow; all Unity submissions occur on the Editor thread.</summary>
    internal sealed class FPVideoFarneback : IDisposable
    {
        internal const string MethodVersion = "FP Farneback polynomial v1";
        private sealed class Level
        {
            internal int Width, Height, Charts;
            internal RenderTexture A, B, A0, A1, B0, B1, F0, F1, G, H, GX, HX;
            internal IEnumerable<RenderTexture> Textures { get { yield return A; yield return B; yield return A0; yield return A1; yield return B0; yield return B1; yield return F0; yield return F1; yield return G; yield return H; yield return GX; yield return HX; } }
        }
        private readonly ComputeShader shader;
        private readonly ComputeBuffer inverse;
        private readonly int downsample, fit, initialize, tensors, horizontal, solve, confidence, temporal;
        private readonly List<Level> levels = new List<Level>();
        private RenderTexture forward, backward;
        private RenderTexture historyFlow, historyImage, temporalOutput;
        private double historySeconds = double.NaN;
        internal RenderTexture Result { get; private set; }
        internal RenderTexture InputA => levels[0].A;
        internal RenderTexture InputB => levels[0].B;
        internal static bool Supported => SystemInfo.supportsComputeShaders && SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBFloat);

        internal FPVideoFarneback()
        {
            if (!Supported) throw new NotSupportedException("Seam flow requires compute shaders and float32 random-write textures on this graphics device.");
            ComputeShader asset = null;
            foreach (string guid in AssetDatabase.FindAssets("FPVideoFarneback t:ComputeShader"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "FPVideoFarneback.compute") { asset = AssetDatabase.LoadAssetAtPath<ComputeShader>(path); break; }
            }
            if (asset == null) throw new FileNotFoundException("FPVideoFarneback.compute could not be located.");
            shader = UnityEngine.Object.Instantiate(asset); shader.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                downsample = shader.FindKernel("Downsample"); fit = shader.FindKernel("FitPolynomial"); initialize = shader.FindKernel("InitializeFlow");
                tensors = shader.FindKernel("FormTensors"); horizontal = shader.FindKernel("BlurHorizontal"); solve = shader.FindKernel("SolveVertical"); confidence = shader.FindKernel("Confidence");
                temporal = shader.FindKernel("TemporalSmooth");
                inverse = new ComputeBuffer(36, sizeof(float)); inverse.SetData(FitInverse()); shader.SetBuffer(fit, "_FitInverse", inverse);
            }
            catch { Dispose(); throw; }
        }

        internal void EnsureSize(int width, int height, int charts)
        {
            if (charts < 1 || charts > 2 || width % charts != 0 || width / charts < 32 || height < 32 || width > 2048 || height > 1024)
                throw new ArgumentException("Flow requires one or two independent charts, each at least 32 pixels wide, within 2048×1024.");
            if (levels.Count > 0 && levels[0].Width == width && levels[0].Height == height && levels[0].Charts == charts) return;
            ReleaseTextures();
            try
            {
                int chartWidth = width / charts;
                for (int i = 0; i < 4 && chartWidth >= 8 && height >= 16; i++, chartWidth /= 2, height /= 2)
                {
                    var l = new Level { Width = chartWidth * charts, Height = height, Charts = charts }; levels.Add(l);
                    l.A = Create(l.Width,l.Height); l.B = Create(l.Width,l.Height);
                    l.A0 = Create(l.Width,l.Height); l.A1 = Create(l.Width,l.Height); l.B0 = Create(l.Width,l.Height); l.B1 = Create(l.Width,l.Height);
                    l.F0 = Create(l.Width,l.Height); l.F1 = Create(l.Width,l.Height); l.G = Create(l.Width,l.Height); l.H = Create(l.Width,l.Height);
                    l.GX = Create(l.Width,l.Height); l.HX = Create(l.Width,l.Height);
                }
                var full = levels[0]; forward = Create(full.Width,full.Height); backward = Create(full.Width,full.Height); Result = Create(full.Width,full.Height);
            }
            catch { ReleaseTextures(); throw; }
        }

        internal void Solve(Texture a, Texture b, int charts, Vector2 maximumPixels, int iterations = 5)
        {
            if (a.width != b.width || a.height != b.height) throw new ArgumentException("Flow image dimensions must match.");
            EnsureSize(a.width,a.height,charts);
            bool previous = GL.sRGBWrite;
            try { GL.sRGBWrite = false; Graphics.Blit(a,InputA); Graphics.Blit(b,InputB); }
            finally { GL.sRGBWrite = previous; }
            SolveCurrent(maximumPixels,iterations);
        }

        internal void SolveCurrent(Vector2 maximumPixels, int iterations = 5)
        {
            if (levels.Count == 0 || iterations < 1 || iterations > 8 || !FinitePositive(maximumPixels.x) || !FinitePositive(maximumPixels.y))
                throw new ArgumentException("Flow requires initialized inputs, 1–8 iterations and finite positive displacement bounds.");
            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i]; SetSize(l);
                if (i > 0)
                {
                    var parent = levels[i-1]; SetSourceSize(parent);
                    shader.SetTexture(downsample,"_Image",parent.A); shader.SetTexture(downsample,"_WriteImage",l.A); Dispatch(downsample,l);
                    shader.SetTexture(downsample,"_Image",parent.B); shader.SetTexture(downsample,"_WriteImage",l.B); Dispatch(downsample,l);
                }
                shader.SetTexture(fit,"_Image",l.A); shader.SetTexture(fit,"_WritePoly0",l.A0); shader.SetTexture(fit,"_WritePoly1",l.A1); Dispatch(fit,l);
                shader.SetTexture(fit,"_Image",l.B); shader.SetTexture(fit,"_WritePoly0",l.B0); shader.SetTexture(fit,"_WritePoly1",l.B1); Dispatch(fit,l);
            }
            Graphics.CopyTexture(SolveDirection(false,maximumPixels,iterations),forward);
            Graphics.CopyTexture(SolveDirection(true,maximumPixels,iterations),backward);
            var full = levels[0]; SetSize(full);
            shader.SetTexture(confidence,"_Forward",forward); shader.SetTexture(confidence,"_Backward",backward);
            shader.SetTexture(confidence,"_ImageA",full.A); shader.SetTexture(confidence,"_ImageB",full.B); shader.SetTexture(confidence,"_FlowWrite",Result);
            shader.SetFloat("_Consistency",1.5f); shader.SetFloat("_PhotoTolerance",.12f); Dispatch(confidence,full);
        }

        internal void ResetTemporal() => historySeconds = double.NaN;

        internal void DisableTemporal()
        {
            ResetTemporal(); Release(historyFlow); Release(historyImage); Release(temporalOutput);
            historyFlow = historyImage = temporalOutput = null;
        }

        internal void Smooth(double seconds, double frameSeconds, float responseSeconds, float minimumConfidence, Vector2 rejectPixels)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || !(frameSeconds > 0) || double.IsInfinity(frameSeconds))
            { ResetTemporal(); return; }
            if (!FinitePositive(responseSeconds) || !FinitePositive(rejectPixels.x) || !FinitePositive(rejectPixels.y))
                throw new ArgumentException("Temporal response and rejection bounds must be finite and positive.");
            var full = levels[0];
            if (historyFlow == null)
            {
                try
                {
                    historyFlow = Create(full.Width,full.Height); historyImage = Create(full.Width,full.Height);
                    temporalOutput = Create(full.Width,full.Height);
                }
                catch { DisableTemporal(); throw; }
            }
            double delta = seconds-historySeconds;
            if (Math.Abs(delta) < frameSeconds*.001)
            {
                // Repainting a paused frame must not integrate history again.
                Graphics.CopyTexture(historyFlow,Result); return;
            }
            if (Math.Abs(delta-frameSeconds) <= frameSeconds*.01)
            {
                SetSize(full);
                shader.SetTexture(temporal,"_Forward",Result); shader.SetTexture(temporal,"_HistoryFlow",historyFlow);
                shader.SetTexture(temporal,"_HistoryImage",historyImage); shader.SetTexture(temporal,"_ImageA",full.A);
                shader.SetTexture(temporal,"_ImageB",full.B); shader.SetTexture(temporal,"_FlowWrite",temporalOutput);
                shader.SetFloat("_HistoryWeight",(float)Math.Exp(-delta/responseSeconds));
                shader.SetFloat("_TemporalConfidence",minimumConfidence);
                shader.SetVector("_TemporalRejectPixels",rejectPixels);
                Dispatch(temporal,full); Graphics.CopyTexture(temporalOutput,Result);
            }
            Graphics.CopyTexture(Result,historyFlow); Graphics.CopyTexture(full.A,historyImage); historySeconds = seconds;
        }

        private RenderTexture SolveDirection(bool reverse, Vector2 maximumPixels, int iterations)
        {
            RenderTexture coarse = null; Level coarseLevel = null;
            for (int i = levels.Count-1; i >= 0; i--)
            {
                var l = levels[i]; SetSize(l);
                shader.SetVector("_MaxFlowPixels",new Vector4(maximumPixels.x*l.Width/levels[0].Width,maximumPixels.y*l.Height/levels[0].Height,0,0));
                shader.SetInt("_HasCoarse",coarse == null ? 0 : 1); if (coarseLevel != null) SetSourceSize(coarseLevel);
                shader.SetTexture(initialize,"_CoarseFlow",coarse == null ? (Texture)Texture2D.blackTexture : coarse);
                shader.SetTexture(initialize,"_FlowWrite",l.F0); Dispatch(initialize,l);
                shader.SetTexture(tensors,"_PolyA0",reverse ? l.B0 : l.A0); shader.SetTexture(tensors,"_PolyA1",reverse ? l.B1 : l.A1);
                shader.SetTexture(tensors,"_PolyB0",reverse ? l.A0 : l.B0); shader.SetTexture(tensors,"_PolyB1",reverse ? l.A1 : l.B1);
                var current = l.F0; var next = l.F1;
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    shader.SetTexture(tensors,"_FlowRead",current); shader.SetTexture(tensors,"_WriteTensor0",l.G); shader.SetTexture(tensors,"_WriteTensor1",l.H); Dispatch(tensors,l);
                    shader.SetTexture(horizontal,"_Tensor0",l.G); shader.SetTexture(horizontal,"_Tensor1",l.H);
                    shader.SetTexture(horizontal,"_WriteTensor0",l.GX); shader.SetTexture(horizontal,"_WriteTensor1",l.HX); Dispatch(horizontal,l);
                    shader.SetTexture(solve,"_Tensor0",l.GX); shader.SetTexture(solve,"_Tensor1",l.HX); shader.SetTexture(solve,"_FlowWrite",next); Dispatch(solve,l);
                    var swap = current; current = next; next = swap;
                }
                coarse = current; coarseLevel = l;
            }
            return coarse;
        }
        private void SetSize(Level l) => shader.SetInts("_Size",l.Width,l.Height,l.Width/l.Charts,l.Charts);
        private void SetSourceSize(Level l) => shader.SetInts("_SourceSize",l.Width,l.Height,l.Width/l.Charts,l.Charts);
        private void Dispatch(int kernel, Level l) => shader.Dispatch(kernel,(l.Width+7)/8,(l.Height+7)/8,1);
        private static bool FinitePositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
        private static RenderTexture Create(int width, int height)
        {
            var texture = new RenderTexture(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear)
            { enableRandomWrite = true, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!texture.Create()) { UnityEngine.Object.DestroyImmediate(texture); throw new NotSupportedException("Could not allocate float32 flow texture."); }
            return texture;
        }
        private static void Release(RenderTexture texture) { if (texture == null) return; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
        private void ReleaseTextures()
        {
            DisableTemporal();
            foreach (var level in levels) foreach (var texture in level.Textures) Release(texture);
            levels.Clear(); Release(forward); Release(backward); Release(Result); forward = backward = Result = null;
        }
        public void Dispose() { ReleaseTextures(); inverse?.Dispose(); if (shader != null) UnityEngine.Object.DestroyImmediate(shader); }

        private static float[] FitInverse()
        {
            var matrix = new double[6,12];
            for (int y=-2;y<=2;y++) for (int x=-2;x<=2;x++)
            {
                double[] basis = {1,x,y,x*x,y*y,x*y}; double weight = Math.Exp(-(x*x+y*y)/(2*1.1*1.1));
                for (int i=0;i<6;i++) for (int j=0;j<6;j++) matrix[i,j]+=weight*basis[i]*basis[j];
            }
            for (int i=0;i<6;i++) matrix[i,i+6]=1;
            for (int i=0;i<6;i++)
            {
                int pivot=i; for (int p=i+1;p<6;p++) if (Math.Abs(matrix[p,i])>Math.Abs(matrix[pivot,i])) pivot=p;
                if (Math.Abs(matrix[pivot,i])<1e-12) throw new InvalidOperationException("Singular polynomial fit matrix.");
                for (int j=0;j<12;j++) { double temp=matrix[i,j];matrix[i,j]=matrix[pivot,j];matrix[pivot,j]=temp; }
                double divisor=matrix[i,i]; for (int j=0;j<12;j++) matrix[i,j]/=divisor;
                for (int p=0;p<6;p++) if (p!=i) { double multiplier=matrix[p,i];for(int j=0;j<12;j++) matrix[p,j]-=multiplier*matrix[i,j]; }
            }
            var result=new float[36];for(int i=0;i<6;i++)for(int j=0;j<6;j++)result[i*6+j]=(float)matrix[i,j+6];return result;
        }
    }
}
