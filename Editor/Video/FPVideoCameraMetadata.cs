// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>Bounded subset of Insta360 ExtraMetadata protobuf; no protobuf runtime dependency.</summary>
    internal sealed class FPVideoCameraMetadata
    {
        internal string Camera, Gamma, OffsetV6, OriginalOffsetV6;
        internal ulong FirstFrameTimestamp;
        internal double GyroTimestamp, RollingShutterTime;
        internal bool HasGyroTimestamp, RawGyro, HasImu;
        internal int StreamType;
        internal FPVideoFactoryCalibration Factory;
        internal static FPVideoCameraMetadata Read(string path)
        {
            var index = FPVideoInstaMetadata.Read(path);
            foreach (var record in index.Records) if (record.Type == 1 && record.Format != 1) throw new InvalidDataException("Camera metadata is not protobuf encoded.");
            byte[] bytes = index.ReadRecord(path, 1);
            if (bytes == null) throw new InvalidDataException("No camera metadata record.");
            var result = Parse(bytes);
            foreach (var record in index.Records) if (record.Type == 3 && record.Size > 0) result.HasImu = true;
            if (result.Camera == "Insta360 X6" && !string.IsNullOrEmpty(result.OffsetV6))
                result.Factory = FPVideoFactoryCalibration.Parse(result.OffsetV6, result.StreamType);
            return result;
        }
        private static ulong Varint(byte[] bytes, ref int position)
        {
            ulong value = 0;
            for (int shift = 0; shift <= 63; shift += 7)
            {
                if (position >= bytes.Length) throw new InvalidDataException("Truncated camera metadata varint.");
                byte b = bytes[position++];
                if (shift == 63 && b > 1) throw new InvalidDataException("Camera metadata varint overflow.");
                value |= (ulong)(b & 127) << shift;
                if (b < 128) return value;
            }
            throw new InvalidDataException("Invalid camera metadata varint.");
        }
        internal static FPVideoCameraMetadata Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length > 1024 * 1024) throw new InvalidDataException("Camera metadata exceeds its read budget.");
            var result = new FPVideoCameraMetadata(); int position = 0, fields = 0;
            var utf8 = new UTF8Encoding(false, true);
            while (position < bytes.Length)
            {
                if (++fields > 4096) throw new InvalidDataException("Too many camera metadata fields.");
                ulong key = Varint(bytes, ref position); int wire = (int)(key & 7); ulong tag = key >> 3;
                if (tag == 0 || tag > 536870911) throw new InvalidDataException("Invalid camera metadata field.");
                int expected = tag == 2 || tag == 22 || tag == 111 || tag == 112 ? 2 : tag == 25 || tag == 28 ? 1 : tag == 24 || tag == 29 || tag == 62 || tag == 131 ? 0 : -1;
                if (expected >= 0 && expected != wire) throw new InvalidDataException("Unexpected camera metadata field encoding.");
                if (wire == 0)
                {
                    ulong value = Varint(bytes, ref position);
                    if (tag == 24) result.FirstFrameTimestamp = value;
                    if (tag == 131) { if (value > int.MaxValue) throw new InvalidDataException("Invalid stream layout."); result.StreamType = (int)value; }
                    if (tag == 29 || tag == 62)
                    {
                        if (value > 1) throw new InvalidDataException("Invalid metadata boolean.");
                        if (tag == 29) result.HasGyroTimestamp = value != 0; else result.RawGyro = value != 0;
                    }
                }
                else if (wire == 1 || wire == 5)
                {
                    int length = wire == 1 ? 8 : 4;
                    if (length > bytes.Length - position) throw new InvalidDataException("Truncated camera metadata scalar.");
                    if (tag == 25 || tag == 28)
                    {
                        if (wire != 1) throw new InvalidDataException("Invalid timing field encoding.");
                        double value = BitConverter.ToDouble(bytes, position);
                        if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException("Non-finite camera timing.");
                        if (tag == 25) result.RollingShutterTime = value; else result.GyroTimestamp = value;
                    }
                    position += length;
                }
                else if (wire == 2)
                {
                    ulong count = Varint(bytes, ref position);
                    if (count > (ulong)(bytes.Length - position)) throw new InvalidDataException("Truncated camera metadata payload.");
                    if (tag == 2 || tag == 22 || tag == 111 || tag == 112)
                    {
                        if (count > (tag < 100 ? 256ul : 8192ul)) throw new InvalidDataException("Camera metadata string exceeds its read budget.");
                        string value;
                        try { value = utf8.GetString(bytes, position, (int)count); }
                        catch (DecoderFallbackException e) { throw new InvalidDataException("Invalid metadata text.", e); }
                        if (tag == 2) result.Camera = value; else if (tag == 22) result.Gamma = value;
                        else if (tag == 111) result.OffsetV6 = value; else result.OriginalOffsetV6 = value;
                    }
                    position += (int)count;
                }
                else throw new InvalidDataException("Unsupported camera metadata wire type.");
            }
            return result;
        }
    }
}
