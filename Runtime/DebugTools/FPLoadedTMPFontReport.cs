namespace FuzzPhyte.Utility.DebugTools
{
    using System.Linq;
    using TMPro;
    using UnityEngine;

    public static class FPLoadedTMPFontReport
    {
        public static void LogLoadedTMPFonts()
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .Where(f => f != null)
                .OrderBy(f => f.name)
                .ToArray();

            Debug.Log($"[FP_TMP_FONT_REPORT] Loaded TMP Font Assets: {fonts.Length}");

            foreach (var font in fonts)
            {
                int fallbackCount = font.fallbackFontAssetTable != null
                    ? font.fallbackFontAssetTable.Count
                    : 0;

                int atlasCount = font.atlasTextures != null
                    ? font.atlasTextures.Length
                    : 0;

                string sourceFontName = font.sourceFontFile != null
                    ? font.sourceFontFile.name
                    : "None";

                Debug.Log(
                    $"[FP_TMP_FONT] {font.name} | " +
                    $"Source={sourceFontName} | " +
                    $"AtlasPopulation={font.atlasPopulationMode} | " +
                    $"AtlasCount={atlasCount} | " +
                    $"Fallbacks={fallbackCount} | " +
                    $"Glyphs={font.glyphTable?.Count ?? 0} | " +
                    $"Characters={font.characterTable?.Count ?? 0}"
                );

                if (font.fallbackFontAssetTable != null)
                {
                    foreach (var fallback in font.fallbackFontAssetTable)
                    {
                        if (fallback != null)
                        {
                            Debug.Log($"[FP_TMP_FONT_FALLBACK] {font.name} -> {fallback.name}");
                        }
                    }
                }
            }
        }
    }
}
