// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>Bounded ISO-BMFF track inspection; no decoding or whole-file reads.</summary>
    internal sealed class FPVideoIsoMediaInfo
    {
        internal int Width, Height, AudioChannels, AudioSampleRate;
        internal uint TimeScale, FrameDelta;
        internal long FrameCount;
        internal double Duration;
        internal bool ConstantFrameRate = true;
        internal sealed class VideoTrack
        {
            internal long TypeOffset, Frames;
            internal int Width, Height;
            internal uint Scale, Delta;
            internal bool Constant;
        }
        internal readonly List<VideoTrack> VideoTracks = new List<VideoTrack>();
        internal double FrameRate => FrameDelta == 0 ? 0 : (double)TimeScale / FrameDelta;
        internal bool HasAudio => AudioChannels > 0;
        internal uint RateNumerator => TimeScale / Gcd(TimeScale, FrameDelta);
        internal uint RateDenominator => FrameDelta / Gcd(TimeScale, FrameDelta);

        private struct Box { internal long Data, End, TypeOffset; internal string Type; }
        private static uint Gcd(uint a, uint b) { while (b != 0) { uint c = a % b; a = b; b = c; } return Math.Max(1u, a); }
        private static uint U32(BinaryReader r) { var b = r.ReadBytes(4); if (b.Length != 4) throw new EndOfStreamException(); return (uint)b[0] << 24 | (uint)b[1] << 16 | (uint)b[2] << 8 | b[3]; }
        private static ulong U64(BinaryReader r) => ((ulong)U32(r) << 32) | U32(r);
        private static int U16(BinaryReader r) { int a = r.ReadByte(); return (a << 8) | r.ReadByte(); }
        private static Box Next(BinaryReader r, long parentEnd)
        {
            long start = r.BaseStream.Position;
            if (parentEnd - start < 8) throw new InvalidDataException("Truncated MP4 box header.");
            ulong size = U32(r); string type = Encoding.ASCII.GetString(r.ReadBytes(4));
            if (size == 1) size = U64(r);
            if (size == 0) size = (ulong)(parentEnd - start);
            if (size > (ulong)(parentEnd - start) || size < (ulong)(r.BaseStream.Position - start)) throw new InvalidDataException("Invalid MP4 box size.");
            return new Box { Data = r.BaseStream.Position, End = start + (long)size, TypeOffset = start + 4, Type = type };
        }

        internal static FPVideoIsoMediaInfo Read(string path)
        {
            var info = new FPVideoIsoMediaInfo();
            using (var stream = File.OpenRead(path))
            using (var r = new BinaryReader(stream))
            {
                while (stream.Position < stream.Length)
                {
                    var box = Next(r, stream.Length);
                    if (box.Type == "moov")
                    {
                        while (stream.Position < box.End)
                        {
                            var child = Next(r, box.End);
                            if (child.Type == "trak") ReadTrack(r, child, info);
                            stream.Position = child.End;
                        }
                    }
                    stream.Position = box.End;
                }
            }
            if (info.Width <= 0 || info.Height <= 0 || info.FrameCount <= 0 || info.FrameDelta == 0 || info.TimeScale == 0)
                throw new InvalidDataException("No supported MP4 video sample table. Fragmented MP4 and non-MP4 LRV files are unsupported.");
            return info;
        }

        private static void ReadTrack(BinaryReader r, Box track, FPVideoIsoMediaInfo info)
        {
            string handler = null; uint scale = 0, delta = 0; ulong duration = 0; long count = 0;
            int width = 0, height = 0, channels = 0, rate = 0; bool constant = true;
            // Only descend the container boxes required for stream metadata.
            Action<long> visit = null;
            visit = end =>
            {
                while (r.BaseStream.Position < end)
                {
                    var b = Next(r, end);
                    if (b.Type == "mdia" || b.Type == "minf" || b.Type == "stbl") visit(b.End);
                    else if (b.Type == "hdlr") { r.BaseStream.Position = b.Data + 8; handler = Encoding.ASCII.GetString(r.ReadBytes(4)); }
                    else if (b.Type == "mdhd")
                    {
                        int version = r.ReadByte();
                        if (version != 0 && version != 1) throw new InvalidDataException("Unsupported MP4 media header version.");
                        r.BaseStream.Position = b.Data + (version == 1 ? 20 : 12); scale = U32(r); duration = version == 1 ? U64(r) : U32(r);
                    }
                    else if (b.Type == "stsd")
                    {
                        r.BaseStream.Position = b.Data + 4; uint entries = U32(r);
                        if (entries > 0)
                        {
                            var sample = Next(r, b.End);
                            if (handler == "vide") { r.BaseStream.Position = sample.Data + 24; width = U16(r); height = U16(r); }
                            else if (handler == "soun") { r.BaseStream.Position = sample.Data + 16; channels = U16(r); r.BaseStream.Position = sample.Data + 24; rate = (int)(U32(r) >> 16); }
                        }
                    }
                    else if (b.Type == "stts")
                    {
                        r.BaseStream.Position = b.Data + 4; uint entries = U32(r);
                        if (entries > (b.End - r.BaseStream.Position) / 8) throw new InvalidDataException("Invalid MP4 sample timing table.");
                        for (uint i = 0; i < entries; i++) { uint n = U32(r), d = U32(r); count += n; if (delta == 0) delta = d; else if (delta != d) constant = false; }
                    }
                    r.BaseStream.Position = b.End;
                }
            };
            visit(track.End);
            if (handler == "vide") info.VideoTracks.Add(new VideoTrack { TypeOffset=track.TypeOffset,Width=width,Height=height,Scale=scale,Delta=delta,Frames=count,Constant=constant });
            if (handler == "vide" && info.Width == 0)
            {
                info.Width = width; info.Height = height; info.TimeScale = scale; info.FrameDelta = delta;
                info.FrameCount = count; info.Duration = scale == 0 ? 0 : (double)duration / scale; info.ConstantFrameRate = constant;
            }
            else if (handler == "soun" && info.AudioChannels == 0) { info.AudioChannels = channels; info.AudioSampleRate = rate; }
        }
    }
}
