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
        private static IEnumerator Seek(FPVideoGpuPreview preview,double time)
        {
            double deadline=EditorApplication.timeSinceStartup+15;
            while(true)
            {
                preview.Request(time,EditorApplication.timeSinceStartup,false,false);
                if(preview.HasFrame&&Math.Abs(preview.PresentedSeconds-time)<.00001)yield break;
                if(EditorApplication.timeSinceStartup>deadline)Assert.Fail("GPU preview did not reach requested frame.");
                yield return null;
            }
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
