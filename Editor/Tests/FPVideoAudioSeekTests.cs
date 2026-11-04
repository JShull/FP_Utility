// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.IO;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using Unity.Collections;
    using UnityEngine;

    public sealed class FPVideoAudioSeekTests
    {
        // Interleaved stereo PCM on the source's 48 kHz clock, with no resampling.
        private static float[] ReadWindow(FPVideoNativeDecoder decoder, long discard, int count)
        {
            var result = new float[count]; int written = 0, pumps = 0;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                using (var buffer = new NativeArray<float>(16384, Allocator.Temp))
                while (written < count)
                {
                    int n = decoder.ReadAudio(buffer);
                    if (n == 0)
                    {
                        Assert.That(++pumps, Is.LessThan(1000));
                        Assert.That(decoder.Read(texture, out _), Is.True, "Audio ended before comparison window.");
                        continue;
                    }
                    int skip = (int)Math.Min(discard, n); discard -= skip;
                    for (int i = skip; i < n && written < count; i++) result[written++] = buffer[i];
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            return result;
        }

        [TestCase(6.12)]
        [TestCase(6.28)]
        [TestCase(7.2)]
        public void SampleTimeSeekWithPrerollMatchesSequentialPcm(double time)
        {
            if (!FPVideoNativeDecoder.SupportsAudioSeek(48000)) Assert.Ignore("Audio seek backend is not qualified on this configuration.");
            string folder = Path.Combine(Path.GetTempPath(), "FPVideoAudioSeek_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                string input = Path.Combine(folder, "source.mp4"); FPVideoUnwrapTests.CreateFixture(input, 200);
                long target = (long)Math.Round(time * 48000), anchor = target - 5L * 48000;
                float[] expected, actual;
                using (var decoder = new FPVideoNativeDecoder(input, resizeFrames: false)) expected = ReadWindow(decoder, target * 2, 4096);
                using (var decoder = new FPVideoNativeDecoder(input, resizeFrames: false))
                {
                    decoder.SeekAudio(anchor, 48000); actual = ReadWindow(decoder, (target - anchor) * 2, expected.Length);
                }
                double squared = 0; float maximum = 0;
                for (int i = 0; i < expected.Length; i++) { float e = actual[i] - expected[i]; maximum = Math.Max(maximum, Math.Abs(e)); squared += e * e; }
                Assert.That(maximum, Is.LessThan(1e-5), "Audio must retain the same sample origin, including non-whole-second anchors.");
                Assert.That(Math.Sqrt(squared / expected.Length), Is.LessThan(1e-6));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void FastAndSequentialExportsPreserveTrimmedAudioAndFrameCounts()
        {
            if (!FPVideoNativeDecoder.SupportsAudioSeek(48000)) Assert.Ignore("Audio seek backend is not qualified on this configuration.");
            string folder = Path.Combine(Path.GetTempPath(), "FPVideoAudioSeek_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                string input = Path.Combine(folder, "source.mp4"); FPVideoUnwrapTests.CreateFixture(input, 200);
                float[] expected = null;
                using (var source = new FPVideoNativeSource(input))
                foreach (bool fast in new[] { false, true })
                {
                    string output = Path.Combine(folder, fast ? "fast.mp4" : "full.mp4");
                    using (var job = new FPVideoUnwrapExport(source, new FPVideoUnwrapSettings { sourceProjection = FPVideoSourceProjection.Equirectangular, width = 320, height = 160 }, 6.12, 6.44, output, true, fast))
                    {
                        var timeout = System.Diagnostics.Stopwatch.StartNew();
                        while (!job.Finished && timeout.Elapsed.TotalSeconds < 20) { job.Tick(); System.Threading.Thread.Sleep(1); }
                        Assert.That(job.Succeeded, Is.True, job.Error ?? job.Status);
                    }
                    var info = FPVideoIsoMediaInfo.Read(output); Assert.That(info.FrameCount, Is.EqualTo(8));
                    string report = File.ReadAllText(output + ".json");
                    Assert.That(report, Does.Contain("\"audioSampleFrames\": 15360"));
                    Assert.That(report, Does.Contain(fast ? "\"audioSeekSampleFrame\": 53760" : "\"audioSeekSampleFrame\": 0"));
                    using (var decoder = new FPVideoNativeDecoder(output, resizeFrames: false))
                    {
                        var pcm = ReadWindow(decoder, 0, 4096);
                        if (expected == null) expected = pcm;
                        else for (int i = 0; i < pcm.Length; i++) Assert.That(pcm[i], Is.EqualTo(expected[i]).Within(1e-5), "Export audio differs at sample " + i);
                    }
                }
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
