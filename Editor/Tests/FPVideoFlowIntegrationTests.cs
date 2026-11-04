// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class FPVideoFlowIntegrationTests
    {
        internal static Texture2D SyntheticLenses()
        {
            const int width=1024,height=512;var texture=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true);
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int lens=x/(width/2);float u=((x+.5f)/width-(lens==0?.25f:.75f))/.25f,v=((y+.5f)/height-.5f)/.5f;
                float radius=Mathf.Sqrt(u*u+v*v);if(radius>1)continue;
                float angle=radius*98.2f*Mathf.Deg2Rad;
                var ray=new Vector3(u*Mathf.Sin(angle)/Mathf.Max(radius,.000001f),v*Mathf.Sin(angle)/Mathf.Max(radius,.000001f),Mathf.Cos(angle));
                if(lens==1)ray=Quaternion.Euler(0,181,0)*ray; // Deliberate one-degree camera-model error.
                float lon=Mathf.Atan2(ray.x,ray.z),lat=Mathf.Asin(Mathf.Clamp(ray.y,-1,1));
                float value=.5f+.15f*Mathf.Sin(17*lon+7*lat)+.13f*Mathf.Cos(8*lon-19*lat)+.12f*Mathf.Sin(30*lon+21*lat)+.05f*Mathf.Cos(47*lon-33*lat);
                pixels[y*width+x]=new Color(value,value,value,1);
            }
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        [Test]
        public void ReprojectedOneDegreeMisalignmentIsRecoveredAndWarpReducesDifference()
        {
            if(!FPVideoFarneback.Supported)Assert.Ignore("Compute shaders unavailable.");
            var source=SyntheticLenses();
            try
            {
                var settings=new FPVideoUnwrapSettings{width=1024,height=512,lensFov=196.4f,useDirectionalSeam=true,opticalFlow=true,flowMinConfidence=.35f};
                using(var renderer=new FPVideoUnwrapRenderer())
                {
                    renderer.Render(source,settings,1024,512,FPVideoCalibrationView.Difference,.5f,1);var raw=FPVideoFarnebackTests.Read(renderer.Output);
                    renderer.Render(source,settings,1024,512,FPVideoCalibrationView.WarpedDifference,.5f,1);var corrected=FPVideoFarnebackTests.Read(renderer.Output);
                    var field=FPVideoFarnebackTests.Read(renderer.FlowField);int cw=renderer.FlowField.width/2,fh=renderer.FlowField.height;
                    var errors=new List<float>();int confident=0;
                    for(int chart=0;chart<2;chart++)for(int y=fh/4;y<fh*3/4;y++)for(int x=cw*3/8;x<cw*5/8;x++)
                    {
                        var p=field[y*cw*2+chart*cw+x];errors.Add(new Vector2(p.r*32/cw+1,p.g*160/fh).magnitude);if(p.b>=.35f)confident++;
                    }
                    errors.Sort();float coverage=(float)confident/errors.Count;
                    Assert.That(errors[errors.Count/2],Is.LessThanOrEqualTo(.5));Assert.That(coverage,Is.GreaterThanOrEqualTo(.4));
                    double before=0,after=0;int accepted=0;
                    for(int y=128;y<384;y++)for(int x=0;x<1024;x++)
                    {
                        if(Math.Abs(x-256)>10&&Math.Abs(x-768)>10)continue;
                        Color a=raw[y*1024+x],b=corrected[y*1024+x];if(b.r>.9f&&b.b>.9f&&b.g<.1f)continue;
                        before+=a.r;after+=b.r;accepted++;
                    }
                    TestContext.WriteLine($"angular median={errors[errors.Count/2]:F4}° coverage={coverage:F3} corrected/raw={after/before:F3} samples={accepted}");
                    Assert.That(accepted,Is.GreaterThan(100));Assert.That(after,Is.LessThanOrEqualTo(before*.7));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(source);}
        }
        [Test]
        public void RejectedUntexturedMatchesPreserveTheFeatheredFlowOffBlend()
        {
            if(!FPVideoFarneback.Supported)Assert.Ignore("Compute shaders unavailable.");
            var source=new Texture2D(320,160,TextureFormat.RGBAFloat,false,true);
            var pixels=new Color[320*160];
            for(int i=0;i<pixels.Length;i++)pixels[i]=i%320<160?Color.red:Color.blue;
            source.SetPixels(pixels);source.Apply();
            try
            {
                var settings=new FPVideoUnwrapSettings { width=320,height=160,lensFov=196.4f,useDirectionalSeam=true,seamWidth=10 };
                using(var renderer=new FPVideoUnwrapRenderer())
                {
                    renderer.Render(source,settings,320,160);var baseline=FPVideoFarnebackTests.Read(renderer.Output);
                    settings.opticalFlow=true;renderer.Render(source,settings,320,160);var corrected=FPVideoFarnebackTests.Read(renderer.Output);
                    int blended=0;
                    for(int y=60;y<100;y++)for(int x=0;x<320;x++)
                    {
                        var a=baseline[y*320+x];if(a.r<.1f||a.b<.1f)continue;
                        blended++;var b=corrected[y*320+x];
                        Assert.That(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b),Is.LessThan(.00001),"Rejected flow must retain both feathered lens contributions.");
                    }
                    Assert.That(blended,Is.GreaterThan(100),"Fixture must cover the blended seam.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test]
        public void ZeroStrengthIsExactlyTheFlowOffCompositeAndProfilesMigrateSafely()
        {
            var source=SyntheticLenses();
            try
            {
                var settings=new FPVideoUnwrapSettings{width=1024,height=512,lensFov=196.4f,useDirectionalSeam=true};
                using(var renderer=new FPVideoUnwrapRenderer())
                {
                    renderer.Render(source,settings,320,160);var baseline=FPVideoFarnebackTests.Read(renderer.Output);
                    settings.opticalFlow=true;settings.flowStrength=0;renderer.Render(source,settings,320,160);var zero=FPVideoFarnebackTests.Read(renderer.Output);
                    for(int i=0;i<zero.Length;i++)Assert.That(Vector3.Distance(new Vector3(zero[i].r,zero[i].g,zero[i].b),new Vector3(baseline[i].r,baseline[i].g,baseline[i].b)),Is.LessThan(.00001));
                }
                var profile=FPVideoLensCalibration.Capture(settings);var copy=FPVideoLensCalibration.Parse(JsonUtility.ToJson(profile));
                var target=new FPVideoUnwrapSettings();copy.Apply(target);Assert.That(target.opticalFlow,Is.True);Assert.That(target.lensFov,Is.EqualTo(196.4f));
                profile.version=1;profile.opticalFlow=false;FPVideoLensCalibration.Parse(JsonUtility.ToJson(profile)).Apply(target);Assert.That(target.opticalFlow,Is.False);
                profile.version=2;profile.opticalFlow=true;profile.flowMethod="Unknown solver";Assert.Throws<InvalidDataException>(()=>profile.Apply(target));
                Assert.That(target.opticalFlow,Is.False);
            }
            finally{UnityEngine.Object.DestroyImmediate(source);}
        }
    }
}
