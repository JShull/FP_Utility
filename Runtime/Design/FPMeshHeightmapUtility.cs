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

    [Serializable]
    public struct FPMeshHeightmapSettings
    {
        public Texture2D Heightmap;
        public float HeightScale;
        public float HeightOffset;
        public bool Invert;
        public bool FlipX;
        public bool FlipY;
        public FPMeshHeightmapChannel Channel;

        public static FPMeshHeightmapSettings Default => new FPMeshHeightmapSettings
        {
            Heightmap = null,
            HeightScale = 1f,
            HeightOffset = 0f,
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
        public static void ApplyHeightmap(Mesh mesh, FPMeshHeightmapSettings settings, FPMeshHeightProcessSettings processSettings)
        {
            if (mesh == null || settings.Heightmap == null)
            {
                return;
            }

            var safeSettings = settings.Sanitized();
            var safeProcessSettings = processSettings.Sanitized();
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
                    float heightValue = ExtractChannel(sample, safeSettings.Channel);
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

        public static void ApplyHeightmap(Mesh mesh, FPMeshHeightmapSettings settings)
        {
            ApplyHeightmap(mesh, settings, FPMeshHeightProcessSettings.Default);
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
