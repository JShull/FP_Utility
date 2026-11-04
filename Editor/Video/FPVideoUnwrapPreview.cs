// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using UnityEngine;

    internal sealed class FPVideoUnwrapPreview : IDisposable
    {
        private sealed class Frame { internal long Index; internal double Time; internal Texture2D Image; }
        private readonly List<Frame> cache = new List<Frame>();
        private readonly FPVideoNativeDecoder decoder;
        private readonly FPVideoGpuPreview gpu;
        private readonly FPVideoIsoMediaInfo info;
        private readonly int capacity;
        private readonly int decodedWidth, decodedHeight;
        private double lastDecode = double.NegativeInfinity;
        private double decoderNextSeconds = double.NaN;
        private long displayedIndex = -1;
        internal Texture Texture { get; private set; }
        internal bool UsesGpu => gpu != null;
        internal string BackendStatus => gpu?.Status ?? "Native CPU texture preview";
        internal void Pause() => gpu?.Pause();
        internal double PresentedSeconds { get; private set; } = double.NaN;
        internal int CachedFrames => gpu != null ? (gpu.HasFrame ? 1 : 0) : cache.Count;
        internal long SeekCount { get; private set; }
        internal long SequentialFrames { get; private set; }
        internal double LastDecodeMilliseconds { get; private set; }
        internal FPVideoUnwrapPreview(FPVideoNativeSource source, int maxWidth = 2048, bool useGpu = false)
        {
            info = source.Info;
            if (info.Width > 8192 || info.Height > 8192) throw new NotSupportedException("Source dimensions exceed the 8192-pixel native preview limit.");
            if (maxWidth < 16 || maxWidth > 8192) throw new ArgumentOutOfRangeException(nameof(maxWidth));
            double scale = Math.Min(1,(double)maxWidth / info.Width);
            decodedWidth = Math.Max(2,(int)Math.Round(info.Width*scale/2)*2);
            decodedHeight = Math.Max(2,(int)Math.Round(info.Height*scale/2)*2);
            capacity = Math.Max(1, Math.Min(8, (int)(64L * 1024 * 1024 / ((long)decodedWidth * decodedHeight * 8))));
            if (useGpu)
            {
                if (!info.ConstantFrameRate) throw new NotSupportedException("GPU preview requires constant-frame-rate source timing. Choose CPU preview for this source.");
                gpu = new FPVideoGpuPreview(source.DecodePath,source.SecondDecodePath,decodedWidth,decodedHeight,info.FrameRate,info.FrameCount);
                return;
            }
            decoder = new FPVideoNativeDecoder(source.DecodePath,source.SecondDecodePath);
        }
        internal bool Request(double seconds, double now, bool scrubbing, bool playing = false)
        {
            if (gpu != null)
            {
                bool changed=gpu.Request(seconds,now,scrubbing,playing);
                if (changed) { Texture=gpu.Texture;PresentedSeconds=gpu.PresentedSeconds; }
                LastDecodeMilliseconds=gpu.LastSubmissionMilliseconds;SeekCount=gpu.SeekCount;
                SequentialFrames=Math.Max(0,gpu.PresentedFrames-gpu.SeekCount);
                return changed;
            }
            long index = Math.Min(info.FrameCount - 1, Math.Max(0, (long)Math.Floor(seconds * info.FrameRate + .000001)));
            if (index == displayedIndex) return false;
            int found = -1;
            for (int i = 0; i < cache.Count; i++) if (cache[i].Index == index) { found = i; break; }
            if (found >= 0)
            {
                var hit = cache[found]; cache.RemoveAt(found); cache.Add(hit); Publish(hit); return true;
            }
            if (scrubbing && now - lastDecode < .08) return false;
            lastDecode = now;
            Frame frame;
            if (cache.Count >= capacity) { frame = cache[0]; cache.RemoveAt(0); }
            else frame = new Frame { Image = new Texture2D(decodedWidth, decodedHeight, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp } };
            long started = Stopwatch.GetTimestamp();
            try
            {
                double target = index / info.FrameRate, pts;
                if (playing && info.ConstantFrameRate && target + .000001 >= decoderNextSeconds && target - decoderNextSeconds < 8 / info.FrameRate)
                {
                    // Continue the compressed stream instead of restarting at a keyframe for every displayed frame.
                    int remaining = 16;
                    do
                    {
                        if (remaining-- == 0) throw new InvalidOperationException("Native playback did not advance to the requested frame within its decode budget.");
                        if (!decoder.Read(frame.Image, out pts)) throw new InvalidOperationException("Native playback reached the end of the stream.");
                        SequentialFrames++;
                    } while (pts + .000001 < target);
                }
                else { pts = decoder.ReadAt(frame.Image, target); SeekCount++; }
                decoderNextSeconds = pts + 1 / info.FrameRate;
                frame.Index = index; frame.Time = pts; cache.Add(frame); Publish(frame); return true;
            }
            catch { UnityEngine.Object.DestroyImmediate(frame.Image); Texture = null; displayedIndex = -1; decoderNextSeconds = double.NaN; throw; }
            finally { LastDecodeMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency; }
        }
        private void Publish(Frame frame) { displayedIndex = frame.Index; PresentedSeconds = frame.Time; Texture = frame.Image; }
        public void Dispose()
        {
            gpu?.Dispose(); decoder?.Dispose(); foreach (var frame in cache) UnityEngine.Object.DestroyImmediate(frame.Image); cache.Clear(); Texture = null;
        }
    }
}
