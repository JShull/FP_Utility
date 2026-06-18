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
    using UnityEngine;

    public enum FPMeshHeightmapChannel
    {
        Grayscale = 0,
        Red = 1,
        Green = 2,
        Blue = 3,
        Alpha = 4
    }

    public enum FPMeshGeoTiffCoordinateSystem
    {
        WGS84 = 0,
        UTM = 1,
        StatePlane = 2,
        Projected = 3
    }

    public enum FPMeshGeoTiffGridAnchor
    {
        LowerLeftPixel = 0,
        Center = 1,
        UpperLeftPixel = 2,
        Custom = 3
    }

    public enum FPMeshSonarLogMeshMode
    {
        Waterfall = 0,
        GeospatialMosaic = 1
    }

    public enum FPMeshSonarLogNavSource
    {
        LocalPositionNed = 0,
        GlobalPositionInt = 1
    }

    public enum FPMeshSonarLogHeadingSource
    {
        AttitudeYaw = 0,
        GlobalPositionHeading = 1,
        OmniscanVehicleHeading = 2
    }

    public enum FPMeshSonarLogOverlapMode
    {
        Mean = 0,
        Max = 1,
        Latest = 2
    }

    [Serializable]
    public struct FPMeshHeightmapSettings
    {
        public Texture2D Heightmap;
        public float HeightScale;
        public float HeightOffset;
        public bool MapImageToSurface;
        public Texture2D SurfaceTexture;
        public bool UseGeoTiffElevationData;
        public string GeoTiffSourcePath;
        public bool MatchGridToGeoTiffScale;
        public float GeoTiffHorizontalUnitsToMeters;
        public FPMeshGeoTiffCoordinateSystem GeoTiffCoordinateSystem;
        public string GeoTiffProjectedCrs;
        public FPMeshGeoTiffGridAnchor GeoTiffGridAnchor;
        public bool GeoTiffNorthToPositiveZ;
        public bool UseSonarLogDepthData;
        public string SonarLogSourcePath;
        public FPMeshSonarLogMeshMode SonarLogMeshMode;
        public FPMeshSonarLogNavSource SonarLogNavSource;
        public FPMeshSonarLogHeadingSource SonarLogHeadingSource;
        public FPMeshSonarLogOverlapMode SonarLogOverlapMode;
        public bool MatchGridToSonarLogBounds;
        public float SonarLogSurveySpeedMetersPerSecond;
        public float SonarLogPingRateHz;
        public float SonarLogRangeMeters;
        public float SonarLogForwardStepMeters;
        public float SonarLogCellSizeMeters;
        public int SonarLogTimeOffsetMs;
        public int SonarLogRasterWidth;
        public int SonarLogRasterLength;
        public bool Invert;
        public bool FlipX;
        public bool FlipY;
        public FPMeshHeightmapChannel Channel;

        public static FPMeshHeightmapSettings Default => new FPMeshHeightmapSettings
        {
            Heightmap = null,
            HeightScale = 1f,
            HeightOffset = 0f,
            MapImageToSurface = true,
            SurfaceTexture = null,
            UseGeoTiffElevationData = false,
            GeoTiffSourcePath = string.Empty,
            MatchGridToGeoTiffScale = false,
            GeoTiffHorizontalUnitsToMeters = 1f,
            GeoTiffCoordinateSystem = FPMeshGeoTiffCoordinateSystem.WGS84,
            GeoTiffProjectedCrs = string.Empty,
            GeoTiffGridAnchor = FPMeshGeoTiffGridAnchor.LowerLeftPixel,
            GeoTiffNorthToPositiveZ = true,
            UseSonarLogDepthData = false,
            SonarLogSourcePath = string.Empty,
            SonarLogMeshMode = FPMeshSonarLogMeshMode.Waterfall,
            SonarLogNavSource = FPMeshSonarLogNavSource.LocalPositionNed,
            SonarLogHeadingSource = FPMeshSonarLogHeadingSource.AttitudeYaw,
            SonarLogOverlapMode = FPMeshSonarLogOverlapMode.Mean,
            MatchGridToSonarLogBounds = false,
            SonarLogSurveySpeedMetersPerSecond = 1f,
            SonarLogPingRateHz = 20f,
            SonarLogRangeMeters = 30f,
            SonarLogForwardStepMeters = 0.05f,
            SonarLogCellSizeMeters = 0.5f,
            SonarLogTimeOffsetMs = 0,
            SonarLogRasterWidth = 512,
            SonarLogRasterLength = 512,
            Invert = false,
            FlipX = false,
            FlipY = false,
            Channel = FPMeshHeightmapChannel.Grayscale
        };

        public FPMeshHeightmapSettings Sanitized()
        {
            return new FPMeshHeightmapSettings
            {
                Heightmap = Heightmap,
                HeightScale = HeightScale,
                HeightOffset = HeightOffset,
                MapImageToSurface = MapImageToSurface,
                SurfaceTexture = SurfaceTexture,
                UseGeoTiffElevationData = UseGeoTiffElevationData,
                GeoTiffSourcePath = string.IsNullOrWhiteSpace(GeoTiffSourcePath) ? string.Empty : GeoTiffSourcePath.Trim(),
                MatchGridToGeoTiffScale = MatchGridToGeoTiffScale,
                GeoTiffHorizontalUnitsToMeters = Mathf.Max(0.000001f, GeoTiffHorizontalUnitsToMeters),
                GeoTiffCoordinateSystem = GeoTiffCoordinateSystem,
                GeoTiffProjectedCrs = string.IsNullOrWhiteSpace(GeoTiffProjectedCrs) ? string.Empty : GeoTiffProjectedCrs.Trim(),
                GeoTiffGridAnchor = GeoTiffGridAnchor,
                GeoTiffNorthToPositiveZ = GeoTiffNorthToPositiveZ,
                UseSonarLogDepthData = UseSonarLogDepthData,
                SonarLogSourcePath = string.IsNullOrWhiteSpace(SonarLogSourcePath) ? string.Empty : SonarLogSourcePath.Trim(),
                SonarLogMeshMode = SonarLogMeshMode,
                SonarLogNavSource = SonarLogNavSource,
                SonarLogHeadingSource = SonarLogHeadingSource,
                SonarLogOverlapMode = SonarLogOverlapMode,
                MatchGridToSonarLogBounds = MatchGridToSonarLogBounds,
                SonarLogSurveySpeedMetersPerSecond = Mathf.Max(0f, SonarLogSurveySpeedMetersPerSecond),
                SonarLogPingRateHz = Mathf.Max(0.000001f, SonarLogPingRateHz <= 0f ? 20f : SonarLogPingRateHz),
                SonarLogRangeMeters = Mathf.Max(0.000001f, SonarLogRangeMeters <= 0f ? 30f : SonarLogRangeMeters),
                SonarLogForwardStepMeters = Mathf.Max(0.000001f, SonarLogForwardStepMeters),
                SonarLogCellSizeMeters = Mathf.Max(0.001f, SonarLogCellSizeMeters <= 0f ? 0.5f : SonarLogCellSizeMeters),
                SonarLogTimeOffsetMs = SonarLogTimeOffsetMs,
                SonarLogRasterWidth = Mathf.Clamp(SonarLogRasterWidth <= 0 ? 512 : SonarLogRasterWidth, 8, 8192),
                SonarLogRasterLength = Mathf.Clamp(SonarLogRasterLength <= 0 ? 512 : SonarLogRasterLength, 8, 8192),
                Invert = Invert,
                FlipX = FlipX,
                FlipY = FlipY,
                Channel = Channel
            };
        }
    }

    public enum FPMeshEdgeFalloffMode
    {
        None = 0,
        Rectangular = 1,
        Radial = 2
    }

    [Serializable]
    public struct FPMeshHeightProcessSettings
    {
        public bool UseRemap;
        public float RemapMin;
        public float RemapMax;
        public FPMeshEdgeFalloffMode EdgeFalloffMode;
        public float EdgeFalloffStart;
        public float EdgeFalloffStrength;
        public bool UseTerracing;
        public int TerraceSteps;

        public static FPMeshHeightProcessSettings Default => new FPMeshHeightProcessSettings
        {
            UseRemap = false,
            RemapMin = 0f,
            RemapMax = 1f,
            EdgeFalloffMode = FPMeshEdgeFalloffMode.None,
            EdgeFalloffStart = 0.75f,
            EdgeFalloffStrength = 1f,
            UseTerracing = false,
            TerraceSteps = 4
        };

        public FPMeshHeightProcessSettings Sanitized()
        {
            return new FPMeshHeightProcessSettings
            {
                UseRemap = UseRemap,
                RemapMin = Mathf.Clamp01(Mathf.Min(RemapMin, RemapMax)),
                RemapMax = Mathf.Clamp01(Mathf.Max(RemapMin, RemapMax)),
                EdgeFalloffMode = EdgeFalloffMode,
                EdgeFalloffStart = Mathf.Clamp01(EdgeFalloffStart),
                EdgeFalloffStrength = Mathf.Max(0f, EdgeFalloffStrength),
                UseTerracing = UseTerracing,
                TerraceSteps = Mathf.Max(2, TerraceSteps)
            };
        }
    }

    /// <summary>
    /// Applies heightmap displacement to an existing mesh using its UV0 coordinates.
    /// </summary>
    public static class FPMeshHeightmapUtility
    {
        public static FPMeshHeightmapSettings ApplyGridGenerationMode(
            FPMeshGridGenerationMode generationMode,
            FPMeshHeightmapSettings heightmapSettings)
        {
            FPMeshHeightmapSettings safeSettings = heightmapSettings.Sanitized();
            switch (generationMode)
            {
                case FPMeshGridGenerationMode.GeoTiffGrid:
                    safeSettings.UseGeoTiffElevationData = true;
                    safeSettings.UseSonarLogDepthData = false;
                    break;
                case FPMeshGridGenerationMode.SonarLogGrid:
                    safeSettings.UseGeoTiffElevationData = false;
                    safeSettings.UseSonarLogDepthData = true;
                    break;
                default:
                    safeSettings.UseGeoTiffElevationData = false;
                    safeSettings.UseSonarLogDepthData = false;
                    break;
            }

            return safeSettings;
        }

        public static FPMeshGridBuildSettings ApplySourceRealScaleToGrid(FPMeshGridBuildSettings gridSettings, FPMeshHeightmapSettings heightmapSettings)
        {
            FPMeshGridBuildSettings safeGridSettings = gridSettings.Sanitized();
            FPMeshHeightmapSettings safeHeightmapSettings = ApplyGridGenerationMode(safeGridSettings.GenerationMode, heightmapSettings);
            if (safeHeightmapSettings.UseSonarLogDepthData)
            {
                return ApplySonarLogRealScaleToGrid(safeGridSettings, safeHeightmapSettings);
            }

            return ApplyGeoTiffRealScaleToGrid(safeGridSettings, safeHeightmapSettings);
        }

        public static FPMeshGridBuildSettings ApplyGeoTiffRealScaleToGrid(FPMeshGridBuildSettings gridSettings, FPMeshHeightmapSettings heightmapSettings)
        {
            FPMeshGridBuildSettings safeGridSettings = gridSettings.Sanitized();
            FPMeshHeightmapSettings safeHeightmapSettings = heightmapSettings.Sanitized();

            if (!safeHeightmapSettings.UseGeoTiffElevationData
                || !safeHeightmapSettings.MatchGridToGeoTiffScale
                || string.IsNullOrWhiteSpace(safeHeightmapSettings.GeoTiffSourcePath))
            {
                return safeGridSettings;
            }

            if (!FPMeshGeoTiffRaster.TryLoad(safeHeightmapSettings.GeoTiffSourcePath, out FPMeshGeoTiffRaster raster, out string loadError))
            {
                Debug.LogWarning($"[FP Mesh Generator] Could not load GeoTIFF scale data from '{safeHeightmapSettings.GeoTiffSourcePath}'. {loadError}");
                return safeGridSettings;
            }

            if (!raster.TryGetRealSizeMeters(
                    safeHeightmapSettings.GeoTiffCoordinateSystem,
                    safeHeightmapSettings.GeoTiffHorizontalUnitsToMeters,
                    out float widthMeters,
                    out float lengthMeters,
                    out string scaleError))
            {
                Debug.LogWarning($"[FP Mesh Generator] Could not derive real-scale GeoTIFF bounds from '{safeHeightmapSettings.GeoTiffSourcePath}'. {scaleError}");
                return safeGridSettings;
            }

            safeGridSettings.Width = widthMeters;
            safeGridSettings.Length = lengthMeters;
            return safeGridSettings.Sanitized();
        }

        public static FPMeshGridBuildSettings ApplySonarLogRealScaleToGrid(FPMeshGridBuildSettings gridSettings, FPMeshHeightmapSettings heightmapSettings)
        {
            FPMeshGridBuildSettings safeGridSettings = gridSettings.Sanitized();
            FPMeshHeightmapSettings safeHeightmapSettings = heightmapSettings.Sanitized();

            if (!safeHeightmapSettings.UseSonarLogDepthData
                || !safeHeightmapSettings.MatchGridToSonarLogBounds
                || string.IsNullOrWhiteSpace(safeHeightmapSettings.SonarLogSourcePath))
            {
                return safeGridSettings;
            }

            if (!FPMeshSonarLogRaster.TryLoad(safeHeightmapSettings.SonarLogSourcePath, safeHeightmapSettings, out FPMeshSonarLogRaster raster, out string loadError))
            {
                Debug.LogWarning($"[FP Mesh Generator] Could not load sonar log bounds from '{safeHeightmapSettings.SonarLogSourcePath}'. {loadError}");
                return safeGridSettings;
            }

            if (!raster.TryGetRealSizeMeters(out float widthMeters, out float lengthMeters))
            {
                Debug.LogWarning($"[FP Mesh Generator] Could not derive sonar log bounds from '{safeHeightmapSettings.SonarLogSourcePath}'.");
                return safeGridSettings;
            }

            safeGridSettings.Width = widthMeters;
            safeGridSettings.Length = lengthMeters;
            return safeGridSettings.Sanitized();
        }

        public static void ApplyHeightmap(Mesh mesh, FPMeshHeightmapSettings settings, FPMeshHeightProcessSettings processSettings)
        {
            if (mesh == null)
            {
                return;
            }

            var safeSettings = settings.Sanitized();
            var safeProcessSettings = processSettings.Sanitized();
            string geoTiffLoadError = string.Empty;
            string sonarLogLoadError = string.Empty;

            if (safeSettings.UseSonarLogDepthData
                && !string.IsNullOrWhiteSpace(safeSettings.SonarLogSourcePath)
                && FPMeshSonarLogRaster.TryLoad(safeSettings.SonarLogSourcePath, safeSettings, out FPMeshSonarLogRaster sonarRaster, out sonarLogLoadError))
            {
                ApplySonarLogRaster(mesh, safeSettings, sonarRaster);
                return;
            }

            if (safeSettings.UseSonarLogDepthData
                && !string.IsNullOrWhiteSpace(safeSettings.SonarLogSourcePath))
            {
                Debug.LogWarning($"[FP Mesh Generator] Could not load sonar log '{safeSettings.SonarLogSourcePath}'. {sonarLogLoadError}");
                return;
            }

            if (safeSettings.UseGeoTiffElevationData
                && !string.IsNullOrWhiteSpace(safeSettings.GeoTiffSourcePath)
                && FPMeshGeoTiffRaster.TryLoad(safeSettings.GeoTiffSourcePath, out FPMeshGeoTiffRaster raster, out geoTiffLoadError))
            {
                ApplyGeoTiffRaster(mesh, safeSettings, raster);
                return;
            }

            if (safeSettings.UseGeoTiffElevationData
                && !string.IsNullOrWhiteSpace(safeSettings.GeoTiffSourcePath)
                && safeSettings.Heightmap == null)
            {
                Debug.LogWarning($"[FP Mesh Generator] Could not load GeoTIFF '{safeSettings.GeoTiffSourcePath}'. {geoTiffLoadError}");
                return;
            }

            if (safeSettings.Heightmap == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;

            if (vertices == null || uv == null || vertices.Length == 0 || uv.Length != vertices.Length)
            {
                return;
            }

            Texture2D readableTexture = null;
            bool destroyReadableTexture = false;

            try
            {
                readableTexture = GetReadableTexture(safeSettings.Heightmap, out destroyReadableTexture);
                if (readableTexture == null)
                {
                    return;
                }

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector2 sampleUv = uv[i];
                    if (safeSettings.FlipX)
                    {
                        sampleUv.x = 1f - sampleUv.x;
                    }

                    if (safeSettings.FlipY)
                    {
                        sampleUv.y = 1f - sampleUv.y;
                    }

                    Color sample = readableTexture.GetPixelBilinear(sampleUv.x, sampleUv.y);
                    float heightValue = safeSettings.UseGeoTiffElevationData
                        ? ExtractGeoTiffElevation(sample, safeSettings.Channel)
                        : ExtractChannel(sample, safeSettings.Channel);

                    if (safeSettings.UseGeoTiffElevationData)
                    {
                        if (safeSettings.Invert)
                        {
                            heightValue = -heightValue;
                        }

                        vertices[i].y = IsValidElevation(heightValue)
                            ? safeSettings.HeightOffset + (heightValue * safeSettings.HeightScale)
                            : safeSettings.HeightOffset;
                        continue;
                    }

                    if (safeSettings.Invert)
                    {
                        heightValue = 1f - heightValue;
                    }

                    heightValue = ProcessHeight(heightValue, sampleUv, safeProcessSettings);
                    vertices[i].y = safeSettings.HeightOffset + (heightValue * safeSettings.HeightScale);
                }

                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
            }
            finally
            {
                if (destroyReadableTexture && readableTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(readableTexture);
                }
            }
        }

        private static void ApplyGeoTiffRaster(Mesh mesh, FPMeshHeightmapSettings settings, FPMeshGeoTiffRaster raster)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;

            if (vertices == null || uv == null || vertices.Length == 0 || uv.Length != vertices.Length)
            {
                return;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 sampleUv = uv[i];
                if (settings.FlipX)
                {
                    sampleUv.x = 1f - sampleUv.x;
                }

                if (settings.FlipY)
                {
                    sampleUv.y = 1f - sampleUv.y;
                }

                float heightValue = raster.SampleBilinear(sampleUv.x, sampleUv.y, settings.GeoTiffNorthToPositiveZ);
                if (settings.Invert)
                {
                    heightValue = -heightValue;
                }

                vertices[i].y = IsValidElevation(heightValue)
                    ? settings.HeightOffset + (heightValue * settings.HeightScale)
                    : settings.HeightOffset;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        private static void ApplySonarLogRaster(Mesh mesh, FPMeshHeightmapSettings settings, FPMeshSonarLogRaster raster)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;

            if (vertices == null || uv == null || vertices.Length == 0 || uv.Length != vertices.Length)
            {
                return;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 sampleUv = uv[i];
                if (settings.FlipX)
                {
                    sampleUv.x = 1f - sampleUv.x;
                }

                if (settings.FlipY)
                {
                    sampleUv.y = 1f - sampleUv.y;
                }

                float heightValue = raster.SampleBilinear(sampleUv.x, sampleUv.y);
                if (settings.Invert)
                {
                    heightValue = -heightValue;
                }

                vertices[i].y = IsValidElevation(heightValue)
                    ? settings.HeightOffset + (heightValue * settings.HeightScale)
                    : settings.HeightOffset;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        public static void ApplyHeightmap(Mesh mesh, FPMeshHeightmapSettings settings)
        {
            ApplyHeightmap(mesh, settings, FPMeshHeightProcessSettings.Default);
        }

        private static float ExtractGeoTiffElevation(Color sample, FPMeshHeightmapChannel channel)
        {
            if (channel == FPMeshHeightmapChannel.Grayscale && IsSingleBandSample(sample))
            {
                return sample.r;
            }

            return ExtractChannel(sample, channel);
        }

        private static bool IsSingleBandSample(Color sample)
        {
            const float epsilon = 0.000001f;
            return Mathf.Abs(sample.g) <= epsilon
                && Mathf.Abs(sample.b) <= epsilon;
        }

        private static bool IsValidElevation(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value > -1e30f
                && value < 1e30f;
        }

        private static Texture2D GetReadableTexture(Texture2D source, out bool destroyWhenDone)
        {
            destroyWhenDone = false;

            if (source == null)
            {
                return null;
            }

            try
            {
                source.GetPixel(0, 0);
                return source;
            }
            catch (UnityException)
            {
                destroyWhenDone = true;
                return source.Decompress();
            }
        }

        private static float ExtractChannel(Color sample, FPMeshHeightmapChannel channel)
        {
            switch (channel)
            {
                case FPMeshHeightmapChannel.Red:
                    return sample.r;
                case FPMeshHeightmapChannel.Green:
                    return sample.g;
                case FPMeshHeightmapChannel.Blue:
                    return sample.b;
                case FPMeshHeightmapChannel.Alpha:
                    return sample.a;
                case FPMeshHeightmapChannel.Grayscale:
                default:
                    return sample.grayscale;
            }
        }

        private static float ProcessHeight(float heightValue, Vector2 uv, FPMeshHeightProcessSettings settings)
        {
            float processed = Mathf.Clamp01(heightValue);

            if (settings.UseRemap)
            {
                processed = Mathf.InverseLerp(settings.RemapMin, settings.RemapMax, processed);
            }

            float falloff = EvaluateFalloff(uv, settings);
            processed *= falloff;

            if (settings.UseTerracing)
            {
                float stepCount = settings.TerraceSteps;
                processed = Mathf.Floor(processed * stepCount) / stepCount;
            }

            return Mathf.Clamp01(processed);
        }

        private static float EvaluateFalloff(Vector2 uv, FPMeshHeightProcessSettings settings)
        {
            if (settings.EdgeFalloffMode == FPMeshEdgeFalloffMode.None || settings.EdgeFalloffStrength <= 0f)
            {
                return 1f;
            }

            float centerDistance;
            switch (settings.EdgeFalloffMode)
            {
                case FPMeshEdgeFalloffMode.Radial:
                    centerDistance = Vector2.Distance(uv, new Vector2(0.5f, 0.5f)) / 0.70710678f;
                    break;
                case FPMeshEdgeFalloffMode.Rectangular:
                default:
                    float dx = Mathf.Abs((uv.x - 0.5f) * 2f);
                    float dy = Mathf.Abs((uv.y - 0.5f) * 2f);
                    centerDistance = Mathf.Max(dx, dy);
                    break;
            }

            if (centerDistance <= settings.EdgeFalloffStart)
            {
                return 1f;
            }

            float t = Mathf.InverseLerp(settings.EdgeFalloffStart, 1f, centerDistance);
            float falloff = 1f - Mathf.Pow(t, Mathf.Max(0.0001f, settings.EdgeFalloffStrength));
            return Mathf.Clamp01(falloff);
        }
    }
}
