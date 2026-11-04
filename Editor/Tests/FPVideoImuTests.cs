// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Text;
    using FuzzPhyte.Utility.Editor.Video;
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class FPVideoImuTests
    {
        // Analytic baseline: level camera turning at quantized 59.9975586 deg/s for two seconds.
        private static byte[] Samples(int gyro = 983)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    for (int i = 0; i <= 2000; i++)
                    {
                        writer.Write((ulong)(5000000 + i * 1000));
                        writer.Write((ushort)(32768 - 1024)); writer.Write((ushort)32768); writer.Write((ushort)32768);
                        writer.Write((ushort)(32768 + gyro)); writer.Write((ushort)32768); writer.Write((ushort)32768);
                    }
                return stream.ToArray();
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void GyroMatchesAnalyticConstantYawAndPreservesNorm(bool burstJob)
        {
            using (var bytes = new NativeArray<byte>(Samples(), Allocator.TempJob))
            using (var poses = new NativeArray<double4>(202, Allocator.TempJob))
            using (var times = new NativeArray<double>(202, Allocator.TempJob))
            using (var stats = new NativeArray<double>(5, Allocator.TempJob))
            using (var result = new NativeArray<int>(2, Allocator.TempJob))
            {
                var job = new FPVideoImuKernel.Fuse { Bytes = bytes, Poses = poses, Times = times, Stats = stats, Result = result, EpochMicros = 5000000, AccelerationRange = 32, GyroRange = 2000, Stride = 10 };
                if (burstJob) job.Schedule().Complete(); else job.Execute();
                Assert.That(result[0], Is.Zero); Assert.That(result[1], Is.EqualTo(201));
                double radians = 983d * 2000 / 32768 * 2 * Math.PI / 180;
                double4 expected = new double4(0, Math.Sin(radians / 2), 0, Math.Cos(radians / 2));
                Assert.That(math.length(poses[200] - expected), Is.LessThan(1e-10));
                for (int i = 0; i < 201; i++) Assert.That(math.abs(math.length(poses[i]) - 1), Is.LessThan(1e-12));
                Assert.That(times[200], Is.EqualTo(2).Within(1e-12));
            }
        }
        [TestCase(1, 2)] [TestCase(2, 3)] [TestCase(3, 4)]
        public void FusionRejectsInvalidClockClippingAndMissingGravity(int fault, int code)
        {
            byte[] data = Samples(fault == 2 ? 32760 : 0);
            if (fault == 1) Array.Copy(data, 0, data, 20, 8);
            if (fault == 3) for (int i = 0; i <= 2000; i++) { data[i * 20 + 8] = 0; data[i * 20 + 9] = 128; }
            using (var bytes = new NativeArray<byte>(data, Allocator.TempJob))
            using (var poses = new NativeArray<double4>(202, Allocator.TempJob))
            using (var times = new NativeArray<double>(202, Allocator.TempJob))
            using (var stats = new NativeArray<double>(5, Allocator.TempJob))
            using (var result = new NativeArray<int>(2, Allocator.TempJob))
            {
                new FPVideoImuKernel.Fuse { Bytes = bytes, Poses = poses, Times = times, Stats = stats, Result = result, EpochMicros = 5000000, AccelerationRange = 32, GyroRange = 2000, Stride = 10 }.Schedule().Complete();
                Assert.That(result[0], Is.EqualTo(code));
            }
        }
        [Test]
        public void StationaryTiltUsesGravityWithoutInventingYawMotion()
        {
            byte[] data = Samples(0);
            // Quantized 30-degree roll about the forward axis: up in body coordinates.
            short ax = -887, az = 512;
            for (int i = 0; i <= 2000; i++)
            {
                ushort x = (ushort)(32768 + ax), z = (ushort)(32768 + az);
                data[i * 20 + 8] = (byte)x; data[i * 20 + 9] = (byte)(x >> 8);
                data[i * 20 + 12] = (byte)z; data[i * 20 + 13] = (byte)(z >> 8);
            }
            using (var bytes = new NativeArray<byte>(data, Allocator.TempJob))
            using (var poses = new NativeArray<double4>(202, Allocator.TempJob))
            using (var times = new NativeArray<double>(202, Allocator.TempJob))
            using (var stats = new NativeArray<double>(5, Allocator.TempJob))
            using (var result = new NativeArray<int>(2, Allocator.TempJob))
            {
                new FPVideoImuKernel.Fuse { Bytes = bytes, Poses = poses, Times = times, Stats = stats, Result = result, EpochMicros = 5000000, AccelerationRange = 32, GyroRange = 2000, Stride = 10 }.Schedule().Complete();
                Assert.That(result[0], Is.Zero);
                double3 up = math.normalize(new double3(512, 887, 0));
                Assert.That(math.length(FPVideoImuKernel.Rotate(poses[200], up) - new double3(0, 1, 0)), Is.LessThan(1e-10));
                Assert.That(math.length(poses[200] - poses[0]), Is.LessThan(1e-10));
            }
        }
        [Test]
        public void SmoothingAttenuatesAlternatingMotionAndSamplingIsOrderIndependent()
        {
            using (var raw = new NativeArray<double4>(401, Allocator.TempJob))
            using (var times = new NativeArray<double>(401, Allocator.TempJob))
            using (var output = new NativeArray<double4>(401, Allocator.TempJob))
            {
                var rotations = new double4[401]; var timestamps = new double[401];
                for (int i = 0; i < 401; i++) { rotations[i] = FPVideoImuKernel.Step(new double3(i % 2 == 0 ? .1 : -.1, 0, 0)); if (i % 3 == 0) rotations[i] = -rotations[i]; timestamps[i] = i * .01; }
                raw.CopyFrom(rotations); times.CopyFrom(timestamps);
                new FPVideoImuKernel.Smooth { Raw = raw, Times = times, Filtered = output, Seconds = .3 }.Schedule().Complete();
                var clock = times.ToArray(); var poses = output.ToArray();
                for (int i = 100; i < 300; i++) Assert.That(Math.Abs(poses[i].x), Is.LessThan(.004));
                Quaternion first = FPVideoImuMotion.Sample(clock, poses, 2.123);
                FPVideoImuMotion.Sample(clock, poses, 3.9); FPVideoImuMotion.Sample(clock, poses, .1);
                Assert.That(FPVideoImuMotion.Sample(clock, poses, 2.123), Is.EqualTo(first));
                Assert.Throws<ArgumentOutOfRangeException>(() => FPVideoImuMotion.Sample(clock, poses, -1));
            }
        }
        [UnityTest]
        public IEnumerator IndexedMotionLoadsAndDirectionLockUsesFullSourceClock()
        {
            string path = Path.GetTempFileName(); byte[] data = Samples();
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(data); writer.Write((byte)3); writer.Write((byte)0); writer.Write((uint)data.Length); writer.Write(0u);
                writer.Write((byte)0); writer.Write((byte)0); writer.Write(10u);
                writer.Write(new byte[32]); writer.Write((uint)(data.Length + 88)); writer.Write(3u); writer.Write(Encoding.ASCII.GetBytes("8db42d694ccc418790edff439fe026bf"));
            }
            var metadata = new FPVideoCameraMetadata { Camera = "Insta360 X6", RawGyro = true, HasImu = true, HasGyroTimestamp = true, StreamType = 4,
                Factory = FPVideoFactoryCalibration.Parse(FPVideoFactoryCalibrationTests.X6, 4), FirstFrameTimestamp = 5000000, AccelerationRangeG = 32, GyroRangeDps = 2000 };
            try
            {
                using (var motion = new FPVideoImuMotion(path, metadata, 2))
                {
                    double deadline = UnityEditor.EditorApplication.timeSinceStartup + 15;
                    while (!motion.Ready && motion.Error == null && UnityEditor.EditorApplication.timeSinceStartup < deadline) { motion.Update(); yield return null; }
                    Assert.That(motion.Error, Is.Null); Assert.That(motion.Ready, Is.True);
                    var settings = new FPVideoUnwrapSettings { stabilizeMotion = true, useFactoryCalibration = true, factoryCalibration = metadata.Factory.Snapshot(), lockDirection = true, motionReferenceSeconds = .5 };
                    Quaternion correction = motion.Correction(1.5, settings);
                    Quaternion later = Quaternion.Euler(0, 59.9975586f * 1.5f, 0) * correction;
                    Quaternion earlier = Quaternion.Euler(0, 59.9975586f * .5f, 0) * motion.Correction(.5, settings);
                    Assert.That(Vector3.Angle(later * Vector3.forward, earlier * Vector3.forward), Is.LessThan(.01));
                    Quaternion saved = correction; motion.Correction(.1, settings); Assert.That(motion.Correction(1.5, settings), Is.EqualTo(saved));
                    settings.factoryCalibration.fingerprint = "different";
                    Assert.Throws<InvalidDataException>(() => motion.Correction(1, settings));
                }
            }
            finally { File.Delete(path); }
        }
    }
}
