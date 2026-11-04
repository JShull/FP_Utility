// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.IO;
    using System.Reflection;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class FPVideoFactoryCalibrationTests
    {
        // Calibration only from the user's X6. No serial number, location or camera media is bundled.
        internal const string X6 = "2_2.455430_7197.990_7197.990_3857.430_3882.430_0.111_0.587_89.969_0.000000_0.000000_0.000000_1.25771189_-0.41751385_-2.32294750_24.16248894_0.00000000_0.00129065_0.00375834_0.00118109_-0.00433409_-0.00333254_-0.00348473_0.03142127_0.02579573_15488_7744_193_2.455430_7237.600_7237.600_11612.700_3876.060_-0.080_0.276_89.129_-0.000173_-0.000123_-0.032631_1.29791701_-1.30203509_4.20809793_5.29687166_0.00000000_-0.00019950_-0.00101663_0.00682494_0.00928172_-0.00057409_0.00078010_-0.01744008_-0.00634739_15488_7744_193_394240";

        [Test]
        public void FactoryProjectionMatchesIndependentDoublePrecisionReference()
        {
            var model = FPVideoFactoryCalibration.Parse(X6, 4);
            var rays = new[] { Vector3.forward, new Vector3(1, 0, .1f), new Vector3(-1, .1f, .1f), new Vector3(.3f, .2f, 1), new Vector3(-.2f, -.4f, 1) };
            // NumPy double rotation matrices and scalar unified-sphere/radtan equations; see plan provenance.
            var expected = new[] {
                new Vector2(.2504383849f,.4990980675f), new Vector2(.4659407021f,.4928108859f), new Vector2(.0362250356f,.5486533361f), new Vector2(.2901644350f,.5503830631f), new Vector2(.2238244600f,.3967300203f),
                new Vector2(.7504372153f,.4991742918f), new Vector2(.5362689112f,.4984785628f), new Vector2(.9645261148f,.5410032080f), new Vector2(.7113700966f,.5512350671f), new Vector2(.7760299518f,.3963412856f) };
            for (int track = 0; track < 2; track++) for (int i = 0; i < rays.Length; i++)
            {
                Vector3 ray = rays[i]; if (track == 1) ray.z = -ray.z;
                Vector2 uv = model.Project(ray, track), reference = expected[track * rays.Length + i];
                Assert.That(Math.Abs(uv.x - reference.x) * 7680, Is.LessThan(.02), "Horizontal projection error");
                Assert.That(Math.Abs(uv.y - reference.y) * 3840, Is.LessThan(.02), "Vertical projection error");
            }
        }
        [Test]
        public void InvalidFactoryModelsAreRejectedAndSnapshotsAreIndependent()
        {
            Assert.Throws<InvalidDataException>(() => FPVideoFactoryCalibration.Parse(X6, 0));
            Assert.Throws<InvalidDataException>(() => FPVideoFactoryCalibration.Parse(X6.Replace("394240", "197632"), 4));
            Assert.Throws<InvalidDataException>(() => FPVideoFactoryCalibration.Parse(X6.Replace("2.455430", "NaN"), 4));
            Assert.Throws<InvalidDataException>(() => FPVideoFactoryCalibration.Parse(X6.Replace("_193_", "_999_"), 4));
            var s = new FPVideoUnwrapSettings { useFactoryCalibration = true, factoryCalibration = FPVideoFactoryCalibration.Parse(X6, 4) };
            var copy = s.Snapshot(); Assert.That(copy.SameFlowHistory(s), Is.True);
            s.factoryCalibration.lenses[0].values[11] += .1;
            Assert.That(copy.SameFlowHistory(s), Is.False); Assert.That(copy.factoryCalibration.lenses[0].values[11], Is.EqualTo(1.25771189));
            s = copy.Snapshot(); s.factoryCalibration.reverseTracks = false; Assert.That(copy.SameFlowHistory(s), Is.False);
            s = copy.Snapshot(); s.useFactoryCalibration = false; Assert.That(copy.SameFlowHistory(s), Is.False);
        }
        [Test]
        public void FactoryProfileRoundTripAndLegacyProfilesPreserveOutputGradeAndManualCalibration()
        {
            var original = new FPVideoUnwrapSettings { useFactoryCalibration = true, factoryCalibration = FPVideoFactoryCalibration.Parse(X6, 4), lensFov = 196.4f, rotation = new Vector3(-15, 0, 0), colorTuning = true, colorExposure = 2 };
            var profile = FPVideoLensCalibration.Capture(original);
            var target = new FPVideoUnwrapSettings { colorTuning = true, colorExposure = -1, width = 4096, height = 2048 };
            FPVideoLensCalibration.Parse(JsonUtility.ToJson(profile)).Apply(target);
            Assert.That(target.useFactoryCalibration, Is.True); Assert.That(target.factoryCalibration.SameModel(original.factoryCalibration), Is.True);
            Assert.That(target.colorExposure, Is.EqualTo(-1)); Assert.That(target.width, Is.EqualTo(4096)); Assert.That(target.rotation, Is.EqualTo(Vector3.zero));
            target.useFactoryCalibration = false; Assert.That(target.lensFov, Is.EqualTo(196.4f)); Assert.That(target.leftCenter, Is.EqualTo(original.leftCenter));
            profile.version = 3; profile.Apply(target); Assert.That(target.useFactoryCalibration, Is.False);
        }
        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        public void FactoryGpuMappingMatchesCpuWithinQuarterSourcePixel(int track, bool mirrored)
        {
            var source = new Texture2D(1024, 512, TextureFormat.RGBAFloat, false, true) { wrapMode = TextureWrapMode.Clamp };
            var image = new Color[1024 * 512];
            for (int y = 0; y < 512; y++) for (int x = 0; x < 1024; x++) image[y * 1024 + x] = new Color((x + .5f) / 1024, (y + .5f) / 512, 0, 1);
            source.SetPixels(image); source.Apply();
            var output = new RenderTexture(128, 64, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear); output.Create();
            bool previous = GL.sRGBWrite;
            try
            {
                var settings = new FPVideoUnwrapSettings { width = 128, height = 64, useFactoryCalibration = true, factoryCalibration = FPVideoFactoryCalibration.Parse(mirrored ? X6.Replace("394240", "394496") : X6, 4), lensFov = 196.4f };
                using (var renderer = new FPVideoUnwrapRenderer())
                {
                    renderer.Render(source, settings, 128, 64, track == 0 ? FPVideoCalibrationView.LensA : FPVideoCalibrationView.LensB);
                    var material = (Material)typeof(FPVideoUnwrapRenderer).GetField("material", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
                    material.SetFloat("_LinearOutput", 0); GL.sRGBWrite = false; Graphics.Blit(source, output, material);
                    var pixels = FPVideoFarnebackTests.Read(output); int compared = 0;
                    for (int y = 12; y < 52; y += 5) for (int x = 4; x < 124; x += 3)
                    {
                        Vector3 ray = settings.OutputDirection(new Vector2((x + .5f) / 128, (y + .5f) / 64));
                        Vector3 local = settings.factoryCalibration.WorldToLens(track).MultiplyVector(ray);
                        Vector2 uv = settings.factoryCalibration.Project(ray, track);
                        if (local.z < -.13 || uv.x < track * .5f + .01 || uv.x > (track + 1) * .5f - .01 || uv.y < .01 || uv.y > .99) continue;
                        Color actual = pixels[y * 128 + x]; compared++;
                        Assert.That(Math.Abs(actual.r - uv.x) * 7680, Is.LessThan(.25), "GPU horizontal mapping");
                        Assert.That(Math.Abs(actual.g - uv.y) * 3840, Is.LessThan(.25), "GPU vertical mapping");
                    }
                    Assert.That(compared, Is.GreaterThan(20));
                    settings.useFactoryCalibration = false; renderer.Render(source, settings, 128, 64);
                    var manual = FPVideoFarnebackTests.Read(renderer.Output);
                    using (var fresh = new FPVideoUnwrapRenderer())
                    {
                        fresh.Render(source, settings, 128, 64); var expected = FPVideoFarnebackTests.Read(fresh.Output);
                        for (int i = 0; i < expected.Length; i++) Assert.That(manual[i], Is.EqualTo(expected[i]), "Factory toggle must restore manual mapping.");
                    }
                }
            }
            finally { GL.sRGBWrite = previous; UnityEngine.Object.DestroyImmediate(source); output.Release(); UnityEngine.Object.DestroyImmediate(output); }
        }
    }
}
