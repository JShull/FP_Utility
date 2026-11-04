// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>Source-owned asynchronous IMU read/fusion with deterministic, source-time pose lookup.</summary>
    internal sealed class FPVideoImuMotion : IDisposable
    {
        internal const string Method = "FP X6 gyro + gravity-gated tilt v1; quaternion forward/back smoothing; relative heading only";
        private Task<byte[]> read;
        private readonly FPVideoCameraMetadata metadata;
        private readonly double duration;
        private NativeArray<byte> bytes;
        private NativeArray<double4> poses;
        private NativeArray<double> clock, stats;
        private NativeArray<int> result;
        private JobHandle handle;
        private bool scheduled, disposed;
        private double[] times;
        private double4[] raw, filtered;
        private float smoothing = -1;
        internal bool Ready { get; private set; }
        internal string Error { get; private set; }
        internal int Samples { get; private set; }
        internal double MaximumGapSeconds { get; private set; }
        internal string Fingerprint => metadata.Factory.fingerprint;
        internal string Status => Error ?? (Ready ? $"{Samples:N0} IMU samples • max gap {MaximumGapSeconds*1000:F2} ms • relative heading" : scheduled ? "Fusing IMU motion…" : "Reading IMU record…");
        internal static bool Supported(FPVideoCameraMetadata m) => m != null && m.Camera == "Insta360 X6" && m.RawGyro && m.HasImu && m.HasGyroTimestamp &&
            m.StreamType == 4 && m.Factory != null && m.AccelerationRangeG > 0 && m.AccelerationRangeG <= 64 && m.GyroRangeDps > 0 && m.GyroRangeDps <= 8000 &&
            m.FirstFrameTimestamp <= 9000000000000000ul && Math.Abs(m.GyroTimestamp) <= 1000;
        internal FPVideoImuMotion(string path, FPVideoCameraMetadata metadata, double duration)
        {
            if (!Supported(metadata)) throw new NotSupportedException("Motion requires a supported raw-IMU X6 recording with sensor ranges, timing and reversed tracks.");
            this.metadata = metadata; this.duration = duration;
            read = Task.Run(() =>
            {
                var index = FPVideoInstaMetadata.Read(path);
                foreach (var record in index.Records) if (record.Type == 3 && record.Format != 0) throw new InvalidDataException("Unsupported IMU record encoding.");
                var data = index.ReadRecord(path, 3, 128 * 1024 * 1024);
                if (data == null || data.Length < 40 || data.Length % 20 != 0) throw new InvalidDataException("X6 raw IMU requires complete 20-byte samples.");
                return data;
            });
        }
        internal void Update()
        {
            if (disposed || Ready || Error != null) return;
            try
            {
                if (!scheduled)
                {
                    if (!read.IsCompleted) return;
                    var data = read.GetAwaiter().GetResult(); Samples = data.Length / 20;
                    bytes = new NativeArray<byte>(data, Allocator.Persistent);
                    read = null;
                    int capacity = (Samples + 9) / 10 + 1;
                    poses = new NativeArray<double4>(capacity, Allocator.Persistent); clock = new NativeArray<double>(capacity, Allocator.Persistent);
                    stats = new NativeArray<double>(5, Allocator.Persistent); result = new NativeArray<int>(2, Allocator.Persistent);
                    handle = new FPVideoImuKernel.Fuse { Bytes = bytes, Poses = poses, Times = clock, Stats = stats, Result = result,
                        EpochMicros = (long)metadata.FirstFrameTimestamp, OffsetMs = metadata.GyroTimestamp, AccelerationRange = metadata.AccelerationRangeG,
                        GyroRange = metadata.GyroRangeDps, Stride = 10 }.Schedule();
                    scheduled = true; return;
                }
                if (!handle.IsCompleted) return;
                handle.Complete();
                if (result[0] != 0) throw new InvalidDataException("IMU fusion rejected timestamps, clipping or gravity initialization (code " + result[0] + ").");
                int count = result[1];
                if (count < 2 || clock[0] > 0 || clock[count - 1] < duration - .02 || stats[3] > 0)
                    throw new InvalidDataException("IMU clock/gravity initialization does not cover the video range.");
                raw = new double4[count]; times = new double[count];
                for (int i = 0; i < count; i++) { raw[i] = poses[i]; times[i] = clock[i]; }
                MaximumGapSeconds = stats[1]; Ready = true; ReleaseBuffers();
            }
            catch (Exception e) { Error = e.Message; if (scheduled) handle.Complete(); ReleaseBuffers(); }
        }
        private void Smooth(float seconds)
        {
            if (filtered != null && smoothing == seconds) return;
            using (var source = new NativeArray<double4>(raw, Allocator.TempJob))
            using (var time = new NativeArray<double>(times, Allocator.TempJob))
            using (var output = new NativeArray<double4>(raw.Length, Allocator.TempJob))
            {
                new FPVideoImuKernel.Smooth { Raw = source, Times = time, Filtered = output, Seconds = seconds }.Schedule().Complete();
                filtered = output.ToArray(); smoothing = seconds;
            }
        }
        internal static Quaternion Sample(double[] times, double4[] values, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < times[0] || seconds > times[times.Length - 1]) throw new ArgumentOutOfRangeException(nameof(seconds), "Motion query is outside validated sensor coverage.");
            int index = Array.BinarySearch(times, seconds);
            if (index >= 0) return ToQuaternion(values[index]);
            int next = ~index; double alpha = (seconds - times[next - 1]) / (times[next] - times[next - 1]);
            return ToQuaternion(FPVideoImuKernel.Mix(values[next - 1], values[next], alpha));
        }
        private static Quaternion ToQuaternion(double4 q) => new Quaternion((float)q.x, (float)q.y, (float)q.z, (float)q.w).normalized;
        internal static Quaternion Heading(Quaternion orientation)
        {
            Vector3 forward = orientation * Vector3.forward;
            if (forward.x * forward.x + forward.z * forward.z < .000001f) { Vector3 right = orientation * Vector3.right; forward = new Vector3(-right.z, 0, right.x); }
            return Quaternion.AngleAxis(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, Vector3.up);
        }
        internal Quaternion Correction(double seconds, FPVideoUnwrapSettings settings)
        {
            if (!Ready || Error != null) throw new InvalidOperationException("Load and validate motion data before enabling stabilization.");
            if (settings.factoryCalibration?.fingerprint != Fingerprint) throw new InvalidDataException("Motion and factory calibration do not belong to the same recording configuration.");
            Smooth(settings.motionSmoothingSeconds);
            double query = seconds + settings.motionOffsetMilliseconds * .001;
            Quaternion original = Sample(times, raw, query), target = Sample(times, filtered, query);
            Quaternion heading = Heading(target);
            if (settings.levelHorizon) target = heading;
            if (settings.lockDirection)
            {
                Quaternion anchor = Heading(Sample(times, filtered, settings.motionReferenceSeconds + settings.motionOffsetMilliseconds * .001));
                target = anchor * Quaternion.Inverse(heading) * target;
            }
            return Quaternion.Inverse(original) * target;
        }
        internal string Provenance => Method + $"; accel ±{metadata.AccelerationRangeG}g; gyro ±{metadata.GyroRangeDps}deg/s; epoch {metadata.FirstFrameTimestamp}us; gyro offset {metadata.GyroTimestamp}ms; {Samples} raw samples";
        private void ReleaseBuffers()
        {
            if (bytes.IsCreated) bytes.Dispose(); if (poses.IsCreated) poses.Dispose(); if (clock.IsCreated) clock.Dispose();
            if (stats.IsCreated) stats.Dispose(); if (result.IsCreated) result.Dispose();
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; if (scheduled) handle.Complete(); ReleaseBuffers(); raw = filtered = null; times = null; read = null;
        }
    }
}
