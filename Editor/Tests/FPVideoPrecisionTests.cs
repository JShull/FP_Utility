// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class FPVideoPrecisionTests
    {
        private static float Ramp(int x) => (float)(x * 876 / 1023) / 876;
        private static Texture2D Source()
        {
            var texture = new Texture2D(1024, 512, TextureFormat.RGBAFloat, false, true);
            var pixels = new Color[1024 * 512];
            for (int y = 0; y < 512; y++) for (int x = 0; x < 1024; x++) pixels[y * 1024 + x] = new Color(Ramp(x), Ramp(x), Ramp(x), 1);
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        private static FPVideoUnwrapSettings Settings() => new FPVideoUnwrapSettings { width = 1024, height = 512, sourceProjection = FPVideoSourceProjection.Equirectangular };
        private static void RequirePrecision() { if (!FPVideoUnwrapRenderer.SupportsHighPrecision) Assert.Ignore("FP16 rendering is unsupported on this graphics device."); }
        private static int Levels(Color[] pixels, int width, int y)
        {
            var levels = new HashSet<float>(); for (int x = 0; x < width; x++) levels.Add(pixels[y * width + x].r); return levels.Count;
        }
        [Test]
        public void FloatingWorkingBufferRetainsSubByteLevelsBeforeSdrResolve()
        {
            RequirePrecision(); var source = Source(); var working = new Texture2D(1024, 512, TextureFormat.RGBAHalf, false, true);
            try
            {
                using (var renderer = new FPVideoUnwrapRenderer())
                {
                    renderer.Render(source, Settings(), 1024, 512); renderer.ReadWorking(working);
                    var pixels = working.GetPixels(); Assert.That(Levels(pixels, 1024, 256), Is.GreaterThanOrEqualTo(800));
                    for (int x = 4; x < 1020; x++)
                    {
                        float expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? Ramp(x) : Mathf.GammaToLinearSpace(Ramp(x));
                        Assert.That(pixels[256 * 1024 + x].r, Is.EqualTo(expected).Within(.002), "Linear working value at column " + x);
                    }
                    Assert.That(Levels(FPVideoFarnebackTests.Read(renderer.Output), 1024, 256), Is.LessThanOrEqualTo(256));
                    Assert.That(renderer.Working.format, Is.EqualTo(RenderTextureFormat.ARGBHalf));
                    Assert.That(renderer.Output.format, Is.EqualTo(RenderTextureFormat.ARGB32));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(working); }
        }
        [Test]
        public void ExrRoundTripPreservesHighPrecisionAndReportsItsActualSource()
        {
            RequirePrecision(); var source = Source();
            string asset = "Assets/FPVideoPrecision_" + Guid.NewGuid().ToString("N") + ".exr";
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, asset);
            try
            {
                FPVideoExrExport.WriteTexture(source, Settings(), path, "Synthetic 877-level linear reference", 2.5, "Synthetic FP32; not native video decode");
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(asset);
                importer.sRGBTexture = false; importer.isReadable = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                var platform = importer.GetDefaultPlatformTextureSettings(); platform.format = TextureImporterFormat.RGBAHalf; importer.SetPlatformTextureSettings(platform); importer.SaveAndReimport();
                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
                Assert.That(imported.width, Is.EqualTo(1024)); Assert.That(imported.height, Is.EqualTo(512));
                var pixels = imported.GetPixels(); Assert.That(Levels(pixels, 1024, 256), Is.GreaterThanOrEqualTo(800));
                float expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? Ramp(512) : Mathf.GammaToLinearSpace(Ramp(512));
                Assert.That(pixels[256 * 1024 + 512].r, Is.EqualTo(expected).Within(.002));
                string report = File.ReadAllText(path + ".json");
                Assert.That(report, Does.Contain("Synthetic FP32")); Assert.That(report, Does.Contain("FP16 linear RGB")); Assert.That(report, Does.Contain("2.5"));
                Assert.Throws<IOException>(() => FPVideoExrExport.WriteTexture(source, Settings(), path, "overwrite", 0, "unknown"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source); AssetDatabase.DeleteAsset(asset);
                AssetDatabase.DeleteAsset(asset + ".json");
                if (File.Exists(path + ".json")) File.Delete(path + ".json");
            }
        }
        [Test]
        public void NativeExrFrameReportsEightBitIngestAndUsesFullResolution()
        {
            RequirePrecision(); string folder = Path.Combine(Path.GetTempPath(), "FPVideoExr_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                string input = Path.Combine(folder, "input.mp4"), output = Path.Combine(folder, "frame.exr"); FPVideoUnwrapTests.CreateFixture(input);
                using (var source = new FPVideoNativeSource(input))
                    FPVideoExrExport.Write(source, new FPVideoUnwrapSettings { width = 320, height = 160, sourceProjection = FPVideoSourceProjection.Equirectangular }, .4, output);
                byte[] bytes = File.ReadAllBytes(output); Assert.That(BitConverter.ToUInt32(bytes, 0), Is.EqualTo(20000630u));
                var report = File.ReadAllText(output + ".json"); Assert.That(report, Does.Contain("Native video decode: 8-bit"));
                Assert.That(report, Does.Contain("\"width\": 320")); Assert.That(report, Does.Contain("\"sourceSeconds\": 0.4"));
                Assert.That(Directory.GetFiles(folder, "*.partial*"), Is.Empty);
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
