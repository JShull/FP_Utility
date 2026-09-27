// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    public class FPDitherStampTests
    {
        internal static string StampPath => AssetDatabase.GetAssetPath(Shader.Find("FuzzPhyte/Dither/FP_DitherLightingLit"))
            .Replace("Shaders/FP_DitherLightingLit.shader", "Examples/Stamps/FP_BoltRank.png");

        [Test]
        public void StampImportsAsUncompressedLinearRankData()
        {
            var importer = AssetImporter.GetAtPath(StampPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        }

        [Test]
        public void StampPreservesToneCoverageAndExactEndpoints()
        {
            foreach (float tone in new[] { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f })
            {
                var pixels = Render(tone);
                Assert.That(pixels.Average(c => c.r), Is.EqualTo(tone).Within(0.005), "Tone " + tone);
                Assert.That(pixels.All(c => float.IsFinite(c.r) && c.r >= 0 && c.r <= 1), Is.True);
                if (tone == 0 || tone == 1) Assert.That(pixels.All(c => c.r == tone), Is.True);
            }
        }

        [Test]
        public void StampRepeatsAtNegativeCoordinatesAndRotates()
        {
            var original = Render(0.75f);
            var repeated = Render(0.75f, offset: new Vector2(-3, 2));
            Assert.That(original.Select(c => c.r), Is.EqualTo(repeated.Select(c => c.r)));
            var rotated = Render(0.75f, rotation: 90);
            Assert.That(original.Zip(rotated, (a, b) => Mathf.Abs(a.r - b.r)).Average(), Is.GreaterThan(0.05));
            Assert.That(rotated.Average(c => c.r), Is.EqualTo(0.75f).Within(0.005));
        }

        [Test]
        public void StampOffMaskAndMinificationRecoverInput()
        {
            foreach (var pixels in new[] { Render(0.37f, strength: 0), Render(0.37f, mask: 0),
                Render(0.37f, scale: 256, filtering: 1) })
                Assert.That(pixels.All(c => Mathf.Abs(c.r - 0.37f) < 0.00001f), Is.True);
        }

        private static Color[] Render(float tone, float strength = 1, float mask = 1,
            float scale = 1, float filtering = 0, Vector2 offset = default, float rotation = 0)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
            var material = new Material(Shader.Find("Hidden/FuzzPhyte/Tests/DitherStampProbe"));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(StampPath);
            Assert.That(texture, Is.Not.Null);
            var target = RenderTexture.GetTemporary(128, 128, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(128, 128, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                material.SetTexture("_DitherStamp", texture);
                material.SetFloat("_Tone", tone); material.SetFloat("_DitherPattern", 7);
                material.SetFloat("_DitherScale", scale); material.SetFloat("_DitherAmount", 1);
                material.SetFloat("_DitherFiltering", filtering); material.SetFloat("_DitherRotation", rotation);
                material.SetFloat("_DitherMaskStrength", mask); material.SetFloat("_LightingSteps", 2);
                material.SetFloat("_LightingDitherStrength", strength);
                material.SetVector("_Offset", offset);
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); readback.Apply();
                Assert.That(ShaderUtil.GetShaderMessages(material.shader), Is.Empty);
                return readback.GetPixels();
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async; RenderTexture.active = previous;
                Object.DestroyImmediate(material); Object.DestroyImmediate(readback);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
