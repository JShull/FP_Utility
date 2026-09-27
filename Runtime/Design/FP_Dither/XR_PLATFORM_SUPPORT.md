# Dither lighting and shadows: XR support

Research date: 2026-09-26. This is a platform planning matrix, not a device certification. Current package rendering tests run in Unity 6000.6 / URP 17.6 on Windows DX12. No Metal, Android, stereo or headset performance result has been recorded.

## Rendering paths

| Target | Platform capability and implications for this package | Acceptance still required |
| --- | --- | --- |
| Apple Vision Pro, Unity-rendered Metal / Compositor Services | Unity documents URP for Metal-based immersive apps, including full or mixed immersion. This is the candidate path for the existing HLSL shaders, shared URP lighting and per-material dithered receiving surfaces. Mixed immersion alone does not imply PolySpatial. | visionOS build on the supported Apple toolchain; shader compilation; both eyes; foveation; animation; shadows; measured device cost |
| Apple Vision Pro, PolySpatial / RealityKit | Materials are converted to MaterialX. Arbitrary ShaderLab lighting shaders are not supported. Custom Function conversion supports only a subset of HLSL; our original Shader Graphs are therefore not automatically portable either. A separately validated MaterialX-compatible albedo graph is needed; parity with our custom URP per-light/shadow loop is not promised. | Selected PolySpatial/visionOS versions, conversion audit, supported-node implementation and device rendering |
| Meta Quest / Horizon OS, Unity URP | Native Unity URP is the intended route. Directional, point and spot lighting/shadows are available through URP; per-material receiver styling remains local to our shader. Forward and Forward+ require a workload comparison. | Android compiler, XR provider, stereo instancing/multiview, foveation, target refresh rate and device profiling |
| Meta VR Glasses, announced for Spring 2027 | Official Unity requirements now list the device. Meta documents Quest-based FoV simulation and eye-tracked foveation with runtime capability/permission checks. This supports preparing a Unity route now; it does not establish our shader's performance on the new device. | Confirm this is the user's 2027 target, supported SDK/version setup, device readiness checks and real-device acceptance |

Sources: [Unity Metal-based apps](https://docs.unity.cn/Packages/com.unity.polyspatial.visionos@2.0/manual/MetalApps.html), [PolySpatial 3.1 material support](https://docs.unity.cn/Packages/com.unity.polyspatial.visionos@3.1/manual/Materials.html), [Custom Function conversion limitations](https://docs.unity.cn/Packages/com.unity.polyspatial.visionos@2.0/manual/CustomFunctionNode.html), [Meta Unity requirements](https://developers.meta.com/horizon/documentation/unity/unity-development-requirements/), [Meta VR Glasses announcement](https://about.fb.com/news/2026/09/introducing-meta-vr-glasses-3d-movies-immersive-live-sports-100-grams/). Versioned PolySpatial pages describe those versions, not an installed or tested integration in this project.

PolySpatial's documented native lighting settings allow point/spot/directional lights and **spot/directional shadows**. They do not list native point-light shadows. Its lighting-node/data-extension route supports up to four dynamic lights; do not confuse that limit with the native RealityKit lighting path. Directional shadows use a maximum-distance setting and do not match URP cascades. Changing the project's URP shadow atlas cannot make RealityKit run our custom shadow sampler. See [lighting support (2.1)](https://docs.unity.cn/Packages/com.unity.polyspatial.visionos@2.1/manual/PolySpatialLighting.html) and [settings (3.1)](https://docs.unity.cn/Packages/com.unity.polyspatial.visionos@3.1/manual/PolySpatialSettings.html).

## Point and spot shadow controls in the Unity URP path

1. Select the URP asset actually used by the target's current Quality level (a Quality override can replace Graphics settings).
2. Enable Additional Lights **Per Pixel**, then additional-light **Cast Shadows**. The current FP_Synthetic asset was observed with additional shadows disabled; this research does not change it.
3. Enable shadows on each relevant Light. Tune intensity, range/spot angle, shadow strength, bias, normal bias, near plane and resolution tier for that scene.
4. Tune the additional-light shadow atlas/resolution tiers and shadow distance. Soft-shadow support/quality also comes from URP and the Light. Check actual atlas allocation in Frame Debugger.
5. Set receiver material Shadow Dither Strength independently of Light shadow quality. A fully lit value of 1 or fully blocked value of 0 stays exact. Dots appear in intermediate visibility, such as soft transitions or partial Light shadow strength.

Point and spot lights share the punctual-light atlas. A spot needs one shadow map; a point needs six faces, so shadowed points can be disproportionately expensive. Dithering does not reduce those caster renders or shadow-map lookups. Start with a shadowed directional light and add a small number of shadowed spots only after profiling; reserve shadowed points for demonstrated needs. This is a proposed workload policy, not a measured device limit. [Unity shadow resolution](https://docs.unity.com/en-us/engine/6000.7/manual/lighting-overview/lighting/shadows-in-urp/shadow-resolution-urp), [shadow optimization](https://docs.unity.com/en-us/engine/6000.7/manual/lighting-overview/lighting/shadows-in-urp/shadows-optimization).

## Renderer and foveation decisions

- Meta recommends Forward for low dynamic-light counts/small meshes and measuring Forward+ for heavier light workloads. Unity 6000.3+ includes Quest Forward+ optimizations; don't install an older custom URP fork into this 6000.6 project. Reflection-probe settings still matter. [Meta Forward+ guide](https://developers.meta.com/horizon/documentation/unity/unity-forward-plus-rendering/).
- Do not assume our custom direct-light loop receives every optimization added to stock Lit. Audit the installed URP helpers and compile generated target variants when building; reusing helpers alone is not proof of equal cost.
- Our patterns use UVs and preserve stock URP pass wrappers, but screen-space shadow/GI inputs and derivative filtering still require foveation tests. Test peripheral and gaze-transition shimmer as well as ordinary head motion. [Unity custom shader foveation](https://docs.unity.com/en-us/engine/6000.6/manual/xr/graphics/foveated-rendering/custom-shaders).
- For Meta VR Glasses, check ETFR availability and permissions at runtime. The same foveation level can behave differently on different devices. FoV simulation is useful for framing and legibility, not a GPU performance substitute. [Meta foveation](https://developers.meta.com/horizon/essentials/fixed-foveated-rendering/), [FoV simulation](https://developers.meta.com/horizon/documentation/unity/in-headset-fov-simulation/), [device readiness](https://developers.meta.com/horizon/documentation/unity/device-readiness/).
- Preserve the existing desktop renderer as an authoring configuration. Choose target-specific URP assets only after the target app mode and quality budget are agreed. No XR packages, platform switches or renderer settings were changed for this research.

## Work queue after desktop lighting

Update: the first separate opaque/cutout surface candidates have been implemented and desktop-tested. See [PolySpatial implementation](POLYSPATIAL_IMPLEMENTATION.md). Their presence does not complete the platform acceptance column above. Unshadowed point lights are the mobile default for planning; the new comparison explicitly disables point shadows.

1. **Custom stamp vertical slice:** one calibrated authored motif on the opt-in lighting shader, import rules, example and GPU coverage/off/repetition tests. Keep original analytic graphs unchanged. See README for current implementation and evidence.
2. **Production lighting evidence (L3):** representative normal/metallic materials and animated characters; colored overlapping lights; mixed/baked light/shadow-mask modes; cookies/layers; SSAO; reflection probes; soft-shadow/cascade transitions. Compilation of variants alone does not complete these items.
3. **Resolve the orthographic + screen-space-shadow capture discrepancy** before claiming that combination; perspective desktop evidence already exists.
4. **Platform implementation:** choose Apple Metal versus PolySpatial scope; establish Meta SDK/build targets and compile the native shader variants. A PolySpatial implementation is a separate material path, not a toggle in the existing lighting shader.
5. **XR acceptance (L4):** actual headset, refresh rate, both eyes, foveation on/off, slow/fast motion, near/far, all tree LODs and animation. Record baseline versus style-on GPU frame time, variance and sample count with identical light/shadow workload. Keep the proposed 0.25 ms incremental budget marked unmeasured until then.

Custom stamps do not complete the broader XR/production checklist. Cross-hatching, ink-color/polarity controls, packed material maps, transparent rendering and space-warp support remain separate future scope rather than implied capabilities.
