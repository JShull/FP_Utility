// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Unity.Collections;
    using Unity.Burst;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEditor.Media;
    using UnityEngine;

    /// <summary>Version-checked boundary around Unity's internal Editor decoder. Main thread only.</summary>
    internal sealed class FPVideoNativeDecoder : IDisposable
    {
        private delegate bool FrameReader(Texture2D texture, out MediaTime time);
        private delegate int SampleReader(ushort track, NativeArray<float> samples);
        private readonly IDisposable decoder;
        private readonly FrameReader readFrame;
        private readonly SampleReader readSamples;
        private readonly Func<MediaTime, bool> seek;
        private FPVideoNativeDecoder second;
        private Texture2D lensA, lensB;
        private Texture2D decodeFrame;
        private readonly int nativeWidth, nativeHeight;
        private readonly bool resizeFrames;

        [BurstCompile]
        private struct ResizeFrame : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> Source;
            [WriteOnly] public NativeArray<Color32> Destination;
            public int Width, Height, OutputWidth, OutputHeight;
            private float4 Sample(int x,int y)
            {
                Color32 c=Source[math.clamp(y,0,Height-1)*Width+math.clamp(x,0,Width-1)];
                return new float4(c.r,c.g,c.b,c.a);
            }
            public void Execute(int index)
            {
                float x=(index%OutputWidth+.5f)*Width/OutputWidth-.5f;
                float y=(index/OutputWidth+.5f)*Height/OutputHeight-.5f;
                int ix=(int)math.floor(x),iy=(int)math.floor(y);
                float4 a=math.lerp(Sample(ix,iy),Sample(ix+1,iy),x-ix);
                float4 b=math.lerp(Sample(ix,iy+1),Sample(ix+1,iy+1),x-ix);
                float4 c=math.clamp(math.round(math.lerp(a,b,y-iy)),0,255);
                Destination[index]=new Color32((byte)c.x,(byte)c.y,(byte)c.z,(byte)c.w);
            }
        }
        internal static Type DecoderType => typeof(MediaEncoder).Assembly.GetType("UnityEditorInternal.Media.MediaDecoder");
        internal static bool Available => DecoderType?.GetMethod("GetNextFrame") != null && DecoderType.GetMethod("SetPosition") != null && DecoderType.GetMethod("GetNextSamples") != null;

        internal FPVideoNativeDecoder(string path, string secondPath = null, bool resizeFrames = true)
        {
            this.resizeFrames=resizeFrames;
            if (!Available) throw new NotSupportedException("This Unity version does not expose the expected native Editor decoder. No external decoder is installed automatically.");
            object instance;
            try { instance = Activator.CreateInstance(DecoderType, path); }
            catch (TargetInvocationException e) { throw new InvalidOperationException("Unity could not decode this media: " + e.InnerException?.Message, e.InnerException); }
            decoder = (IDisposable)instance;
            try
            {
                if (resizeFrames) { var info=FPVideoIsoMediaInfo.Read(path);nativeWidth=info.Width;nativeHeight=info.Height; }
                readFrame = (FrameReader)Delegate.CreateDelegate(typeof(FrameReader), instance, DecoderType.GetMethod("GetNextFrame"));
                readSamples = (SampleReader)Delegate.CreateDelegate(typeof(SampleReader), instance, DecoderType.GetMethod("GetNextSamples"));
                seek = (Func<MediaTime, bool>)Delegate.CreateDelegate(typeof(Func<MediaTime, bool>), instance, DecoderType.GetMethod("SetPosition"));
                if (secondPath != null) second = new FPVideoNativeDecoder(secondPath);
            }
            catch { decoder.Dispose(); throw; }
        }
        internal void Seek(double seconds)
        {
            if (!seek(new MediaTime((long)Math.Round(seconds * 1000000), 1000000, 1))) throw new InvalidOperationException("Native decoder could not seek to the requested time.");
            second?.Seek(seconds);
        }
        // Sample-time seeking is qualified against sequential PCM on this backend only.
        // Other configurations retain the sequential audio path until independently qualified.
        internal static bool SupportsAudioSeek(int sampleRate) => sampleRate == 48000 &&
            Application.platform == RuntimePlatform.WindowsEditor && Application.unityVersion.StartsWith("6000.6.", StringComparison.Ordinal);
        internal void SeekAudio(long sampleFrame, int sampleRate)
        {
            if (!SupportsAudioSeek(sampleRate)) throw new NotSupportedException("Sample-time audio seeking is not qualified on this configuration.");
            if (sampleFrame <= 0) throw new ArgumentOutOfRangeException(nameof(sampleFrame));
            // Windows MediaDecoder starts one PCM frame after the requested sample time.
            // Use the audio clock directly; rounding through microseconds loses sample precision.
            if (!seek(new MediaTime(sampleFrame - 1, checked((uint)sampleRate), 1)))
                throw new InvalidOperationException("Native decoder could not seek to the audio preroll.");
        }
        internal bool Read(Texture2D texture, out double seconds)
        {
            if (second != null && (lensA == null || lensA.width*2 != texture.width || lensA.height != texture.height))
            {
                ReleaseLenses();
                if (texture.width % 2 != 0) throw new ArgumentException("Paired lens output requires an even width.");
                lensA = new Texture2D(texture.width/2,texture.height,TextureFormat.RGBA32,false) { hideFlags=HideFlags.HideAndDontSave };
                lensB = new Texture2D(texture.width/2,texture.height,TextureFormat.RGBA32,false) { hideFlags=HideFlags.HideAndDontSave };
            }
            Texture2D target = second == null ? texture : lensA;
            bool resize=resizeFrames&&(target.width!=nativeWidth||target.height!=nativeHeight);
            if (resize && decodeFrame == null)
                decodeFrame=new Texture2D(nativeWidth,nativeHeight,TextureFormat.RGBA32,false) { hideFlags=HideFlags.HideAndDontSave };
            bool result = readFrame(resize ? decodeFrame : target, out var time);
            if (result && resize)
                new ResizeFrame { Source=decodeFrame.GetRawTextureData<Color32>(),Destination=target.GetRawTextureData<Color32>(),
                    Width=nativeWidth,Height=nativeHeight,OutputWidth=target.width,OutputHeight=target.height }
                    .Schedule(target.width*target.height,256).Complete();
            // The Editor decoder writes CPU texture storage; publish it before GPU projection.
            if (result) target.Apply(false, false);
            seconds = time.rate.numerator > 0 ? time.count * (double)time.rate.denominator / time.rate.numerator : double.NaN;
            if (second != null)
            {
                bool other = second.Read(lensB,out double otherSeconds);
                if (result != other || (result && (double.IsNaN(seconds) || double.IsNaN(otherSeconds) || Math.Abs(seconds-otherSeconds) > .00001)))
                    throw new InvalidDataException("Lens tracks did not decode at the same timestamp.");
                if (result)
                {
                    Graphics.CopyTexture(lensA,0,0,0,0,lensA.width,lensA.height,texture,0,0,0,0);
                    Graphics.CopyTexture(lensB,0,0,0,0,lensB.width,lensB.height,texture,0,0,lensA.width,0);
                }
            }
            return result;
        }
        internal double ReadAt(Texture2D texture, double seconds)
        {
            Seek(seconds);
            // Platform decoders can seek to the preceding sample/keyframe. Never label it as the requested frame.
            for (int i = 0; i < 512; i++)
            {
                if (!Read(texture, out double pts)) throw new EndOfStreamException("No frame at the requested source time.");
                if (pts + .000001 >= seconds) return pts;
            }
            throw new InvalidOperationException("Native seek preroll exceeded 512 frames.");
        }
        internal int ReadAudio(NativeArray<float> samples) => readSamples(0, samples);
        private void ReleaseLenses() { UnityEngine.Object.DestroyImmediate(lensA); UnityEngine.Object.DestroyImmediate(lensB); lensA=lensB=null; }
        public void Dispose() { second?.Dispose(); ReleaseLenses(); UnityEngine.Object.DestroyImmediate(decodeFrame); decoder.Dispose(); }
    }

    /// <summary>Stage unsupported extensions and paired-lens track views without modifying the original.</summary>
    internal sealed class FPVideoNativeSource : IDisposable
    {
        internal FPVideoImuMotion Motion { get; private set; }
        internal void LoadMotion(FPVideoCameraMetadata metadata)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FPVideoNativeSource));
            if (Motion == null) Motion = new FPVideoImuMotion(OriginalPath, metadata, Info.Duration);
        }
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly string stagingFolder;
        private long copied;
        private readonly long length;
        internal readonly string OriginalPath, DecodePath, SecondDecodePath;
        internal readonly FPVideoIsoMediaInfo Info;
        internal Task Ready { get; }
        internal float Progress => length == 0 ? 0 : (float)((double)Interlocked.Read(ref copied) / length);
        private bool disposed;

        internal FPVideoNativeSource(string path)
        {
            OriginalPath = Path.GetFullPath(path);
            Info = FPVideoIsoMediaInfo.Read(OriginalPath);
            length = new FileInfo(OriginalPath).Length;
            bool insv = string.Equals(Path.GetExtension(path), ".insv", StringComparison.OrdinalIgnoreCase);
            bool paired = insv && Info.VideoTracks.Count == 2;
            if (Info.VideoTracks.Count > 1 && !paired) throw new NotSupportedException("Multiple video tracks require a supported two-lens INSV recording.");
            if (paired)
            {
                var a=Info.VideoTracks[0]; var b=Info.VideoTracks[1];
                if (a.Width != a.Height || a.Width != b.Width || a.Height != b.Height || a.Frames != b.Frames ||
                    a.Scale != b.Scale || a.Delta != b.Delta || !a.Constant || !b.Constant || a.Width*2 > 8192)
                    throw new NotSupportedException("INSV requires two matching square, constant-rate lens tracks with combined width up to 8192.");
                Info.Width *= 2; length *= 2;
            }
            if (!insv && !string.Equals(Path.GetExtension(path), ".lrv", StringComparison.OrdinalIgnoreCase))
            { DecodePath = OriginalPath; Ready = Task.CompletedTask; return; }
            stagingFolder = Path.Combine(Path.GetTempPath(), "FuzzPhyteVideo", Guid.NewGuid().ToString("N"));
            DecodePath = Path.Combine(stagingFolder, "source.mp4");
            if (paired) SecondDecodePath = Path.Combine(stagingFolder,"lens_b.mp4");
            var token = cancellation.Token;
            Ready = Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(stagingFolder);
                    int copies = paired ? 2 : 1;
                    for (int selected=0;selected<copies;selected++)
                    {
                        using (var input = File.OpenRead(OriginalPath))
                        using (var output = new FileStream(selected == 0 ? DecodePath : SecondDecodePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[1024 * 1024]; int n;
                            while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                            { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, n); Interlocked.Add(ref copied, n); }
                            if (paired)
                            {
                                // Retain all bytes/offsets; hide only the other track's index box.
                                output.Position=Info.VideoTracks[1-selected].TypeOffset;
                                output.Write(new byte[]{(byte)'f',(byte)'r',(byte)'e',(byte)'e'},0,4);
                            }
                        }
                    }
                    token.ThrowIfCancellationRequested();
                }
                catch { DeleteStaging(); throw; }
            }, token);
        }
        private void DeleteStaging()
        {
            if (stagingFolder == null) return;
            try { if (File.Exists(DecodePath)) File.Delete(DecodePath); if (SecondDecodePath != null && File.Exists(SecondDecodePath)) File.Delete(SecondDecodePath); if (Directory.Exists(stagingFolder)) Directory.Delete(stagingFolder); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            Motion?.Dispose(); Motion = null;
            cancellation.Cancel();
            // The copy worker owns its file handles until completion, including cancellation races.
            _ = Ready.ContinueWith(_ => { DeleteStaging(); cancellation.Dispose(); }, TaskScheduler.Default);
        }
    }
}
