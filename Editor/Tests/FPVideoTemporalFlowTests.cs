// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class FPVideoTemporalFlowTests
    {
        private static void Fill(RenderTexture target, Color value)
        {
            var previous = RenderTexture.active;
            try { RenderTexture.active=target; GL.Clear(false,true,value); }
            finally { RenderTexture.active=previous; }
        }

        private static Color Center(FPVideoFarneback solver) => FPVideoFarnebackTests.Read(solver.Result)[32*64+32];
        private static void RequireGpu()
        {
            if (!FPVideoFarneback.Supported) Assert.Ignore("Float32 compute shaders unavailable.");
        }

        [Test]
        public void ReliableVectorJitterFallsWithoutAveragingVideoPixelsOrRepeatedRepaints()
        {
            RequireGpu();
            using (var solver = new FPVideoFarneback())
            {
                solver.EnsureSize(64,64,1);
                Fill(solver.InputA,new Color(.5f,.5f,.5f,1)); Fill(solver.InputB,new Color(.5f,.5f,.5f,1));
                double squaredError=0;
                for (int frame=0;frame<60;frame++)
                {
                    float observation=1+(frame%2==0?.2f:-.2f);
                    Fill(solver.Result,new Color(observation,0,1,0));
                    solver.Smooth(frame*.04,.04,.08f,.5f,new Vector2(2,2));
                    float result=Center(solver).r;
                    if (frame>=10) squaredError+=(result-1)*(result-1);
                    Assert.That(result,Is.InRange(.7999f,1.2001f));
                }
                double rms=Math.Sqrt(squaredError/50);
                TestContext.WriteLine($"Vector jitter RMS raw=0.2 px, smoothed={rms:F5} px");
                Assert.That(rms,Is.LessThan(.1),"At least 50% RMS jitter reduction after warmup.");
                Color paused=Center(solver);
                Fill(solver.Result,new Color(4,0,1,0));
                solver.Smooth(59*.04,.04,.08f,.5f,new Vector2(2,2));
                Assert.That(Center(solver),Is.EqualTo(paused),"Repaint must not integrate another sample.");
                Assert.That(FPVideoFarnebackTests.Read(solver.InputA)[32*64+32].r,Is.EqualTo(.5f));
            }
        }

        [Test]
        public void ConfidenceAppearanceAndLargeMotionRejectHistoryImmediately()
        {
            RequireGpu();
            using (var solver = new FPVideoFarneback())
            {
                solver.EnsureSize(64,64,1);
                foreach (string reason in new[]{"confidence","appearance","motion","badMatch"})
                {
                    solver.ResetTemporal();
                    Fill(solver.InputA,new Color(.5f,.5f,.5f,1)); Fill(solver.InputB,new Color(.5f,.5f,.5f,1));
                    Fill(solver.Result,new Color(1,0,1,0)); solver.Smooth(0,.04,.08f,.5f,new Vector2(2,2));
                    var current=new Color(reason=="motion"?5:1.3f,0,reason=="confidence"?0:1,0);
                    if (reason=="appearance") Fill(solver.InputA,new Color(.9f,.9f,.9f,1));
                    if (reason=="badMatch") Fill(solver.InputB,new Color(.8f,.8f,.8f,1));
                    Fill(solver.Result,current); solver.Smooth(.04,.04,.08f,.5f,new Vector2(2,2));
                    Assert.That(Center(solver),Is.EqualTo(current),reason);
                }
            }
        }

        [Test]
        public void GapsReverseSeeksAndExplicitResetsStartFromCurrentFrame()
        {
            RequireGpu();
            using (var solver = new FPVideoFarneback())
            {
                solver.EnsureSize(64,64,1);
                Fill(solver.InputA,new Color(.5f,.5f,.5f,1)); Fill(solver.InputB,new Color(.5f,.5f,.5f,1));
                foreach (double time in new[]{0d,.4,.2,double.NaN})
                {
                    solver.ResetTemporal();
                    Fill(solver.Result,new Color(1,0,1,0)); solver.Smooth(.08,.04,.08f,.5f,new Vector2(2,2));
                    Fill(solver.Result,new Color(1.3f,0,1,0)); solver.Smooth(time,.04,.08f,.5f,new Vector2(2,2));
                    Assert.That(Center(solver).r,Is.EqualTo(1.3f).Within(.00001));
                }
                solver.ResetTemporal(); Fill(solver.Result,new Color(2,0,1,0));
                solver.Smooth(.24,.04,.08f,.5f,new Vector2(2,2)); Assert.That(Center(solver).r,Is.EqualTo(2));
            }
        }

        [Test]
        public void ProfilesPreserveTemporalControlsAndOlderVersionsDisableThem()
        {
            var s=new FPVideoUnwrapSettings { opticalFlow=true,useDirectionalSeam=true,flowTemporalSmoothing=true,flowSmoothingSeconds=.12f,flowTemporalRejectDegrees=.7f };
            var profile=FPVideoLensCalibration.Capture(s);
            var target=new FPVideoUnwrapSettings();
            FPVideoLensCalibration.Parse(JsonUtility.ToJson(profile)).Apply(target);
            Assert.That(target.flowTemporalSmoothing,Is.True); Assert.That(target.flowSmoothingSeconds,Is.EqualTo(.12f));
            Assert.That(target.flowTemporalRejectDegrees,Is.EqualTo(.7f));
            profile.version=2; profile.Apply(target); Assert.That(target.flowTemporalSmoothing,Is.False);
            s.flowSmoothingSeconds=float.NaN; Assert.Throws<ArgumentException>(()=>s.Validate());
            s.flowSmoothingSeconds=.08f; s.flowTemporalRejectDegrees=0; Assert.Throws<ArgumentException>(()=>s.Validate());
            var changed=target.Snapshot(); changed.lensFov+=1; Assert.That(changed.SameFlowHistory(target),Is.False);
        }

        [Test]
        public void RendererResetsAfterCalibrationChangesAndSeparateSequencesAgree()
        {
            RequireGpu(); var source=FPVideoFlowIntegrationTests.SyntheticLenses();
            try
            {
                var settings=new FPVideoUnwrapSettings { opticalFlow=true,useDirectionalSeam=true,flowTemporalSmoothing=true,lensFov=196.4f };
                using (var preview=new FPVideoUnwrapRenderer())
                using (var export=new FPVideoUnwrapRenderer())
                {
                    for (int frame=0;frame<3;frame++)
                    {
                        preview.Render(source,settings,320,160,sourceSeconds:frame*.04,frameSeconds:.04);
                        export.Render(source,settings,640,320,sourceSeconds:frame*.04,frameSeconds:.04);
                    }
                    var a=FPVideoFarnebackTests.Read(preview.FlowField); var b=FPVideoFarnebackTests.Read(export.FlowField);
                    for (int i=0;i<a.Length;i++) Assert.That(Vector2.Distance(new Vector2(a[i].r,a[i].g),new Vector2(b[i].r,b[i].g)),Is.LessThan(.00001));
                    settings.rightRotation.y+=.2f;
                    preview.Render(source,settings,320,160,sourceSeconds:.12,frameSeconds:.04);
                    export.ResetTemporal(); export.Render(source,settings,640,320,sourceSeconds:.12,frameSeconds:.04);
                    a=FPVideoFarnebackTests.Read(preview.FlowField); b=FPVideoFarnebackTests.Read(export.FlowField);
                    for (int i=0;i<a.Length;i++) Assert.That(Vector2.Distance(new Vector2(a[i].r,a[i].g),new Vector2(b[i].r,b[i].g)),Is.LessThan(.00001));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}

