// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections;
    using System.IO;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class FPVideoGpuPreviewTests
    {
        private IDisposable activePreview;
        [TearDown]
        public void DisposeActivePreview() { activePreview?.Dispose(); activePreview = null; }
        [Test]
        public void OneFrameGapMapsSourceFramesAndTimelineWithoutLosingLastFrame()
        {
            Assert.That(FPVideoGpuPreview.SourceFrame(1049,1051),Is.EqualTo(1049));
            Assert.That(FPVideoGpuPreview.SourceFrame(1050,1051),Is.EqualTo(1050));
            Assert.That(FPVideoGpuPreview.SourceFrame(1051,1051),Is.EqualTo(1050));
            Assert.That(FPVideoGpuPreview.SourceFrame(1052,1051),Is.EqualTo(1051));
            Assert.That(FPVideoGpuPreview.TimelineFrame(1051,1051),Is.EqualTo(1052));
            Assert.That(FPVideoGpuPreview.TimelineFrame(3235,1051),Is.EqualTo(3236));
        }

        [Test]
        public void DisposingPreviewDestroysItsVideoPlayers()
        {
            string path=Path.Combine(Path.GetTempPath(),"FPVideoGpuDispose_"+Guid.NewGuid().ToString("N")+".mp4");
            try
            {
                FPVideoInsvTests.Encode(path,Color.red);
                var preview=new FPVideoGpuPreview(path,null,64,64,25,4);
                preview.Dispose(); preview.Dispose();
                foreach(var player in Resources.FindObjectsOfTypeAll<UnityEngine.Video.VideoPlayer>())
                    Assert.That(player.url,Is.Not.EqualTo(path));
            }
            finally { if(File.Exists(path))File.Delete(path); }
        }

        [UnityTest, Explicit("Requires FP_VIDEO_GPU_SAMPLE_DIR with staged paired INSV MP4 views.")]
        public IEnumerator SuppliedVariableTimingInsvSeeksAcrossGap()
        {
            string folder=Environment.GetEnvironmentVariable("FP_VIDEO_GPU_SAMPLE_DIR");
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("Set FP_VIDEO_GPU_SAMPLE_DIR to the staged paired lens views.");
            string a=Path.Combine(folder,"source.mp4"), b=Path.Combine(folder,"lens_b.mp4");
            using(var preview=new FPVideoGpuPreview(a,b,512,256,60000d/1001,3236,1051))
            {
                activePreview=preview;
                foreach(long frame in new long[]{1049,1050,1051,1052,1100,1050})
                {
                    double time=frame*1001d/60000; yield return Seek(preview,time);
                    Assert.That(preview.PresentedSeconds,Is.EqualTo(time).Within(.00001));
                }
            }
        }
        private static IEnumerator Seek(FPVideoGpuPreview preview,double time)
        {
            double deadline=EditorApplication.timeSinceStartup+15;
            while(true)
            {
                preview.Request(time,EditorApplication.timeSinceStartup,false,false);
                if(preview.HasFrame&&Math.Abs(preview.PresentedSeconds-time)<.00001)yield break;
                if(EditorApplication.timeSinceStartup>deadline)Assert.Fail($"GPU preview did not reach {time:F6}: {preview.Status}, presented {preview.PresentedSeconds:F6}.");
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator GpuPlaybackKeepsTheTwoLensImagesDistinct()
        {
            string folder=Path.Combine(Path.GetTempPath(),"FPVideoGpuTest_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                string a=Path.Combine(folder,"a.mp4"),b=Path.Combine(folder,"b.mp4");
                FPVideoInsvTests.Encode(a,Color.red,50); FPVideoInsvTests.Encode(b,Color.blue,50);
                using(var preview=new FPVideoGpuPreview(a,b,128,64,25,50))
                {
                    activePreview=preview; yield return Seek(preview,0);
                    double deadline=EditorApplication.timeSinceStartup+15; int checkedFrames=0;
                    while(checkedFrames<5)
                    {
                        if(preview.Request(0,EditorApplication.timeSinceStartup,false,true))
                        {
                            var pixels=FPVideoFarnebackTests.Read(preview.Texture);
                            Assert.That(pixels[32*128+32].r,Is.GreaterThan(.8)); Assert.That(pixels[32*128+32].b,Is.LessThan(.1));
                            Assert.That(pixels[32*128+96].b,Is.GreaterThan(.8)); Assert.That(pixels[32*128+96].r,Is.LessThan(.1));
                            checkedFrames++;
                        }
                        if(EditorApplication.timeSinceStartup>deadline) Assert.Fail("GPU playback did not publish enough matching frames.");
                        yield return null;
                    }
                }
            }
            finally { Directory.Delete(folder,true); }
        }

        [UnityTest]
        public IEnumerator GpuPairsPublishDistinctMatchingLensesAndSeekInBothDirections()
        {
            string folder=Path.Combine(Path.GetTempPath(),"FPVideoGpuTest_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                string a=Path.Combine(folder,"a.mp4"),b=Path.Combine(folder,"b.mp4");
                FPVideoInsvTests.Encode(a,Color.red);FPVideoInsvTests.Encode(b,Color.blue);
                using(var preview=new FPVideoGpuPreview(a,b,128,64,25,4))
                {
                    activePreview=preview;
                    foreach(double time in new[]{0d,.04,.08,0})
                    {
                        yield return Seek(preview,time);
                        var pixels=FPVideoFarnebackTests.Read(preview.Texture);
                        Assert.That(pixels[32*128+32].r,Is.GreaterThan(.8));Assert.That(pixels[32*128+32].b,Is.LessThan(.1));
                        Assert.That(pixels[32*128+96].b,Is.GreaterThan(.8));Assert.That(pixels[32*128+96].r,Is.LessThan(.1));
                    }
                    long count=preview.PresentedFrames;
                    Assert.That(preview.Request(0,EditorApplication.timeSinceStartup,false,false),Is.False);
                    Assert.That(preview.PresentedFrames,Is.EqualTo(count));
                }
            }
            finally { Directory.Delete(folder,true); }
        }

        [UnityTest]
        public IEnumerator GpuPreviewWrapperScalesTheWholeImageAndPreservesSourceTiming()
        {
            string folder=Path.Combine(Path.GetTempPath(),"FPVideoGpuTest_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                string input=Path.Combine(folder,"source.mp4");FPVideoUnwrapTests.CreateFixture(input);
                using(var source=new FPVideoNativeSource(input))
                using(var preview=new FPVideoUnwrapPreview(source,64,true))
                {
                    activePreview=preview;
                    double deadline=EditorApplication.timeSinceStartup+15;
                    while(!preview.Request(.4,EditorApplication.timeSinceStartup,false))
                    { if(EditorApplication.timeSinceStartup>deadline)Assert.Fail("GPU preview preparation/seek timeout.");yield return null; }
                    Assert.That(preview.UsesGpu,Is.True);Assert.That(preview.Texture,Is.TypeOf<RenderTexture>());
                    Assert.That(preview.Texture.width,Is.EqualTo(64));Assert.That(source.Info.Width,Is.EqualTo(320));
                    Assert.That(preview.PresentedSeconds,Is.EqualTo(.4).Within(.00001));
                    var center=FPVideoFarnebackTests.Read((RenderTexture)preview.Texture)[16*64+32];
                    Assert.That(center.r,Is.InRange(.45f,.55f));Assert.That(center.g,Is.InRange(.45f,.55f));Assert.That(center.b,Is.EqualTo(10/24f).Within(.025f));
                    // Exercise real-time playback before returning to exact forward/reverse scrub frames.
                    deadline=EditorApplication.timeSinceStartup+15;
                    while(preview.PresentedSeconds<.64)
                    {
                        preview.Request(.4,EditorApplication.timeSinceStartup,false,true);
                        if(EditorApplication.timeSinceStartup>deadline)Assert.Fail("GPU playback did not advance.");
                        yield return null;
                    }
                    preview.Pause();
                    foreach(double time in new[]{.08,.64,.4})
                    {
                        deadline=EditorApplication.timeSinceStartup+15;
                        while(true)
                        {
                            preview.Request(time,EditorApplication.timeSinceStartup,true);
                            if(Math.Abs(preview.PresentedSeconds-time)<.00001)break;
                            if(EditorApplication.timeSinceStartup>deadline)Assert.Fail("GPU scrub after playback did not reach the exact frame.");
                            yield return null;
                        }
                        center=FPVideoFarnebackTests.Read((RenderTexture)preview.Texture)[16*64+32];
                        Assert.That(center.b,Is.EqualTo((float)(time*25/24)).Within(.025f),"Decoded pixels must match the requested frame after playback.");
                    }
                    preview.Pause();Assert.That(preview.CachedFrames,Is.EqualTo(1));
                }
            }
            finally { Directory.Delete(folder,true); }
        }
    }
}
