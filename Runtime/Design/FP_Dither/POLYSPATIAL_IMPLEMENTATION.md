# PolySpatial surface-material implementation

Status, 2026-09-26: **portable candidate implemented and desktop-tested; MaterialX export and visionOS rendering not yet validated.** PolySpatial is absent from this project's manifest. No XR package, build target or renderer setting was changed. The original URP materials remain intact.

## First implementation: styled albedo with conventional lighting

Two new material shaders are available:

- `FuzzPhyte/Dither/FP_PolySpatialDitherLit`: opaque Lit surface.
- `FuzzPhyte/Dither/FP_PolySpatialFoliageDitherLit`: Lit surface with fixed alpha clipping; Base Map alpha × Base Color alpha controls coverage independently of dithering.

Both use `Shaders/SubGraphs/FP_DitherMaterialXCore.shadersubgraph` and `Shaders/Includes/FP_DitherMaterialX.hlsl`. Strength defaults to zero, Pattern to Bayer and Scale to 24. Assign these materials only to selected objects. Separate material assets provide independent choices without a global effect or a runtime manager.

Base Map, Base Color, Normal Map, Metallic, Smoothness, Strength, Scale, Color Steps, Amount, Bias, Mask, Pattern, Rotation, Plus Arm Ratio and Filtering retain the established property names. The seven analytic choices remain Bayer, Squares, Dots, Lines, Plus, Diamond and Star. Coordinates are raw UV0, matching the original albedo graphs. No independent light/shadow style controls appear on these graphs.

The new function expresses the existing math using only float assignments, supported intrinsics and conditional expressions. It contains no includes, global declarations, array indexing, loops, statement branches, early returns, half overloads or URP lighting calls. Circle expressions have safe domains even when circles are not selected. The subgraph stays fragment-only with float precision. This conforms to the *documented source subset*; it is not proof that the package converter has accepted the graph. [PolySpatial 3.1 Custom Function conversion](https://docs.unity3d.com/Packages/com.unity.polyspatial.visionos@3.1/manual/CustomFunctionNode.html).

Selection may become a MaterialX expression network that evaluates unselected shapes. There are zero authored pattern keywords or dither texture samples, but equivalent appearance does not imply equivalent execution cost. Profile this first; if selection is expensive, use fixed-pattern graphs that allow unused calculations to be removed. Do not claim mobile optimization from desktop arithmetic comparisons.

## Lighting and interaction with ordinary objects

On desktop, Shader Graph generates ordinary URP Lit lighting/shadow passes. On the intended PolySpatial path, Lit targets use native visionOS lighting unless an explicit PolySpatial lighting extension changes that behavior. Native lighting supports directional/spot shadows. The material changes albedo before lighting; it does not read, replace or quantize native shadow visibility. Ordinary and styled materials can therefore share the scene's lights, with conventional cast silhouettes. Native cross-material receiving/casting still needs device verification. [PolySpatial lighting](https://docs.unity3d.com/Packages/com.unity.polyspatial.visionos@3.1/manual/PolySpatialLighting.html).

Mobile planning now assumes **unshadowed point lights by default**. The new comparison's optional point light explicitly has shadows off. Use a shadowed directional light first; add shadowed spots only after profiling. This does not remove point-shadow support from the existing URP shader or change the user's pipeline.

## Example and desktop evidence

Open `Examples/PolySpatial/FP_DitherPolySpatial.unity` for four columns: standard URP, the existing URP dither graph, the portable Lit candidate and a portable cutout sphere. The two middle columns use matching surface controls. A full-strength directional shadow shows that native shadow coverage is separate from the surface pattern. This is a desktop authoring scene, **not** a configured PolySpatial application/Volume Camera sample.

After the change, **176/176 package Editor tests passed**, with no failures or skips. The 12 added cases cover:

- Two graph property/dependency/import contracts, including opt-in strength defaults.
- Seven GPU comparisons against the original production core across rotated negative UVs, filtering on/off, RGB and threshold outputs, exact black/white endpoints and zero-strength identity. Mean summed RGB difference is bounded below 0.0001 in the exercised cases.
- One additional actual-graph cutout-coverage regression across all seven patterns.
- Two full scene-render checks for zero-strength Lit parity, selected-material albedo changes, mask identity and conventional shadows.

These tests execute on Windows DX12 with Unity 6000.6 / URP 17.6. They do not run the MaterialX parser, RealityKit or stereo rendering. Shader Graph assets and metadata were authored/imported through the connected Editor; existing asset identities were preserved.

## Remaining implementation and acceptance

1. **Establish the target PolySpatial integration.** Select the visionOS host project and supported PolySpatial/Editor versions. Add the candidate graphs to that project or deliberately add its package dependencies here. This package currently has no mandatory PolySpatial dependency.
2. **Run MaterialX conversion.** Check all graph/node diagnostics with the selected PolySpatial version, then use Play To Device / an actual visionOS build. Verify the exported graph rather than treating the normal URP preview as evidence. Repair subset/conversion differences in this separate function.
3. **Check native rendering.** Styled and ordinary receivers/casters, directional and spot shadows, normal maps, metallic surfaces, alpha cutouts, skinned animation and transformed volumes. Avoid inadvertently adding a second lighting contribution via emission or the PolySpatial Lighting Data Extension.
4. **Measure stereo and cost.** Both eyes, head motion, foveation, distance, tree LODs and baseline-versus-pattern GPU measurements. Bayer deliberately retains its original unfiltered behavior; analytic shapes retain derivative smoothing/fade. Neither is accepted for headset use yet.
5. **Port custom stamps separately.** Keep the analytic path texture-free. A stamp graph can consume calibrated rank data through supported sample nodes, but sampler addressing, explicit LOD and coverage must be verified after MaterialX conversion. The existing lighting shader's global texture/sampler declarations cannot simply be included in a Custom Function.
6. **Prototype stylized lighting only after the surface path passes.** The PolySpatial Lighting Node exposes a combined color that can be processed and sent to an Unlit output, avoiding a second native lighting evaluation. That is a different look/control model from our URP per-light diffuse and shadow strengths. Independent native shadow attenuation is not exposed by the documented interface; exact patterned received-shadow parity remains experimental.

No custom global shadow-map renderer or shadowed-point requirement is introduced. The Lit surface path is the baseline; a later lighting prototype remains a separate material option.
