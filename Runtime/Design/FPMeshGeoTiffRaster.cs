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
    using UnityEngine;

    /// <summary>
    /// Minimal classic TIFF elevation raster reader for strip-based GeoTIFF DEM files.
    /// </summary>
    public sealed class FPMeshGeoTiffRaster
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float[] ElevationValues { get; private set; }
        public float? NoDataValue { get; private set; }
        public double[] ModelPixelScale { get; private set; }
        public double[] ModelTiepoint { get; private set; }
        public double[] ModelTransformation { get; private set; }
        public uint BitsPerSample { get; private set; }
        public ushort SampleFormat { get; private set; }
        public uint SamplesPerPixel { get; private set; }
        public ushort Compression { get; private set; }
        public ushort Predictor { get; private set; }
        public ushort PlanarConfiguration { get; private set; }
        public bool IsTiled { get; private set; }
        public uint RowsPerStrip { get; private set; }
        public uint TileWidth { get; private set; }
        public uint TileLength { get; private set; }
        public float MinValue { get; private set; }
        public float MaxValue { get; private set; }
        public int ValidSampleCount { get; private set; }
        public int NoDataSampleCount { get; private set; }
        public double? GdalScale { get; private set; }
        public double? GdalOffset { get; private set; }
        public string GdalMetadata { get; private set; }

        private const ushort TiffMagic = 42;
        private const ushort CompressionNone = 1;
        private const ushort CompressionLzw = 5;
        private const ushort PlanarChunky = 1;
        private const ushort PlanarSeparate = 2;
        private const ushort PredictorNone = 1;
        private const ushort PredictorHorizontal = 2;
        private const ushort SampleFormatUnsignedInt = 1;
        private const ushort SampleFormatSignedInt = 2;
        private const ushort SampleFormatFloat = 3;

        private const ushort TagImageWidth = 256;
        private const ushort TagImageLength = 257;
        private const ushort TagBitsPerSample = 258;
        private const ushort TagCompression = 259;
        private const ushort TagStripOffsets = 273;
        private const ushort TagSamplesPerPixel = 277;
        private const ushort TagRowsPerStrip = 278;
        private const ushort TagStripByteCounts = 279;
        private const ushort TagPlanarConfiguration = 284;
        private const ushort TagPredictor = 317;
        private const ushort TagTileWidth = 322;
        private const ushort TagTileLength = 323;
        private const ushort TagTileOffsets = 324;
        private const ushort TagTileByteCounts = 325;
        private const ushort TagSampleFormat = 339;
        private const ushort TagModelPixelScale = 33550;
        private const ushort TagModelTiepoint = 33922;
        private const ushort TagModelTransformation = 34264;
        private const ushort TagGdalMetadata = 42112;
        private const ushort TagGdalNoData = 42113;

        private FPMeshGeoTiffRaster()
        {
        }

        public static bool TryLoad(string sourcePath, out FPMeshGeoTiffRaster raster, out string error)
        {
            raster = null;
            error = string.Empty;

            if (!TryResolvePath(sourcePath, out string resolvedPath))
            {
                error = "The GeoTIFF file path could not be resolved.";
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(resolvedPath);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            return TryParse(bytes, out raster, out error);
        }

        public bool TryGetRealSizeMeters(FPMeshGeoTiffCoordinateSystem coordinateSystem, float projectedUnitsToMeters, out float widthMeters, out float lengthMeters, out string error)
        {
            widthMeters = 0f;
            lengthMeters = 0f;
            error = string.Empty;

            if (!TryGetPixelScale(out double pixelScaleX, out double pixelScaleY, out error))
            {
                return false;
            }

            double widthInSourceUnits = Math.Abs(pixelScaleX) * Width;
            double lengthInSourceUnits = Math.Abs(pixelScaleY) * Height;

            if (coordinateSystem == FPMeshGeoTiffCoordinateSystem.WGS84)
            {
                double latitude = TryGetCenterLatitude(pixelScaleY, out double centerLatitude)
                    ? centerLatitude
                    : 0.0;

                double metersPerDegreeLatitude = MetersPerDegreeLatitude(latitude);
                double metersPerDegreeLongitude = MetersPerDegreeLongitude(latitude);
                widthMeters = (float)(widthInSourceUnits * metersPerDegreeLongitude);
                lengthMeters = (float)(lengthInSourceUnits * metersPerDegreeLatitude);
            }
            else
            {
                double unitScale = Math.Max(0.000001, projectedUnitsToMeters);
                widthMeters = (float)(widthInSourceUnits * unitScale);
                lengthMeters = (float)(lengthInSourceUnits * unitScale);
            }

            if (widthMeters <= 0f || lengthMeters <= 0f || float.IsNaN(widthMeters) || float.IsNaN(lengthMeters))
            {
                error = "GeoTIFF spatial scale produced an invalid width or length.";
                return false;
            }

            return true;
        }

        public float SampleBilinear(float u, float v, bool northToPositiveZ)
        {
            if (ElevationValues == null || ElevationValues.Length == 0 || Width <= 0 || Height <= 0)
            {
                return float.NaN;
            }

            float x = Mathf.Clamp01(u) * (Width - 1);
            float y = (northToPositiveZ ? 1f - Mathf.Clamp01(v) : Mathf.Clamp01(v)) * (Height - 1);

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

            float bottom = Mathf.Lerp(h00, h10, tx);
            float top = Mathf.Lerp(h01, h11, tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        private float GetValue(int x, int y)
        {
            return ElevationValues[x + (y * Width)];
        }

        private static bool TryParse(byte[] bytes, out FPMeshGeoTiffRaster raster, out string error)
        {
            raster = null;
            error = string.Empty;

            if (bytes == null || bytes.Length < 8)
            {
                error = "File is too small to be a TIFF.";
                return false;
            }

            bool littleEndian;
            if (bytes[0] == 'I' && bytes[1] == 'I')
            {
                littleEndian = true;
            }
            else if (bytes[0] == 'M' && bytes[1] == 'M')
            {
                littleEndian = false;
            }
            else
            {
                error = "Missing TIFF byte-order header.";
                return false;
            }

            if (ReadUInt16(bytes, 2, littleEndian) != TiffMagic)
            {
                error = "BigTIFF or non-classic TIFF files are not supported yet.";
                return false;
            }

            uint ifdOffset = ReadUInt32(bytes, 4, littleEndian);
            if (!TryReadIfd(bytes, ifdOffset, littleEndian, out Dictionary<ushort, TiffEntry> entries, out error))
            {
                return false;
            }

            if (!TryGetUInt(entries, TagImageWidth, bytes, littleEndian, out uint width)
                || !TryGetUInt(entries, TagImageLength, bytes, littleEndian, out uint height))
            {
                error = "Missing TIFF image width or height.";
                return false;
            }

            ushort compression = TryGetUInt(entries, TagCompression, bytes, littleEndian, out uint compressionValue)
                ? (ushort)compressionValue
                : CompressionNone;
            if (compression != CompressionNone && compression != CompressionLzw)
            {
                error = $"Unsupported TIFF compression {compression}. Import an uncompressed DEM or add decoder support.";
                return false;
            }

            uint[] stripOffsets = Array.Empty<uint>();
            uint[] stripByteCounts = Array.Empty<uint>();
            uint[] tileOffsets = Array.Empty<uint>();
            uint[] tileByteCounts = Array.Empty<uint>();
            bool hasStrips = TryGetUIntArray(entries, TagStripOffsets, bytes, littleEndian, out stripOffsets)
                && TryGetUIntArray(entries, TagStripByteCounts, bytes, littleEndian, out stripByteCounts);
            bool hasTiles = TryGetUIntArray(entries, TagTileOffsets, bytes, littleEndian, out tileOffsets)
                && TryGetUIntArray(entries, TagTileByteCounts, bytes, littleEndian, out tileByteCounts);
            if (!hasStrips && !hasTiles)
            {
                error = "Missing strip or tile data offsets.";
                return false;
            }

            uint rowsPerStrip = TryGetUInt(entries, TagRowsPerStrip, bytes, littleEndian, out uint rowsValue)
                ? rowsValue
                : height;
            uint samplesPerPixel = TryGetUInt(entries, TagSamplesPerPixel, bytes, littleEndian, out uint samplesValue)
                ? samplesValue
                : 1;
            ushort planarConfiguration = TryGetUInt(entries, TagPlanarConfiguration, bytes, littleEndian, out uint planarValue)
                ? (ushort)planarValue
                : PlanarChunky;
            ushort predictor = TryGetUInt(entries, TagPredictor, bytes, littleEndian, out uint predictorValue)
                ? (ushort)predictorValue
                : PredictorNone;

            if (!TryGetUIntArray(entries, TagBitsPerSample, bytes, littleEndian, out uint[] bitsPerSampleValues)
                || bitsPerSampleValues.Length == 0)
            {
                error = "Missing TIFF bits-per-sample information.";
                return false;
            }

            uint bitsPerSample = bitsPerSampleValues[0];
            if (bitsPerSample % 8 != 0)
            {
                error = $"Unsupported packed sample size: {bitsPerSample} bits.";
                return false;
            }

            ushort sampleFormat = TryGetUIntArray(entries, TagSampleFormat, bytes, littleEndian, out uint[] sampleFormatValues)
                && sampleFormatValues.Length > 0
                ? (ushort)sampleFormatValues[0]
                : SampleFormatUnsignedInt;

            if (sampleFormat != SampleFormatUnsignedInt
                && sampleFormat != SampleFormatSignedInt
                && sampleFormat != SampleFormatFloat)
            {
                error = $"Unsupported TIFF sample format {sampleFormat}.";
                return false;
            }

            FPMeshGeoTiffRaster parsed = new FPMeshGeoTiffRaster
            {
                Width = checked((int)width),
                Height = checked((int)height),
                ElevationValues = new float[checked((int)(width * height))],
                BitsPerSample = bitsPerSample,
                SampleFormat = sampleFormat,
                SamplesPerPixel = samplesPerPixel,
                Compression = compression,
                Predictor = predictor,
                PlanarConfiguration = planarConfiguration,
                IsTiled = hasTiles,
                RowsPerStrip = rowsPerStrip,
                MinValue = float.NaN,
                MaxValue = float.NaN,
                GdalMetadata = string.Empty
            };

            if (TryGetAscii(entries, TagGdalNoData, bytes, out string noDataText)
                && float.TryParse(noDataText.TrimEnd('\0'), NumberStyles.Float, CultureInfo.InvariantCulture, out float noData))
            {
                parsed.NoDataValue = noData;
            }

            if (TryGetDoubleArray(entries, TagModelPixelScale, bytes, littleEndian, out double[] modelPixelScale))
            {
                parsed.ModelPixelScale = modelPixelScale;
            }

            if (TryGetDoubleArray(entries, TagModelTiepoint, bytes, littleEndian, out double[] modelTiepoint))
            {
                parsed.ModelTiepoint = modelTiepoint;
            }

            if (TryGetDoubleArray(entries, TagModelTransformation, bytes, littleEndian, out double[] modelTransformation))
            {
                parsed.ModelTransformation = modelTransformation;
            }

            if (TryGetAscii(entries, TagGdalMetadata, bytes, out string gdalMetadata))
            {
                parsed.GdalMetadata = gdalMetadata.TrimEnd('\0');
                if (TryExtractGdalMetadataValue(parsed.GdalMetadata, "Scale", out double gdalScale))
                {
                    parsed.GdalScale = gdalScale;
                }

                if (TryExtractGdalMetadataValue(parsed.GdalMetadata, "Offset", out double gdalOffset))
                {
                    parsed.GdalOffset = gdalOffset;
                }
            }

            if (hasTiles)
            {
                if (TryGetUInt(entries, TagTileWidth, bytes, littleEndian, out uint tileWidth))
                {
                    parsed.TileWidth = tileWidth;
                }

                if (TryGetUInt(entries, TagTileLength, bytes, littleEndian, out uint tileLength))
                {
                    parsed.TileLength = tileLength;
                }
            }

            bool readElevation = hasTiles
                ? TryReadTiledElevationValues(bytes, littleEndian, parsed, entries, tileOffsets, tileByteCounts, samplesPerPixel, planarConfiguration, compression, predictor, bitsPerSample, sampleFormat, out error)
                : TryReadStripElevationValues(bytes, littleEndian, parsed, stripOffsets, stripByteCounts, rowsPerStrip, samplesPerPixel, planarConfiguration, compression, predictor, bitsPerSample, sampleFormat, out error);
            if (!readElevation)
            {
                return false;
            }

            parsed.ComputeValueStats();
            raster = parsed;
            return true;
        }

        private void ComputeValueStats()
        {
            ValidSampleCount = 0;
            NoDataSampleCount = 0;
            MinValue = float.NaN;
            MaxValue = float.NaN;

            if (ElevationValues == null || ElevationValues.Length == 0)
            {
                return;
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < ElevationValues.Length; i++)
            {
                float value = ElevationValues[i];
                if (!IsFinite(value))
                {
                    NoDataSampleCount++;
                    continue;
                }

                ValidSampleCount++;
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }

            if (ValidSampleCount > 0)
            {
                MinValue = min;
                MaxValue = max;
            }
        }

        public bool TryGetPixelScale(out double pixelScaleX, out double pixelScaleY, out string error)
        {
            pixelScaleX = 0.0;
            pixelScaleY = 0.0;
            error = string.Empty;

            if (ModelPixelScale != null && ModelPixelScale.Length >= 2)
            {
                pixelScaleX = ModelPixelScale[0];
                pixelScaleY = ModelPixelScale[1];
                return true;
            }

            if (ModelTransformation != null && ModelTransformation.Length >= 16)
            {
                pixelScaleX = ModelTransformation[0];
                pixelScaleY = ModelTransformation[5];
                return true;
            }

            error = "GeoTIFF does not contain ModelPixelScale or ModelTransformation metadata.";
            return false;
        }

        private bool TryGetCenterLatitude(double pixelScaleY, out double centerLatitude)
        {
            centerLatitude = 0.0;

            if (ModelTiepoint != null && ModelTiepoint.Length >= 6)
            {
                double rasterY = ModelTiepoint[1];
                double modelY = ModelTiepoint[4];
                double topLatitude = modelY - (rasterY * pixelScaleY);
                centerLatitude = topLatitude - ((Height * pixelScaleY) * 0.5);
                return true;
            }

            if (ModelTransformation != null && ModelTransformation.Length >= 16)
            {
                double originY = ModelTransformation[3];
                centerLatitude = originY + ((Height * ModelTransformation[5]) * 0.5);
                return true;
            }

            return false;
        }

        private static double MetersPerDegreeLatitude(double latitudeDegrees)
        {
            double latitudeRadians = latitudeDegrees * Mathf.Deg2Rad;
            return 111132.92
                - (559.82 * Math.Cos(2.0 * latitudeRadians))
                + (1.175 * Math.Cos(4.0 * latitudeRadians))
                - (0.0023 * Math.Cos(6.0 * latitudeRadians));
        }

        private static double MetersPerDegreeLongitude(double latitudeDegrees)
        {
            double latitudeRadians = latitudeDegrees * Mathf.Deg2Rad;
            return (111412.84 * Math.Cos(latitudeRadians))
                - (93.5 * Math.Cos(3.0 * latitudeRadians))
                + (0.118 * Math.Cos(5.0 * latitudeRadians));
        }

        private static bool TryReadStripElevationValues(
            byte[] bytes,
            bool littleEndian,
            FPMeshGeoTiffRaster raster,
            uint[] stripOffsets,
            uint[] stripByteCounts,
            uint rowsPerStrip,
            uint samplesPerPixel,
            ushort planarConfiguration,
            ushort compression,
            ushort predictor,
            uint bitsPerSample,
            ushort sampleFormat,
            out string error)
        {
            error = string.Empty;
            int bytesPerSample = checked((int)(bitsPerSample / 8));
            int width = raster.Width;
            int height = raster.Height;
            int rowsPerStripInt = Math.Max(1, checked((int)rowsPerStrip));
            int stripsPerPlane = Mathf.CeilToInt(height / (float)rowsPerStripInt);

            if (planarConfiguration != PlanarChunky && planarConfiguration != PlanarSeparate)
            {
                error = $"Unsupported planar configuration {planarConfiguration}.";
                return false;
            }

            int stripsToRead = planarConfiguration == PlanarSeparate
                ? Mathf.Min(stripsPerPlane, stripOffsets.Length)
                : stripOffsets.Length;

            for (int stripIndex = 0; stripIndex < stripsToRead; stripIndex++)
            {
                int rowStart = stripIndex * rowsPerStripInt;
                if (rowStart >= height)
                {
                    break;
                }

                int rowsInStrip = Math.Min(rowsPerStripInt, height - rowStart);
                int stripOffset = checked((int)stripOffsets[stripIndex]);
                int stripByteCount = checked((int)stripByteCounts[Mathf.Min(stripIndex, stripByteCounts.Length - 1)]);
                if (stripOffset < 0 || stripOffset + stripByteCount > bytes.Length)
                {
                    error = "A TIFF strip points outside the file.";
                    return false;
                }

                int expectedStripBytes = CalculateExpectedStripBytes(width, rowsInStrip, samplesPerPixel, planarConfiguration, bytesPerSample);
                if (!TryGetDecodedStripBytes(bytes, stripOffset, stripByteCount, expectedStripBytes, compression, out byte[] stripBytes, out error))
                {
                    return false;
                }

                if (!TryApplyPredictor(stripBytes, width, rowsInStrip, samplesPerPixel, planarConfiguration, bytesPerSample, bitsPerSample, predictor, littleEndian, out error))
                {
                    return false;
                }

                for (int localRow = 0; localRow < rowsInStrip; localRow++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int sampleIndex = planarConfiguration == PlanarSeparate
                            ? (localRow * width) + x
                            : (((localRow * width) + x) * checked((int)samplesPerPixel));
                        int sampleOffset = sampleIndex * bytesPerSample;
                        if (sampleOffset + bytesPerSample > stripBytes.Length)
                        {
                            error = "A TIFF strip ended before all raster samples were read.";
                            return false;
                        }

                        float elevation = ReadSample(stripBytes, sampleOffset, littleEndian, bytesPerSample, sampleFormat);
                        if (raster.NoDataValue.HasValue && Approximately(elevation, raster.NoDataValue.Value))
                        {
                            elevation = float.NaN;
                        }

                        raster.ElevationValues[x + ((rowStart + localRow) * width)] = elevation;
                    }
                }
            }

            return true;
        }

        private static bool TryReadTiledElevationValues(
            byte[] bytes,
            bool littleEndian,
            FPMeshGeoTiffRaster raster,
            Dictionary<ushort, TiffEntry> entries,
            uint[] tileOffsets,
            uint[] tileByteCounts,
            uint samplesPerPixel,
            ushort planarConfiguration,
            ushort compression,
            ushort predictor,
            uint bitsPerSample,
            ushort sampleFormat,
            out string error)
        {
            error = string.Empty;

            if (!TryGetUInt(entries, TagTileWidth, bytes, littleEndian, out uint tileWidthValue)
                || !TryGetUInt(entries, TagTileLength, bytes, littleEndian, out uint tileLengthValue))
            {
                error = "Tiled TIFF is missing tile width or tile length.";
                return false;
            }

            if (planarConfiguration != PlanarChunky && planarConfiguration != PlanarSeparate)
            {
                error = $"Unsupported planar configuration {planarConfiguration}.";
                return false;
            }

            int bytesPerSample = checked((int)(bitsPerSample / 8));
            int width = raster.Width;
            int height = raster.Height;
            int tileWidth = Math.Max(1, checked((int)tileWidthValue));
            int tileLength = Math.Max(1, checked((int)tileLengthValue));
            int tilesAcross = Mathf.CeilToInt(width / (float)tileWidth);
            int tilesDown = Mathf.CeilToInt(height / (float)tileLength);
            int tilesPerPlane = checked(tilesAcross * tilesDown);
            int tilesToRead = planarConfiguration == PlanarSeparate
                ? Mathf.Min(tilesPerPlane, tileOffsets.Length)
                : Mathf.Min(tilesPerPlane, tileOffsets.Length);

            for (int tileIndex = 0; tileIndex < tilesToRead; tileIndex++)
            {
                int tileX = tileIndex % tilesAcross;
                int tileY = tileIndex / tilesAcross;
                int xStart = tileX * tileWidth;
                int yStart = tileY * tileLength;
                int validWidth = Math.Min(tileWidth, width - xStart);
                int validHeight = Math.Min(tileLength, height - yStart);
                if (validWidth <= 0 || validHeight <= 0)
                {
                    continue;
                }

                int tileOffset = checked((int)tileOffsets[tileIndex]);
                int tileByteCount = checked((int)tileByteCounts[Mathf.Min(tileIndex, tileByteCounts.Length - 1)]);
                if (tileOffset < 0 || tileOffset + tileByteCount > bytes.Length)
                {
                    error = "A TIFF tile points outside the file.";
                    return false;
                }

                int expectedTileBytes = CalculateExpectedStripBytes(tileWidth, tileLength, samplesPerPixel, planarConfiguration, bytesPerSample);
                if (!TryGetDecodedStripBytes(bytes, tileOffset, tileByteCount, expectedTileBytes, compression, out byte[] tileBytes, out error))
                {
                    return false;
                }

                if (!TryApplyPredictor(tileBytes, tileWidth, tileLength, samplesPerPixel, planarConfiguration, bytesPerSample, bitsPerSample, predictor, littleEndian, out error))
                {
                    return false;
                }

                for (int localRow = 0; localRow < validHeight; localRow++)
                {
                    for (int localX = 0; localX < validWidth; localX++)
                    {
                        int sampleIndex = planarConfiguration == PlanarSeparate
                            ? (localRow * tileWidth) + localX
                            : (((localRow * tileWidth) + localX) * checked((int)samplesPerPixel));
                        int sampleOffset = sampleIndex * bytesPerSample;
                        if (sampleOffset + bytesPerSample > tileBytes.Length)
                        {
                            error = "A TIFF tile ended before all raster samples were read.";
                            return false;
                        }

                        float elevation = ReadSample(tileBytes, sampleOffset, littleEndian, bytesPerSample, sampleFormat);
                        if (raster.NoDataValue.HasValue && Approximately(elevation, raster.NoDataValue.Value))
                        {
                            elevation = float.NaN;
                        }

                        raster.ElevationValues[(xStart + localX) + ((yStart + localRow) * width)] = elevation;
                    }
                }
            }

            return true;
        }

        private static int CalculateExpectedStripBytes(int width, int rowsInStrip, uint samplesPerPixel, ushort planarConfiguration, int bytesPerSample)
        {
            int sampleCount = checked(width * rowsInStrip);
            if (planarConfiguration == PlanarChunky)
            {
                sampleCount = checked(sampleCount * (int)samplesPerPixel);
            }

            return checked(sampleCount * bytesPerSample);
        }

        private static bool TryGetDecodedStripBytes(byte[] bytes, int stripOffset, int stripByteCount, int expectedStripBytes, ushort compression, out byte[] stripBytes, out string error)
        {
            error = string.Empty;
            stripBytes = new byte[stripByteCount];
            Buffer.BlockCopy(bytes, stripOffset, stripBytes, 0, stripByteCount);

            if (compression == CompressionNone)
            {
                return true;
            }

            if (compression == CompressionLzw)
            {
                return TryDecodeLzw(stripBytes, expectedStripBytes, out stripBytes, out error);
            }

            error = $"Unsupported TIFF compression {compression}.";
            return false;
        }

        private static bool TryApplyPredictor(
            byte[] stripBytes,
            int width,
            int rowsInStrip,
            uint samplesPerPixel,
            ushort planarConfiguration,
            int bytesPerSample,
            uint bitsPerSample,
            ushort predictor,
            bool littleEndian,
            out string error)
        {
            error = string.Empty;
            if (predictor == PredictorNone)
            {
                return true;
            }

            if (predictor != PredictorHorizontal)
            {
                error = $"Unsupported TIFF predictor {predictor}.";
                return false;
            }

            if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 32 && bitsPerSample != 64)
            {
                error = $"Unsupported horizontal predictor sample size: {bitsPerSample} bits.";
                return false;
            }

            int componentsPerRow = planarConfiguration == PlanarSeparate ? 1 : Math.Max(1, checked((int)samplesPerPixel));
            int rowStride = checked(width * componentsPerRow * bytesPerSample);
            ulong mask = bitsPerSample == 64 ? ulong.MaxValue : (1UL << checked((int)bitsPerSample)) - 1UL;

            for (int y = 0; y < rowsInStrip; y++)
            {
                int rowOffset = y * rowStride;
                if (rowOffset + rowStride > stripBytes.Length)
                {
                    error = "Predictor row exceeded decoded strip data.";
                    return false;
                }

                for (int x = 1; x < width; x++)
                {
                    for (int component = 0; component < componentsPerRow; component++)
                    {
                        int currentOffset = rowOffset + (((x * componentsPerRow) + component) * bytesPerSample);
                        int previousOffset = currentOffset - (componentsPerRow * bytesPerSample);
                        ulong current = ReadUnsignedInteger(stripBytes, currentOffset, littleEndian, bytesPerSample);
                        ulong previous = ReadUnsignedInteger(stripBytes, previousOffset, littleEndian, bytesPerSample);
                        WriteUnsignedInteger(stripBytes, currentOffset, littleEndian, bytesPerSample, (current + previous) & mask);
                    }
                }
            }

            return true;
        }

        private static bool TryDecodeLzw(byte[] input, int expectedMinBytes, out byte[] output, out string error)
        {
            output = Array.Empty<byte>();
            error = string.Empty;

            const int clearCode = 256;
            const int endOfInformationCode = 257;
            const int firstAvailableCode = 258;
            const int maxCode = 4095;

            var reader = new TiffLzwBitReader(input);
            var decoded = new List<byte>(Math.Max(expectedMinBytes, input.Length * 2));
            var table = new Dictionary<int, byte[]>(maxCode + 1);
            int codeSize = 9;
            int nextCode = firstAvailableCode;
            int previousCode = -1;

            void ResetTable()
            {
                table.Clear();
                codeSize = 9;
                nextCode = firstAvailableCode;
                previousCode = -1;
            }

            ResetTable();

            while (reader.TryReadBits(codeSize, out int code))
            {
                if (code == clearCode)
                {
                    ResetTable();
                    continue;
                }

                if (code == endOfInformationCode)
                {
                    break;
                }

                if (!TryResolveLzwCode(code, table, previousCode, nextCode, out byte[] entry))
                {
                    error = $"Invalid TIFF LZW code {code}.";
                    return false;
                }

                decoded.AddRange(entry);

                if (previousCode >= 0)
                {
                    if (!TryResolveLzwCode(previousCode, table, -1, nextCode, out byte[] previousEntry))
                    {
                        error = $"Invalid previous TIFF LZW code {previousCode}.";
                        return false;
                    }

                    if (nextCode <= maxCode)
                    {
                        byte[] nextEntry = new byte[previousEntry.Length + 1];
                        Buffer.BlockCopy(previousEntry, 0, nextEntry, 0, previousEntry.Length);
                        nextEntry[nextEntry.Length - 1] = entry[0];
                        table[nextCode] = nextEntry;
                        nextCode++;

                        if (nextCode >= ((1 << codeSize) - 1) && codeSize < 12)
                        {
                            codeSize++;
                        }
                    }
                }

                previousCode = code;
            }

            output = decoded.ToArray();
            if (expectedMinBytes > 0 && output.Length < expectedMinBytes)
            {
                error = $"TIFF LZW strip decoded to {output.Length} bytes, expected at least {expectedMinBytes}.";
                return false;
            }

            return true;
        }

        private static bool TryResolveLzwCode(int code, Dictionary<int, byte[]> table, int previousCode, int nextCode, out byte[] entry)
        {
            if (code >= 0 && code < 256)
            {
                entry = new[] { (byte)code };
                return true;
            }

            if (table.TryGetValue(code, out entry))
            {
                return true;
            }

            if (code == nextCode && previousCode >= 0 && TryResolveLzwCode(previousCode, table, -1, nextCode, out byte[] previousEntry))
            {
                entry = new byte[previousEntry.Length + 1];
                Buffer.BlockCopy(previousEntry, 0, entry, 0, previousEntry.Length);
                entry[entry.Length - 1] = previousEntry[0];
                return true;
            }

            entry = Array.Empty<byte>();
            return false;
        }

        private static float ReadSample(byte[] bytes, int offset, bool littleEndian, int bytesPerSample, ushort sampleFormat)
        {
            switch (sampleFormat)
            {
                case SampleFormatSignedInt:
                    return ReadSignedInteger(bytes, offset, littleEndian, bytesPerSample);
                case SampleFormatFloat:
                    return bytesPerSample == 8
                        ? (float)ReadDouble(bytes, offset, littleEndian)
                        : ReadSingle(bytes, offset, littleEndian);
                case SampleFormatUnsignedInt:
                default:
                    return ReadUnsignedInteger(bytes, offset, littleEndian, bytesPerSample);
            }
        }

        private static long ReadSignedInteger(byte[] bytes, int offset, bool littleEndian, int bytesPerSample)
        {
            switch (bytesPerSample)
            {
                case 1:
                    return unchecked((sbyte)bytes[offset]);
                case 2:
                    return unchecked((short)ReadUInt16(bytes, offset, littleEndian));
                case 4:
                    return unchecked((int)ReadUInt32(bytes, offset, littleEndian));
                case 8:
                    return unchecked((long)ReadUInt64(bytes, offset, littleEndian));
                default:
                    throw new NotSupportedException($"Unsupported integer sample size: {bytesPerSample} bytes.");
            }
        }

        private static ulong ReadUnsignedInteger(byte[] bytes, int offset, bool littleEndian, int bytesPerSample)
        {
            switch (bytesPerSample)
            {
                case 1:
                    return bytes[offset];
                case 2:
                    return ReadUInt16(bytes, offset, littleEndian);
                case 4:
                    return ReadUInt32(bytes, offset, littleEndian);
                case 8:
                    return ReadUInt64(bytes, offset, littleEndian);
                default:
                    throw new NotSupportedException($"Unsupported integer sample size: {bytesPerSample} bytes.");
            }
        }

        private static void WriteUnsignedInteger(byte[] bytes, int offset, bool littleEndian, int bytesPerSample, ulong value)
        {
            for (int i = 0; i < bytesPerSample; i++)
            {
                int shift = littleEndian ? i * 8 : (bytesPerSample - 1 - i) * 8;
                bytes[offset + i] = (byte)((value >> shift) & 0xffUL);
            }
        }

        private static bool TryReadIfd(byte[] bytes, uint ifdOffset, bool littleEndian, out Dictionary<ushort, TiffEntry> entries, out string error)
        {
            entries = new Dictionary<ushort, TiffEntry>();
            error = string.Empty;

            if (ifdOffset > bytes.Length - 2)
            {
                error = "The first TIFF image directory is outside the file.";
                return false;
            }

            int offset = checked((int)ifdOffset);
            ushort entryCount = ReadUInt16(bytes, offset, littleEndian);
            offset += 2;

            int directoryBytes = entryCount * 12;
            if (offset + directoryBytes > bytes.Length)
            {
                error = "The TIFF image directory is truncated.";
                return false;
            }

            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = offset + (i * 12);
                ushort tag = ReadUInt16(bytes, entryOffset, littleEndian);
                ushort type = ReadUInt16(bytes, entryOffset + 2, littleEndian);
                uint count = ReadUInt32(bytes, entryOffset + 4, littleEndian);
                uint valueOffset = ReadUInt32(bytes, entryOffset + 8, littleEndian);
                entries[tag] = new TiffEntry(type, count, valueOffset, entryOffset + 8);
            }

            return true;
        }

        private static bool TryGetUInt(Dictionary<ushort, TiffEntry> entries, ushort tag, byte[] bytes, bool littleEndian, out uint value)
        {
            value = 0;
            if (!TryGetUIntArray(entries, tag, bytes, littleEndian, out uint[] values) || values.Length == 0)
            {
                return false;
            }

            value = values[0];
            return true;
        }

        private static bool TryExtractGdalMetadataValue(string metadata, string itemName, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(metadata) || string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            int searchIndex = 0;
            while (searchIndex < metadata.Length)
            {
                int itemIndex = metadata.IndexOf("<Item", searchIndex, StringComparison.OrdinalIgnoreCase);
                if (itemIndex < 0)
                {
                    return false;
                }

                int itemEnd = metadata.IndexOf("</Item>", itemIndex, StringComparison.OrdinalIgnoreCase);
                if (itemEnd < 0)
                {
                    return false;
                }

                int openEnd = metadata.IndexOf('>', itemIndex);
                if (openEnd < 0 || openEnd > itemEnd)
                {
                    return false;
                }

                string header = metadata.Substring(itemIndex, openEnd - itemIndex);
                if (header.IndexOf($"name=\"{itemName}\"", StringComparison.OrdinalIgnoreCase) >= 0
                    || header.IndexOf($"name='{itemName}'", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string valueText = metadata.Substring(openEnd + 1, itemEnd - openEnd - 1).Trim();
                    return double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                }

                searchIndex = itemEnd + 7;
            }

            return false;
        }

        private static bool TryGetUIntArray(Dictionary<ushort, TiffEntry> entries, ushort tag, byte[] bytes, bool littleEndian, out uint[] values)
        {
            values = Array.Empty<uint>();
            if (!entries.TryGetValue(tag, out TiffEntry entry))
            {
                return false;
            }

            if (!TryGetEntryBytes(entry, bytes, out byte[] entryBytes))
            {
                return false;
            }

            int count = checked((int)entry.Count);
            values = new uint[count];
            for (int i = 0; i < count; i++)
            {
                int offset = i * TypeSize(entry.Type);
                switch (entry.Type)
                {
                    case 3:
                        values[i] = ReadUInt16(entryBytes, offset, littleEndian);
                        break;
                    case 4:
                        values[i] = ReadUInt32(entryBytes, offset, littleEndian);
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

        private static bool TryGetAscii(Dictionary<ushort, TiffEntry> entries, ushort tag, byte[] bytes, out string value)
        {
            value = string.Empty;
            if (!entries.TryGetValue(tag, out TiffEntry entry) || entry.Type != 2)
            {
                return false;
            }

            if (!TryGetEntryBytes(entry, bytes, out byte[] entryBytes))
            {
                return false;
            }

            value = System.Text.Encoding.ASCII.GetString(entryBytes);
            return true;
        }

        private static bool TryGetDoubleArray(Dictionary<ushort, TiffEntry> entries, ushort tag, byte[] bytes, bool littleEndian, out double[] values)
        {
            values = Array.Empty<double>();
            if (!entries.TryGetValue(tag, out TiffEntry entry) || entry.Type != 12)
            {
                return false;
            }

            if (!TryGetEntryBytes(entry, bytes, out byte[] entryBytes))
            {
                return false;
            }

            int count = checked((int)entry.Count);
            values = new double[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = ReadDouble(entryBytes, i * 8, littleEndian);
            }

            return true;
        }

        private static bool TryGetEntryBytes(TiffEntry entry, byte[] bytes, out byte[] entryBytes)
        {
            entryBytes = Array.Empty<byte>();
            int typeSize = TypeSize(entry.Type);
            if (typeSize <= 0)
            {
                return false;
            }

            int byteCount = checked((int)(entry.Count * typeSize));
            if (byteCount <= 4)
            {
                entryBytes = new byte[byteCount];
                Buffer.BlockCopy(bytes, entry.InlineValueOffset, entryBytes, 0, byteCount);
                return true;
            }

            if (entry.ValueOffset > bytes.Length || entry.ValueOffset + byteCount > bytes.Length)
            {
                return false;
            }

            entryBytes = new byte[byteCount];
            Buffer.BlockCopy(bytes, checked((int)entry.ValueOffset), entryBytes, 0, byteCount);
            return true;
        }

        private static int TypeSize(ushort type)
        {
            switch (type)
            {
                case 1:
                case 2:
                case 6:
                case 7:
                    return 1;
                case 3:
                case 8:
                    return 2;
                case 4:
                case 9:
                case 11:
                    return 4;
                case 5:
                case 10:
                case 12:
                    return 8;
                default:
                    return 0;
            }
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

        private static ushort ReadUInt16(byte[] bytes, int offset, bool littleEndian)
        {
            return littleEndian
                ? (ushort)(bytes[offset] | (bytes[offset + 1] << 8))
                : (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
        }

        private static uint ReadUInt32(byte[] bytes, int offset, bool littleEndian)
        {
            return littleEndian
                ? (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24))
                : (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
        }

        private static ulong ReadUInt64(byte[] bytes, int offset, bool littleEndian)
        {
            uint low;
            uint high;
            if (littleEndian)
            {
                low = ReadUInt32(bytes, offset, true);
                high = ReadUInt32(bytes, offset + 4, true);
            }
            else
            {
                high = ReadUInt32(bytes, offset, false);
                low = ReadUInt32(bytes, offset + 4, false);
            }

            return low | ((ulong)high << 32);
        }

        private static float ReadSingle(byte[] bytes, int offset, bool littleEndian)
        {
            uint value = ReadUInt32(bytes, offset, littleEndian);
            return BitConverter.Int32BitsToSingle(unchecked((int)value));
        }

        private static double ReadDouble(byte[] bytes, int offset, bool littleEndian)
        {
            ulong value = ReadUInt64(bytes, offset, littleEndian);
            return BitConverter.Int64BitsToDouble(unchecked((long)value));
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

        private static bool Approximately(float a, float b)
        {
            float tolerance = Mathf.Max(0.0001f, Mathf.Abs(b) * 0.000001f);
            return Mathf.Abs(a - b) <= tolerance;
        }

        private readonly struct TiffEntry
        {
            public readonly ushort Type;
            public readonly uint Count;
            public readonly uint ValueOffset;
            public readonly int InlineValueOffset;

            public TiffEntry(ushort type, uint count, uint valueOffset, int inlineValueOffset)
            {
                Type = type;
                Count = count;
                ValueOffset = valueOffset;
                InlineValueOffset = inlineValueOffset;
            }
        }

        private struct TiffLzwBitReader
        {
            private readonly byte[] data;
            private int bitPosition;

            public TiffLzwBitReader(byte[] data)
            {
                this.data = data ?? Array.Empty<byte>();
                bitPosition = 0;
            }

            public bool TryReadBits(int bitCount, out int value)
            {
                value = 0;
                if (bitCount <= 0 || bitPosition + bitCount > data.Length * 8)
                {
                    return false;
                }

                for (int i = 0; i < bitCount; i++)
                {
                    int byteIndex = bitPosition >> 3;
                    int bitIndex = 7 - (bitPosition & 7);
                    int bit = (data[byteIndex] >> bitIndex) & 1;
                    value = (value << 1) | bit;
                    bitPosition++;
                }

                return true;
            }
        }
    }
}
