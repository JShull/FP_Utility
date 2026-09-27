# FP Dither — URP surface materials

Editor-validated implementation of the [design](FP_URP_XR_Dithered_Surface_Rendering.md), including analytic shapes and an opt-in lighting/shadow extension. The original Lit graphs modify linear albedo before standard URP lighting. The separate lighting shader can also dither direct diffuse shading and received shadows per material. Both share texture-free Bayer 4x4 and square/dot/line/plus/diamond/star patterns. No Renderer Feature, full-screen effect, dither texture or patterned alpha discard is required.

## Per-material lighting and shadows

For Apple Vision Pro versus Meta Quest / Spring 2027 Meta VR Glasses, see [XR platform support and remaining work](XR_PLATFORM_SUPPORT.md). Unity/Metal and PolySpatial/RealityKit are different shader paths; desktop success does not establish either headset's compatibility or performance.

The first **PolySpatial surface candidates** now exist: `FP_PolySpatialDitherLit` and `FP_PolySpatialFoliageDitherLit`, with a separate shared function written for the documented MaterialX conversion subset. They provide the seven procedural albedo patterns with conventional lighting/shadows, not our URP lighting interception. See [implementation and validation boundaries](POLYSPATIAL_IMPLEMENTATION.md) and `Examples/PolySpatial/FP_DitherPolySpatial.unity`. **176/176 package Editor tests passed** after this addition; actual MaterialX export and visionOS rendering remain untested because PolySpatial is not installed here.

Select **FuzzPhyte / Dither / FP_DitherLightingLit** on a material to use the lighting extension. This is a separate hand-authored URP shader; the three original Shader Graph assets and their existing materials keep their original behavior. The new shader supports opaque materials and ordinary texture-alpha cutouts in one material interface. Base Map, Base Color, Normal Map, Metallic, Smoothness and the existing dither property names are shared. Check values deliberately when switching a material's shader.

| Control | Default | Purpose |
| --- | --- | --- |
| Albedo Dither Strength (`_DitherStrength`) | 0 | Existing pre-lighting RGB quantization. |
| Diffuse Lighting Dither Strength (`_LightingDitherStrength`) | 0 | Quantizes each real-time light's normal-facing factor into patterned diffuse coverage. |
| Received Shadow Dither Strength (`_ShadowDitherStrength`) | 0 | Quantizes each light's received shadow visibility into patterned coverage. Also shadows specular highlights. |
| Diffuse Lighting Levels (`_LightingSteps`) | 3 | Levels including unlit and fully facing the light; minimum 2. |
| Received Shadow Levels (`_ShadowSteps`) | 2 | Visibility levels including fully shadowed and fully visible; minimum 2. |
| Dither Mask (`_DitherMaskStrength`) | 1 | Common blend mask for all three effects; zero disables them. |
| Alpha Cutout (`_AlphaClip`) / Alpha Cutoff (`_Cutoff`) | Off / 0.5 | Conventional texture-alpha clipping in color, depth and shadow passes. Toggle uses standard `_ALPHATEST_ON`. |

The shared Pattern, Scale, Rotation, Plus Arm Ratio, Amount, Bias and Filtering controls drive the same seven procedural motifs, plus **CustomStamp (7)** on this lighting shader. Three strengths are independent: a material can use lighting only, shadows only, albedo only, any combination, or no style. Distinct material assets (or per-renderer MaterialPropertyBlocks) allow different objects to choose different settings. Editing a shared material affects every renderer using it.

**The receiver owns the style.** A standard URP Lit caster can cast a patterned shadow on this material. This material can cast a conventional shadow on a standard URP receiver. Cast silhouettes remain normal mesh/texture-alpha silhouettes, without perforating the shared shadow map. A dithered caster does not force its pattern onto other objects. Light color/intensity, range, geometry, shadow strength and shadow resolution remain shared URP scene inputs.

Diffuse bands operate on `saturate(N dot L)` after normal mapping. Shadow bands operate on URP's sampled shadow attenuation. HDR light color and distance/spot falloff remain continuous; we do not clamp the final lit RGB into 0–1. Specular highlights retain URP's BRDF response, while their received-shadow attenuation can be stylized. Ambient/probe/lightmap illumination, reflections and fog remain continuous. Vertex additional lighting remains URP's ordinary fallback; select **Per Pixel** additional lights for patterned point/spot lighting.

Fully lit and fully shadowed visibility remain exact endpoints. A perfectly hard, fully opaque shadow has no intermediate visibility to distribute into marks. Use soft shadow edges, a light's ordinary partial Shadow Strength, or diffuse lighting dithering to make broader patterned regions visible. This shader does not add a material-local shadow fill or lighten a fully shadowed region by itself.

`Examples/Lighting/FP_DitherLighting.unity` provides four matched columns: **Standard URP**, **Dithered light**, **Dithered shadow**, and **Light + shadow**. Standard-material floating blocks cast onto the different receiver materials. The shared sun uses Shadow Strength 0.65 to show broad coverage differences. Optional point/spot lights start disabled. Example materials use Dots, scale 20 and two lighting/shadow levels; adjust these as art-direction examples, not accepted XR presets. The comparison uses a perspective camera.

The currently selected `FP_Synthetic` pipeline enables main-light shadows but disables additional-light shadows. Its point/spot lights can illuminate the example, but their cast shadows require **Additional Lights > Cast Shadows** in the pipeline asset. That project setting was preserved. Automated checks use disposable pipeline/renderer instances with additional shadows enabled, then restore the original settings.

Implementation: `Shaders/FP_DitherLightingLit.shader`, `Includes/FP_DitherLightingInput.hlsl` and `Includes/FP_DitherLighting.hlsl`. The shader delegates vertex processing, normal mapping, shadow casting, depth/normals, Meta and object motion-vector passes to installed URP includes. One consistent `UnityPerMaterial` buffer is shared across passes. Custom lighting reuses URP light retrieval, BRDF, GI, shadow masks, cookies and clustered light loops. The zero-lighting/shadow-strength path calls `UniversalFragmentPBR`. Pattern selection and style strengths introduce no artistic shader keywords; conventional cutout and URP lighting variants remain.

The color pass is `UniversalForwardOnly` and the normal pass is `DepthNormalsOnly`, so the material keeps its custom lighting even in a deferred renderer. This follows [Unity's forward-only material integration](https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/introduction/urp-concepts/rendering-paths/deferred-rendering-path/make-shader-compatible-with-deferred). No GBuffer lighting replacement or project-wide feature is installed. Tests add an assembly reference to the already-used `Unity.RenderPipelines.Universal.Runtime`; no package is installed or upgraded.

The new shader uses transformed Base Map UV0 for both texture and pattern sampling; its tiling/offset also moves the pattern. Lighting filtering applies to Bayer as well as analytic modes, unlike the original albedo-only Bayer path. Pattern derivatives are evaluated before light loops. Alpha and cast shadow coverage are never dithered. The Meta pass exports the underlying material rather than baking the real-time art treatment.

### Lighting validation and limits

Desktop render checks cover zero-strength agreement with matched URP Lit materials, independent receivers, direct point/spot lighting, standard-to-stylized and stylized-to-standard cast shadows, cutout silhouettes, and Forward / Forward+ / Deferred material coexistence. The perspective Screen Space Shadows feature path is included. Tests render through `RenderPipeline.SubmitRenderRequest`, use floating-point readback and restore their temporary pipeline, lights and render settings.

On 2026-09-26, **13/13 `FPDitherLightingTests` cases and 157/157 package Editor tests passed**, with no failures or skipped cases. Brightness and finite-output assertions accompany difference comparisons so a missing/black render cannot satisfy the lighting tests. The new shader has no compiler messages; Unity's SRP Batcher compatibility query returned `OK`. These are Editor/DX12 results, not build or headset performance evidence.

An orthographic Screen Space Shadows capture in this Editor failed to show the expected shadow on **both** standard and styled receivers; equivalent perspective captures worked. Treat that combination as unresolved, not accepted. Ordinary shadow-map checks use orthographic cameras; the screen-space feature check and saved lighting comparison use perspective. No project/package-cache workaround was applied.

This is a desktop prototype for Unity 6000.6 / URP 17.6. XR stereo/head motion, mobile compilation, GPU cost, production animated characters, baked/mixed-lighting scenes, reflection-probe transitions, rendering-layer/cookie combinations, SSAO, decals and temporal effects still need representative validation. No clear coat, packed metallic/occlusion maps, emission maps, transparent blending, DOTS material overrides or XR space-warp pass is provided. Do not treat this shader as a drop-in replacement for every URP Lit feature.

## Assets and setup

- `Shaders/Includes/FP_Dither.hlsl`: shared function, float and half entry points.
- `Shaders/SubGraphs/FP_DitherCore.shadersubgraph`: reusable color/coordinate/control inputs and color/threshold outputs.
- `Shaders/Graphs/FP_CharacterDitherLit.shadergraph`: shader menu **FuzzPhyte / Dither / FP_CharacterDitherLit**.
- `Shaders/Graphs/FP_OrganicDitherLit.shadergraph`: same implementation, stronger defaults; **FuzzPhyte / Dither / FP_OrganicDitherLit**.
- `Shaders/Graphs/FP_OrganicFoliageDitherLit.shadergraph`: organic RGB effect plus ordinary texture-alpha cutout, for the supplied tree leaves. `_Cutoff` defaults to 0.5. Base Map alpha times Base Color alpha feeds the standard Alpha output; it never passes through the dither function. Cutout is fixed on in this graph, with front-face rendering matching the supplied leaf material. Standard URP alpha clipping/alpha-to-coverage applies; no dither-based alpha or LOD fading is added.
- `Examples/FP_DitherComparison.unity`: isolated desktop comparison. Left = URP Lit baseline; middle = character; right = organic. Spheres below, two-bone posed skinned capsules above. These are synthetic checks, not representative production assets or an XR rig. Rotate the `Bend Bone` to inspect deformation; no animation clip is provided.
- `Examples/FP_DitherBaseline.mat`, `FP_DitherCharacter.mat`, `FP_DitherOrganic.mat`: matched gradient texture, white tint, metallic 0, smoothness 0.35. Example pattern scale is 64 for visibility.
- `Examples/Shapes/FP_DitherShapes.unity`: labeled Bayer / Squares / Dots / Lines / Plus / Diamond / Star comparison with Lit spheres and linear tone ramps. Seven matching materials use strength 1, scale 12 and two color levels to make each motif easy to inspect. These are demonstration settings, not final character or foliage presets.

Create or duplicate a material and select one of the shaders. Assign its Base Map and optional Normal Map, then adjust the controls below. Character and organic graphs are opaque, single-sided URP Lit metallic materials; the foliage variant adds standard alpha cutout. Texture sampling and dithering use raw UV0. Standard URP shadows/depth and skinning are generated by Shader Graph. Existing renderer assets need no modifications.

Base Map, Base Color, Normal Map, Metallic and Smoothness use `_BaseMap`, `_BaseColor`, `_BumpMap`, `_Metallic` and `_Smoothness`. Copy values deliberately when replacing materials; packed PBR maps, normal strength, emission maps, texture tiling/offset, two-sided foliage and alpha-blended transparency are not implemented in this first slice. Normal maps must be imported as normal maps. There are two material texture samples (base and normal); Bayer itself adds none.

## Supplied tree integration

The user supplied `Assets/_FPTests/DitheringTests/DitheringTests.unity` and `URP_Tree_1` from the sample tree pack. All four LODs use the same opaque `Trunk` and alpha-tested `Leaf` source materials. This concrete asset requirement motivated the small foliage graph: replacing its leaves with the opaque organic graph would lose their silhouettes. The shared color core is unchanged; the original design's prohibition on using patterned alpha as the dither effect remains intact.

Two copied materials live in `Assets/_FPTests/DitheringTests/Materials`: `FP_Dither_Tree1_Trunk.mat` and `FP_Dither_Tree1_Leaf.mat`. They preserve source textures, white tint, metallic 0, smoothness 0.5, and leaf cutoff 0.5. Both start with strength 0.65, scale 64, six color levels and full ordered dither amount. All four scene LOD renderers receive these materials as prefab-instance overrides. Asset-pack source materials and prefab assets remain unchanged; fade mode stays None and screen-relative thresholds remain 0.6 / 0.3 / 0.1 / 0.

The active test scene was already dirty, so it was left unsaved with an Undo group named **Apply FP Dither tree test materials**. A separate `Assets/_FPTests/DitheringTests/DitheringTests_DitherPreview.unity` copy persists the setup. Desktop baseline/enabled captures and forced LOD0–LOD3 captures were produced, restoring automatic LOD selection afterward. Visible leaf cutouts, bark and cast shadows were checked in the Editor; this is not a head-motion, animated foliage or headset timing result. Sample-pack assets remain outside FP_Utility; no redistributable package examples depend on them.

## Dither controls

| Property | Character / organic default | Behavior |
| --- | --- | --- |
| `_DitherStrength` | 0.35 / 0.65 | Master original-to-stylized blend, 0–1. Zero returns original albedo, even if quantization is configured. |
| `_DitherScale` | 128 / 128 | Cells per UV unit, minimum 1; one Bayer tile spans four cells. Inspector range 1–1024. |
| `_DitherColorSteps` | 8 / 6 | Number of levels **including black and white**, rounded to an integer, minimum 2. Inspector range 2–64. |
| `_DitherAmount` | 1 / 1 | Independent ordered-rounding amount: 0 = nearest quantization, 1 = Bayer rounding. Does not override Strength. |
| `_DitherBias` | 0 / 0 | Rounding bias in fractions of a quantization interval, −0.5–0.5. Positive values favor brighter levels. |
| `_DitherMaskStrength` | 1 / 1 | Constant material mask, 0–1. Zero disables stylization. Connect an already-sampled mask channel to the subgraph for region masking. |
| `_DitherPattern` | Bayer (0) | Material dropdown: 0 Bayer, 1 Squares, 2 Dots, 3 Lines, 4 Plus, 5 Diamond, 6 Star. No shader keyword is added. |
| `_DitherRotation` | 0 | UV pattern rotation in degrees, −180–180; affects analytic shapes only. |
| `_DitherArmRatio` | 0.35 | Plus arm thickness/length ratio, 0.1–1. At 1 the footprint becomes square. Ignored by other patterns. |
| `_DitherFiltering` | 1 | Analytic-shape edge smoothing and minification fade, 0–1. Bayer retains its original behavior. |

The core uses `(BayerRank + 0.5) / 16`, quantizes saturated linear albedo to `steps - 1` intervals, and blends by `saturate(strength) * saturate(mask)`. Bias is zero-centered. Color steps and dither amount are independent of the master blend. Normals, alpha and PBR channels are never quantized. There is no final-lighting quantization, so lighting can produce continuous shaded tones.

Use low strength and more steps on faces; stronger settings on clothing, rocks or wood. Separate eye/teeth materials can set Strength to zero. Defaults are provisional starting points, not headset-approved presets.

## Shape authoring

Select **Pattern** on any character, organic or foliage material. Existing materials stay on Bayer; the original function and subgraph input/output identifiers are retained. To see clear marks initially, try scale 12–24, two to four color levels, and strength 0.7–1, then reduce strength for the textured asset. Scale still counts cells per UV unit, not marks per world-space meter. UV density and stretching affect apparent size and roundness.

- **Squares:** centered square ink marks grow with darker requested tone; these differ from the original Bayer threshold arrangement.
- **Dots:** circular ink marks grow and merge across cell boundaries in darker tones. The area calculation accounts for clipped circles, so dark endpoints are reachable without an unfilled corner plateau.
- **Lines:** bands thicken with darker tone. Rotation changes their direction independently of the base map. No second cross-hatch layer is implemented.
- **Plus:** a union of two bars grows first in arm length and then across cell boundaries in thickness. Overlap is counted once; full coverage remains reachable. Arm Ratio changes its proportions.
- **Diamond:** a centered diamond grows into the square cell and merges at darker coverage. Its lattice stays axis-aligned; rotating square sampling coordinates would also rotate the lattice and is a different appearance.
- **Star:** a five-point star with fixed inner/outer radius ratio 0.45. Marks grow and merge at darker coverage; Rotation turns the motif and lattice together. Arm Ratio affects only Plus, not Star.

All analytic modes currently share RGB quantization and a dark-mark convention. There is no separate ink color, polarity selector, luminance-only mode, or free-form shape-size control. Size is determined by tone coverage; scale determines spacing. The original three graphs remain analytic-only; the separate lighting shader now supplies the first calibrated custom-stamp slice below.

Shape distance is converted to an area-ranked threshold before quantization. Squares, lines, plus signs and diamonds use algebraic area expressions; dots use circle-segment area with `acos`, reciprocal square root and square root in the corner region. Star uses sector reflections and a piecewise-quadratic clipped-polygon area function with eight fixed events. Its coefficients can be reproduced by `Tools~/Dither/generate_star_coverage.py` using Python's standard library; this is an offline maintenance tool, not a runtime dependency. These are calibrated area mappings rather than raw distance gradients. Dot and star modes have more ALU work and still require headset profiling.

Filtering uses threshold derivatives to soften RGB edges and fades toward the original albedo as the UV footprint grows from 0.25 to 0.75 cells per pixel (roughly four to 1.33 pixels per cell along the measured screen direction). Filtering 0 disables that fade and leaves a hard threshold apart from a tiny numerical transition. Partial filtering blends the policy. Filtered edges can contain intermediate colors; this is intentional. It does not modify alpha or the foliage cutoff. This heuristic is not a complete anisotropic/prefiltered solution and has not been accepted in-headset.

The shared subgraph is now explicitly **fragment-only** because of derivatives. `FP_Dither_float` / `_half` retain the original texture-free Bayer function signature; new `FP_DitherShape_float` / `_half` entry points accept Pattern, Rotation, ArmRatio and Filtering before their outputs. The three supplied graphs use float precision. Selection uses material-uniform branches and adds zero authored shader keywords. Branch execution cost and compiler behavior on mobile remain unmeasured; no claim is made that all modes cost the same.

Six analytic modes were captured on the supplied tree at each of its four LODs using temporary property blocks. The blocks were removed without changing saved material selections, and automatic LOD selection was restored. Tree preview settings were strength 0.85, scale 16 and three levels. At completion of the shape milestone the saved trunk material selected Diamond and the leaf material selected Star. At completion of the later lighting milestone both selected Star; those user selections and the original tree shaders were preserved. The labeled shape board provides a clearer broad-surface comparison than the leaves' small UV islands.

## Verified environment and results (2026-09-26)

### Custom stamp slice (S3)

Open `Examples/Stamps/FP_DitherStamps.unity` for standard, stamp-lit, stamp-shadowed and combined receivers. Select **CustomStamp** on `FP_DitherLightingLit` and assign `Examples/Stamps/FP_BoltRank.png` to **Stamp Rank**. Existing materials keep their pattern selection. The original albedo Shader Graphs do not expose this texture-backed mode.

The lightning-bolt polygon is authored in `Tools~/Dither/generate_stamp_rank.py`. This standard-library Python helper computes signed distance at 128×128 texel centers, sorts it, and converts that ordering into 256 equally populated rank bins. Edit the polygon and generate a separate example texture to author another simple motif. Arbitrary SVG parsing, arbitrary mask conversion, atlases and animated stamps are not implemented.

- **Data contract:** linear red-channel coverage rank, low values inside the dark mark and high values toward the background. This is a rank-transformed distance field, not a binary mask or a raw SDF; there is no fixed 0.5 contour that must reproduce the source outline. Increasing dark coverage expands the motif and eventually merges neighboring cells. Black and white stay exact.
- **Import:** Default texture type; sRGB off; uncompressed; alpha unused; mipmaps off; Point filter; Repeat wrap. The shader explicitly samples point/repeat at LOD zero. Do not supply averaged rank mipmaps: they change the histogram and bias tone. Keep meaningful details clear of the tile boundary in the source artwork. The sample has padding, but expanded dark coverage intentionally reaches the boundary.
- **Cost:** one source-level stamp sample per fragment when stamp albedo or lighting/shadow styling is active, shared across all enabled style channels and all lights. Analytic modes branch around it; all strengths off/master mask zero also bypass it. Zero added artist keywords, passes or dependencies. This describes source structure; generated mobile instruction cost remains unmeasured.
- **Filtering:** shared derivative edge smoothing and footprint fade; explicit LOD zero retains ranks until the effect fades back to continuous shading. Point sampling can show texel steps on enlarged marks. This does not replace a full prefiltered anisotropic solution. For the bolt, begin around 24–32 pixels per cell for visual review; that is an authoring starting point, not a tested headset minimum. Check sharp tips, oblique angles, stereo and foveation before accepting smaller marks.
- **Tone:** unfiltered two-level GPU tests measure 0/0.1/0.25/0.5/0.75/0.9/1 within 0.005 absolute linear tone, with exact endpoints. Filtering, rotations other than quarter turns, resampling and arbitrary rank assets can change this distribution. RGB channels quantize independently, so colored albedo can produce colored contour bands; lighting-only styling preserves continuous albedo color.

Four stamp tests cover import settings, GPU coverage/endpoints, rotation/negative-UV repetition, disabled/masked identity and minification fallback. Three additional scene-render cases exercise stamp albedo, diffuse light and received-shadow controls through the actual lighting shader. These are desktop checks; headset legibility and GPU measurements remain pending.

After this slice, **164/164 package Editor tests passed**, including **16 lighting integration cases and four stamp cases**, with no failures or skips. The comparison capture was inspected in the active desktop pipeline. The earlier 13/157 and 31/144 counts elsewhere on this page record the preceding lighting and analytic milestones.

| Setting | Observed |
| --- | --- |
| Unity | 6000.6.0f1 |
| URP / Shader Graph | 17.6.0 |
| Color space / Editor graphics API | Linear / Direct3D 12 |
| Build target | StandaloneWindows64 |
| Active pipeline asset | FP_Synthetic |
| Default renderer | `Assets/FP_Synthetic/Samples/Oysters/FP_Synthetic_URP_Oysters.asset` (index 0) |
| Render scale / MSAA | 1 / disabled (sample count 1) |
| XR provider | AR Foundation and ARKit listed; OpenXR not listed in manifest |
| Headset / target refresh rate | Pending user selection |

All three graphs imported and rendered in the connected Editor with no shader messages. The original synthetic comparison confirms textured static and posed skinned meshes; the shape board adds static spheres/ramps. **31 `FPDitherTests` cases passed**, including material properties/dependencies, depth/shadow passes, GPU Bayer ranks, exact legacy Bayer comparison, disabled-effect identity, bounded quantization, shape coverage, negative-UV repetition, hatch rotation, minification fallback, and foliage alpha coverage across all seven patterns. Core tests execute production HLSL through an Editor-only GPU probe. The foliage test renders the actual graph's ForwardLit pass. This is desktop evidence, not mobile compiler or XR validation.

Coverage checks use a 256×256 cell, two quantization levels, no filtering, zero bias, full strength/mask/amount, and tones 0 / 0.1 / 0.25 / 0.5 / 0.75 / 0.9 / 1. Average channel error is bounded to 0.008 absolute linear units; black and white are exact and average coverage is monotonic. This tolerance includes finite raster sampling and is not a guarantee for arbitrary filtered textures, bias settings or viewing angles.

The containing `com.fuzzphyte.utility.editor.tests` assembly passed **144/144 tests**, with zero failures, skips or inconclusive results, after the six analytic shapes were added. Earlier first-slice validation passed 124/124. A first-slice run started while the working scene was dirty was cancelled at Unity's Save Scenes prompt; completed runs occurred with the user's saved scene preserved.

The graphs were authored through the installed Shader Graph editor API and serialized by Unity; Unity generated asset metadata. No package dependency, assembly reference, XR configuration or pipeline setting was changed. The user-supplied test scene's material overrides are described above.

## Remaining validation

The design's complete acceptance criteria are **not yet met**. Representative character animation, organic/foliage art direction, stereo stability, head movement, distance aliasing, mobile shader compilation and device GPU measurements remain pending. The organic graph shares the validated desktop core; it does not establish the headset milestone. The lighting extension above is a separate user-requested desktop prototype.

UV stability does not prevent subpixel aliasing. UV seams and nonuniform texel density can change pattern appearance; high scales can shimmer at distance. Analytic shapes have derivative-based smoothing/fading as described above; Bayer remains unfiltered for compatibility. Use float graph precision as supplied, particularly for large or tiled UVs; a half entry point cannot recover coordinate precision already lost upstream.

Use a dedicated headset validation scene with representative meshes, animation, lighting and pixel coverage. Compare equivalent baseline Lit and dither materials on the **same objects**, swapping material sets between captures. The side-by-side desktop scene is for inspection, not a fair timing comparison. Match texture samples, lights, shadows, render scale, MSAA and camera settings. Warm shader variants before capture. Record multiple baseline and enabled samples, their range/variance and GPU/CPU times. Inspect slow/fast head rotation and translation, both eyes, near/far distances, character animation and foliage motion.

| Device measurement | Baseline | Character | Organic |
| --- | --- | --- | --- |
| GPU frame time / delta | Not measured | Not measured | Not measured |
| CPU frame time | Not measured | Not measured | Not measured |
| Sample count / range | Not measured | Not measured | Not measured |

Also record headset, XR provider/version, graphics API, refresh rate, build type, active renderer, render resolution/scale, MSAA and visible material counts. The proposed ≤0.25 ms incremental GPU cost remains a target, not a measured result. No performance claim is made from Editor timing.

Future work should follow evidence from that slice. Object/world coordinates, temporal patterns and patterned alpha remain outside this implementation. Lighting bands are now available in the separate opt-in shader described above; they are not changes to the original three albedo graphs.

Reference: [Unity Shader Graph 17.6 Custom Function documentation](https://docs.unity.cn/Packages/com.unity.shadergraph%4017.6/manual/Custom-Function-Node.html).
