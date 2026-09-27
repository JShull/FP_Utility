// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    public class FPDitherTests
    {
        private static readonly string[] Properties = { "_BaseMap", "_BaseColor", "_BumpMap",
            "_Metallic", "_Smoothness", "_DitherStrength", "_DitherScale", "_DitherColorSteps",
            "_DitherAmount", "_DitherBias", "_DitherMaskStrength", "_DitherPattern",
            "_DitherRotation", "_DitherArmRatio", "_DitherFiltering" };

        [TestCase("Character")]
        [TestCase("Organic")]
        [TestCase("OrganicFoliage")]
        [TestCase("PolySpatial")]
        [TestCase("PolySpatialFoliage")]
        public void LitGraphImportsWithExpectedPropertiesAndSharedCore(string family)
        {
            // Locate by shader name so the contract also works when installed as UPM.
            var shader = Shader.Find("FuzzPhyte/Dither/FP_" + family + "DitherLit");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            Assert.That(ShaderUtil.GetShaderMessages(shader), Is.Empty);
            var material = new Material(shader);
            try
            {
                foreach (var property in Properties) Assert.That(material.HasProperty(property), Is.True, property);
                Assert.That(material.GetFloat("_DitherStrength"), Is.InRange(0f, 1f));
                Assert.That(material.GetFloat("_DitherScale"), Is.GreaterThanOrEqualTo(1f));
                Assert.That(material.GetFloat("_DitherColorSteps"), Is.GreaterThanOrEqualTo(2f));
                Assert.That(material.GetFloat("_DitherPattern"), Is.Zero, "Existing materials must default to Bayer.");
                var dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(shader), true);
                bool portable = family.StartsWith("PolySpatial");
                Assert.That(dependencies.Any(p => p.EndsWith(portable ? "FP_DitherMaterialXCore.shadersubgraph" : "FP_DitherCore.shadersubgraph")), Is.True);
                Assert.That(dependencies.Any(p => p.EndsWith(portable ? "FP_DitherMaterialX.hlsl" : "FP_Dither.hlsl")), Is.True);
                if (portable) Assert.That(material.GetFloat("_DitherStrength"), Is.Zero);
                Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
                if (family.EndsWith("Foliage"))
                {
                    Assert.That(material.HasProperty("_Cutoff"), Is.True);
                    Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.5f));
                }
            }
            finally { Object.DestroyImmediate(material); }
        }

        [TestCase("OrganicFoliage")]
        [TestCase("PolySpatialFoliage")]
        public void FoliageCutoutRemainsUnchangedWhenDitheringChanges(string family)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Foliage render checks require an active graphics device.");
            var material = new Material(Shader.Find("FuzzPhyte/Dither/FP_" + family + "DitherLit"));
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                normals = Enumerable.Repeat(Vector3.back, 4).ToArray(),
                tangents = Enumerable.Repeat(new Vector4(1, 0, 0, -1), 4).ToArray(),
                triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }
            };
            var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            texture.SetPixels(new[] { new Color(0.5f, 0.5f, 0.5f, 0), new Color(0.5f, 0.5f, 0.5f, 1) }); texture.Apply();
            material.SetTexture("_BaseMap", texture);
            var target = RenderTexture.GetTemporary(32, 32, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(32, 32, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            var asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                ShaderUtil.CompilePass(material, material.FindPass("ForwardLit"), true);
                Color[] Capture(float strength)
                {
                    material.SetFloat("_DitherStrength", strength);
                    using (var commands = new CommandBuffer { name = "FP Dither foliage coverage" })
                    {
                        commands.SetRenderTarget(target);
                        commands.ClearRenderTarget(true, true, Color.clear);
                        var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.Translate(new Vector3(0, 0, 2));
                        var projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-0.5f, 0.5f, -0.5f, 0.5f, 0.1f, 10), true);
                        commands.SetViewProjectionMatrices(view, projection);
                        commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, material.FindPass("ForwardLit"));
                        Graphics.ExecuteCommandBuffer(commands);
                    }
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); readback.Apply();
                    return readback.GetPixels();
                }
                var baseline = Capture(0);
                Assert.That(baseline.Count(c => c.a > 0.5f), Is.InRange(480, 544), "Half of the quad should survive the texture cutout. Max pixel=" + baseline.OrderByDescending(c => c.maxColorComponent).First());
                for (int pattern = 0; pattern <= 6; pattern++)
                {
                    material.SetFloat("_DitherPattern", pattern);
                    material.SetFloat("_DitherScale", 2);
                    var dither = Capture(1);
                    for (int i = 0; i < baseline.Length; i++)
                        Assert.That(dither[i].a, Is.EqualTo(baseline[i].a), "Pattern " + pattern + " must not alter alpha coverage.");
                }
                Assert.That(ShaderUtil.GetShaderMessages(material.shader), Is.Empty);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(mesh); Object.DestroyImmediate(readback); Object.DestroyImmediate(texture); Object.DestroyImmediate(material);
            }
        }

        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        public void DisabledEffectPreservesInputIncludingOutsideUnitRange(float strength, float mask)
        {
            var expected = new Color(0.237f, 1.5f, -0.1f, 1);
            foreach (var color in Render(expected, strength, mask))
            {
                Assert.That(color.r, Is.EqualTo(expected.r).Within(0.00001f));
                Assert.That(color.g, Is.EqualTo(expected.g).Within(0.00001f));
                Assert.That(color.b, Is.EqualTo(expected.b).Within(0.00001f));
            }
        }

        [Test]
        public void BayerContainsSixteenUniqueCenteredRanks()
        {
            var ranks = Render(Color.gray, 1, 1, debug: true).Select(c => c.r).OrderBy(v => v).ToArray();
            for (int i = 0; i < 16; i++) Assert.That(ranks[i], Is.EqualTo((i + 0.5f) / 16f).Within(0.00001f));
        }

        [Test]
        public void OrderedRoundingPreservesTileAverageAndAmountZeroIsUniform()
        {
            var ordered = Render(new Color(0.5f, 0.5f, 0.5f, 1), 1, 1, steps: 2);
            Assert.That(ordered.Count(c => c.r < 0.01f), Is.EqualTo(8));
            Assert.That(ordered.Average(c => c.r), Is.EqualTo(0.5f).Within(0.00001f));
            var nearest = Render(new Color(0.4f, 0.4f, 0.4f, 1), 1, 1, steps: 2, amount: 0);
            Assert.That(nearest.All(c => c.r == 0), Is.True);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(8f)]
        public void QuantizationIsFiniteAndBounded(float steps)
        {
            foreach (var color in Render(new Color(0.2f, 0.7f, 1, 1), 1, 1, steps: steps))
            {
                Assert.That(color.r, Is.InRange(0f, 1f));
                Assert.That(color.g, Is.InRange(0f, 1f));
                Assert.That(color.b, Is.EqualTo(1f).Within(0.00001f));
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void ShapeCoverageIsMonotonicAndPreservesAverageTone(int pattern)
        {
            float previous = -1;
            foreach (float tone in new[] { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f })
            {
                var pixels = Render(new Color(tone, tone, tone, 1), 1, 1, steps: 2,
                    pattern: pattern, size: 256, scale: 1);
                float mean = pixels.Average(c => c.r);
                Assert.That(mean, Is.EqualTo(tone).Within(0.008f), "Pattern " + pattern + ", tone " + tone);
                Assert.That(mean, Is.GreaterThanOrEqualTo(previous));
                if (tone == 0 || tone == 1) Assert.That(pixels.All(c => c.r == tone), Is.True);
                previous = mean;
            }
        }

        [Test]
        public void BayerSelectionMatchesLegacyEvenWithNewControlsSet()
        {
            var color = new Color(0.217f, 0.503f, 0.819f, 1);
            var legacy = Render(color, 0.73f, 0.61f, steps: 7, amount: 0.82f, size: 32, legacy: true);
            var upgraded = Render(color, 0.73f, 0.61f, steps: 7, amount: 0.82f, size: 32,
                rotation: 45, filtering: 1, armRatio: 0.1f);
            Assert.That(upgraded, Is.EqualTo(legacy));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void DisabledShapesAndMinifiedShapesPreserveOriginal(int pattern)
        {
            var input = new Color(0.237f, 1.5f, -0.1f, 1);
            var off = Render(input, 0, 1, pattern: pattern);
            var masked = Render(input, 1, 0, pattern: pattern);
            var minified = Render(input, 1, 1, pattern: pattern, filtering: 1, size: 32, scale: 128);
            foreach (var c in off.Concat(masked).Concat(minified))
            {
                Assert.That(c.r, Is.EqualTo(input.r).Within(0.00001f));
                Assert.That(c.g, Is.EqualTo(input.g).Within(0.00001f));
                Assert.That(c.b, Is.EqualTo(input.b).Within(0.00001f));
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void ShapesRepeatAcrossPositiveAndNegativeCells(int pattern)
        {
            var first = Render(Color.gray, 1, 1, pattern: pattern, size: 64, scale: 1, debug: true);
            var repeated = Render(Color.gray, 1, 1, pattern: pattern, size: 64, scale: 1, debug: true,
                uvOffset: new Vector2(2, -3));
            for (int i = 0; i < first.Length; i++) Assert.That(repeated[i].r, Is.EqualTo(first[i].r).Within(0.00001f));
        }

        [Test]
        public void HatchRotationTurnsHorizontalBandsIntoVerticalBands()
        {
            var horizontal = Render(Color.gray, 1, 1, pattern: 3, size: 32, scale: 1, debug: true);
            var vertical = Render(Color.gray, 1, 1, pattern: 3, size: 32, scale: 1, debug: true, rotation: 90);
            for (int x = 0; x < 32; x++)
            for (int y = 0; y < 32; y++)
                Assert.That(vertical[y * 32 + x].r, Is.EqualTo(horizontal[x * 32 + y].r).Within(0.00001f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void MaterialXCandidateMatchesOriginalMathOnDesktop(int pattern)
        {
            foreach (float filtering in new[] { 0f, 1f })
            foreach (bool threshold in new[] { false, true })
            {
                var input = new Color(0.23f, 0.57f, 0.81f, 1);
                var expected = Render(input, 0.85f, 0.8f, steps: 4, pattern: pattern,
                    filtering: filtering, size: 64, scale: 7, rotation: 27,
                    uvOffset: new Vector2(-2.3f, 1.4f), debug: threshold);
                var actual = Render(input, 0.85f, 0.8f, steps: 4, pattern: pattern,
                    filtering: filtering, size: 64, scale: 7, rotation: 27,
                    uvOffset: new Vector2(-2.3f, 1.4f), debug: threshold, portable: true);
                Assert.That(actual.All(c => float.IsFinite(c.r) && float.IsFinite(c.g) && float.IsFinite(c.b)), Is.True);
                Assert.That(expected.Zip(actual, (a, b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)).Average(),
                    Is.LessThan(0.0001f), "Filtering=" + filtering + ", threshold=" + threshold);
            }
            foreach (float tone in new[] { 0f, 1f })
                Assert.That(Render(new Color(tone, tone, tone, 1), 1, 1, pattern: pattern,
                    size: 32, portable: true).All(c => c.r == tone && c.g == tone && c.b == tone), Is.True);
            var unchanged = Render(new Color(0.3f, 0.4f, 0.5f, 1), 0, 1, pattern: pattern, portable: true);
            Assert.That(unchanged.All(c => Mathf.Abs(c.r - 0.3f) < 0.00001f), Is.True);
        }

        private static Color[] Render(Color input, float strength, float mask, float steps = 8, float amount = 1, bool debug = false,
            int pattern = 0, float rotation = 0, float filtering = 0, int size = 4, float scale = 4,
            float armRatio = 0.35f, bool legacy = false, Vector2 uvOffset = default, bool portable = false)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
                Assert.Ignore("Dither GPU checks require an active graphics device with float render targets.");
            var shader = Shader.Find("Hidden/FuzzPhyte/Tests/DitherProbe");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            try
            {
                material.SetVector("_Color", input);
                material.SetFloat("_Strength", strength); material.SetFloat("_Mask", mask);
                material.SetFloat("_Scale", scale); material.SetFloat("_Steps", steps);
                material.SetFloat("_Amount", amount); material.SetFloat("_Bias", 0);
                material.SetFloat("_Pattern", pattern); material.SetFloat("_Rotation", rotation);
                material.SetFloat("_ArmRatio", armRatio); material.SetFloat("_Filtering", filtering);
                material.SetFloat("_Legacy", legacy ? 1 : 0); material.SetVector("_UVOffset", uvOffset);
                material.SetFloat("_Portable", portable ? 1 : 0);
                material.SetFloat("_DebugThreshold", debug ? 1 : 0);
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, size, size), 0, 0); readback.Apply();
                Assert.That(ShaderUtil.GetShaderMessages(shader), Is.Empty);
                return readback.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readback); Object.DestroyImmediate(material);
            }
        }
    }
}
