// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class FPVideoColorTuningTests
    {
        private static Texture2D Source(Color color)
        {
            var source=new Texture2D(64,32,TextureFormat.RGBAFloat,false,true);
            var pixels=new Color[64*32];for(int i=0;i<pixels.Length;i++)pixels[i]=color;
            source.SetPixels(pixels);source.Apply();return source;
        }
        private static Color Render(FPVideoUnwrapRenderer renderer,Texture source,FPVideoUnwrapSettings settings,
            FPVideoCalibrationView view=FPVideoCalibrationView.Composite)
        {
            renderer.Render(source,settings,64,32,view);
            return FPVideoFarnebackTests.Read(renderer.Output)[16*64+32];
        }

        [TestCase(0)]
        [TestCase(1)]
        public void BypassNeutralExposureAndWhiteBalanceWorkForBothLayouts(int layout)
        {
            var source=Source(new Color(.2f,.2f,.2f,1));
            try
            {
                using(var renderer=new FPVideoUnwrapRenderer())
                {
                    var s=new FPVideoUnwrapSettings { width=64,height=32,sourceProjection=(FPVideoSourceProjection)layout };
                    var original=Render(renderer,source,s);
                    s.colorTuning=true;Assert.That(Render(renderer,source,s),Is.EqualTo(original));
                    s.colorExposure=1;var exposed=Render(renderer,source,s);Assert.That(exposed.r,Is.GreaterThan(original.r));
                    s.colorTuning=false;Assert.That(Render(renderer,source,s),Is.EqualTo(original));
                    s.colorTuning=true;s.colorExposure=0;s.colorTemperature=1;
                    var warm=Render(renderer,source,s);Assert.That(warm.r,Is.GreaterThan(warm.b));
                    s.colorTemperature=0;s.colorTint=1;
                    var magenta=Render(renderer,source,s);Assert.That(magenta.r,Is.GreaterThan(magenta.g));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void SaturationContrastAndDiagnosticIsolationWork()
        {
            var source=Source(new Color(.1f,.3f,.6f,1));
            try
            {
                using(var renderer=new FPVideoUnwrapRenderer())
                {
                    var s=new FPVideoUnwrapSettings { width=64,height=32 };
                    var raw=Render(renderer,source,s,FPVideoCalibrationView.LensA);
                    s.colorTuning=true;s.colorSaturation=0;
                    var gray=Render(renderer,source,s);
                    Assert.That(Mathf.Abs(gray.r-gray.g)+Mathf.Abs(gray.g-gray.b),Is.LessThan(.005));
                    s.colorSaturation=2;s.colorContrast=0;
                    gray=Render(renderer,source,s);Assert.That(Mathf.Abs(gray.r-gray.g)+Mathf.Abs(gray.g-gray.b),Is.LessThan(.005));
                    s.colorExposure=4;s.colorTemperature=-1;s.colorTint=1;
                    s.colorCurve=FPVideoToneCurve.FlatLog113;
                    Assert.That(Render(renderer,source,s,FPVideoCalibrationView.LensA),Is.EqualTo(raw));
                    var before=s.Snapshot();s.colorExposure=-4;
                    Assert.That(s.SameFlowHistory(before),Is.True,"Output grading must not reset or change correspondence.");
                    FPVideoLensCalibration.Capture(new FPVideoUnwrapSettings()).Apply(s);
                    Assert.That(s.colorExposure,Is.EqualTo(-4));Assert.That(s.colorTuning,Is.True,"Lens profiles preserve output grading.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void GradeSettingsRejectInvalidActiveControlsAndSnapshotExportValues()
        {
            var s=new FPVideoUnwrapSettings { colorTuning=true,colorExposure=1,colorTemperature=.2f,colorTint=-.3f,colorSaturation=1.2f,colorContrast=.8f };
            s.Validate();var copy=s.Snapshot();s.colorExposure=2;
            Assert.That(copy.colorExposure,Is.EqualTo(1));Assert.That(copy.colorTemperature,Is.EqualTo(.2f));
            var restored=JsonUtility.FromJson<FPVideoUnwrapSettings>(JsonUtility.ToJson(copy));
            Assert.That(restored.colorTint,Is.EqualTo(-.3f));Assert.That(restored.colorSaturation,Is.EqualTo(1.2f));
            s.colorExposure=float.NaN;Assert.Throws<ArgumentException>(()=>s.Validate());
            s.colorExposure=0;s.colorContrast=2.1f;Assert.Throws<ArgumentException>(()=>s.Validate());
        }
    }
}
