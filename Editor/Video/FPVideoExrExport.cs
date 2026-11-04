// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.IO;
    using UnityEngine;

    /// <summary>Full-resolution frame decode and FP16 processing export; native video ingest remains 8-bit.</summary>
    internal static class FPVideoExrExport
    {
        [Serializable]
        private sealed class Report
        {
            public string source, createdUtc, sourcePrecision, processingPrecision, outputEncoding;
            public double sourceSeconds;
            public FPVideoUnwrapSettings settings;
        }
        internal static void Write(FPVideoNativeSource source, FPVideoUnwrapSettings settings, double seconds, string path)
        {
            if (!source.Ready.IsCompletedSuccessfully) throw new InvalidOperationException("Source staging is not complete.");
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds >= source.Info.Duration)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            ValidatePath(path); settings.Validate();
            var texture = new Texture2D(source.Info.Width, source.Info.Height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                using (var decoder = new FPVideoNativeDecoder(source.DecodePath, source.SecondDecodePath))
                {
                    double actual = decoder.ReadAt(texture, seconds);
                    if (Math.Abs(actual - seconds) > .5 / source.Info.FrameRate + .00001) throw new InvalidDataException("Decoded frame did not match the requested source time.");
                    WriteTexture(texture, settings, path, source.OriginalPath, actual, "Native video decode: 8-bit; original 10-bit precision is not retained");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        private static void ValidatePath(string path)
        {
            if (!FPVideoUnwrapRenderer.SupportsHighPrecision) throw new NotSupportedException("FP16 processing is unavailable on this graphics device.");
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !string.Equals(Path.GetExtension(path), ".exr", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a full output path ending in .exr.");
            if (!Directory.Exists(Path.GetDirectoryName(path))) throw new DirectoryNotFoundException("Choose an existing output folder.");
            if (File.Exists(path) || File.Exists(path + ".json")) throw new IOException("Output or its JSON report already exists. Choose a new filename.");
        }
        internal static void WriteTexture(Texture source, FPVideoUnwrapSettings settings, string path, string sourceName, double seconds, string sourcePrecision)
        {
            ValidatePath(path); settings.Validate();
            string partial = Path.Combine(Path.GetDirectoryName(path), "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".partial");
            Texture2D pixels = null; bool published = false;
            try
            {
                using (var renderer = new FPVideoUnwrapRenderer())
                {
                    renderer.Render(source, settings, settings.width, settings.height, sourceSeconds: seconds);
                    pixels = new Texture2D(settings.width, settings.height, TextureFormat.RGBAHalf, false, true) { hideFlags = HideFlags.HideAndDontSave };
                    renderer.ReadWorking(pixels);
                    File.WriteAllBytes(partial, pixels.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));
                    File.WriteAllText(partial + ".json", JsonUtility.ToJson(new Report
                    {
                        source = sourceName, createdUtc = DateTime.UtcNow.ToString("O"), sourceSeconds = seconds,
                        sourcePrecision = sourcePrecision, processingPrecision = renderer.WorkingPrecision,
                        outputEncoding = "OpenEXR half-float linear RGB using the existing Unity SDR interpretation; not a recovered camera RAW or HDR signal",
                        settings = settings.Snapshot()
                    }, true));
                }
                File.Move(partial, path); published = true;
                try { File.Move(partial + ".json", path + ".json"); }
                catch { if (published) File.Delete(path); throw; }
            }
            finally
            {
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (File.Exists(partial)) File.Delete(partial);
                if (File.Exists(partial + ".json")) File.Delete(partial + ".json");
            }
        }
    }
}
