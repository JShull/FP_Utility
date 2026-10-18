// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later. See LICENSE.md.
// Commercial/proprietary use requires a separate license from John B. Shull.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Changes only FBX channel-name properties and affected binary record lengths/offsets.
    /// Geometry, compressed arrays, IDs, connections, animation and unknown records stay opaque.
    /// This is deliberately not a Unity-mesh-to-FBX exporter.
    /// </summary>
    internal sealed class FPBlendShapeFbxDocument
    {
        internal sealed class Channel
        {
            public long Id;
            public long Group;
            public string Name;
            public string Suffix;
            public int Offset;
            public int Length;
            public int Header;
        }

        private sealed class Header
        {
            public int Offset;
            public int End;
            public int PropertyLength;
        }

        private sealed class Edit
        {
            public int Offset;
            public int Length;
            public byte[] Bytes;
            public int Delta => Bytes.Length - Length;
        }

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0\x1a\0");
        private readonly byte[] source;
        private readonly List<Header> headers = new List<Header>();
        private readonly Dictionary<long, long> parents = new Dictionary<long, long>();
        private readonly Dictionary<long, string> objectNames = new Dictionary<long, string>();
        private readonly bool binary;
        private readonly bool wide;
        public readonly List<Channel> Channels = new List<Channel>();

        public string GetOwnerLabel(Channel channel)
        {
            string deformer = objectNames.TryGetValue(channel.Group, out string name) ? name : "Blendshapes";
            if (parents.TryGetValue(channel.Group, out long geometry) && objectNames.TryGetValue(geometry, out string mesh))
                return mesh + " / " + deformer;
            return deformer;
        }

        public FPBlendShapeFbxDocument(byte[] bytes)
        {
            source = bytes ?? throw new ArgumentNullException(nameof(bytes));
            binary = bytes.Length >= 27 && Magic.SequenceEqual(bytes.Take(Magic.Length));
            if (binary)
            {
                int version = BitConverter.ToInt32(bytes, 23);
                if (version < 7000 || version > 7700)
                    throw new InvalidDataException("Supported binary FBX versions are 7.0 through 7.7.");
                wide = version >= 7500;
                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                    ReadNodes(reader, 27, bytes.Length, 0);
            }
            else
            {
                ReadAscii();
            }
            var ids = new HashSet<long>();
            foreach (Channel channel in Channels)
            {
                if (!ids.Add(channel.Id)) throw new InvalidDataException("Duplicate FBX channel object ID.");
                channel.Group = parents.TryGetValue(channel.Id, out long parent) ? parent : 0;
            }
        }

        public byte[] Rename(IReadOnlyDictionary<long, string> names)
        {
            foreach (long id in names.Keys)
                if (!Channels.Any(c => c.Id == id)) throw new ArgumentException("A selected channel no longer exists. Reload the FBX.");

            foreach (var group in Channels.GroupBy(c => c.Group))
            {
                var used = new HashSet<string>(StringComparer.Ordinal);
                foreach (Channel channel in group)
                {
                    string name = names.TryGetValue(channel.Id, out string renamed) ? renamed : channel.Name;
                    ValidateName(name);
                    if (!used.Add(name)) throw new ArgumentException("Duplicate blendshape name in one deformer: " + name);
                }
            }

            var edits = new List<Edit>();
            foreach (Channel channel in Channels)
            {
                if (!names.TryGetValue(channel.Id, out string name) || name == channel.Name) continue;
                byte[] value = Utf8.GetBytes(binary ? name + channel.Suffix : Escape(name));
                if (binary)
                {
                    byte[] property = new byte[value.Length + 4];
                    Buffer.BlockCopy(BitConverter.GetBytes(value.Length), 0, property, 0, 4);
                    Buffer.BlockCopy(value, 0, property, 4, value.Length);
                    value = property;
                }
                edits.Add(new Edit { Offset = channel.Offset, Length = channel.Length, Bytes = value });
            }

            if (binary)
            {
                // All coordinates are in the original file, so edits cannot invalidate later offsets.
                Edit[] nameEdits = edits.ToArray();
                foreach (Header header in headers)
                {
                    long shiftedEnd = header.End + nameEdits.Where(e => e.Offset + e.Length <= header.End).Sum(e => (long)e.Delta);
                    int ownDelta = Channels.Where(c => c.Header == header.Offset)
                        .Sum(c => nameEdits.Where(e => e.Offset == c.Offset).Sum(e => e.Delta));
                    edits.Add(new Edit { Offset = header.Offset, Length = wide ? 8 : 4,
                        Bytes = wide ? BitConverter.GetBytes(shiftedEnd) : BitConverter.GetBytes(checked((uint)shiftedEnd)) });
                    edits.Add(new Edit { Offset = header.Offset + (wide ? 16 : 8), Length = wide ? 8 : 4,
                        Bytes = wide ? BitConverter.GetBytes((long)header.PropertyLength + ownDelta)
                            : BitConverter.GetBytes(checked((uint)(header.PropertyLength + ownDelta))) });
                }
            }

            using (var output = new MemoryStream())
            {
                int position = 0;
                foreach (Edit edit in edits.OrderBy(e => e.Offset))
                {
                    if (edit.Offset < position) throw new InvalidDataException("Overlapping FBX edits.");
                    output.Write(source, position, edit.Offset - position);
                    output.Write(edit.Bytes, 0, edit.Bytes.Length);
                    position = edit.Offset + edit.Length;
                }
                output.Write(source, position, source.Length - position);
                byte[] result = output.ToArray();
                var check = new FPBlendShapeFbxDocument(result);
                if (check.Channels.Count != Channels.Count) throw new InvalidDataException("FBX verification failed.");
                foreach (Channel channel in check.Channels)
                    if (names.TryGetValue(channel.Id, out string name) && channel.Name != name)
                        throw new InvalidDataException("Renamed FBX channel verification failed.");
                return result;
            }
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.IndexOfAny(new[] { '\0', '\r', '\n', ':', '"', '\\' }) >= 0)
                throw new ArgumentException("Blendshape names must be nonempty with no surrounding spaces, colons, quotes, backslashes or line breaks.");
        }

        private long Number(BinaryReader reader) => wide ? checked((long)reader.ReadUInt64()) : reader.ReadUInt32();

        private void ReadNodes(BinaryReader reader, int position, int limit, int depth)
        {
            if (depth > 128) throw new InvalidDataException("FBX hierarchy is too deep.");
            int headerSize = wide ? 25 : 13;
            while (position + headerSize <= limit)
            {
                reader.BaseStream.Position = position;
                int end = checked((int)Number(reader));
                long count = Number(reader);
                int length = checked((int)Number(reader));
                int nameLength = reader.ReadByte();
                if (end == 0)
                {
                    if (count != 0 || length != 0 || nameLength != 0) throw new InvalidDataException("Invalid FBX null record.");
                    return;
                }
                int properties = checked(position + headerSize + nameLength);
                int childStart = checked(properties + length);
                if (end <= position || end > limit || childStart > end || count > length)
                    throw new InvalidDataException("Invalid FBX record bounds.");
                string tag = Encoding.ASCII.GetString(reader.ReadBytes(nameLength));
                headers.Add(new Header { Offset = position, End = end, PropertyLength = length });
                long id = 0;
                var strings = new List<string>();
                int objectNameOffset = 0, objectNameLength = 0;
                var ids = new List<long>();
                for (long i = 0; i < count; i++)
                {
                    if (reader.BaseStream.Position >= childStart) throw new InvalidDataException("Truncated FBX properties.");
                    char type = (char)reader.ReadByte();
                    if (type == 'S' || type == 'R')
                    {
                        int offset = checked((int)reader.BaseStream.Position);
                        int size = checked((int)reader.ReadUInt32());
                        if (size > childStart - reader.BaseStream.Position) throw new InvalidDataException("Invalid FBX string length.");
                        if (type == 'S' && (tag == "Deformer" || tag == "Geometry" || tag == "Model" || tag == "C"))
                        {
                            strings.Add(Utf8.GetString(reader.ReadBytes(size)));
                            if (i == 1) { objectNameOffset = offset; objectNameLength = size + 4; }
                        }
                        else reader.BaseStream.Position += size;
                    }
                    else if (type == 'L')
                    {
                        long value = reader.ReadInt64();
                        if (i == 0) id = value;
                        ids.Add(value);
                    }
                    else if ("fdlibc".IndexOf(type) >= 0)
                    {
                        reader.ReadUInt32();
                        reader.ReadUInt32();
                        uint size = reader.ReadUInt32();
                        reader.BaseStream.Position += size;
                    }
                    else
                    {
                        int size = type == 'Y' ? 2 : type == 'C' ? 1 : type == 'I' || type == 'F' ? 4 : type == 'D' ? 8 : 0;
                        if (size == 0) throw new InvalidDataException("Unsupported FBX property type: " + type);
                        reader.BaseStream.Position += size;
                    }
                    if (reader.BaseStream.Position > childStart) throw new InvalidDataException("FBX property exceeds its record.");
                }
                if (reader.BaseStream.Position != childStart) throw new InvalidDataException("Invalid FBX property list length.");
                if ((tag == "Deformer" || tag == "Geometry" || tag == "Model") && strings.Count >= 2)
                {
                    int separator = strings[0].IndexOf('\0');
                    objectNames[id] = separator < 0 ? strings[0] : strings[0].Substring(0, separator);
                }
                if (tag == "Deformer" && strings.Count == 2 && strings[1] == "BlendShapeChannel")
                {
                    int separator = strings[0].IndexOf('\0');
                    Channels.Add(new Channel { Id = id, Name = separator < 0 ? strings[0] : strings[0].Substring(0, separator),
                        Suffix = separator < 0 ? "" : strings[0].Substring(separator), Offset = objectNameOffset,
                        Length = objectNameLength, Header = position });
                }
                if (tag == "C" && strings.Count > 0 && strings[0] == "OO" && ids.Count >= 2)
                    parents[ids[0]] = ids[1];
                if (childStart < end) ReadNodes(reader, childStart, end, depth + 1);
                position = end;
            }
            if (position != limit) throw new InvalidDataException("Truncated FBX record header.");
        }

        private void ReadAscii()
        {
            string text = Utf8.GetString(source);
            if (!Regex.IsMatch(text, @"(?m)^[\uFEFF\s]*;\s*FBX\s+7\.[0-7]"))
                throw new InvalidDataException("Select a binary or UTF-8 ASCII FBX 7.x model.");
            const string quoted = "\"((?:\\\\.|[^\"\\\\])*)\"";
            foreach (Match match in Regex.Matches(text, @"(?m)^[\t ]*(?:Deformer|Geometry|Model):\s*(-?\d+)\s*,\s*" + quoted))
            {
                string name = match.Groups[2].Value;
                int separator = name.IndexOf("::", StringComparison.Ordinal);
                objectNames[long.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)] =
                    separator < 0 ? name : name.Substring(separator + 2);
            }
            string pattern = @"(?m)^[\t ]*Deformer:\s*(-?\d+)\s*,\s*" + quoted + @"\s*,\s*" + "\"BlendShapeChannel\"" + @"\s*\{";
            foreach (Match match in Regex.Matches(text, pattern))
            {
                Group name = match.Groups[2];
                string raw = name.Value;
                int prefix = raw.IndexOf("::", StringComparison.Ordinal);
                prefix = prefix < 0 ? 0 : prefix + 2;
                Channels.Add(new Channel { Id = long.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    Name = raw.Substring(prefix).Replace("\\\"", "\"").Replace("\\\\", "\\"),
                    Offset = Utf8.GetByteCount(text.Substring(0, name.Index + prefix)),
                    Length = Utf8.GetByteCount(raw.Substring(prefix)) });
            }
            foreach (Match match in Regex.Matches(text, "(?m)^[\\t ]*C:\\s*\"OO\"\\s*,\\s*(-?\\d+)\\s*,\\s*(-?\\d+)"))
                parents[long.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)] =
                    long.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Escape(string name) => name.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
