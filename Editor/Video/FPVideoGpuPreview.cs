// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Video;

    /// <summary>Asynchronous Unity VideoPlayer textures; publish only matching lens frame indices.</summary>
    internal sealed class FPVideoGpuPreview : IDisposable
    {
        private static readonly HashSet<FPVideoGpuPreview> livePreviews = new HashSet<FPVideoGpuPreview>();
        static FPVideoGpuPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
        }
        private static void DisposeAll()
        {
            foreach (var preview in new List<FPVideoGpuPreview>(livePreviews)) preview.Dispose();
        }
        private readonly VideoPlayer[] players;
        private readonly RenderTexture[] lenses;
        private readonly VideoPlayer.FrameReadyEventHandler[] frameHandlers;
        private readonly VideoPlayer.ErrorEventHandler[] errorHandlers;
        private readonly long[] readyFrames;
        private readonly double frameRate;
        private readonly long frameCount;
        private readonly long missingFrame;
        private readonly double prepareStarted;
        private bool disposed, configured, nativePlaying, seeking;
        private long seekFrame, publishedFrame=-1;
        private double seekStarted, lastSeekRequest=double.NegativeInfinity;
        private string error;
        internal RenderTexture Texture { get; private set; }
        internal bool HasFrame => publishedFrame >= 0;
        internal double PresentedSeconds { get; private set; } = double.NaN;
        internal long PresentedFrames { get; private set; }
        internal long SeekCount { get; private set; }
        internal double LastSubmissionMilliseconds { get; private set; }
        internal string Status => !configured ? "Preparing GPU preview..." : seeking ? "Seeking GPU frame..." : "Unity GPU texture preview";

        internal FPVideoGpuPreview(string firstPath,string secondPath,int width,int height,double fps,long frames,long missingFrame = -1)
        {
            if(width<2 || height<2 || width>8192 || height>8192 || fps<=0 || double.IsNaN(fps) || double.IsInfinity(fps) || frames<=0)
                throw new ArgumentException("GPU preview requires bounded dimensions, finite positive frame rate and frame count.");
            int count=secondPath==null?1:2;
            if(width%count!=0)throw new ArgumentException("GPU preview width must divide evenly between lens tracks.");
            if (missingFrame < -1 || missingFrame == 0 || missingFrame >= frames) throw new ArgumentOutOfRangeException(nameof(missingFrame));
            frameRate=fps;frameCount=frames;this.missingFrame=missingFrame;prepareStarted=EditorApplication.timeSinceStartup;
            players=new VideoPlayer[count];lenses=new RenderTexture[count];readyFrames=new long[count];
            frameHandlers=new VideoPlayer.FrameReadyEventHandler[count];errorHandlers=new VideoPlayer.ErrorEventHandler[count];
            livePreviews.Add(this);
            try
            {
                Texture=CreateTexture(width,height);
                for(int i=0;i<count;i++)
                {
                    int index=i;readyFrames[i]=-1;
                    lenses[i]=CreateTexture(width/count,height);
                    var go=new GameObject("FP Video GPU preview lens "+i) { hideFlags=HideFlags.HideAndDontSave };
                    var player=go.AddComponent<VideoPlayer>();players[i]=player;
                    player.playOnAwake=false;player.isLooping=false;player.waitForFirstFrame=true;
                    player.source=VideoSource.Url;player.url=i==0?firstPath:secondPath;
                    player.audioOutputMode=VideoAudioOutputMode.None;
                    // Own the copy of each frame instead of reading a target that the player can
                    // overwrite independently while the other lens is still catching up.
                    player.renderMode=VideoRenderMode.APIOnly;
                    player.sendFrameReadyEvents=true;
                    frameHandlers[i]=(p,frame)=>
                    {
                        if(disposed)return;
                        var decoded = p.texture;
                        if (decoded == null) return;
                        bool previousSrgbWrite = GL.sRGBWrite;
                        try
                        {
                            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                            Graphics.Blit(decoded, lenses[index]);
                        }
                        finally { GL.sRGBWrite = previousSrgbWrite; }
                        readyFrames[index]=frame;
                        if(seeking && frame==seekFrame)p.Pause();
                    };
                    errorHandlers[i]=(p,message)=>{if(!disposed)error=message;};
                    player.frameReady+=frameHandlers[i];player.errorReceived+=errorHandlers[i];
                    player.Prepare();
                }
            }
            catch { Dispose();throw; }
        }

        private static RenderTexture CreateTexture(int width,int height)
        {
            var texture=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB)
                { hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp };
            if(!texture.Create()){UnityEngine.Object.DestroyImmediate(texture);throw new NotSupportedException("GPU preview texture allocation failed.");}
            return texture;
        }

        internal bool Request(double seconds,double now,bool scrubbing,bool playing)
        {
            if(disposed)throw new ObjectDisposedException(nameof(FPVideoGpuPreview));
            if(double.IsNaN(seconds)||double.IsInfinity(seconds))throw new ArgumentException("GPU preview time must be finite.");
            long timelineTarget=Math.Max(0,Math.Min(frameCount-1+(missingFrame>=0?1:0),(long)Math.Floor(seconds*frameRate+.000001)));
            long target=SourceFrame(timelineTarget,missingFrame);
            if(!configured||nativePlaying||seeking||playing||target!=publishedFrame)EditorApplication.QueuePlayerLoopUpdate();
            if(error!=null)throw new InvalidOperationException("GPU preview: "+error+" Choose CPU preview to retry with the Editor decoder.");
            if(!configured)
            {
                for(int i=0;i<players.Length;i++)
                    if(!players[i].isPrepared)
                    {
                        if(EditorApplication.timeSinceStartup-prepareStarted>20)throw new TimeoutException("GPU preview did not prepare. Choose CPU preview.");
                        return false;
                    }
                for(int i=0;i<players.Length;i++)
                {
                    if(Math.Abs(players[i].frameRate-frameRate)>Math.Max(.01,missingFrame >= 0 ? frameRate * .001 : .01))throw new NotSupportedException("GPU decoder frame rate differs from source timing.");
                    if(players[i].canSetSkipOnDrop)players[i].skipOnDrop=false;
                }
                configured=true;
            }
            long started=Stopwatch.GetTimestamp();
            try
            {
                if(!playing)Pause();
                if(seeking)
                {
                    bool match=true;
                    for(int i=0;i<readyFrames.Length;i++)if(readyFrames[i]!=seekFrame)match=false;
                    if(!match)
                    {
                        if(now-seekStarted>10)throw new TimeoutException("GPU preview seek did not produce matching lens frames. Requested frame "
                            +seekFrame+"; received "+string.Join(", ",readyFrames)+".");
                        return false;
                    }
                    Publish(seekFrame, TimelineFrame(seekFrame,missingFrame));seeking=false;
                    if(playing)Play();
                    return true;
                }
                if(nativePlaying&&playing)
                {
                    long frame=readyFrames[0];
                    if(frame<0||frame==publishedFrame)return false;
                    for(int i=1;i<readyFrames.Length;i++)if(readyFrames[i]!=frame)return false;
                    Publish(frame,TimelineFrame(frame,missingFrame));return true;
                }
                if(target!=publishedFrame)
                {
                    if(scrubbing&&now-lastSeekRequest<.08)return false;
                    BeginSeek(target,now);return false;
                }
                if (!playing && Math.Abs(PresentedSeconds - timelineTarget / frameRate) > .000001)
                { PresentedSeconds = timelineTarget / frameRate; return true; }
                if(playing)Play();
                return false;
            }
            finally { LastSubmissionMilliseconds=(Stopwatch.GetTimestamp()-started)*1000d/Stopwatch.Frequency; }
        }

        private void BeginSeek(long frame,double now)
        {
            Pause();seekFrame=frame;seeking=true;seekStarted=lastSeekRequest=now;SeekCount++;
            for(int i=0;i<players.Length;i++)
            {
                readyFrames[i]=-1;
                // Scrubbing must deliver this exact frame, even after real-time playback.
                if(players[i].canSetSkipOnDrop)players[i].skipOnDrop=false;
                players[i].frame=frame;
                // A paused initial frame is not delivered by every backend; stop at the requested frame callback.
                players[i].Play();
            }
        }
        private void Play()
        {
            if(nativePlaying)return;
            for(int i=0;i<players.Length;i++)
            {
                // Preview may skip late frames instead of accumulating latency under Editor load.
                if(players[i].canSetSkipOnDrop)players[i].skipOnDrop=true;
                players[i].Play();
            }
            nativePlaying=true;
        }
        internal void Pause()
        {
            if(!nativePlaying)return;
            for(int i=0;i<players.Length;i++)players[i].Pause();
            nativePlaying=false;
        }
        internal static long SourceFrame(long timelineFrame,long missingFrame) => missingFrame < 0 || timelineFrame < missingFrame ? timelineFrame : timelineFrame - 1;
        internal static long TimelineFrame(long sourceFrame,long missingFrame) => missingFrame < 0 || sourceFrame < missingFrame ? sourceFrame : sourceFrame + 1;
        private void Publish(long frame,long timelineFrame)
        {
            for(int i=0;i<lenses.Length;i++)
                Graphics.CopyTexture(lenses[i],0,0,0,0,lenses[i].width,lenses[i].height,Texture,0,0,i*lenses[i].width,0);
            publishedFrame=frame;PresentedSeconds=timelineFrame/frameRate;PresentedFrames++;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;livePreviews.Remove(this);
            for(int i=0;i<players.Length;i++)
            {
                if(players[i]!=null)
                {
                    players[i].frameReady-=frameHandlers[i];players[i].errorReceived-=errorHandlers[i];
                    players[i].Stop();UnityEngine.Object.DestroyImmediate(players[i].gameObject);
                }
                if(lenses[i]!=null){lenses[i].Release();UnityEngine.Object.DestroyImmediate(lenses[i]);}
            }
            if(Texture!=null){Texture.Release();UnityEngine.Object.DestroyImmediate(Texture);Texture=null;}
        }
    }
}
