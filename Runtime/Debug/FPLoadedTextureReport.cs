namespace FuzzPhyte.Utility.Debug
{
    using UnityEngine;
    using System.Linq;
    using System.Collections.Generic;
    public static class FPLoadedTextureReport
    {
        public static void LogTopLoadedTextures(int count = 50)
        {
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>()
                .Where(t => t != null)
                .Select(t => new TextureInfo(t))
                .OrderByDescending(t => t.EstimatedBytes)
                .Take(count);

            Debug.Log($"[FP_TEXTURE_REPORT] Top {count} loaded textures:");

            foreach (var tex in textures)
            {
                Debug.Log(
                    $"[FP_TEXTURE] {tex.Name} | " +
                    $"{tex.Width}x{tex.Height} | " +
                    $"Format={tex.Format} | " +
                    $"Mipmaps={tex.MipmapCount} | " +
                    $"Readable={tex.IsReadable} | " +
                    $"Estimated={tex.EstimatedMB:F2}MB"
                );
            }
        }

        private readonly struct TextureInfo
        {
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;
            public readonly TextureFormat Format;
            public readonly int MipmapCount;
            public readonly bool IsReadable;
            public readonly long EstimatedBytes;

            public double EstimatedMB => EstimatedBytes / (1024.0 * 1024.0);

            public TextureInfo(Texture2D texture)
            {
                Name = texture.name;
                Width = texture.width;
                Height = texture.height;
                Format = texture.format;
                MipmapCount = texture.mipmapCount;
                IsReadable = texture.isReadable;
                EstimatedBytes = EstimateTextureBytes(texture);
            }

            private static long EstimateTextureBytes(Texture2D texture)
            {
                int bytesPerPixel = texture.format switch
                {
                    TextureFormat.RGBA32 => 4,
                    TextureFormat.ARGB32 => 4,
                    TextureFormat.BGRA32 => 4,
                    TextureFormat.RGB24 => 3,
                    TextureFormat.RGBAHalf => 8,
                    TextureFormat.RGBAFloat => 16,
                    TextureFormat.RGBA64 => 8,
                    TextureFormat.R16 => 2,
                    TextureFormat.RHalf => 2,
                    TextureFormat.RFloat => 4,
                    TextureFormat.Alpha8 => 1,
                    _ => 4
                };

                long baseBytes = (long)texture.width * texture.height * bytesPerPixel;

                if (texture.mipmapCount > 1)
                {
                    baseBytes = (long)(baseBytes * 1.33f);
                }

                return baseBytes;
            }
        }
    }
}
