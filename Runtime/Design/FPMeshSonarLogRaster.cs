// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using UnityEngine;

    /// <summary>
    /// Reads Cerulean/SonarView svlog packet streams and grids Surveyor point packets for mesh sampling.
    /// </summary>
    public sealed class FPMeshSonarLogRaster
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float[] DepthValues { get; private set; }
        public int PacketCount { get; private set; }
        public int PointPacketCount { get; private set; }
        public int PointCount { get; private set; }
        public int ValidCellCount { get; private set; }
        public int InvalidChecksumCount { get; private set; }
        public int NavigationPositionCount { get; private set; }
        public int NavigationHeadingCount { get; private set; }
        public string SourceDataKind { get; private set; }
        public string SampleValueLabel { get; private set; }
        public float MinCrossTrack { get; private set; }
        public float MaxCrossTrack { get; private set; }
        public float MinAlongTrack { get; private set; }
        public float MaxAlongTrack { get; private set; }
        public float MinDepth { get; private set; }
        public float MaxDepth { get; private set; }

        private const ushort PacketIdYzPointData = 3011;
        private const ushort PacketIdAtofPointData = 3012;
        private const ushort PacketIdOmniscanMonoProfile = 2198;
        private const int DefaultRasterResolution = 512;
        private const int OmniscanMonoProfileHeaderBytes = 52;

        private FPMeshSonarLogRaster()
        {
        }

        public static bool TryLoad(string sourcePath, FPMeshHeightmapSettings settings, out FPMeshSonarLogRaster raster, out string error)
        {
            raster = null;
            error = string.Empty;

            FPMeshHeightmapSettings safeSettings = settings.Sanitized();
            if (!TryResolvePath(sourcePath, out string resolvedPath))
            {
                error = "The sonar log file path could not be resolved.";
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = ReadLogBytes(resolvedPath);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (safeSettings.SonarLogMeshMode == FPMeshSonarLogMeshMode.GeospatialMosaic)
            {
                if (TryReadOmniscanGeospatialMosaic(bytes, safeSettings, out raster, out error))
                {
                    return true;
                }

                string geospatialError = error;
                if (!TryReadPointCloud(bytes, safeSettings, out List<SonarPoint> fallbackPoints, out int fallbackPacketCount, out int fallbackPointPacketCount, out int fallbackInvalidChecksumCount, out error))
                {
                    error = geospatialError;
                    return false;
                }

                if (fallbackPoints.Count == 0)
                {
                    error = geospatialError;
                    return false;
                }

                raster = BuildRaster(fallbackPoints, safeSettings, fallbackPacketCount, fallbackPointPacketCount, fallbackInvalidChecksumCount);
                return true;
            }

            if (!TryReadPointCloud(bytes, safeSettings, out List<SonarPoint> points, out int packetCount, out int pointPacketCount, out int invalidChecksumCount, out error))
            {
                return false;
            }

            if (points.Count == 0)
            {
                if (TryReadOmniscanMonoProfiles(bytes, safeSettings, out raster, out error))
                {
                    return true;
                }

                error = "No supported sonar data packets were found. Supported packets: Omniscan os_mono_profile, Surveyor ATOF_POINT_DATA, and Surveyor YZ_POINT_DATA.";
                return false;
            }

            raster = BuildRaster(points, safeSettings, packetCount, pointPacketCount, invalidChecksumCount);
            return true;
        }

        public bool TryGetRealSizeMeters(out float widthMeters, out float lengthMeters)
        {
            widthMeters = Mathf.Max(0.01f, MaxCrossTrack - MinCrossTrack);
            lengthMeters = Mathf.Max(0.01f, MaxAlongTrack - MinAlongTrack);
            return Width > 0 && Height > 0 && widthMeters > 0f && lengthMeters > 0f;
        }

        public float SampleBilinear(float u, float v)
        {
            if (DepthValues == null || DepthValues.Length == 0 || Width <= 0 || Height <= 0)
            {
                return float.NaN;
            }

            float x = Mathf.Clamp01(u) * (Width - 1);
            float y = Mathf.Clamp01(v) * (Height - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, Width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, Height - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, Width - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, Height - 1);
            float tx = x - x0;
            float ty = y - y0;

            float h00 = GetValue(x0, y0);
            float h10 = GetValue(x1, y0);
            float h01 = GetValue(x0, y1);
            float h11 = GetValue(x1, y1);

            if (!IsFinite(h00) || !IsFinite(h10) || !IsFinite(h01) || !IsFinite(h11))
            {
                return FirstFinite(h00, h10, h01, h11);
            }

            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
        }

        private float GetValue(int x, int y)
        {
            return DepthValues[x + (y * Width)];
        }

        private static byte[] ReadLogBytes(string resolvedPath)
        {
            string extension = Path.GetExtension(resolvedPath);
            if (!string.Equals(extension, ".svlz", StringComparison.OrdinalIgnoreCase))
            {
                return File.ReadAllBytes(resolvedPath);
            }

            using (FileStream fileStream = File.OpenRead(resolvedPath))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var memoryStream = new MemoryStream())
            {
                gzipStream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private static bool TryReadPointCloud(
            byte[] bytes,
            FPMeshHeightmapSettings settings,
            out List<SonarPoint> points,
            out int packetCount,
            out int pointPacketCount,
            out int invalidChecksumCount,
            out string error)
        {
            points = new List<SonarPoint>();
            packetCount = 0;
            pointPacketCount = 0;
            invalidChecksumCount = 0;
            error = string.Empty;

            if (bytes == null || bytes.Length < 10)
            {
                error = "The sonar log file is too small to contain a packet stream.";
                return false;
            }

            int offset = 0;
            while (offset <= bytes.Length - 10)
            {
                if (bytes[offset] != 0x42 || bytes[offset + 1] != 0x52)
                {
                    offset++;
                    continue;
                }

                ushort payloadLength = ReadUInt16(bytes, offset + 2);
                int packetLength = 8 + payloadLength + 2;
                if (payloadLength > bytes.Length || offset + packetLength > bytes.Length)
                {
                    offset++;
                    continue;
                }

                ushort packetId = ReadUInt16(bytes, offset + 4);
                if (!HasValidChecksum(bytes, offset, packetLength))
                {
                    invalidChecksumCount++;
                }

                packetCount++;
                int payloadOffset = offset + 8;
                if (packetId == PacketIdYzPointData)
                {
                    int before = points.Count;
                    TryReadYzPointData(bytes, payloadOffset, payloadLength, settings, points);
                    if (points.Count > before)
                    {
                        pointPacketCount++;
                    }
                }
                else if (packetId == PacketIdAtofPointData)
                {
                    int before = points.Count;
                    TryReadAtofPointData(bytes, payloadOffset, payloadLength, settings, points);
                    if (points.Count > before)
                    {
                        pointPacketCount++;
                    }
                }

                offset += packetLength;
            }

            return true;
        }

        private static void TryReadYzPointData(byte[] bytes, int payloadOffset, int payloadLength, FPMeshHeightmapSettings settings, List<SonarPoint> points)
        {
            const int numPointsOffset = 98;
            const int pointDataOffset = 100;
            if (payloadLength < pointDataOffset || payloadOffset + pointDataOffset > bytes.Length)
            {
                return;
            }

            uint pingNumber = ReadUInt32(bytes, payloadOffset + 4);
            ushort numPoints = ReadUInt16(bytes, payloadOffset + numPointsOffset);
            int availablePoints = Mathf.Min(numPoints, (payloadLength - pointDataOffset) / 8);
            float alongTrack = pingNumber * settings.SonarLogForwardStepMeters;

            for (int i = 0; i < availablePoints; i++)
            {
                int pointOffset = payloadOffset + pointDataOffset + (i * 8);
                float crossTrack = ReadSingle(bytes, pointOffset);
                float depth = ReadSingle(bytes, pointOffset + 4);
                AddPoint(points, crossTrack, alongTrack, depth);
            }
        }

        private static void TryReadAtofPointData(byte[] bytes, int payloadOffset, int payloadLength, FPMeshHeightmapSettings settings, List<SonarPoint> points)
        {
            const int alignedHeaderBytes = 44;
            const int compactHeaderBytes = 40;
            if (payloadLength < compactHeaderBytes)
            {
                return;
            }

            int headerBytes = ChooseAtofHeaderSize(bytes, payloadOffset, payloadLength);
            if (headerBytes <= 0)
            {
                return;
            }

            int sosOffset = headerBytes == alignedHeaderBytes ? 20 : 16;
            int pingNumberOffset = headerBytes == alignedHeaderBytes ? 24 : 20;
            int numPointsOffset = headerBytes == alignedHeaderBytes ? 40 : 36;

            float speedOfSound = ReadSingle(bytes, payloadOffset + sosOffset);
            uint pingNumber = ReadUInt32(bytes, payloadOffset + pingNumberOffset);
            ushort numPoints = ReadUInt16(bytes, payloadOffset + numPointsOffset);
            int availablePoints = Mathf.Min(numPoints, (payloadLength - headerBytes) / 16);
            float alongTrack = pingNumber * settings.SonarLogForwardStepMeters;

            for (int i = 0; i < availablePoints; i++)
            {
                int pointOffset = payloadOffset + headerBytes + (i * 16);
                float angle = ReadSingle(bytes, pointOffset);
                float tofSeconds = ReadSingle(bytes, pointOffset + 4);
                if (!IsFinite(speedOfSound) || speedOfSound <= 0f || !IsFinite(tofSeconds) || tofSeconds <= 0f)
                {
                    continue;
                }

                float distance = 0.5f * speedOfSound * tofSeconds;
                float crossTrack = distance * Mathf.Sin(angle);
                float depth = -distance * Mathf.Cos(angle);
                AddPoint(points, crossTrack, alongTrack, depth);
            }
        }

        private static int ChooseAtofHeaderSize(byte[] bytes, int payloadOffset, int payloadLength)
        {
            const int alignedHeaderBytes = 44;
            const int compactHeaderBytes = 40;
            if (payloadLength >= alignedHeaderBytes)
            {
                ushort alignedCount = ReadUInt16(bytes, payloadOffset + 40);
                if (alignedCount <= (payloadLength - alignedHeaderBytes) / 16)
                {
                    return alignedHeaderBytes;
                }
            }

            if (payloadLength >= compactHeaderBytes)
            {
                ushort compactCount = ReadUInt16(bytes, payloadOffset + 36);
                if (compactCount <= (payloadLength - compactHeaderBytes) / 16)
                {
                    return compactHeaderBytes;
                }
            }

            return 0;
        }

        private static void AddPoint(List<SonarPoint> points, float crossTrack, float alongTrack, float depth)
        {
            if (!IsFinite(crossTrack) || !IsFinite(alongTrack) || !IsFinite(depth))
            {
                return;
            }

            points.Add(new SonarPoint(crossTrack, alongTrack, depth));
        }

        private static FPMeshSonarLogRaster BuildRaster(List<SonarPoint> points, FPMeshHeightmapSettings settings, int packetCount, int pointPacketCount, int invalidChecksumCount)
        {
            Bounds2D bounds = CalculateBounds(points);
            int width = Mathf.Clamp(settings.SonarLogRasterWidth, 8, 8192);
            int height = Mathf.Clamp(settings.SonarLogRasterLength, 8, 8192);
            if (width <= 0)
            {
                width = DefaultRasterResolution;
            }

            if (height <= 0)
            {
                height = DefaultRasterResolution;
            }

            float[] sums = new float[width * height];
            int[] counts = new int[width * height];
            for (int i = 0; i < sums.Length; i++)
            {
                sums[i] = 0f;
                counts[i] = 0;
            }

            float crossRange = Mathf.Max(0.0001f, bounds.MaxCrossTrack - bounds.MinCrossTrack);
            float alongRange = Mathf.Max(0.0001f, bounds.MaxAlongTrack - bounds.MinAlongTrack);
            for (int i = 0; i < points.Count; i++)
            {
                SonarPoint point = points[i];
                int x = Mathf.Clamp(Mathf.RoundToInt(((point.CrossTrack - bounds.MinCrossTrack) / crossRange) * (width - 1)), 0, width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(((point.AlongTrack - bounds.MinAlongTrack) / alongRange) * (height - 1)), 0, height - 1);
                int index = x + (y * width);
                sums[index] += point.Depth;
                counts[index]++;
            }

            float[] values = new float[width * height];
            int validCells = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (counts[i] <= 0)
                {
                    values[i] = float.NaN;
                    continue;
                }

                values[i] = sums[i] / counts[i];
                validCells++;
            }

            return new FPMeshSonarLogRaster
            {
                Width = width,
                Height = height,
                DepthValues = values,
                PacketCount = packetCount,
                PointPacketCount = pointPacketCount,
                PointCount = points.Count,
                ValidCellCount = validCells,
                InvalidChecksumCount = invalidChecksumCount,
                SourceDataKind = "Surveyor point depth",
                SampleValueLabel = "Depth Values",
                MinCrossTrack = bounds.MinCrossTrack,
                MaxCrossTrack = bounds.MaxCrossTrack,
                MinAlongTrack = bounds.MinAlongTrack,
                MaxAlongTrack = bounds.MaxAlongTrack,
                MinDepth = bounds.MinDepth,
                MaxDepth = bounds.MaxDepth
            };
        }

        private static bool TryReadOmniscanMonoProfiles(byte[] bytes, FPMeshHeightmapSettings settings, out FPMeshSonarLogRaster raster, out string error)
        {
            raster = null;
            error = string.Empty;

            if (!TryScanOmniscanMonoProfileBounds(bytes, settings, out Bounds2D bounds, out int packetCount, out int monoProfileCount, out int invalidChecksumCount, out long sampleCount))
            {
                error = "No Omniscan os_mono_profile packets were found.";
                return false;
            }

            int width = Mathf.Clamp(settings.SonarLogRasterWidth, 8, 8192);
            int height = Mathf.Clamp(settings.SonarLogRasterLength, 8, 8192);
            float[] sums = new float[width * height];
            int[] counts = new int[width * height];

            float crossRange = Mathf.Max(0.0001f, bounds.MaxCrossTrack - bounds.MinCrossTrack);
            float alongRange = Mathf.Max(0.0001f, bounds.MaxAlongTrack - bounds.MinAlongTrack);
            float minValue = float.PositiveInfinity;
            float maxValue = float.NegativeInfinity;

            int offset = 0;
            while (offset <= bytes.Length - 10)
            {
                if (!TryReadPacketHeader(bytes, offset, out ushort payloadLength, out ushort packetId, out int packetLength))
                {
                    offset++;
                    continue;
                }

                if (packetId == PacketIdOmniscanMonoProfile)
                {
                    int payloadOffset = offset + 8;
                    if (TryReadOmniscanMonoProfileHeader(bytes, payloadOffset, payloadLength, settings, out OmniscanMonoProfileHeader header))
                    {
                        for (int i = 0; i < header.NumResults; i++)
                        {
                            int resultOffset = payloadOffset + OmniscanMonoProfileHeaderBytes + (i * 2);
                            ushort rawPower = ReadUInt16(bytes, resultOffset);
                            float value = Mathf.Lerp(header.MinPowerDb, header.MaxPowerDb, rawPower / 65535f);
                            if (!IsFinite(value))
                            {
                                continue;
                            }

                            float sampleT = header.NumResults <= 1 ? 0f : i / (header.NumResults - 1f);
                            float rangeMeters = header.StartMeters + (sampleT * header.LengthMeters);
                            float crossTrack = header.SideSign * rangeMeters;
                            int x = Mathf.Clamp(Mathf.RoundToInt(((crossTrack - bounds.MinCrossTrack) / crossRange) * (width - 1)), 0, width - 1);
                            int y = Mathf.Clamp(Mathf.RoundToInt(((header.AlongTrack - bounds.MinAlongTrack) / alongRange) * (height - 1)), 0, height - 1);
                            int index = x + (y * width);

                            sums[index] += value;
                            counts[index]++;
                            minValue = Mathf.Min(minValue, value);
                            maxValue = Mathf.Max(maxValue, value);
                        }
                    }
                }

                offset += packetLength;
            }

            float[] values = new float[width * height];
            int validCells = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (counts[i] <= 0)
                {
                    values[i] = float.NaN;
                    continue;
                }

                values[i] = sums[i] / counts[i];
                validCells++;
            }

            if (!IsFinite(minValue) || !IsFinite(maxValue))
            {
                minValue = 0f;
                maxValue = 0f;
            }

            raster = new FPMeshSonarLogRaster
            {
                Width = width,
                Height = height,
                DepthValues = values,
                PacketCount = packetCount,
                PointPacketCount = monoProfileCount,
                PointCount = sampleCount > int.MaxValue ? int.MaxValue : (int)sampleCount,
                ValidCellCount = validCells,
                InvalidChecksumCount = invalidChecksumCount,
                SourceDataKind = "Omniscan profile intensity",
                SampleValueLabel = "Power dB",
                MinCrossTrack = bounds.MinCrossTrack,
                MaxCrossTrack = bounds.MaxCrossTrack,
                MinAlongTrack = bounds.MinAlongTrack,
                MaxAlongTrack = bounds.MaxAlongTrack,
                MinDepth = minValue,
                MaxDepth = maxValue
            };
            return true;
        }

        private static bool TryReadOmniscanGeospatialMosaic(byte[] bytes, FPMeshHeightmapSettings settings, out FPMeshSonarLogRaster raster, out string error)
        {
            raster = null;
            error = string.Empty;

            if (!TryReadMavlinkNavigation(bytes, settings, out NavigationData navigation, out error))
            {
                return false;
            }

            if (!TryScanOmniscanGeospatialBounds(bytes, settings, navigation, out Bounds2D bounds, out int packetCount, out int monoProfileCount, out int invalidChecksumCount, out long sampleCount))
            {
                error = "No Omniscan profile packets could be matched to MAVLink navigation.";
                return false;
            }

            float widthMeters = Mathf.Max(0.01f, bounds.MaxCrossTrack - bounds.MinCrossTrack);
            float lengthMeters = Mathf.Max(0.01f, bounds.MaxAlongTrack - bounds.MinAlongTrack);
            int width = Mathf.Clamp(Mathf.CeilToInt(widthMeters / settings.SonarLogCellSizeMeters) + 1, 8, 8192);
            int height = Mathf.Clamp(Mathf.CeilToInt(lengthMeters / settings.SonarLogCellSizeMeters) + 1, 8, 8192);
            float[] values = new float[width * height];
            int[] counts = new int[width * height];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = settings.SonarLogOverlapMode == FPMeshSonarLogOverlapMode.Max
                    ? float.NegativeInfinity
                    : 0f;
                counts[i] = 0;
            }

            float crossRange = Mathf.Max(0.0001f, bounds.MaxCrossTrack - bounds.MinCrossTrack);
            float alongRange = Mathf.Max(0.0001f, bounds.MaxAlongTrack - bounds.MinAlongTrack);
            float minValue = float.PositiveInfinity;
            float maxValue = float.NegativeInfinity;

            int offset = 0;
            while (offset <= bytes.Length - 10)
            {
                if (!TryReadPacketHeader(bytes, offset, out ushort payloadLength, out ushort packetId, out int packetLength))
                {
                    offset++;
                    continue;
                }

                if (packetId == PacketIdOmniscanMonoProfile)
                {
                    int payloadOffset = offset + 8;
                    if (TryReadOmniscanMonoProfileHeader(bytes, payloadOffset, payloadLength, settings, out OmniscanMonoProfileHeader header)
                        && TryResolveProfilePose(header, settings, navigation, out SonarPose pose))
                    {
                        for (int i = 0; i < header.NumResults; i++)
                        {
                            int resultOffset = payloadOffset + OmniscanMonoProfileHeaderBytes + (i * 2);
                            ushort rawPower = ReadUInt16(bytes, resultOffset);
                            float value = Mathf.Lerp(header.MinPowerDb, header.MaxPowerDb, rawPower / 65535f);
                            if (!IsFinite(value))
                            {
                                continue;
                            }

                            float sampleT = header.NumResults <= 1 ? 0f : i / (header.NumResults - 1f);
                            float rangeMeters = header.StartMeters + (sampleT * header.LengthMeters);
                            float crossTrack = header.SideSign * rangeMeters;
                            float east = pose.EastMeters + (crossTrack * pose.RightEast);
                            float north = pose.NorthMeters + (crossTrack * pose.RightNorth);
                            int x = Mathf.Clamp(Mathf.RoundToInt(((east - bounds.MinCrossTrack) / crossRange) * (width - 1)), 0, width - 1);
                            int y = Mathf.Clamp(Mathf.RoundToInt(((north - bounds.MinAlongTrack) / alongRange) * (height - 1)), 0, height - 1);
                            int index = x + (y * width);

                            AccumulateCell(values, counts, index, value, settings.SonarLogOverlapMode);
                            minValue = Mathf.Min(minValue, value);
                            maxValue = Mathf.Max(maxValue, value);
                        }
                    }
                }

                offset += packetLength;
            }

            int validCells = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (counts[i] <= 0)
                {
                    values[i] = float.NaN;
                    continue;
                }

                if (settings.SonarLogOverlapMode == FPMeshSonarLogOverlapMode.Mean)
                {
                    values[i] /= counts[i];
                }

                validCells++;
            }

            if (!IsFinite(minValue) || !IsFinite(maxValue))
            {
                minValue = 0f;
                maxValue = 0f;
            }

            raster = new FPMeshSonarLogRaster
            {
                Width = width,
                Height = height,
                DepthValues = values,
                PacketCount = packetCount,
                PointPacketCount = monoProfileCount,
                PointCount = sampleCount > int.MaxValue ? int.MaxValue : (int)sampleCount,
                ValidCellCount = validCells,
                InvalidChecksumCount = invalidChecksumCount,
                NavigationPositionCount = navigation.Positions.Count,
                NavigationHeadingCount = navigation.Headings.Count,
                SourceDataKind = settings.SonarLogNavSource == FPMeshSonarLogNavSource.GlobalPositionInt
                    ? "Omniscan geospatial mosaic (GPS)"
                    : "Omniscan geospatial mosaic (LOCAL_NED)",
                SampleValueLabel = "Power dB",
                MinCrossTrack = bounds.MinCrossTrack,
                MaxCrossTrack = bounds.MaxCrossTrack,
                MinAlongTrack = bounds.MinAlongTrack,
                MaxAlongTrack = bounds.MaxAlongTrack,
                MinDepth = minValue,
                MaxDepth = maxValue
            };
            return true;
        }

        private static void AccumulateCell(float[] values, int[] counts, int index, float value, FPMeshSonarLogOverlapMode overlapMode)
        {
            switch (overlapMode)
            {
                case FPMeshSonarLogOverlapMode.Max:
                    values[index] = counts[index] <= 0 ? value : Mathf.Max(values[index], value);
                    counts[index]++;
                    break;
                case FPMeshSonarLogOverlapMode.Latest:
                    values[index] = value;
                    counts[index]++;
                    break;
                default:
                    values[index] += value;
                    counts[index]++;
                    break;
            }
        }

        private static bool TryScanOmniscanGeospatialBounds(
            byte[] bytes,
            FPMeshHeightmapSettings settings,
            NavigationData navigation,
            out Bounds2D bounds,
            out int packetCount,
            out int monoProfileCount,
            out int invalidChecksumCount,
            out long sampleCount)
        {
            bounds = new Bounds2D
            {
                MinCrossTrack = float.PositiveInfinity,
                MaxCrossTrack = float.NegativeInfinity,
                MinAlongTrack = float.PositiveInfinity,
                MaxAlongTrack = float.NegativeInfinity,
                MinDepth = 0f,
                MaxDepth = 0f
            };
            packetCount = 0;
            monoProfileCount = 0;
            invalidChecksumCount = 0;
            sampleCount = 0;

            int offset = 0;
            while (offset <= bytes.Length - 10)
            {
                if (!TryReadPacketHeader(bytes, offset, out ushort payloadLength, out ushort packetId, out int packetLength))
                {
                    offset++;
                    continue;
                }

                if (!HasValidChecksum(bytes, offset, packetLength))
                {
                    invalidChecksumCount++;
                }

                packetCount++;
                if (packetId == PacketIdOmniscanMonoProfile)
                {
                    int payloadOffset = offset + 8;
                    if (TryReadOmniscanMonoProfileHeader(bytes, payloadOffset, payloadLength, settings, out OmniscanMonoProfileHeader header)
                        && TryResolveProfilePose(header, settings, navigation, out SonarPose pose))
                    {
                        float startCross = header.SideSign * header.StartMeters;
                        float endCross = header.SideSign * (header.StartMeters + header.LengthMeters);
                        AddGeospatialBoundPoint(ref bounds, pose, startCross);
                        AddGeospatialBoundPoint(ref bounds, pose, endCross);
                        monoProfileCount++;
                        sampleCount += header.NumResults;
                    }
                }

                offset += packetLength;
            }

            return monoProfileCount > 0
                && IsFinite(bounds.MinCrossTrack)
                && IsFinite(bounds.MaxCrossTrack)
                && IsFinite(bounds.MinAlongTrack)
                && IsFinite(bounds.MaxAlongTrack)
                && !Mathf.Approximately(bounds.MinCrossTrack, bounds.MaxCrossTrack)
                && !Mathf.Approximately(bounds.MinAlongTrack, bounds.MaxAlongTrack);
        }

        private static void AddGeospatialBoundPoint(ref Bounds2D bounds, SonarPose pose, float crossTrack)
        {
            float east = pose.EastMeters + (crossTrack * pose.RightEast);
            float north = pose.NorthMeters + (crossTrack * pose.RightNorth);
            bounds.MinCrossTrack = Mathf.Min(bounds.MinCrossTrack, east);
            bounds.MaxCrossTrack = Mathf.Max(bounds.MaxCrossTrack, east);
            bounds.MinAlongTrack = Mathf.Min(bounds.MinAlongTrack, north);
            bounds.MaxAlongTrack = Mathf.Max(bounds.MaxAlongTrack, north);
        }

        private static bool TryReadMavlinkNavigation(byte[] bytes, FPMeshHeightmapSettings settings, out NavigationData navigation, out string error)
        {
            navigation = new NavigationData();
            error = string.Empty;

            double originLatitude = double.NaN;
            double originLongitude = double.NaN;
            int offset = 0;
            while (offset <= bytes.Length - 10)
            {
                if (!TryReadPacketHeader(bytes, offset, out ushort payloadLength, out ushort packetId, out int packetLength))
                {
                    offset++;
                    continue;
                }

                if (packetId == 150)
                {
                    string json = System.Text.Encoding.UTF8.GetString(bytes, offset + 8, payloadLength).Trim('\0', ' ', '\r', '\n', '\t');
                    if (TryExtractString(json, "type", out string messageType)
                        && TryExtractLong(json, "time_boot_ms", out long timeBootMs))
                    {
                        if (settings.SonarLogNavSource == FPMeshSonarLogNavSource.LocalPositionNed
                            && string.Equals(messageType, "LOCAL_POSITION_NED", StringComparison.OrdinalIgnoreCase)
                            && TryExtractFloat(json, "x", out float north)
                            && TryExtractFloat(json, "y", out float east))
                        {
                            navigation.Positions.Add(new TimedPosition(timeBootMs, east, north));
                        }
                        else if (settings.SonarLogNavSource == FPMeshSonarLogNavSource.GlobalPositionInt
                            && string.Equals(messageType, "GLOBAL_POSITION_INT", StringComparison.OrdinalIgnoreCase)
                            && TryExtractDouble(json, "lat", out double latRaw)
                            && TryExtractDouble(json, "lon", out double lonRaw))
                        {
                            double latitude = latRaw / 10000000.0;
                            double longitude = lonRaw / 10000000.0;
                            if (double.IsNaN(originLatitude) || double.IsNaN(originLongitude))
                            {
                                originLatitude = latitude;
                                originLongitude = longitude;
                            }

                            navigation.Positions.Add(new TimedPosition(
                                timeBootMs,
                                LongitudeToMeters(longitude - originLongitude, originLatitude),
                                LatitudeToMeters(latitude - originLatitude)));
                        }

                        if (settings.SonarLogHeadingSource == FPMeshSonarLogHeadingSource.AttitudeYaw
                            && string.Equals(messageType, "ATTITUDE", StringComparison.OrdinalIgnoreCase)
                            && TryExtractFloat(json, "yaw", out float yawRadians))
                        {
                            navigation.Headings.Add(new TimedHeading(timeBootMs, yawRadians));
                        }
                        else if (settings.SonarLogHeadingSource == FPMeshSonarLogHeadingSource.GlobalPositionHeading
                            && string.Equals(messageType, "GLOBAL_POSITION_INT", StringComparison.OrdinalIgnoreCase)
                            && TryExtractFloat(json, "hdg", out float headingCentidegrees)
                            && headingCentidegrees < 65535f)
                        {
                            navigation.Headings.Add(new TimedHeading(timeBootMs, headingCentidegrees * 0.01f * Mathf.Deg2Rad));
                        }
                    }
                }

                offset += packetLength;
            }

            if (navigation.Positions.Count <= 0)
            {
                error = settings.SonarLogNavSource == FPMeshSonarLogNavSource.GlobalPositionInt
                    ? "No MAVLink GLOBAL_POSITION_INT packets were found for geospatial mosaic."
                    : "No MAVLink LOCAL_POSITION_NED packets were found for geospatial mosaic.";
                return false;
            }

            if (settings.SonarLogHeadingSource != FPMeshSonarLogHeadingSource.OmniscanVehicleHeading
                && navigation.Headings.Count <= 0)
            {
                error = settings.SonarLogHeadingSource == FPMeshSonarLogHeadingSource.GlobalPositionHeading
                    ? "No MAVLink GLOBAL_POSITION_INT heading values were found for geospatial mosaic."
                    : "No MAVLink ATTITUDE yaw packets were found for geospatial mosaic.";
                return false;
            }

            navigation.Positions.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
            navigation.Headings.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
            return true;
        }

        private static bool TryResolveProfilePose(OmniscanMonoProfileHeader header, FPMeshHeightmapSettings settings, NavigationData navigation, out SonarPose pose)
        {
            pose = default;
            long timeMs = header.TimestampMs + settings.SonarLogTimeOffsetMs;
            if (!TrySamplePosition(navigation.Positions, timeMs, out TimedPosition position))
            {
                return false;
            }

            float headingRadians = header.VehicleHeadingRadians;
            if (settings.SonarLogHeadingSource != FPMeshSonarLogHeadingSource.OmniscanVehicleHeading)
            {
                if (!TrySampleHeading(navigation.Headings, timeMs, out headingRadians))
                {
                    return false;
                }
            }

            float rightEast = Mathf.Cos(headingRadians);
            float rightNorth = -Mathf.Sin(headingRadians);
            pose = new SonarPose(position.EastMeters, position.NorthMeters, rightEast, rightNorth);
            return true;
        }

        private static bool TrySamplePosition(List<TimedPosition> positions, long timeMs, out TimedPosition position)
        {
            position = default;
            if (positions == null || positions.Count <= 0)
            {
                return false;
            }

            if (timeMs <= positions[0].TimeMs)
            {
                position = positions[0];
                return true;
            }

            int last = positions.Count - 1;
            if (timeMs >= positions[last].TimeMs)
            {
                position = positions[last];
                return true;
            }

            int upper = FindUpperPositionIndex(positions, timeMs);
            TimedPosition a = positions[Mathf.Max(0, upper - 1)];
            TimedPosition b = positions[upper];
            float t = b.TimeMs == a.TimeMs ? 0f : (timeMs - a.TimeMs) / (float)(b.TimeMs - a.TimeMs);
            position = new TimedPosition(
                timeMs,
                Mathf.Lerp(a.EastMeters, b.EastMeters, t),
                Mathf.Lerp(a.NorthMeters, b.NorthMeters, t));
            return true;
        }

        private static bool TrySampleHeading(List<TimedHeading> headings, long timeMs, out float headingRadians)
        {
            headingRadians = 0f;
            if (headings == null || headings.Count <= 0)
            {
                return false;
            }

            if (timeMs <= headings[0].TimeMs)
            {
                headingRadians = headings[0].HeadingRadians;
                return true;
            }

            int last = headings.Count - 1;
            if (timeMs >= headings[last].TimeMs)
            {
                headingRadians = headings[last].HeadingRadians;
                return true;
            }

            int upper = FindUpperHeadingIndex(headings, timeMs);
            TimedHeading a = headings[Mathf.Max(0, upper - 1)];
            TimedHeading b = headings[upper];
            float t = b.TimeMs == a.TimeMs ? 0f : (timeMs - a.TimeMs) / (float)(b.TimeMs - a.TimeMs);
            headingRadians = Mathf.LerpAngle(a.HeadingRadians * Mathf.Rad2Deg, b.HeadingRadians * Mathf.Rad2Deg, t) * Mathf.Deg2Rad;
            return true;
        }

        private static int FindUpperPositionIndex(List<TimedPosition> positions, long timeMs)
        {
            int low = 0;
            int high = positions.Count - 1;
            while (low < high)
            {
                int mid = low + ((high - low) / 2);
                if (positions[mid].TimeMs < timeMs)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        private static int FindUpperHeadingIndex(List<TimedHeading> headings, long timeMs)
        {
            int low = 0;
            int high = headings.Count - 1;
            while (low < high)
            {
                int mid = low + ((high - low) / 2);
                if (headings[mid].TimeMs < timeMs)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        private static float LatitudeToMeters(double deltaLatitudeDegrees)
        {
            return (float)(deltaLatitudeDegrees * 111320.0);
        }

        private static float LongitudeToMeters(double deltaLongitudeDegrees, double originLatitudeDegrees)
        {
            return (float)(deltaLongitudeDegrees * 111320.0 * Math.Cos(originLatitudeDegrees * Math.PI / 180.0));
        }

        private static bool TryExtractString(string json, string fieldName, out string value)
        {
            value = string.Empty;
            if (!TryFindJsonValueStart(json, fieldName, out int valueStart) || valueStart >= json.Length || json[valueStart] != '"')
            {
                return false;
            }

            int start = valueStart + 1;
            int end = json.IndexOf('"', start);
            if (end < start)
            {
                return false;
            }

            value = json.Substring(start, end - start);
            return true;
        }

        private static bool TryExtractFloat(string json, string fieldName, out float value)
        {
            value = 0f;
            if (!TryExtractDouble(json, fieldName, out double doubleValue))
            {
                return false;
            }

            value = (float)doubleValue;
            return true;
        }

        private static bool TryExtractLong(string json, string fieldName, out long value)
        {
            value = 0L;
            if (!TryExtractNumberToken(json, fieldName, out string token))
            {
                return false;
            }

            return long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryExtractDouble(string json, string fieldName, out double value)
        {
            value = 0.0;
            if (!TryExtractNumberToken(json, fieldName, out string token))
            {
                return false;
            }

            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryExtractNumberToken(string json, string fieldName, out string token)
        {
            token = string.Empty;
            if (!TryFindJsonValueStart(json, fieldName, out int start))
            {
                return false;
            }

            int end = start;
            while (end < json.Length)
            {
                char c = json[end];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                {
                    end++;
                    continue;
                }

                break;
            }

            if (end <= start)
            {
                return false;
            }

            token = json.Substring(start, end - start);
            return true;
        }

        private static bool TryFindJsonValueStart(string json, string fieldName, out int valueStart)
        {
            valueStart = 0;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
            {
                return false;
            }

            string key = "\"" + fieldName + "\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return false;
            }

            int colonIndex = json.IndexOf(':', keyIndex + key.Length);
            if (colonIndex < 0)
            {
                return false;
            }

            valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
            {
                valueStart++;
            }

            return valueStart < json.Length;
        }

        private static bool TryScanOmniscanMonoProfileBounds(
            byte[] bytes,
            FPMeshHeightmapSettings settings,
            out Bounds2D bounds,
            out int packetCount,
            out int monoProfileCount,
            out int invalidChecksumCount,
            out long sampleCount)
        {
            bounds = new Bounds2D
            {
                MinCrossTrack = float.PositiveInfinity,
                MaxCrossTrack = float.NegativeInfinity,
                MinAlongTrack = float.PositiveInfinity,
                MaxAlongTrack = float.NegativeInfinity,
                MinDepth = 0f,
                MaxDepth = 0f
            };
            packetCount = 0;
            monoProfileCount = 0;
            invalidChecksumCount = 0;
            sampleCount = 0;

            int offset = 0;
            while (offset <= bytes.Length - 10)
            {
                if (!TryReadPacketHeader(bytes, offset, out ushort payloadLength, out ushort packetId, out int packetLength))
                {
                    offset++;
                    continue;
                }

                if (!HasValidChecksum(bytes, offset, packetLength))
                {
                    invalidChecksumCount++;
                }

                packetCount++;
                if (packetId == PacketIdOmniscanMonoProfile)
                {
                    int payloadOffset = offset + 8;
                    if (TryReadOmniscanMonoProfileHeader(bytes, payloadOffset, payloadLength, settings, out OmniscanMonoProfileHeader header))
                    {
                        float startCross = header.SideSign * header.StartMeters;
                        float endCross = header.SideSign * (header.StartMeters + header.LengthMeters);
                        bounds.MinCrossTrack = Mathf.Min(bounds.MinCrossTrack, startCross, endCross);
                        bounds.MaxCrossTrack = Mathf.Max(bounds.MaxCrossTrack, startCross, endCross);
                        bounds.MinAlongTrack = Mathf.Min(bounds.MinAlongTrack, header.AlongTrack);
                        bounds.MaxAlongTrack = Mathf.Max(bounds.MaxAlongTrack, header.AlongTrack);
                        monoProfileCount++;
                        sampleCount += header.NumResults;
                    }
                }

                offset += packetLength;
            }

            if (monoProfileCount <= 0)
            {
                return false;
            }

            if (!IsFinite(bounds.MinCrossTrack) || !IsFinite(bounds.MaxCrossTrack) || Mathf.Approximately(bounds.MinCrossTrack, bounds.MaxCrossTrack))
            {
                bounds.MinCrossTrack = -0.5f;
                bounds.MaxCrossTrack = 0.5f;
            }

            if (!IsFinite(bounds.MinAlongTrack) || !IsFinite(bounds.MaxAlongTrack) || Mathf.Approximately(bounds.MinAlongTrack, bounds.MaxAlongTrack))
            {
                bounds.MinAlongTrack = 0f;
                bounds.MaxAlongTrack = 1f;
            }

            return true;
        }

        private static bool TryReadPacketHeader(byte[] bytes, int offset, out ushort payloadLength, out ushort packetId, out int packetLength)
        {
            payloadLength = 0;
            packetId = 0;
            packetLength = 0;
            if (offset > bytes.Length - 10 || bytes[offset] != 0x42 || bytes[offset + 1] != 0x52)
            {
                return false;
            }

            payloadLength = ReadUInt16(bytes, offset + 2);
            packetLength = 8 + payloadLength + 2;
            if (payloadLength > bytes.Length || offset + packetLength > bytes.Length)
            {
                return false;
            }

            packetId = ReadUInt16(bytes, offset + 4);
            return true;
        }

        private static bool TryReadOmniscanMonoProfileHeader(byte[] bytes, int payloadOffset, int payloadLength, FPMeshHeightmapSettings settings, out OmniscanMonoProfileHeader header)
        {
            header = default;
            if (payloadLength < OmniscanMonoProfileHeaderBytes || payloadOffset + OmniscanMonoProfileHeaderBytes > bytes.Length)
            {
                return false;
            }

            ushort numResults = ReadUInt16(bytes, payloadOffset + 22);
            int availableResults = (payloadLength - OmniscanMonoProfileHeaderBytes) / 2;
            if (numResults <= 0 || numResults > availableResults)
            {
                return false;
            }

            uint pingNumber = ReadUInt32(bytes, payloadOffset);
            uint startMillimeters = ReadUInt32(bytes, payloadOffset + 4);
            uint lengthMillimeters = ReadUInt32(bytes, payloadOffset + 8);
            uint timestampMs = ReadUInt32(bytes, payloadOffset + 12);
            byte channelNumber = bytes[payloadOffset + 26];
            float maxPowerDb = ReadSingle(bytes, payloadOffset + 36);
            float minPowerDb = ReadSingle(bytes, payloadOffset + 40);
            float vehicleHeadingDegrees = ReadSingle(bytes, payloadOffset + 48);
            if (!IsFinite(minPowerDb) || !IsFinite(maxPowerDb) || Mathf.Approximately(minPowerDb, maxPowerDb))
            {
                minPowerDb = 0f;
                maxPowerDb = 1f;
            }

            header = new OmniscanMonoProfileHeader(
                pingNumber * settings.SonarLogForwardStepMeters,
                startMillimeters * 0.001f,
                Mathf.Max(0.0001f, lengthMillimeters * 0.001f),
                numResults,
                Mathf.Min(minPowerDb, maxPowerDb),
                Mathf.Max(minPowerDb, maxPowerDb),
                channelNumber == 0 ? -1f : 1f,
                timestampMs,
                IsFinite(vehicleHeadingDegrees) ? vehicleHeadingDegrees * Mathf.Deg2Rad : 0f);
            return true;
        }

        private static Bounds2D CalculateBounds(List<SonarPoint> points)
        {
            var bounds = new Bounds2D
            {
                MinCrossTrack = float.PositiveInfinity,
                MaxCrossTrack = float.NegativeInfinity,
                MinAlongTrack = float.PositiveInfinity,
                MaxAlongTrack = float.NegativeInfinity,
                MinDepth = float.PositiveInfinity,
                MaxDepth = float.NegativeInfinity
            };

            for (int i = 0; i < points.Count; i++)
            {
                SonarPoint point = points[i];
                bounds.MinCrossTrack = Mathf.Min(bounds.MinCrossTrack, point.CrossTrack);
                bounds.MaxCrossTrack = Mathf.Max(bounds.MaxCrossTrack, point.CrossTrack);
                bounds.MinAlongTrack = Mathf.Min(bounds.MinAlongTrack, point.AlongTrack);
                bounds.MaxAlongTrack = Mathf.Max(bounds.MaxAlongTrack, point.AlongTrack);
                bounds.MinDepth = Mathf.Min(bounds.MinDepth, point.Depth);
                bounds.MaxDepth = Mathf.Max(bounds.MaxDepth, point.Depth);
            }

            if (!IsFinite(bounds.MinCrossTrack) || !IsFinite(bounds.MaxCrossTrack) || Mathf.Approximately(bounds.MinCrossTrack, bounds.MaxCrossTrack))
            {
                bounds.MinCrossTrack = -0.5f;
                bounds.MaxCrossTrack = 0.5f;
            }

            if (!IsFinite(bounds.MinAlongTrack) || !IsFinite(bounds.MaxAlongTrack) || Mathf.Approximately(bounds.MinAlongTrack, bounds.MaxAlongTrack))
            {
                bounds.MinAlongTrack = 0f;
                bounds.MaxAlongTrack = 1f;
            }

            if (!IsFinite(bounds.MinDepth) || !IsFinite(bounds.MaxDepth))
            {
                bounds.MinDepth = 0f;
                bounds.MaxDepth = 0f;
            }

            return bounds;
        }

        private static bool HasValidChecksum(byte[] bytes, int offset, int packetLength)
        {
            ushort expected = ReadUInt16(bytes, offset + packetLength - 2);
            uint sum = 0;
            for (int i = 0; i < packetLength - 2; i++)
            {
                sum += bytes[offset + i];
            }

            return (ushort)(sum & 0xffff) == expected;
        }

        private static bool TryResolvePath(string sourcePath, out string resolvedPath)
        {
            resolvedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            string normalized = sourcePath.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized) && File.Exists(normalized))
            {
                resolvedPath = normalized;
                return true;
            }

            string projectRelative = Path.Combine(Directory.GetCurrentDirectory(), normalized);
            if (File.Exists(projectRelative))
            {
                resolvedPath = projectRelative;
                return true;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string assetRelative = Path.Combine(projectRoot, normalized);
                if (File.Exists(assetRelative))
                {
                    resolvedPath = assetRelative;
                    return true;
                }
            }

            return false;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
        }

        private static float ReadSingle(byte[] bytes, int offset)
        {
            return BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32(bytes, offset)));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float FirstFinite(float a, float b, float c, float d)
        {
            if (IsFinite(a))
            {
                return a;
            }

            if (IsFinite(b))
            {
                return b;
            }

            if (IsFinite(c))
            {
                return c;
            }

            return IsFinite(d) ? d : float.NaN;
        }

        private readonly struct SonarPoint
        {
            public readonly float CrossTrack;
            public readonly float AlongTrack;
            public readonly float Depth;

            public SonarPoint(float crossTrack, float alongTrack, float depth)
            {
                CrossTrack = crossTrack;
                AlongTrack = alongTrack;
                Depth = depth;
            }
        }

        private readonly struct OmniscanMonoProfileHeader
        {
            public readonly float AlongTrack;
            public readonly float StartMeters;
            public readonly float LengthMeters;
            public readonly int NumResults;
            public readonly float MinPowerDb;
            public readonly float MaxPowerDb;
            public readonly float SideSign;
            public readonly long TimestampMs;
            public readonly float VehicleHeadingRadians;

            public OmniscanMonoProfileHeader(
                float alongTrack,
                float startMeters,
                float lengthMeters,
                int numResults,
                float minPowerDb,
                float maxPowerDb,
                float sideSign,
                long timestampMs,
                float vehicleHeadingRadians)
            {
                AlongTrack = alongTrack;
                StartMeters = startMeters;
                LengthMeters = lengthMeters;
                NumResults = numResults;
                MinPowerDb = minPowerDb;
                MaxPowerDb = maxPowerDb;
                SideSign = sideSign;
                TimestampMs = timestampMs;
                VehicleHeadingRadians = vehicleHeadingRadians;
            }
        }

        private sealed class NavigationData
        {
            public readonly List<TimedPosition> Positions = new List<TimedPosition>();
            public readonly List<TimedHeading> Headings = new List<TimedHeading>();
        }

        private readonly struct TimedPosition
        {
            public readonly long TimeMs;
            public readonly float EastMeters;
            public readonly float NorthMeters;

            public TimedPosition(long timeMs, float eastMeters, float northMeters)
            {
                TimeMs = timeMs;
                EastMeters = eastMeters;
                NorthMeters = northMeters;
            }
        }

        private readonly struct TimedHeading
        {
            public readonly long TimeMs;
            public readonly float HeadingRadians;

            public TimedHeading(long timeMs, float headingRadians)
            {
                TimeMs = timeMs;
                HeadingRadians = headingRadians;
            }
        }

        private readonly struct SonarPose
        {
            public readonly float EastMeters;
            public readonly float NorthMeters;
            public readonly float RightEast;
            public readonly float RightNorth;

            public SonarPose(float eastMeters, float northMeters, float rightEast, float rightNorth)
            {
                EastMeters = eastMeters;
                NorthMeters = northMeters;
                RightEast = rightEast;
                RightNorth = rightNorth;
            }
        }

        private struct Bounds2D
        {
            public float MinCrossTrack;
            public float MaxCrossTrack;
            public float MinAlongTrack;
            public float MaxAlongTrack;
            public float MinDepth;
            public float MaxDepth;
        }
    }
}
