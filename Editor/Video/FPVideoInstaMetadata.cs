// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>Bounded, read-only inspection of indexed Insta360 v3 trailers. No sensor interpretation.</summary>
    internal sealed class FPVideoInstaMetadata
    {
        internal struct Record { internal byte Type, Format; internal long Offset; internal uint Size; }
        internal readonly List<Record> Records = new List<Record>();
        internal uint Version;
        internal long TrailerBytes;
        internal static FPVideoInstaMetadata Read(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                if (stream.Length < 78) throw new InvalidDataException("No indexed Insta360 trailer.");
                stream.Position = stream.Length - 72;
                var footer = reader.ReadBytes(72);
                if (Encoding.ASCII.GetString(footer, 40, 32) != "8db42d694ccc418790edff439fe026bf") throw new InvalidDataException("No Insta360 trailer signature.");
                uint size = BitConverter.ToUInt32(footer, 32), version = BitConverter.ToUInt32(footer, 36);
                if (version != 3 || size < 78 || size > stream.Length) throw new InvalidDataException("Unsupported or damaged Insta360 trailer.");
                stream.Position = stream.Length - 78;
                byte format = reader.ReadByte(), type = reader.ReadByte(); uint tableSize = reader.ReadUInt32();
                if (format != 0 || type != 0 || tableSize == 0 || tableSize > 2560 || tableSize % 10 != 0 || tableSize > size - 78)
                    throw new InvalidDataException("Invalid Insta360 record directory.");
                long start = stream.Length - size, directory = stream.Length - 78 - tableSize;
                stream.Position = directory;
                var result = new FPVideoInstaMetadata { Version = version, TrailerBytes = size };
                var seen = new HashSet<byte>();
                for (int i = 0; i < tableSize / 10; i++)
                {
                    byte id = reader.ReadByte(), encoding = reader.ReadByte(); uint length = reader.ReadUInt32(), offset = reader.ReadUInt32();
                    if (id == 0) continue;
                    long absolute = start + offset;
                    if (!seen.Add(id) || absolute < start || absolute > directory || length > directory - absolute)
                        throw new InvalidDataException("Insta360 record lies outside its trailer or is duplicated.");
                    result.Records.Add(new Record { Type = id, Format = encoding, Offset = absolute, Size = length });
                }
                return result;
            }
        }
        internal byte[] ReadRecord(string path, byte type, int maximumBytes = 1024 * 1024)
        {
            if (maximumBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            foreach (var record in Records)
            {
                if (record.Type != type) continue;
                if (record.Size > maximumBytes) throw new InvalidDataException("Record exceeds the requested read budget.");
                using (var stream = File.OpenRead(path))
                {
                    stream.Position = record.Offset; var bytes = new byte[checked((int)record.Size)]; int offset = 0;
                    while (offset < bytes.Length) { int n = stream.Read(bytes, offset, bytes.Length - offset); if (n == 0) throw new EndOfStreamException(); offset += n; }
                    return bytes;
                }
            }
            return null;
        }
    }
}
