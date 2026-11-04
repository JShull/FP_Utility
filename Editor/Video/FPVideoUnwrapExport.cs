// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using FuzzPhyte.Utility.Video;
    using Unity.Collections;
    using UnityEditor.Media;
    using UnityEngine;

    internal sealed class FPVideoUnwrapExport : IDisposable
    {
        [Serializable]
        private sealed class ExportReport
        {
            public string source, createdUtc;
            public FPVideoUnwrapSettings settings;
            public double sourceInSeconds, sourceOutSeconds;
            public long frames, audioSampleFrames;
            public uint frameRateNumerator, frameRateDenominator;
            public bool includesAudio;
            public string audioTrimMethod;
            public string sourceDecodePrecision, processingPrecision, outputPrecision;
            public long audioSeekSampleFrame;
            public double audioPrerollSeconds;
            public string stitching;
            public string flowMethod;
            public string temporalMethod;
            public string colorMethod;
            public FPVideoManifestItem video;
        }
        private readonly FPVideoIsoMediaInfo info;
        private readonly FPVideoUnwrapSettings settings;
        private readonly string temporaryPath;
        private readonly bool audio;
        private FPVideoNativeDecoder videoDecoder, audioDecoder;
        private FPVideoUnwrapRenderer renderer;
        private MediaEncoder encoder;
        private Texture2D sourceTexture, outputTexture, audioPumpTexture;
        private NativeArray<float> audioBuffer;
        private int audioOffset, audioCount;
        private readonly long firstFrame, endFrame, firstSample, audioStartSample;
        private long frame, consumedSamples, writtenSampleFrames;
        private Task<string> hashTask;
        private bool disposed;
        private readonly Stopwatch elapsed = new Stopwatch();
        internal string OutputPath { get; }
        internal string Error { get; private set; }
        internal bool Finished { get; private set; }
        internal bool Succeeded { get; private set; }
        internal string Hash { get; private set; }
        internal double FramesPerSecond => elapsed.Elapsed.TotalSeconds > 0 ? (frame - firstFrame) / elapsed.Elapsed.TotalSeconds : 0;
        private double AudioProgress => firstSample > audioStartSample ? Math.Min(1, (double)(consumedSamples - audioStartSample) / (firstSample - audioStartSample)) : 1;
        private float AudioBudget => firstSample > audioStartSample ? .05f : 0;
        internal float Progress => hashTask != null ? .99f : consumedSamples < firstSample ? AudioBudget * (float)AudioProgress : AudioBudget + (float)((double)(frame - firstFrame) / Math.Max(1, endFrame - firstFrame)) * (.98f - AudioBudget);
        internal string AudioTrimMethod => !audio ? "No audio" : audioStartSample > 0 ? "Native sample-time seek with 5-second preroll; Windows Unity 6000.6, 48 kHz" : "Sequential audio from recording start";
        internal string Status => Error ?? (Succeeded ? "Export complete" : hashTask != null ? "Calculating SHA-256…" : consumedSamples < firstSample ? $"Preparing audio {AudioProgress:P0} • {(firstSample-consumedSamples)/(double)(info.AudioSampleRate*info.AudioChannels):F1} s left to decode" : $"Encoding {frame - firstFrame}/{endFrame - firstFrame} frames • {FramesPerSecond:F1} fps" + (FramesPerSecond > 0 ? $" • ~{(endFrame - frame) / FramesPerSecond / 60:F1} min remaining" : ""));

        internal static (long first, long end) FrameRange(FPVideoIsoMediaInfo info, double start, double end)
        {
            if (double.IsNaN(start) || double.IsInfinity(start) || double.IsNaN(end) || double.IsInfinity(end) || start < 0 || end > info.Duration + .000001 || start >= end)
                throw new ArgumentException("Export range must be inside the source with Out greater than In.");
            if (!info.ConstantFrameRate) throw new NotSupportedException("Variable-frame-rate export is not supported; preview remains available.");
            long a = Math.Max(0, (long)Math.Ceiling(start * info.FrameRate - .000001));
            long b = Math.Min(info.FrameCount, (long)Math.Ceiling(end * info.FrameRate - .000001));
            if (b <= a) throw new ArgumentException("The export range contains no complete source frame.");
            return (a, b);
        }

        internal FPVideoUnwrapExport(FPVideoNativeSource source, FPVideoUnwrapSettings settings, double start, double end, string outputPath, bool includeAudio, bool seekAudio = true)
        {
            settings.Validate();
            if (!source.Ready.IsCompletedSuccessfully) throw new InvalidOperationException("Source staging is not complete.");
            info = source.Info; this.settings = settings.Snapshot();
            (firstFrame, endFrame) = FrameRange(info, start, end); frame = firstFrame;
            OutputPath = Path.GetFullPath(outputPath);
            if (!string.Equals(Path.GetExtension(OutputPath), ".mp4", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose an MP4 output path.");
            if (File.Exists(OutputPath) || File.Exists(OutputPath + ".json")) throw new IOException("Output or its JSON report already exists. Choose a new filename.");
            if (!Directory.Exists(Path.GetDirectoryName(OutputPath))) throw new DirectoryNotFoundException("Choose an existing output folder.");
            temporaryPath = Path.Combine(Path.GetDirectoryName(OutputPath), "." + Path.GetFileNameWithoutExtension(OutputPath) + "." + Guid.NewGuid().ToString("N") + ".partial.mp4");
            audio = includeAudio && info.HasAudio;
            if (audio && (info.AudioChannels > 8 || info.AudioSampleRate <= 0)) throw new NotSupportedException("Unsupported source audio layout.");
            firstSample = audio ? (long)Math.Round(firstFrame / info.FrameRate * info.AudioSampleRate) * info.AudioChannels : 0;
            try
            {
                videoDecoder = new FPVideoNativeDecoder(source.DecodePath,source.SecondDecodePath); videoDecoder.Seek(firstFrame / info.FrameRate);
                sourceTexture = new Texture2D(info.Width, info.Height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                outputTexture = new Texture2D(settings.width, settings.height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                renderer = new FPVideoUnwrapRenderer();
                var h264 = new H264EncoderAttributes { profile = UnityEditor.VideoEncodingProfile.H264High, gopSize = (uint)Math.Max(1, Math.Round(info.FrameRate)), numConsecutiveBFrames = 0 };
                var attributes = new VideoTrackEncoderAttributes(h264) { width = (uint)settings.width, height = (uint)settings.height, includeAlpha = false,
                    targetBitRate = checked((uint)Math.Round(settings.videoBitrateMbps * 1000000d)), bitRateMode = UnityEditor.VideoBitrateMode.High,
                    frameRate = new MediaRational(checked((int)info.RateNumerator), checked((int)info.RateDenominator)) };
                if (audio)
                {
                    // The 2x2 video pump advances demuxing only; its cropped pixels are never displayed.
                    audioDecoder = new FPVideoNativeDecoder(source.DecodePath,resizeFrames:false);
                    if (seekAudio && FPVideoNativeDecoder.SupportsAudioSeek(info.AudioSampleRate))
                    {
                        long anchor = firstSample / info.AudioChannels - 5L * info.AudioSampleRate;
                        if (anchor > 0)
                        {
                            audioDecoder.SeekAudio(anchor, info.AudioSampleRate);
                            consumedSamples = audioStartSample = anchor * info.AudioChannels;
                        }
                    }
                    audioPumpTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    audioBuffer = new NativeArray<float>(8192 * info.AudioChannels, Allocator.Persistent);
                    encoder = new MediaEncoder(temporaryPath, attributes, new AudioTrackAttributes { sampleRate = new MediaRational(info.AudioSampleRate), channelCount = (ushort)info.AudioChannels, language = "und" });
                }
                else encoder = new MediaEncoder(temporaryPath, attributes);
                SourcePath = source.OriginalPath;
                elapsed.Start();
            }
            catch { Dispose(); throw; }
        }
        private string SourcePath { get; }

        internal void Tick(int frameLimit = 8)
        {
            if (Finished || disposed) return;
            try
            {
                if (hashTask != null)
                {
                    if (!hashTask.IsCompleted) return;
                    Hash = hashTask.GetAwaiter().GetResult(); Publish(); Finished = Succeeded = true; return;
                }
                var budget = Stopwatch.StartNew();
                // Decode only the bounded preroll after a qualified sample-time seek; otherwise scan from zero.
                while (consumedSamples < firstSample && budget.ElapsedMilliseconds < 12)
                    ConsumeAudio((int)Math.Min(firstSample - consumedSamples, 8192L * info.AudioChannels), false);
                if (consumedSamples < firstSample) return;
                int processed = 0;
                // Amortize Editor update overhead while retaining bounded cancellation/UI latency.
                while (frame < endFrame && processed < frameLimit && (processed == 0 || budget.ElapsedMilliseconds < 12))
                {
                    processed++;
                    if (!videoDecoder.Read(sourceTexture, out double pts)) throw new EndOfStreamException("Video ended before the selected Out point.");
                    if (frame == firstFrame && pts + .000001 < firstFrame / info.FrameRate) continue;
                    if (double.IsNaN(pts) || Math.Abs(pts - frame / info.FrameRate) > .5 / info.FrameRate + .00001)
                        throw new InvalidDataException($"Unexpected video timestamp {pts:F6}; expected {frame / info.FrameRate:F6}. Export stopped to preserve timing.");
                    renderer.Render(sourceTexture, settings, settings.width, settings.height, sourceSeconds: pts, frameSeconds: 1 / info.FrameRate); renderer.ReadBack(outputTexture, false);
                    if (!encoder.AddFrame(outputTexture)) throw new IOException("Unity's encoder rejected a video frame.");
                    frame++;
                    if (audio)
                    {
                        long target = (long)Math.Round((frame - firstFrame) / info.FrameRate * info.AudioSampleRate);
                        while (writtenSampleFrames < target)
                            ConsumeAudio((int)Math.Min((target - writtenSampleFrames) * info.AudioChannels, audioBuffer.Length), true);
                    }
                }
                if (frame < endFrame) return;
                ReleaseMedia();
                hashTask = Task.Run(() => FPVideoHashUtility.ComputeSHA256(temporaryPath));
            }
            catch (Exception e) { Error = e.Message; Finished = true; Dispose(); }
        }

        private void ConsumeAudio(int requested, bool write)
        {
            if (audioOffset == audioCount)
            {
                // The native decoder queues audio as its video demuxer advances; zero is not necessarily EOF.
                audioCount = audioDecoder.ReadAudio(audioBuffer); audioOffset = 0;
                for (int i = 0; audioCount == 0 && i < 512; i++)
                {
                    if (!audioDecoder.Read(audioPumpTexture, out _)) break;
                    audioCount = audioDecoder.ReadAudio(audioBuffer);
                }
                if (audioCount <= 0 || audioCount > audioBuffer.Length || audioCount % info.AudioChannels != 0)
                    throw new EndOfStreamException("Audio ended early or returned an invalid sample block. Export was not published.");
            }
            int count = Math.Min(requested, audioCount - audioOffset);
            if (write && !encoder.AddSamples(audioBuffer.GetSubArray(audioOffset, count))) throw new IOException("Unity's encoder rejected audio samples.");
            audioOffset += count; consumedSamples += count;
            if (write) writtenSampleFrames += count / info.AudioChannels;
        }
        private void Publish()
        {
            var report = new ExportReport
            {
                source = SourcePath, createdUtc = DateTime.UtcNow.ToString("O"), settings = settings,
                sourceInSeconds = firstFrame / info.FrameRate, sourceOutSeconds = endFrame / info.FrameRate,
                frames = endFrame - firstFrame, frameRateNumerator = info.RateNumerator, frameRateDenominator = info.RateDenominator,
                includesAudio = audio, audioSampleFrames = writtenSampleFrames,
                sourceDecodePrecision = "8-bit native video decode",
                processingPrecision = FPVideoUnwrapRenderer.SupportsHighPrecision ? "FP16 linear RGB" : "8-bit SDR fallback",
                outputPrecision = "8-bit H.264 SDR",
                audioTrimMethod = AudioTrimMethod, audioSeekSampleFrame = audio ? audioStartSample / info.AudioChannels : 0,
                audioPrerollSeconds = audio ? (firstSample - audioStartSample) / (double)(info.AudioSampleRate * info.AudioChannels) : 0,
                flowMethod = settings.opticalFlow ? FPVideoFarneback.MethodVersion : "Disabled",
                temporalMethod = settings.opticalFlow && settings.flowTemporalSmoothing ? "Confidence-gated vector EMA v1; reset at export In" : "Disabled",
                colorMethod = settings.colorTuning ? "SDR output grade v3; creative curve " + settings.colorCurve : "Disabled",
                stitching = (settings.sourceProjection == FPVideoSourceProjection.Equirectangular ? "Equirectangular reprojection without lens stitching" :
                    (settings.useFactoryCalibration ? "Experimental X6 v6 unified-sphere lenses at infinity; translation not applied; " : "Equidistant lenses with ") + (settings.useDirectionalSeam ? "directional seam blending" : "angular edge-feather blending")) +
                    (settings.opticalFlow ? "; experimental bidirectional polynomial flow" : "; no optical flow") + "; no gyro stabilization. MP4 spherical metadata is not injected.",
                video = new FPVideoManifestItem { id = Path.GetFileNameWithoutExtension(OutputPath), version = "1.0.0", fileName = Path.GetFileName(OutputPath),
                    sha256 = Hash, contentLength = new FileInfo(temporaryPath).Length, downloadUrl = "" }
            };
            File.WriteAllText(temporaryPath + ".json", JsonUtility.ToJson(report, true));
            File.Move(temporaryPath, OutputPath);
            try { File.Move(temporaryPath + ".json", OutputPath + ".json"); }
            catch { File.Delete(OutputPath); throw; }
        }
        private void ReleaseMedia()
        {
            elapsed.Stop();
            encoder?.Dispose(); encoder = null; videoDecoder?.Dispose(); videoDecoder = null; audioDecoder?.Dispose(); audioDecoder = null;
            renderer?.Dispose(); renderer = null;
            if (audioBuffer.IsCreated) audioBuffer.Dispose();
            if (sourceTexture != null) UnityEngine.Object.DestroyImmediate(sourceTexture); sourceTexture = null;
            if (outputTexture != null) UnityEngine.Object.DestroyImmediate(outputTexture); outputTexture = null;
            if (audioPumpTexture != null) UnityEngine.Object.DestroyImmediate(audioPumpTexture); audioPumpTexture = null;
        }
        private void DeletePartial()
        {
            if (temporaryPath == null) return;
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); if (File.Exists(temporaryPath + ".json")) File.Delete(temporaryPath + ".json"); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { ReleaseMedia(); }
            finally { if (hashTask != null && !hashTask.IsCompleted) _ = hashTask.ContinueWith(_ => DeletePartial(), TaskScheduler.Default); else DeletePartial(); }
        }
    }
}
