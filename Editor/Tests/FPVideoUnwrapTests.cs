// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.IO;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using Unity.Collections;
    using UnityEditor.Media;
    using UnityEngine;

    public sealed class FPVideoUnwrapTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ProjectionCenterFacesForward(int projection)
        {
            var settings = new FPVideoUnwrapSettings { projection = (FPVideoProjection)projection };
            Assert.That(Vector3.Distance(settings.OutputDirection(new Vector2(.5f, .5f)), Vector3.forward), Is.LessThan(.00001));
        }
        [Test]
        public void EquirectangularPolesAndSeamMatch()
        {
            var s = new FPVideoUnwrapSettings();
            Assert.That(Vector3.Distance(s.OutputDirection(new Vector2(.5f, 1)), Vector3.up), Is.LessThan(.00001));
            Assert.That(Vector3.Distance(s.OutputDirection(new Vector2(0, .5f)), s.OutputDirection(new Vector2(1, .5f))), Is.LessThan(.00001));
            s.rotation = new Vector3(0, 90, 0);
            Assert.That(Vector3.Distance(s.OutputDirection(new Vector2(.5f, .5f)), Vector3.right), Is.LessThan(.00001));
        }
        [Test]
        public void PerspectiveUsesHorizontalFovAndOutputAspect()
        {
            var s = new FPVideoUnwrapSettings { projection = FPVideoProjection.Perspective, width = 2000, height = 1000, horizontalFov = 90 };
            var right = s.OutputDirection(new Vector2(1, .5f)); var top = s.OutputDirection(new Vector2(.5f, 1));
            Assert.That(right.x / right.z, Is.EqualTo(1).Within(.00001)); Assert.That(top.y / top.z, Is.EqualTo(.5).Within(.00001));
        }
        [Test]
        public void SettingsRejectInvalidDimensionsAndCalibration()
        {
            var s = new FPVideoUnwrapSettings { width = 1023 }; Assert.Throws<ArgumentException>(() => s.Validate());
            s.width = 1024; Assert.Throws<ArgumentException>(() => s.Validate());
            s.height = 512; s.lensFov = float.NaN; Assert.Throws<ArgumentException>(() => s.Validate());
            s.lensFov = 200; s.lensRadius = Vector2.zero; Assert.Throws<ArgumentException>(() => s.Validate());
        }
        [Test]
        public void ExportBitrateDefaultsToHighQualityAndRejectsInvalidValues()
        {
            var settings = new FPVideoUnwrapSettings();
            Assert.That(settings.videoBitrateMbps, Is.EqualTo(40));
            foreach (float value in new[] { 0, .5f, 501, float.NaN, float.PositiveInfinity })
            { settings.videoBitrateMbps = value; Assert.Throws<ArgumentException>(() => settings.Validate()); }
        }
        [Test]
        public void ExportRangeQuantizesToSourceFramesAndRejectsVfr()
        {
            var info = new FPVideoIsoMediaInfo { TimeScale = 2500, FrameDelta = 100, FrameCount = 250, Duration = 10 };
            var range = FPVideoUnwrapExport.FrameRange(info, .101, .401);
            Assert.That(range.first, Is.EqualTo(3)); Assert.That(range.end, Is.EqualTo(11));
            Assert.Throws<ArgumentException>(() => FPVideoUnwrapExport.FrameRange(info, 2, 1));
            Assert.Throws<ArgumentException>(() => FPVideoUnwrapExport.FrameRange(info, 0, double.PositiveInfinity));
            info.ConstantFrameRate = false; Assert.Throws<NotSupportedException>(() => FPVideoUnwrapExport.FrameRange(info, 0, 1));
        }
        [Test]
        public void MetadataRejectsMalformedBoxWithoutReadingWholeFile()
        {
            string path = Path.GetTempFileName();
            try { File.WriteAllBytes(path, new byte[] { 0, 0, 0, 255, 109, 111, 111, 118 }); Assert.Throws<InvalidDataException>(() => FPVideoIsoMediaInfo.Read(path)); }
            finally { File.Delete(path); }
        }

        [Test]
        public void NativeDecodeSeekAndExportPreserveFrameAndAudioCounts()
        {
            if (!FPVideoNativeDecoder.Available) Assert.Ignore("Native Editor decoder unavailable.");
            string folder = Path.Combine(Path.GetTempPath(), "FPVideoTest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string input = Path.Combine(folder, "source.mp4"), output = Path.Combine(folder, "output.mp4");
            try
            {
                CreateFixture(input);
                var info = FPVideoIsoMediaInfo.Read(input);
                Assert.That(info.Width, Is.EqualTo(320)); Assert.That(info.Height, Is.EqualTo(160)); Assert.That(info.FrameCount, Is.EqualTo(25));
                Assert.That(info.FrameRate, Is.EqualTo(25)); Assert.That(info.AudioChannels, Is.EqualTo(2)); Assert.That(info.AudioSampleRate, Is.EqualTo(48000));
                var texture = new Texture2D(320, 160, TextureFormat.RGBA32, false);
                Color expected;
                try
                {
                    using (var decoder = new FPVideoNativeDecoder(input))
                    {
                        Assert.That(decoder.ReadAt(texture, .4), Is.EqualTo(.4).Within(.001));
                        decoder.ReadAt(texture, .12); expected = texture.GetPixel(160, 80);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                using (var source = new FPVideoNativeSource(input))
                using (var job = new FPVideoUnwrapExport(source, new FPVideoUnwrapSettings { sourceProjection = FPVideoSourceProjection.Equirectangular, width = 320, height = 160 }, .12, .44, output, true))
                {
                    var timeout = System.Diagnostics.Stopwatch.StartNew();
                    while (!job.Finished && timeout.Elapsed.TotalSeconds < 20) { job.Tick(); System.Threading.Thread.Sleep(1); }
                    Assert.That(job.Finished, Is.True, "Export timeout"); Assert.That(job.Succeeded, Is.True, job.Error);
                    Assert.That(job.Hash, Has.Length.EqualTo(64));
                }
                var converted = FPVideoIsoMediaInfo.Read(output);
                Assert.That(converted.FrameCount, Is.EqualTo(8)); Assert.That(converted.Duration, Is.EqualTo(.32).Within(.001)); Assert.That(converted.HasAudio, Is.True);
                var decoded = new Texture2D(320, 160, TextureFormat.RGBA32, false);
                try
                {
                    using (var decoder = new FPVideoNativeDecoder(output)) decoder.ReadAt(decoded, 0);
                    Color actual = decoded.GetPixel(160, 80);
                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(.04)); Assert.That(actual.g, Is.EqualTo(expected.g).Within(.04)); Assert.That(actual.b, Is.EqualTo(expected.b).Within(.04));
                }
                finally { UnityEngine.Object.DestroyImmediate(decoded); }
                var report = File.ReadAllText(output + ".json"); Assert.That(report, Does.Contain("\"audioSampleFrames\": 15360"));
            }
            finally { Directory.Delete(folder, true); }
        }
        [Test]
        public void DualFisheyeShaderMapsForwardAndBackwardLenses()
        {
            var input = new Texture2D(320, 160, TextureFormat.RGBA32, false);
            var output = new Texture2D(320, 160, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color[320 * 160];
                for (int p = 0; p < pixels.Length; p++) pixels[p] = p % 320 < 160 ? Color.red : Color.blue;
                input.SetPixels(pixels); input.Apply();
                using (var renderer = new FPVideoUnwrapRenderer())
                {
                    renderer.Render(input, new FPVideoUnwrapSettings { width = 320, height = 160 }, 320, 160); renderer.ReadBack(output);
                    Color forward = output.GetPixel(160, 80), backward = output.GetPixel(1, 80);
                    Assert.That(forward.r, Is.GreaterThan(.9)); Assert.That(forward.b, Is.LessThan(.1));
                    Assert.That(backward.b, Is.GreaterThan(.9)); Assert.That(backward.r, Is.LessThan(.1));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(input); UnityEngine.Object.DestroyImmediate(output); }
        }
        [Test]
        public void CancelledExportDoesNotPublishOrLeavePartialFiles()
        {
            string folder = Path.Combine(Path.GetTempPath(), "FPVideoTest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                string input = Path.Combine(folder, "source.mp4"), output = Path.Combine(folder, "cancel.mp4"); CreateFixture(input);
                using (var source = new FPVideoNativeSource(input))
                using (var job = new FPVideoUnwrapExport(source, new FPVideoUnwrapSettings { width = 320, height = 160 }, 0, 1, output, true)) job.Tick();
                Assert.That(File.Exists(output), Is.False); Assert.That(Directory.GetFiles(folder).Length, Is.EqualTo(1));
            }
            finally { Directory.Delete(folder, true); }
        }
        [Test]
        public void LrvStagesAsMp4AndDecodesWithoutChangingOriginal()
        {
            string folder = Path.Combine(Path.GetTempPath(), "FPVideoTest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string staged;
            try
            {
                string input = Path.Combine(folder, "source.lrv"), encoded = Path.Combine(folder, "source.mp4");
                CreateFixture(encoded); File.Move(encoded, input); long length = new FileInfo(input).Length;
                using (var source = new FPVideoNativeSource(input))
                {
                    source.Ready.GetAwaiter().GetResult(); staged = source.DecodePath;
                    Assert.That(Path.GetExtension(staged), Is.EqualTo(".mp4"));
                    using (var preview = new FPVideoUnwrapPreview(source))
                    {
                        Assert.That(preview.Request(.4, 0, false), Is.True); Assert.That(preview.PresentedSeconds, Is.EqualTo(.4).Within(.001));
                        Assert.That(preview.Request(.44, 1, false), Is.True); Assert.That(preview.Request(.4, 2, true), Is.True);
                        Assert.That(preview.CachedFrames, Is.EqualTo(2));
                    }
                }
                Assert.That(new FileInfo(input).Length, Is.EqualTo(length));
                Assert.That(System.Threading.SpinWait.SpinUntil(() => !File.Exists(staged), 2000), Is.True, "Staged source was not released");
            }
            finally { Directory.Delete(folder, true); }
        }
        [Test]
        public void PlaybackDecodesEverySourceFrameWhileDragScrubbingKeepsItsThrottle()
        {
            string folder = Path.Combine(Path.GetTempPath(), "FPVideoTest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                string input = Path.Combine(folder, "source.mp4"); CreateFixture(input);
                using (var source = new FPVideoNativeSource(input))
                using (var preview = new FPVideoUnwrapPreview(source))
                {
                    Assert.That(preview.Request(0, 0, false), Is.True);
                    for (int i = 1; i <= 12; i++)
                    {
                        Assert.That(preview.Request(i / 25d, i / 25d, false, true), Is.True);
                        Assert.That(preview.PresentedSeconds, Is.EqualTo(i / 25d).Within(.001));
                    }
                    Assert.That(preview.SeekCount, Is.EqualTo(1), "Continuous playback should not seek per frame.");
                    Assert.That(preview.SequentialFrames, Is.EqualTo(12));
                    Assert.That(preview.Request(.04, 1, true), Is.True); // Reverse seek outside the bounded cache.
                    Assert.That(preview.PresentedSeconds, Is.EqualTo(.04).Within(.001));
                    Assert.That(preview.Request(.08, 1.01, true), Is.False, "Drag throttle must remain active.");
                    Assert.That(preview.Request(.08, 1.01, false, true), Is.True, "Play must bypass the drag throttle.");
                    Assert.That(preview.Request(.28, 1.21, false, true), Is.True);
                    Assert.That(preview.PresentedSeconds, Is.EqualTo(.28).Within(.001));
                    Assert.That(preview.SeekCount, Is.EqualTo(2), "Short playback catch-up should decode forward.");
                }
            }
            finally { Directory.Delete(folder, true); }
        }
        [Test]
        public void CalibrationProfileRoundTripPreservesOutputSettingsAndRejectsInvalidDataAtomically()
        {
            var calibrated = new FPVideoUnwrapSettings
            {
                calibrationName = "X6 user test", lensFov = 205, independentRightLens = true, rightLensFov = 203,
                rightLensRadius = new Vector2(.24f, .49f), rightCenter = new Vector2(.749f, .501f),
                rightRotation = new Vector3(1, 179, 2), useDirectionalSeam = true, seamYaw = 3, seamWidth = 2
            };
            var saved = FPVideoLensCalibration.Capture(calibrated);
            var restored = FPVideoLensCalibration.Parse(JsonUtility.ToJson(saved));
            var target = new FPVideoUnwrapSettings { projection = FPVideoProjection.Perspective, width = 1920, height = 1080, videoBitrateMbps = 25, rotation = new Vector3(3, 4, 5) };
            restored.Apply(target);
            Assert.That(target.rightCenter, Is.EqualTo(calibrated.rightCenter)); Assert.That(target.rightLensRadius, Is.EqualTo(calibrated.rightLensRadius));
            Assert.That(target.rightRotation, Is.EqualTo(calibrated.rightRotation)); Assert.That(target.seamYaw, Is.EqualTo(3));
            Assert.That(target.calibrationName, Is.EqualTo("X6 user test")); Assert.That(target.independentRightLens, Is.True);
            Assert.That(target.width, Is.EqualTo(1920)); Assert.That(target.height, Is.EqualTo(1080)); Assert.That(target.videoBitrateMbps, Is.EqualTo(25));
            Assert.That(target.projection, Is.EqualTo(FPVideoProjection.Perspective)); Assert.That(target.rotation, Is.EqualTo(new Vector3(3, 4, 5)));
            restored.lensRadius = Vector2.zero;
            Assert.Throws<ArgumentException>(() => restored.Apply(target)); Assert.That(target.lensRadius, Is.EqualTo(calibrated.lensRadius));
            Assert.Throws<InvalidDataException>(() => FPVideoLensCalibration.Parse("{}"));
            Assert.Throws<InvalidDataException>(() => FPVideoLensCalibration.Parse("{\"format\":\"FPVideoLensCalibration\",\"version\":99}"));
        }
        [Test]
        public void CalibrationViewportClampsZoomAndPanToImage()
        {
            Assert.That(FPVideoUnwrapWindow.CalibrationUv(1, Vector2.zero), Is.EqualTo(new Rect(0, 0, 1, 1)));
            Assert.That(FPVideoUnwrapWindow.CalibrationUv(2, Vector2.zero), Is.EqualTo(new Rect(.25f, .25f, .5f, .5f)));
            Assert.That(FPVideoUnwrapWindow.CalibrationUv(2, new Vector2(10, -10)), Is.EqualTo(new Rect(.5f, 0, .5f, .5f)));
        }
        [Test]
        public void CalibrationViewsSeparateLensesAndDirectionalSeamChoosesLens()
        {
            var input = new Texture2D(320, 160, TextureFormat.RGBA32, false);
            var output = new Texture2D(320, 160, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color[320 * 160];
                for (int p = 0; p < pixels.Length; p++) pixels[p] = p % 320 < 160 ? Color.red : Color.blue;
                input.SetPixels(pixels); input.Apply();
                var settings = new FPVideoUnwrapSettings { width = 320, height = 160 };
                using (var renderer = new FPVideoUnwrapRenderer())
                {
                    Color Sample(FPVideoCalibrationView mode)
                    {
                        renderer.Render(input, settings, 320, 160, mode, .25f, 1); renderer.ReadBack(output); return output.GetPixel(240, 80);
                    }
                    Color a = Sample(FPVideoCalibrationView.LensA), b = Sample(FPVideoCalibrationView.LensB);
                    Assert.That(a.r, Is.GreaterThan(.9)); Assert.That(a.b, Is.LessThan(.1));
                    Assert.That(b.b, Is.GreaterThan(.9)); Assert.That(b.r, Is.LessThan(.1));
                    Color overlay = Sample(FPVideoCalibrationView.OverlapOverlay); Assert.That(overlay.b, Is.GreaterThan(overlay.r)); Assert.That(overlay.r, Is.GreaterThan(.1));
                    Color difference = Sample(FPVideoCalibrationView.Difference); Assert.That(difference.r, Is.GreaterThan(.9)); Assert.That(difference.b, Is.GreaterThan(.9));
                    Color coverage = Sample(FPVideoCalibrationView.Coverage); Assert.That(coverage.r, Is.GreaterThan(.9)); Assert.That(coverage.g, Is.GreaterThan(.9));
                    settings.useDirectionalSeam = true; settings.seamWidth = 0; settings.seamYaw = 5;
                    Assert.That(Sample(FPVideoCalibrationView.Composite).r, Is.GreaterThan(.9));
                    settings.seamYaw = -5; Assert.That(Sample(FPVideoCalibrationView.Composite).b, Is.GreaterThan(.9));
                    // Preview diagnostics must not remain active when the renderer is used for normal export.
                    Sample(FPVideoCalibrationView.LensA); renderer.Render(input, settings, 320, 160); renderer.ReadBack(output);
                    Assert.That(output.GetPixel(1, 80).b, Is.GreaterThan(.9));
                    for (int p = 0; p < pixels.Length; p++) pixels[p] = Color.green;
                    input.SetPixels(pixels); input.Apply();
                    Color matching = Sample(FPVideoCalibrationView.Difference); Assert.That(matching.maxColorComponent, Is.LessThan(.03));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(input); UnityEngine.Object.DestroyImmediate(output); }
        }
        [Test]
        public void ExportNeverOverwritesExistingFile()
        {
            string folder = Path.Combine(Path.GetTempPath(), "FPVideoTest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                string input = Path.Combine(folder, "source.mp4"), output = Path.Combine(folder, "output.mp4"); CreateFixture(input); File.WriteAllText(output, "keep me");
                using (var source = new FPVideoNativeSource(input)) Assert.Throws<IOException>(() => new FPVideoUnwrapExport(source, new FPVideoUnwrapSettings { width = 320, height = 160 }, 0, 1, output, false));
                Assert.That(File.ReadAllText(output), Is.EqualTo("keep me"));
            }
            finally { Directory.Delete(folder, true); }
        }
        [Test]
        public void ReducedPreviewKeepsNativeSourceMetadataAndFrameTimes()
        {
            string folder=Path.Combine(Path.GetTempPath(),"FPVideoPreviewTest_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                string input=Path.Combine(folder,"source.mp4");CreateFixture(input);
                using(var source=new FPVideoNativeSource(input))
                using(var preview=new FPVideoUnwrapPreview(source,64))
                {
                    Assert.That(preview.Request(.4,0,false),Is.True);
                    Assert.That(preview.Texture.width,Is.EqualTo(64));Assert.That(preview.Texture.height,Is.EqualTo(32));
                    Assert.That(source.Info.Width,Is.EqualTo(320));Assert.That(source.Info.Height,Is.EqualTo(160));
                    Assert.That(preview.PresentedSeconds,Is.EqualTo(.4).Within(.00001));
                    Assert.That(preview.Request(.44,.04,false,true),Is.True);
                    Assert.That(preview.SequentialFrames,Is.EqualTo(1));
                    var target=new RenderTexture(64,32,0);
                    try
                    {
                        Graphics.Blit(preview.Texture,target);var center=FPVideoFarnebackTests.Read(target)[16*64+32];
                        Assert.That(center.b,Is.GreaterThan(.3));
                        Assert.That(center.r,Is.InRange(.45f,.55f),"Preview must scale the whole image, not crop the top-left pixels.");
                        Assert.That(center.g,Is.InRange(.45f,.55f));
                    }
                    finally { target.Release();UnityEngine.Object.DestroyImmediate(target); }
                }
            }
            finally { Directory.Delete(folder,true); }
        }
        internal static void CreateFixture(string path, int frameCount = 25)
        {
            var texture = new Texture2D(320, 160, TextureFormat.RGBA32, false);
            try
            {
                var video = new VideoTrackAttributes { width = 320, height = 160, frameRate = new MediaRational(25), includeAlpha = false };
                var audio = new AudioTrackAttributes { channelCount = 2, sampleRate = new MediaRational(48000), language = "und" };
                using (var encoder = new MediaEncoder(path, video, audio))
                using (var samples = new NativeArray<float>(3840, Allocator.Temp))
                {
                    var writableSamples = samples;
                    var colors = new Color[320 * 160];
                    for (int i = 0; i < frameCount; i++)
                    {
                        for (int p = 0; p < colors.Length; p++) colors[p] = new Color((p % 320) / 319f, (p / 320) / 159f, i / 24f, 1);
                        texture.SetPixels(colors); texture.Apply(); Assert.That(encoder.AddFrame(texture), Is.True);
                        for (int p = 0; p < samples.Length; p++) writableSamples[p] = (float)Math.Sin((i * 1920 + p / 2) * 2 * Math.PI * 440 / 48000) * .1f;
                        Assert.That(encoder.AddSamples(samples), Is.True);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}

