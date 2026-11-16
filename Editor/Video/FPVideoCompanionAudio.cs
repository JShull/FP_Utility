// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>External PCM/float WAV kept in source time for timeline display and export mixing.</summary>
    internal sealed class FPVideoCompanionAudio
    {
        internal readonly string Path;
        internal readonly int Channels, SampleRate;
        internal readonly float[] Samples;
        internal readonly float[] MinEnvelope, MaxEnvelope;
        internal double Duration => (double)Samples.Length / Channels / SampleRate;

        private FPVideoCompanionAudio(string path, int channels, int rate, float[] samples)
        {
            Path = path; Channels = channels; SampleRate = rate; Samples = samples;
            int bins = Math.Min(4096, Math.Max(1, samples.Length / channels));
            MinEnvelope = new float[bins]; MaxEnvelope = new float[bins];
            for (int b = 0; b < bins; b++)
            {
                int start = (int)((long)b * (samples.Length / channels) / bins), end = (int)((long)(b + 1) * (samples.Length / channels) / bins);
                float min = 0, max = 0;
                for (int i = start * channels; i < end * channels; i++) { min = Math.Min(min, samples[i]); max = Math.Max(max, samples[i]); }
                MinEnvelope[b] = min; MaxEnvelope[b] = max;
            }
        }

        internal static FPVideoCompanionAudio Read(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                string FourCC() => Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (FourCC() != "RIFF") throw new InvalidDataException("Expected a RIFF WAV file.");
                long end = Math.Min(stream.Length, (long)reader.ReadUInt32() + 8);
                if (FourCC() != "WAVE") throw new InvalidDataException("Expected a WAVE file.");
                int format = 0, channels = 0, rate = 0, bits = 0, align = 0; long data = -1, bytes = 0;
                while (stream.Position + 8 <= end)
                {
                    string id = FourCC(); uint size = reader.ReadUInt32(); long start = stream.Position;
                    if (size > end - start) throw new InvalidDataException("Truncated WAV chunk.");
                    if (id == "fmt ")
                    {
                        if (size < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                        format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = checked((int)reader.ReadUInt32());
                        reader.ReadUInt32(); align = reader.ReadUInt16(); bits = reader.ReadUInt16();
                        if (format == 0xfffe && size >= 40)
                        {
                            stream.Position = start + 24; var subtype = new Guid(reader.ReadBytes(16));
                            if (subtype == new Guid("00000001-0000-0010-8000-00aa00389b71")) format = 1;
                            else if (subtype == new Guid("00000003-0000-0010-8000-00aa00389b71")) format = 3;
                        }
                    }
                    else if (id == "data" && data < 0) { data = start; bytes = size; }
                    stream.Position = start + size + (size & 1);
                }
                if (data < 0 || channels < 1 || channels > 2 || rate <= 0 ||
                    !((format == 1 && (bits == 16 || bits == 24 || bits == 32)) || (format == 3 && bits == 32)) || align != channels * (bits / 8) || bytes % align != 0)
                    throw new NotSupportedException("WAV must be mono/stereo 16-, 24-, or 32-bit PCM or 32-bit float.");
                long count = bytes / (bits / 8);
                if (count > 128L * 1024 * 1024) throw new NotSupportedException("WAV exceeds the 512 MB decoded-audio limit.");
                var samples = new float[(int)count]; stream.Position = data;
                for (int i = 0; i < samples.Length; i++)
                {
                    float value;
                    if (format == 3) value = reader.ReadSingle();
                    else if (bits == 16) value = reader.ReadInt16() / 32768f;
                    else if (bits == 32) value = (float)(reader.ReadInt32() / 2147483648d);
                    else { int v = reader.ReadByte() | reader.ReadByte() << 8 | reader.ReadByte() << 16; if ((v & 0x800000) != 0) v |= unchecked((int)0xff000000); value = v / 8388608f; }
                    samples[i] = float.IsNaN(value) || float.IsInfinity(value) ? 0 : value;
                }
                return new FPVideoCompanionAudio(System.IO.Path.GetFullPath(path), channels, rate, samples);
            }
        }

        internal float Sample(long sourceFrame, int outputChannel, int outputChannels, long offsetFrames)
        {
            long index = sourceFrame - offsetFrames;
            if (index < 0 || index >= Samples.Length / Channels) return 0;
            int p = checked((int)(index * Channels));
            if (Channels == 1) return Samples[p];
            if (outputChannels == 1) return (Samples[p] + Samples[p + 1]) * .5f;
            return Samples[p + Math.Min(outputChannel, 1)];
        }
    }
}
