// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class FPVideoFarnebackTests
    {
        private static float Pattern(float x,float y) => .5f+.15f*Mathf.Sin(.13f*x+.07f*y)+.13f*Mathf.Cos(.08f*x-.19f*y)+.12f*Mathf.Sin(.3f*x+.21f*y)+.05f*Mathf.Cos(.47f*x-.33f*y);
        private static Texture2D Image(int charts,Vector2 first,Vector2 second,bool flat=false,bool masked=false)
        {
            int width=128*charts;var texture=new Texture2D(width,128,TextureFormat.RGBAFloat,false,true);
            var pixels=new Color[width*128];
            for(int y=0;y<128;y++)for(int x=0;x<width;x++)
            {
                int chart=x/128;var d=chart==0?first:second;float value=flat?.5f:Pattern(x%128-d.x,y-d.y);
                pixels[y*width+x]=new Color(value,value,value,masked&&x%128>40&&x%128<88&&y>40&&y<88?0:1);
            }
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        internal static Color[] Read(RenderTexture texture)
        {
            var copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
            try { RenderTexture.active=texture;copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0,false);return copy.GetPixels(); }
            finally { RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(copy); }
        }
        private static void RequireGpu() { if(!FPVideoFarneback.Supported)Assert.Ignore("Float32 compute shaders unavailable on this device."); }

        [TestCase(0,0)] [TestCase(2.5f,1.5f)] [TestCase(-2,1)]
        public void KnownTranslationsHaveSubPixelEndpointError(float dx,float dy)
        {
            RequireGpu();var a=Image(1,Vector2.zero,Vector2.zero);var b=Image(1,new Vector2(dx,dy),Vector2.zero);
            try
            {
                using(var solver=new FPVideoFarneback())
                {
                    solver.Solve(a,b,1,new Vector2(8,8));var pixels=Read(solver.Result);var errors=new List<float>();int confident=0;
                    for(int y=32;y<96;y++)for(int x=32;x<96;x++)
                    {
                        var p=pixels[y*128+x];Assert.That(float.IsNaN(p.r)||float.IsNaN(p.g)||float.IsInfinity(p.r)||float.IsInfinity(p.g),Is.False);
                        errors.Add(Vector2.Distance(new Vector2(p.r,p.g),new Vector2(dx,dy)));if(p.b>=.35f)confident++;
                    }
                    errors.Sort();TestContext.WriteLine($"shift=({dx},{dy}) median={errors[errors.Count/2]:F4} p95={errors[(int)(errors.Count*.95)]:F4} confidence={(float)confident/errors.Count:F3}");
                    Assert.That(errors[errors.Count/2],Is.LessThanOrEqualTo(.5));Assert.That(errors[(int)(errors.Count*.95)],Is.LessThanOrEqualTo(1));
                    Assert.That((float)confident/errors.Count,Is.GreaterThan(.7));
                    solver.Solve(a,b,1,new Vector2(8,8));var repeated=Read(solver.Result);
                    for(int i=0;i<pixels.Length;i++)Assert.That(Mathf.Abs(pixels[i].r-repeated[i].r)+Mathf.Abs(pixels[i].g-repeated[i].g),Is.LessThanOrEqualTo(.001));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b); }
        }
        [Test]
        public void IndependentChartsDoNotExchangeDisplacement()
        {
            RequireGpu();var a=Image(2,Vector2.zero,Vector2.zero);var b=Image(2,new Vector2(2,-1),new Vector2(-1,2));
            try
            {
                using(var solver=new FPVideoFarneback())
                {
                    solver.Solve(a,b,2,new Vector2(8,8));var pixels=Read(solver.Result);
                    for(int chart=0;chart<2;chart++)
                    {
                        var errors=new List<float>();var expected=chart==0?new Vector2(2,-1):new Vector2(-1,2);
                        for(int y=32;y<96;y++)for(int x=32;x<96;x++){var p=pixels[y*256+chart*128+x];errors.Add(Vector2.Distance(new Vector2(p.r,p.g),expected));}
                        errors.Sort();Assert.That(errors[errors.Count/2],Is.LessThanOrEqualTo(.5));
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b); }
        }
        [Test]
        public void FlatAndMaskedRegionsRejectFlowAndAllVectorsStayBounded()
        {
            RequireGpu();var a=Image(1,Vector2.zero,Vector2.zero,true);var b=Image(1,Vector2.zero,Vector2.zero,true);
            try
            {
                using(var solver=new FPVideoFarneback())
                {
                    solver.Solve(a,b,1,new Vector2(4,4));foreach(var p in Read(solver.Result)){Assert.That(p.b,Is.LessThan(.05));Assert.That(float.IsNaN(p.r)||float.IsNaN(p.g),Is.False);}
                    UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);a=Image(1,Vector2.zero,Vector2.zero);b=Image(1,new Vector2(3,1),Vector2.zero,false,true);
                    solver.Solve(a,b,1,new Vector2(4,4));var result=Read(solver.Result);
                    for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                    {
                        var p=result[y*128+x];Assert.That(float.IsNaN(p.r)||float.IsNaN(p.g),Is.False);Assert.That(new Vector2(p.r/4,p.g/4).magnitude,Is.LessThanOrEqualTo(1.001));
                        if(x>48&&x<80&&y>48&&y<80)Assert.That(p.b,Is.LessThan(.05));
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b); }
        }
    }
}
