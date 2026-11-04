// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class FPVideoToneCurveTests
    {
        private static Texture2D Ramp()
        {
            var texture=new Texture2D(256,128,TextureFormat.RGBA32,false,false);
            var pixels=new Color[256*128];
            for(int y=0;y<128;y++)for(int x=0;x<256;x++)pixels[y*256+x]=new Color(x/255f,x/255f,x/255f,1);
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        private static Color[] Read(FPVideoUnwrapRenderer renderer,Texture source,FPVideoUnwrapSettings settings)
        {
            renderer.Render(source,settings,256,128);return FPVideoFarnebackTests.Read(renderer.Output);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void CurveRampsAreBoundedMonotonicAndMatchDeclaredReference(int preset)
        {
            var source=Ramp();
            try
            {
                using(var renderer=new FPVideoUnwrapRenderer())
                {
                    var settings=new FPVideoUnwrapSettings { width=256,height=128,sourceProjection=FPVideoSourceProjection.Equirectangular,colorTuning=true };
                    var baseline=Read(renderer,source,settings);
                    Assert.That(baseline[64*256+64].r,Is.InRange(.24f,.27f),"The ungraded fixture must render the ramp, not a shader-error fallback.");
                    settings.colorCurve=(FPVideoToneCurve)preset;settings.colorGamma=1.4f;
                    var curved=Read(renderer,source,settings);
                    for(int x=0;x<256;x++)
                    {
                        int i=64*256+x;float code=baseline[i].r;
                        double expected=preset==1 ? .5*code+.5*code*code*(3-2*code) : preset==2 ?
                            Math.Log(1+112*Mathf.GammaToLinearSpace(code))/Math.Log(113) : Math.Pow(code,1/1.4);
                        if(preset>=4)
                        {
                            double luminance=Mathf.GammaToLinearSpace(code);
                            if(preset>=5)luminance+=2*luminance*Math.Pow(1-luminance,6);
                            expected=Mathf.LinearToGammaSpace((float)luminance);
                            if(preset==4||preset==6)expected=.65*expected+.35*expected*expected*(3-2*expected);
                        }
                        Assert.That(curved[i].r,Is.InRange(0f,1f));
                        Assert.That(curved[i].r,Is.EqualTo(expected).Within(.009),$"Preset {preset}, code {code}");
                        if(x>0)Assert.That(curved[i].r,Is.GreaterThanOrEqualTo(curved[i-1].r-.00001));
                    }
                    Assert.That(curved[64*256].r,Is.LessThan(.009));Assert.That(curved[64*256+255].r,Is.GreaterThan(.991));
                    settings.colorCurveStrength=.5f;var half=Read(renderer,source,settings);
                    Assert.That(half[64*256+64].r,Is.EqualTo((baseline[64*256+64].r+curved[64*256+64].r)*.5f).Within(.009));
                    settings.colorCurveStrength=0;var zero=Read(renderer,source,settings);
                    for(int i=0;i<zero.Length;i++)Assert.That(zero[i],Is.EqualTo(baseline[i]));
                    settings.colorTuning=false;settings.colorCurveStrength=1;
                    Assert.That(Read(renderer,source,settings)[64*256+64],Is.EqualTo(baseline[64*256+64]));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void ShadowDetailLiftsDarkSwatchesAndVividBoostsMutedColorWithoutChangingDiagnostics()
        {
            var source=new Texture2D(256,128,TextureFormat.RGBA32,false,false);
            var pixels=new Color[256*128];
            for(int y=0;y<128;y++)for(int x=0;x<256;x++)
                pixels[y*256+x]=x<64?Color.black:x<128?new Color(.15f,.15f,.15f):x<192?new Color(.85f,.85f,.85f):new Color(.2f,.25f,.3f);
            source.SetPixels(pixels);source.Apply();
            try
            {
                using(var renderer=new FPVideoUnwrapRenderer())
                {
                    var s=new FPVideoUnwrapSettings { width=256,height=128,sourceProjection=FPVideoSourceProjection.Equirectangular,colorTuning=true };
                    var baseline=Read(renderer,source,s);s.colorCurve=FPVideoToneCurve.ShadowDetail;var shadow=Read(renderer,source,s);
                    Assert.That(shadow[64*256+32].r,Is.EqualTo(0));
                    Assert.That(shadow[64*256+96].r,Is.GreaterThan(baseline[64*256+96].r+.05f));
                    Assert.That(Mathf.Abs(shadow[64*256+160].r-baseline[64*256+160].r),Is.LessThan(.009));
                    s.colorCurve=FPVideoToneCurve.Vivid;var vivid=Read(renderer,source,s);
                    Color a=baseline[64*256+224].linear,b=vivid[64*256+224].linear;
                    float chromaA=(Mathf.Max(a.r,Mathf.Max(a.g,a.b))-Mathf.Min(a.r,Mathf.Min(a.g,a.b)))/(.2126f*a.r+.7152f*a.g+.0722f*a.b);
                    float chromaB=(Mathf.Max(b.r,Mathf.Max(b.g,b.b))-Mathf.Min(b.r,Mathf.Min(b.g,b.b)))/(.2126f*b.r+.7152f*b.g+.0722f*b.b);
                    Assert.That(chromaB,Is.GreaterThan(chromaA*1.1f));
                    s.colorCurve=FPVideoToneCurve.VividShadowDetail;var combined=Read(renderer,source,s);
                    Assert.That(combined[64*256+96].r,Is.GreaterThan(baseline[64*256+96].r));
                    renderer.Render(source,s,256,128,FPVideoCalibrationView.LensA);var raw=FPVideoFarnebackTests.Read(renderer.Output);
                    Assert.That(raw[64*256+224],Is.EqualTo(baseline[64*256+224]));
                    var copy=JsonUtility.FromJson<FPVideoUnwrapSettings>(JsonUtility.ToJson(s));Assert.That(copy.colorCurve,Is.EqualTo(FPVideoToneCurve.VividShadowDetail));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void GammaOneIsExactBypassAndCurveControlsRoundTripWithoutAffectingLensProfiles()
        {
            var source=Ramp();
            try
            {
                using(var renderer=new FPVideoUnwrapRenderer())
                {
                    var s=new FPVideoUnwrapSettings { width=256,height=128,sourceProjection=FPVideoSourceProjection.Equirectangular,colorTuning=true };
                    var baseline=Read(renderer,source,s);s.colorCurve=FPVideoToneCurve.CustomGamma;
                    var identity=Read(renderer,source,s);for(int i=0;i<identity.Length;i++)Assert.That(identity[i],Is.EqualTo(baseline[i]));
                    s.colorGamma=1.2f;s.colorCurveStrength=.6f;
                    var copy=JsonUtility.FromJson<FPVideoUnwrapSettings>(JsonUtility.ToJson(s));
                    Assert.That(copy.colorCurve,Is.EqualTo(s.colorCurve));Assert.That(copy.colorGamma,Is.EqualTo(1.2f));Assert.That(copy.colorCurveStrength,Is.EqualTo(.6f));
                    FPVideoLensCalibration.Capture(new FPVideoUnwrapSettings()).Apply(s);
                    Assert.That(s.colorCurve,Is.EqualTo(FPVideoToneCurve.CustomGamma));Assert.That(s.colorGamma,Is.EqualTo(1.2f));
                    s.colorCurve=(FPVideoToneCurve)99;Assert.Throws<ArgumentException>(()=>s.Validate());
                    s.colorCurve=FPVideoToneCurve.Neutral;s.colorGamma=float.NaN;Assert.Throws<ArgumentException>(()=>s.Validate());
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}
