// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System.IO;
    using System.Text;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    public sealed class FPVideoInstaMetadataTests
    {
        private static void Varint(BinaryWriter writer, ulong value)
        {
            while (value >= 128) { writer.Write((byte)((value & 127) | 128)); value >>= 7; }
            writer.Write((byte)value);
        }
        private static void Text(BinaryWriter writer, int field, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text); Varint(writer, (ulong)(field * 8 + 2)); Varint(writer, (ulong)bytes.Length); writer.Write(bytes);
        }
        [Test]
        public void CameraMetadataParsesKnownFieldsAndSkipsUnknownPayloads()
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    Text(writer, 2, "Insta360 X6"); Text(writer, 22, "standard"); Text(writer, 111, FPVideoFactoryCalibrationTests.X6);
                    Varint(writer, 131 * 8); Varint(writer, 4); Varint(writer, 24 * 8); Varint(writer, 10735982);
                    Varint(writer, 62 * 8); Varint(writer, 1); Varint(writer, 25 * 8 + 1); writer.Write(12.5);
                    Text(writer, 999, "unknown data");
                }
                var metadata = FPVideoCameraMetadata.Parse(bytes.ToArray());
                Assert.That(metadata.Camera, Is.EqualTo("Insta360 X6")); Assert.That(metadata.Gamma, Is.EqualTo("standard"));
                Assert.That(metadata.StreamType, Is.EqualTo(4)); Assert.That(metadata.FirstFrameTimestamp, Is.EqualTo(10735982));
                Assert.That(metadata.RawGyro, Is.True); Assert.That(metadata.RollingShutterTime, Is.EqualTo(12.5));
                Assert.That(metadata.OffsetV6, Is.EqualTo(FPVideoFactoryCalibrationTests.X6));
            }
        }
        [Test]
        public void CameraMetadataRejectsOverflowTruncationWrongWireTypeAndNonFiniteTiming()
        {
            Assert.Throws<InvalidDataException>(() => FPVideoCameraMetadata.Parse(new byte[] { 0x12, 5, 1 }));
            Assert.Throws<InvalidDataException>(() => FPVideoCameraMetadata.Parse(new byte[] { 0x10, 0 }));
            Assert.Throws<InvalidDataException>(() => FPVideoCameraMetadata.Parse(new byte[] { 0 }));
            Assert.Throws<InvalidDataException>(() => FPVideoCameraMetadata.Parse(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 2 }));
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) { Varint(writer, 25 * 8 + 1); writer.Write(double.NaN); }
                Assert.Throws<InvalidDataException>(() => FPVideoCameraMetadata.Parse(bytes.ToArray()));
            }
        }
        [Test]
        public void IndexedTrailerReadsBoundedPayloadAndRejectsInvalidOffset()
        {
            string path = Path.GetTempFileName();
            try
            {
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(new byte[] { 4, 5, 6 });
                    writer.Write((byte)1); writer.Write((byte)1); writer.Write(3u); writer.Write(0u);
                    writer.Write((byte)0); writer.Write((byte)0); writer.Write(10u);
                    writer.Write(new byte[32]); writer.Write(91u); writer.Write(3u);
                    writer.Write(Encoding.ASCII.GetBytes("8db42d694ccc418790edff439fe026bf"));
                }
                var metadata = FPVideoInstaMetadata.Read(path);
                Assert.That(metadata.Version, Is.EqualTo(3)); Assert.That(metadata.Records.Count, Is.EqualTo(1));
                Assert.That(metadata.ReadRecord(path, 1), Is.EqualTo(new byte[] { 4, 5, 6 }));
                Assert.Throws<InvalidDataException>(() => metadata.ReadRecord(path, 1, 2));
                Assert.That(metadata.ReadRecord(path, 7), Is.Null);
                using (var stream = File.OpenWrite(path)) using (var writer = new BinaryWriter(stream)) { stream.Position = 9; writer.Write(uint.MaxValue); }
                Assert.Throws<InvalidDataException>(() => FPVideoInstaMetadata.Read(path));
            }
            finally { File.Delete(path); }
        }
    }
}
