// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using Object = UnityEngine.Object;

    public class FPDitherLightingTests
    {
        private const string ShaderName = "FuzzPhyte/Dither/FP_DitherLightingLit";

        [Test]
        public void LightingMaterialHasOptInControlsAndSharedPassLayout()
        {
            var shader = Shader.Find(ShaderName);
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var property in new[] { "_DitherStrength", "_LightingDitherStrength", "_ShadowDitherStrength" })
                    Assert.That(material.GetFloat(property), Is.Zero, property);
                foreach (var name in new[] { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals", "Meta", "MotionVectors" })
                {
                    int pass = material.FindPass(name);
                    Assert.That(pass, Is.GreaterThanOrEqualTo(0), name);
                    ShaderUtil.CompilePass(material, pass, true);
                }
                Assert.That(shader.FindPassTagValue(material.FindPass("ForwardLit"), new ShaderTagId("LightMode")).name,
                    Is.EqualTo("UniversalForwardOnly"));
                Assert.That(ShaderUtil.GetShaderMessages(shader), Is.Empty);
            }
            finally { ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(material); }
        }

        [TestCase(RenderingMode.Forward)]
        [TestCase(RenderingMode.ForwardPlus)]
        [TestCase(RenderingMode.Deferred)]
        public void DisabledLightingMatchesLitAndStyleOnlyChangesSelectedReceiver(RenderingMode mode)
        {
            using (var rig = new LightingRig(mode))
            {
                rig.Sun.shadows = LightShadows.None;
                rig.Shape(PrimitiveType.Sphere, new Vector3(-1.4f, 1, 0), rig.Standard);
                var right = rig.Shape(PrimitiveType.Sphere, new Vector3(1.4f, 1, 0), rig.Standard);
                var baseline = rig.Capture();
                right.sharedMaterial = rig.Styled;
                var disabled = rig.Capture();
                Assert.That(Difference(baseline, disabled), Is.LessThan(0.001), "Zero strengths should preserve Lit.");
                rig.Styled.SetFloat("_LightingDitherStrength", 1);
                var enabled = rig.Capture();
                Assert.That(Difference(disabled, enabled, false), Is.LessThan(0.0001), "Standard left receiver changed.");
                Assert.That(Difference(disabled, enabled, true), Is.GreaterThan(0.001), "Selected right receiver did not change.");
                Assert.That(enabled.Average(c => c.grayscale), Is.InRange(
                    disabled.Average(c => c.grayscale) * 0.65, disabled.Average(c => c.grayscale) * 1.35),
                    "Dithering should redistribute coverage, not lose the selected object's illumination.");
                rig.Styled.SetFloat("_DitherMaskStrength", 0);
                Assert.That(Difference(disabled, rig.Capture()), Is.LessThan(0.0001), "Mask zero must disable all style.");
            }
        }

        [TestCase(LightType.Directional, false)]
        [TestCase(LightType.Directional, true)]
        [TestCase(LightType.Point, false)]
        [TestCase(LightType.Spot, false)]
        public void SharedLightsCastAcrossStandardAndStyledMaterials(LightType type, bool screenSpaceShadows)
        {
            using (var rig = new LightingRig(RenderingMode.Forward, screenSpaceShadows))
            {
                rig.Sun.type = type;
                if (type != LightType.Directional)
                {
                    rig.Sun.transform.position = new Vector3(-2, 5, -3);
                    rig.Sun.transform.LookAt(new Vector3(0, 0, 0));
                    rig.Sun.range = 20;
                    rig.Sun.spotAngle = 100;
                    rig.Sun.intensity = 18;
                }
                var floor = rig.Shape(PrimitiveType.Plane, Vector3.zero, rig.Styled);
                floor.transform.localScale = Vector3.one * 0.65f;
                var caster = rig.Shape(PrimitiveType.Cube, new Vector3(0, 1.2f, 0), rig.Standard);
                caster.transform.localScale = new Vector3(1.2f, 1.5f, 1.2f);
                caster.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                var shadow = rig.Capture();
                caster.shadowCastingMode = ShadowCastingMode.Off;
                var clear = rig.Capture();
                Assert.That(Difference(clear, shadow), Is.GreaterThan(0.002), type + " shadow was not received.");
                caster.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                rig.Styled.SetFloat("_ShadowDitherStrength", 1);
                // A partial-strength shadow has broad intermediate coverage,
                // independent of desktop soft-shadow kernel width.
                rig.Sun.shadowStrength = 0.65f;
                var styledShadow = rig.Capture();
                rig.Styled.SetFloat("_ShadowDitherStrength", 0);
                var smoothShadow = rig.Capture();
                Assert.That(Difference(styledShadow, smoothShadow), Is.GreaterThan(0.001), type + " received-shadow style absent.");

                floor.sharedMaterial = rig.Standard;
                caster.sharedMaterial = rig.Styled;
                var standardReceiver = rig.Capture();
                rig.Styled.SetFloat("_ShadowDitherStrength", 1);
                rig.Styled.SetFloat("_LightingDitherStrength", 1);
                Assert.That(Difference(standardReceiver, rig.Capture()), Is.LessThan(0.0001), "Caster style leaked onto standard receiver.");
                caster.shadowCastingMode = ShadowCastingMode.Off;
                Assert.That(Difference(standardReceiver, rig.Capture()), Is.GreaterThan(0.001), "Styled material failed to cast onto standard receiver.");
            }
        }

        [TestCase(LightType.Point, RenderingMode.Forward)]
        [TestCase(LightType.Spot, RenderingMode.Forward)]
        [TestCase(LightType.Point, RenderingMode.ForwardPlus)]
        [TestCase(LightType.Spot, RenderingMode.ForwardPlus)]
        public void AdditionalLightsIlluminateAndDitherSelectedMaterial(LightType type, RenderingMode mode)
        {
            using (var rig = new LightingRig(mode))
            {
                rig.Sun.enabled = false;
                var light = rig.NewLight(type);
                light.transform.position = new Vector3(-2, 3, -3);
                light.transform.LookAt(Vector3.up);
                light.range = 12; light.spotAngle = 100; light.intensity = 12;
                rig.Shape(PrimitiveType.Sphere, Vector3.up, rig.Styled);
                var smooth = rig.Capture();
                light.enabled = false;
                Assert.That(Difference(smooth, rig.Capture()), Is.GreaterThan(0.0005), "Additional light absent.");
                light.enabled = true;
                rig.Styled.SetFloat("_LightingDitherStrength", 1);
                var styled = rig.Capture();
                Assert.That(Difference(smooth, styled), Is.GreaterThan(0.001), "Additional light style absent.");
                Assert.That(styled.Average(c => c.grayscale), Is.InRange(
                    smooth.Average(c => c.grayscale) * 0.5, smooth.Average(c => c.grayscale) * 1.5),
                    "Additional light energy disappeared or increased unexpectedly.");
            }
        }

        [Test]
        public void CutoutShadowSilhouetteIgnoresLightingStyle()
        {
            using (var rig = new LightingRig(RenderingMode.Forward))
            {
                var floor = rig.Shape(PrimitiveType.Plane, Vector3.zero, rig.Standard);
                floor.transform.localScale = Vector3.one * 0.65f;
                var caster = rig.Shape(PrimitiveType.Cube, new Vector3(0, 1.2f, 0), rig.Styled);
                caster.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                var opaque = rig.Capture();
                var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
                try
                {
                    texture.SetPixels(new[] { Color.clear, Color.white }); texture.Apply();
                    rig.Styled.SetTexture("_BaseMap", texture);
                    rig.Styled.SetFloat("_AlphaClip", 1); rig.Styled.EnableKeyword("_ALPHATEST_ON");
                    var cutout = rig.Capture();
                    Assert.That(Difference(opaque, cutout), Is.GreaterThan(0.0005), "Cutout did not affect shadow silhouette.");
                    rig.Styled.SetFloat("_LightingDitherStrength", 1); rig.Styled.SetFloat("_ShadowDitherStrength", 1);
                    Assert.That(Difference(cutout, rig.Capture()), Is.LessThan(0.0001));
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }

        [TestCase("_DitherStrength")]
        [TestCase("_LightingDitherStrength")]
        [TestCase("_ShadowDitherStrength")]
        public void CustomStampChangesOnlyEnabledReceiverChannel(string control)
        {
            using (var rig = new LightingRig(RenderingMode.Forward))
            {
                rig.Styled.SetFloat("_DitherPattern", 7);
                rig.Styled.SetFloat("_DitherColorSteps", 2);
                rig.Styled.SetTexture("_DitherStamp", AssetDatabase.LoadAssetAtPath<Texture2D>(FPDitherStampTests.StampPath));
                rig.Shape(PrimitiveType.Sphere, Vector3.up, rig.Styled);
                var floor = rig.Shape(PrimitiveType.Plane, Vector3.zero, rig.Styled);
                floor.transform.localScale = Vector3.one * 0.6f;
                var caster = rig.Shape(PrimitiveType.Cube, new Vector3(0, 2, 0), rig.Standard);
                caster.transform.localScale = new Vector3(3, 0.5f, 2);
                caster.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                rig.Sun.shadowStrength = 0.65f;
                var off = rig.Capture();
                rig.Styled.SetFloat(control, 1);
                Assert.That(Difference(off, rig.Capture()), Is.GreaterThan(0.001), control);
                rig.Styled.SetFloat("_DitherMaskStrength", 0);
                Assert.That(Difference(off, rig.Capture()), Is.LessThan(0.0001), "Master mask must restore Lit.");
            }
        }

        [TestCase("PolySpatial")]
        [TestCase("PolySpatialFoliage")]
        public void MaterialXCandidateUsesStandardLightingAndReceiverLocalAlbedo(string family)
        {
            using (var rig = new LightingRig(RenderingMode.Forward, false, "FuzzPhyte/Dither/FP_" + family + "DitherLit"))
            {
                rig.Shape(PrimitiveType.Sphere, new Vector3(-1.4f, 1, 0), rig.Standard);
                var right = rig.Shape(PrimitiveType.Sphere, new Vector3(1.4f, 1, 0), rig.Standard);
                var floor = rig.Shape(PrimitiveType.Plane, Vector3.zero, rig.Standard);
                floor.transform.localScale = Vector3.one * 0.7f;
                var baseline = rig.Capture();
                right.sharedMaterial = rig.Styled;
                var disabled = rig.Capture();
                Assert.That(Difference(baseline, disabled), Is.LessThan(0.001), "Zero strength must preserve Lit.");
                rig.Styled.SetFloat("_DitherStrength", 1);
                rig.Styled.SetFloat("_DitherColorSteps", 2);
                var enabled = rig.Capture();
                Assert.That(Difference(disabled, enabled, true), Is.GreaterThan(0.001));
                Assert.That(Difference(disabled, enabled, false), Is.LessThan(0.0001), "Standard receiver/cast shadow changed.");
                rig.Styled.SetFloat("_DitherMaskStrength", 0);
                Assert.That(Difference(disabled, rig.Capture()), Is.LessThan(0.0001));
                rig.Sun.shadows = LightShadows.None;
                Assert.That(Difference(disabled, rig.Capture()), Is.GreaterThan(0.0005), "Conventional shadow was absent.");
            }
        }

        private static double Difference(Color[] a, Color[] b, bool? rightHalf = null)
        {
            double sum = 0; int count = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (rightHalf.HasValue && (i % LightingRig.Size >= LightingRig.Size / 2) != rightHalf.Value) continue;
                sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
                count += 3;
            }
            return sum / count;
        }

        // Owned objects and temporary pipeline: never save, clear, or replace
        // the user's scene/renderer assets. Dispose restores changed globals.
        private sealed class LightingRig : IDisposable
        {
            public const int Size = 256;
            public readonly Material Standard, Styled;
            public readonly Light Sun;
            private readonly GameObject root;
            private readonly AmbientMode ambientMode;
            private readonly Color ambientLight;
            private readonly float ambientIntensity, reflectionIntensity;
            private readonly bool fog;
            private readonly RenderPipelineAsset originalQuality;
            private readonly UniversalRenderPipelineAsset pipeline;
            private readonly UniversalRendererData renderer;
            private readonly ScriptableRendererFeature shadowFeature;
            private readonly Camera camera;
            private readonly Light[] existingLights;
            private readonly bool async;
            private bool warmed;

            public LightingRig(RenderingMode mode, bool screenSpaceShadows = false, string styledShader = ShaderName)
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
                originalQuality = QualitySettings.renderPipeline;
                async = ShaderUtil.allowAsyncCompilation;
                ambientMode = RenderSettings.ambientMode;
                ambientLight = RenderSettings.ambientLight;
                ambientIntensity = RenderSettings.ambientIntensity;
                reflectionIntensity = RenderSettings.reflectionIntensity;
                fog = RenderSettings.fog;
#if UNITY_6000_6_OR_NEWER
                existingLights = Object.FindObjectsByType<Light>().Where(l => l.enabled).ToArray();
#else
                existingLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled).ToArray();
#endif
                // Own a root in Test Runner's scene; do not trigger scene-save UI.
                root = new GameObject("FP Dither lighting test") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    foreach (var light in existingLights) light.enabled = false;
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = Color.black;
                    RenderSettings.ambientIntensity = 0;
                    RenderSettings.reflectionIntensity = 0;
                    RenderSettings.fog = false;
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    renderer.renderingMode = mode;
                    if (screenSpaceShadows)
                    {
                        // URP's built-in feature is internal, so instantiate its
                        // installed type rather than take an Editor assembly dependency.
                        var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceShadows", true);
                        shadowFeature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                        renderer.rendererFeatures.Add(shadowFeature);
                    }
                    pipeline = UniversalRenderPipelineAsset.Create(renderer);
                    var settings = new SerializedObject(pipeline);
                    settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                    settings.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                    settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
                    settings.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    pipeline.shadowDistance = 30;
                    pipeline.mainLightShadowmapResolution = 2048;
                    pipeline.additionalLightsShadowmapResolution = 2048;
                    QualitySettings.renderPipeline = pipeline;
                    ShaderUtil.allowAsyncCompilation = false;
                    Standard = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    Styled = new Material(Shader.Find(styledShader));
                    foreach (var material in new[] { Standard, Styled })
                    {
                        material.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.55f, 1));
                        material.SetFloat("_Smoothness", 0.2f);
                        material.SetFloat("_Metallic", 0);
                    }
                    Styled.SetFloat("_DitherPattern", 2);
                    Styled.SetFloat("_DitherScale", 12);
                    Styled.SetFloat("_LightingSteps", 2);
                    Styled.SetFloat("_DitherFiltering", 0);
                    camera = new GameObject("Dither lighting test camera").AddComponent<Camera>();
                    camera.transform.SetParent(root.transform);
                    camera.enabled = false;
                    camera.transform.position = new Vector3(0, 4.5f, -8);
                    camera.transform.LookAt(new Vector3(0, 0.7f, 0));
                    camera.orthographic = !screenSpaceShadows; camera.orthographicSize = 3.3f;
                    camera.fieldOfView = 40;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                    camera.allowHDR = true; camera.allowMSAA = false; camera.cullingMask = 1 << 31;
                    camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                    Sun = NewLight(LightType.Directional);
                    Sun.transform.rotation = Quaternion.Euler(45, -35, 0);
                    Sun.intensity = 1;
                    Sun.shadows = LightShadows.Soft;
                    Sun.shadowBias = 0.02f; Sun.shadowNormalBias = 0.1f;
                }
                catch { Dispose(); throw; }
            }

            public Light NewLight(LightType type)
            {
                var light = new GameObject("Test " + type).AddComponent<Light>();
                light.transform.SetParent(root.transform);
                light.type = type; light.cullingMask = 1 << 31;
                light.gameObject.AddComponent<UniversalAdditionalLightData>();
                return light;
            }

            public Renderer Shape(PrimitiveType type, Vector3 position, Material material)
            {
                var go = GameObject.CreatePrimitive(type);
                go.transform.SetParent(root.transform);
                go.layer = 31; go.transform.position = position;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                var result = go.GetComponent<Renderer>(); result.sharedMaterial = material;
                return result;
            }

            public Color[] Capture()
            {
                var target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                var readback = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true);
                var previous = RenderTexture.active;
                try
                {
                    camera.targetTexture = target;
                    if (!warmed)
                    {
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        warmed = true;
                    }
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); readback.Apply();
                    Assert.That(ShaderUtil.GetShaderMessages(Styled.shader), Is.Empty);
                    var pixels = readback.GetPixels();
                    Assert.That(pixels.All(c => float.IsFinite(c.r) && float.IsFinite(c.g) && float.IsFinite(c.b)), Is.True);
                    return pixels;
                }
                finally
                {
                    camera.targetTexture = null; RenderTexture.active = previous;
                    Object.DestroyImmediate(readback); RenderTexture.ReleaseTemporary(target);
                }
            }

            public void Dispose()
            {
                QualitySettings.renderPipeline = originalQuality;
                ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(root);
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.reflectionIntensity = reflectionIntensity;
                RenderSettings.fog = fog;
                foreach (var light in existingLights) if (light) light.enabled = true;
                Object.DestroyImmediate(Standard); Object.DestroyImmediate(Styled);
                Object.DestroyImmediate(pipeline); Object.DestroyImmediate(renderer);
                Object.DestroyImmediate(shadowFeature);
            }
        }
    }
}
