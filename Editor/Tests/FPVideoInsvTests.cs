// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using UnityEditor.Media;
    using UnityEngine;

    public sealed class FPVideoInsvTests
    {
        private static byte[] U32(uint n) => new[]{(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n};
        private static byte[] Box(string type,params byte[][] data)
        {
            var payload=data.SelectMany(x=>x).ToArray();
            return U32((uint)(8+payload.Length)).Concat(Encoding.ASCII.GetBytes(type)).Concat(payload).ToArray();
        }
        private static byte[] Track(int width=64)
        {
            var sample=new byte[28];sample[24]=(byte)(width>>8);sample[25]=(byte)width;sample[27]=64;
            return Box("trak",Box("mdia",
                Box("hdlr",new byte[8],Encoding.ASCII.GetBytes("vide")),
                Box("mdhd",new byte[12],U32(5000),U32(1000)),
                Box("minf",Box("stbl",Box("stsd",new byte[4],U32(1),Box("hvc1",sample)),
                    Box("stts",new byte[4],U32(1),U32(5),U32(200))))));
        }

        [Test]
        public void TwoLensStagingPreservesOffsetsAndOriginalAndCleansUpBothViews()
        {
            string folder=Path.Combine(Path.GetTempPath(),"FPVideoInsvTest_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            string a=null,b=null;
            try
            {
                byte[] bytes=Box("moov",Track(),Track());string path=Path.Combine(folder,"dual.insv");File.WriteAllBytes(path,bytes);
                using(var source=new FPVideoNativeSource(path))
                {
                    source.Ready.GetAwaiter().GetResult();a=source.DecodePath;b=source.SecondDecodePath;
                    Assert.That(source.Info.Width,Is.EqualTo(128));Assert.That(source.Info.Height,Is.EqualTo(64));
                    Assert.That(source.Progress,Is.EqualTo(1));
                    Assert.That(FPVideoIsoMediaInfo.Read(a).VideoTracks.Count,Is.EqualTo(1));
                    Assert.That(FPVideoIsoMediaInfo.Read(b).VideoTracks.Count,Is.EqualTo(1));
                    byte[] first=File.ReadAllBytes(a),second=File.ReadAllBytes(b);
                    for(int i=0;i<bytes.Length;i++)
                    {
                        bool hiddenA=i>=source.Info.VideoTracks[1].TypeOffset&&i<source.Info.VideoTracks[1].TypeOffset+4;
                        bool hiddenB=i>=source.Info.VideoTracks[0].TypeOffset&&i<source.Info.VideoTracks[0].TypeOffset+4;
                        if(!hiddenA)Assert.That(first[i],Is.EqualTo(bytes[i]));
                        if(!hiddenB)Assert.That(second[i],Is.EqualTo(bytes[i]));
                    }
                    Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
                }
                Assert.That(System.Threading.SpinWait.SpinUntil(()=>!File.Exists(a)&&!File.Exists(b),2000),Is.True);
                File.WriteAllBytes(Path.Combine(folder,"mismatch.insv"),Box("moov",Track(),Track(32)));
                Assert.Throws<NotSupportedException>(()=>new FPVideoNativeSource(Path.Combine(folder,"mismatch.insv")));
            }
            finally { Directory.Delete(folder,true); }
        }

        internal static void Encode(string path,Color color)
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            try
            {
                texture.SetPixels(Enumerable.Repeat(color,64*64).ToArray());texture.Apply();
                using(var encoder=new MediaEncoder(path,new VideoTrackAttributes { width=64,height=64,frameRate=new MediaRational(25),includeAlpha=false }))
                    for(int i=0;i<4;i++)Assert.That(encoder.AddFrame(texture),Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void PairedNativeDecodeCombinesDistinctLensesOnGpuAndSeeksTogether()
        {
            if(!FPVideoNativeDecoder.Available)Assert.Ignore("Native Editor decoder unavailable.");
            string folder=Path.Combine(Path.GetTempPath(),"FPVideoInsvTest_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            var texture=new Texture2D(128,64,TextureFormat.RGBA32,false);var target=new RenderTexture(128,64,0);
            try
            {
                string a=Path.Combine(folder,"a.mp4"),b=Path.Combine(folder,"b.mp4");Encode(a,Color.red);Encode(b,Color.blue);
                using(var decoder=new FPVideoNativeDecoder(a,b))
                {
                    foreach(double time in new[]{.04,.08,0})
                    {
                        Assert.That(decoder.ReadAt(texture,time),Is.EqualTo(time).Within(.00001));
                        Graphics.Blit(texture,target);var pixels=FPVideoFarnebackTests.Read(target);
                        Assert.That(pixels[32*128+32].r,Is.GreaterThan(.8));Assert.That(pixels[32*128+32].b,Is.LessThan(.1));
                        Assert.That(pixels[32*128+96].b,Is.GreaterThan(.8));Assert.That(pixels[32*128+96].r,Is.LessThan(.1));
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture);target.Release();UnityEngine.Object.DestroyImmediate(target);Directory.Delete(folder,true); }
        }
    }
}
