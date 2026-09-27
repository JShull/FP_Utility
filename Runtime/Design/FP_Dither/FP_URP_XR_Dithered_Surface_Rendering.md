# FuzzPhyte URP XR Dithered Surface Rendering

## Purpose

Create a lightweight, tunable dithering/stylization system for textured 3D assets in Unity URP, with standalone/mobile VR performance as a first-class constraint.

The initial system should support two primary material families:

1. **Textured 3D characters**
2. **Organic/environmental assets**

The desired result is not a full-screen retro filter. The goal is to make dithering feel like part of the rendered material while preserving normal URP lighting, depth, shadows, XR stereo rendering, and existing textured asset workflows.

The first implementation should remain deliberately small, measurable, and reusable.

---

# Core Decision

Implement dithering **inside Shader Graph materials**, using a small shared dithering primitive.

Do **not** begin with a URP Renderer Feature or full-screen post-process.

The preferred architecture is:

```text
URP Lit Shader Graph
        │
        ├── Existing texture/material inputs
        │
        ├── Shared FP dither logic
        │      ├── Bayer threshold
        │      ├── Quantization
        │      ├── Strength
        │      └── Mask
        │
        └── Standard URP lighting/output
```

The core dithering implementation should add a small amount of fragment ALU and should not require:

- An additional render pass
- A full-screen render target
- A full-screen texture read/write
- A dedicated noise texture
- Alpha discard/`clip()` for the core visual effect
- CPU-side per-frame work

---

# Target Rendering Environment

Assume:

- Unity URP
- XR/OpenXR-compatible rendering
- Standalone/mobile VR-class GPU performance constraints
- Forward-oriented URP rendering
- Textured 3D assets
- Skinned characters
- Static and animated organic/environmental assets
- Stereo rendering must remain correct
- The shader must remain compatible with Unity's normal XR rendering path

The exact Unity version, URP version, XR provider, headset, render scale, MSAA setting, and target refresh rate must be read from the repository/project before final implementation decisions are made.

Do not add packages or alter render-pipeline configuration unless explicitly required.

---

# XR-Specific Visual Rule

## Do Not Make Screen-Space Dithering the Default

A screen-locked pattern may produce undesirable VR artifacts:

- Pattern crawling during head motion
- Temporal shimmer
- Stereo inconsistency
- A "screen door over the eyes" appearance
- Excessively visible fixed-pattern structure

For the first implementation, dithering should be **surface-stable**.

Preferred coordinate strategy:

```text
Characters
    └── UV-space dithering

Organic/environmental assets
    └── UV-space dithering for v1
```

UV-space is the preferred first implementation because it is:

- Stable during head motion
- Stable between stereo views
- Stable on skinned characters
- Naturally attached to textured surfaces
- Cheap
- Easy to reason about in Shader Graph

Object-space or world-space dithering may be explored for static organic assets after the first vertical slice is validated.

Screen-space dithering should only be added later as an explicitly experimental mode and must be tested in-headset before adoption.

---

# Proposed Asset Architecture

Before creating new folders or a package, inspect the repository for an existing FuzzPhyte rendering/shader package.

If an appropriate package already exists, integrate there.

Conceptually, the shader assets should be organized as:

```text
FP_StylizedRendering
│
├── Shaders
│   │
│   ├── Includes
│   │   └── FP_Dither.hlsl
│   │
│   ├── SubGraphs
│   │   └── FP_DitherCore.shadersubgraph
│   │
│   └── Graphs
│       ├── FP_CharacterDitherLit.shadergraph
│       └── FP_OrganicDitherLit.shadergraph
│
├── Materials
│   └── Examples
│
├── Tests
│   ├── Editor
│   └── Scenes
│
└── Documentation
    └── Dithering.md
```

Exact naming should follow existing repository conventions.

---

# Shared Dither Core

The shared implementation should contain the reusable math used by both Shader Graphs.

Preferred structure:

```text
FP_DitherCore

Inputs
├── Base Color
├── Coordinates
├── Strength
├── Pattern Scale
├── Color Steps
└── Mask

Operations
├── Calculate surface-stable dither coordinate
├── Evaluate ordered threshold
├── Quantize color/value
├── Apply threshold perturbation
├── Apply mask
└── Blend against original color

Outputs
├── Dithered Color
└── Optional Threshold/Debug Value
```

A small HLSL Custom Function node is preferred over recreating an ordered-dither matrix as a very large Shader Graph node network.

The HLSL should remain:

- Small
- Deterministic
- Texture-free for the core effect
- Compatible with Shader Graph Custom Function usage
- Friendly to mobile shader compilers
- Free of unnecessary dynamic branching

---

# Ordered Dither Pattern

Start with a **4x4 Bayer ordered dither**.

Conceptually:

```text
 0   8   2  10
12   4  14   6
 3  11   1   9
15   7  13   5
```

Normalize the threshold to the required range and use it when quantizing the material color/value.

The first implementation does not need every possible pattern.

## v1

Required:

- Bayer 4x4

Optional after profiling:

- Bayer 8x8

Do not add a noise texture merely to create variation unless the ordered pattern fails to achieve the desired art direction.

---

# Color Quantization

Dithering should be able to work with color quantization.

Conceptually:

```text
Textured Base Color
        │
        ▼
Optional Color Quantization
        │
        ▼
Ordered Dither Adjustment
        │
        ▼
URP Lit Base Color
```

Approximate operation:

```text
quantized = floor(color * steps + thresholdAdjustment) / steps
```

The exact implementation should be tuned visually and validated in Unity's active color space.

Important:

- Dithering and quantization are separate concepts.
- `Dither Strength = 0` should restore the normal material appearance.
- Quantization should be independently tunable.
- Avoid quantizing normals, PBR mask data, or other unrelated channels unless deliberately added later.

The first version should operate on the material's visible color/albedo path before standard URP lighting.

---

# Character Shader

## Asset

```text
FP_CharacterDitherLit.shadergraph
```

## Purpose

Provide subtle-to-moderate stylization for textured, animated, skinned 3D characters while preserving character readability.

## Coordinate Space

Use the character texture UVs.

```text
UV0
 ├── Base texture sampling
 └── Dither coordinates
```

This keeps the dither pattern attached to the character during:

- Skeletal animation
- Character movement
- Camera/head movement
- Stereo rendering

## Recommended Character Controls

Expose a deliberately small set of properties:

```text
Dither
├── Strength
├── Pattern Scale
├── Color Steps
├── Contrast/Bias
└── Mask Strength
```

Possible shader property names should follow project conventions, for example:

```text
_DitherStrength
_DitherScale
_DitherColorSteps
_DitherBias
_DitherMaskStrength
```

Do not create a large number of artistic properties in the first pass.

## Character Masking

Character materials may need different levels of stylization across a single asset.

Typical desired behavior:

```text
Hair       strong
Clothing   moderate
Skin       light
Eyes       none/light
Teeth      none/light
```

Prefer reusing a free channel from an existing packed mask texture when available.

Do not add a dedicated dither-mask texture sample unless the asset workflow actually requires it.

A constant material-level mask value is sufficient for the first vertical slice.

---

# Organic Shader

## Asset

```text
FP_OrganicDitherLit.shadergraph
```

## Purpose

Provide stronger stylization for natural and environmental assets such as:

- Rocks
- Wood
- Plants
- Ground props
- Mushrooms
- Bark
- Natural clutter
- Other textured environmental forms

## Coordinate Space

Use UV-space dithering for v1.

This creates a stable baseline and avoids introducing VR shimmer before the basic visual language is established.

After v1 is profiled and validated, evaluate:

```text
Static organic objects
    ├── Object-space pattern
    └── World-space pattern

Moving/wind-animated foliage
    └── Continue preferring UV-space
```

A world-space pattern should not be used on moving assets without specifically evaluating pattern swimming/crawling.

## Organic Controls

The organic version may permit a stronger range than the character shader:

```text
Dither
├── Strength
├── Pattern Scale
├── Color Steps
├── Contrast/Bias
├── Mask Strength
└── Optional Variation
```

Do not introduce procedural noise in the first implementation simply because the shader is "organic."

First determine how far ordered dithering + texture content + quantization can carry the visual style.

---

# Avoid an Uber Shader

Do not build one Shader Graph with a large matrix of toggles such as:

```text
Character
Organic
UV Space
Object Space
World Space
Screen Space
Bayer 2x2
Bayer 4x4
Bayer 8x8
Noise
Lighting Dither
Shadow Dither
Alpha Dither
...
```

This risks:

- Excessive shader keyword variants
- More difficult profiling
- More difficult material authoring
- Harder maintenance
- Feature combinations that are never actually used

Instead use:

```text
Shared FP_DitherCore
        │
        ├── Character Shader Graph
        └── Organic Shader Graph
```

Each graph should have a known, intentional feature set.

---

# Shader Variant Policy

Minimize shader keywords.

Do not use a shader keyword for every artistic control.

Prefer normal material properties for:

- Strength
- Scale
- Color steps
- Bias
- Mask amount

Only use shader keywords when they materially remove meaningful shader work and the variant count remains justified.

Record any keyword added and explain why it is required.

---

# Lighting Scope for v1

The first version should stylize the material color/albedo path while retaining normal URP lighting.

```text
Texture
   │
   ▼
Base Color
   │
   ▼
Dither + Quantization
   │
   ▼
URP Lit Shader
   │
   ▼
Lighting / Shadows / Output
```

Do not immediately replace URP lighting or build a custom lighting model.

A later stylized-lighting phase may explore:

- Quantized lighting bands
- Dithered shadow transitions
- Shadow-dependent dither strength
- Highlight preservation
- Toon/stylized BRDF behavior

Those features are explicitly outside the initial implementation.

---

# Alpha and Transparency

The base dithering effect is a color stylization effect.

Do not implement the core appearance using:

```hlsl
clip(...)
```

or patterned alpha discard.

Alpha dithering may later be useful for:

- LOD fades
- Dissolves
- Transition effects

but it is a separate feature and should not be mixed into v1.

This is particularly important on mobile XR hardware where alpha testing, overdraw, foliage, MSAA, and fill rate may already be significant rendering costs.

---

# Performance Contract

The implementation should be designed around these constraints.

## Required

- No additional render pass for core dithering
- No full-screen effect
- No render texture allocation for core dithering
- No CPU per-frame processing
- No additional texture sample required by the Bayer dither itself
- No GC allocation
- No dependency on a URP Renderer Feature
- Stereo/XR compatibility
- No visible eye-to-eye mismatch
- No obvious temporal shimmer caused by the dither coordinate system

## Provisional GPU Budget

Until the exact target headset and representative scene are established, use this as an engineering target rather than a guaranteed requirement:

> The complete dither feature should add approximately no more than 0.25 ms GPU time in a representative target-headset scene, and should preferably remain within normal capture variance.

If the effect exceeds that range, profile before optimizing.

Do not remove useful art controls solely based on Editor measurements.

Headset/player measurements are authoritative.

---

# What to Profile

The cost comparison must be made between equivalent materials.

## Baseline

```text
URP Lit material
```

## Test A

```text
Character Dither Lit
Dither Strength = representative value
```

## Test B

```text
Organic Dither Lit
Dither Strength = representative value
```

Test a scene containing enough visible affected pixels to make the difference measurable.

Record:

- Unity version
- URP version
- XR provider
- Headset
- Build type
- Render resolution/render scale
- Refresh rate
- MSAA
- Active renderer
- Number of visible character materials
- Number of visible organic materials
- GPU frame time
- CPU frame time
- Dither-enabled GPU frame time
- Baseline GPU frame time
- Delta
- Sample count/range

Do not use Editor performance as proof of VR performance.

---

# XR Visual Validation

The feature must be visually inspected in the headset.

Test:

## Head Motion

Rotate and translate the head slowly and quickly.

Verify:

- No pattern visibly sticks to the headset display
- No distracting crawling
- No abnormal shimmer

## Stereo

Inspect high-contrast dithered surfaces at multiple depths.

Verify:

- Both eyes perceive a coherent surface
- No stereo rivalry caused by eye-dependent pattern placement

## Distance

View assets:

- Very close
- Typical interaction distance
- Mid distance
- Far distance

Verify:

- Pattern does not become excessively noisy
- Fine geometry does not dissolve visually
- Character faces remain readable

## Animation

For characters:

- Idle
- Walk
- Large limb movement
- Facial/head movement if available

Verify the pattern remains attached to the textured surface.

For foliage:

- Wind animation
- Object motion if applicable

Watch specifically for pattern swimming.

---

# Material Authoring Guidance

The design should support reusable material presets rather than forcing artists to tune every object independently.

Suggested conceptual presets:

```text
Character - Soft
Character - Strong
Fabric
Wood
Rock
Foliage
Painted Prop
```

Do not create a complex preset system during the first implementation.

Example materials are sufficient initially.

The purpose of presets is to establish a coherent visual language.

---

# Debugging Support

The shared subgraph may expose or temporarily support debug outputs for development:

```text
Threshold
Quantized Color
Dither Mask
Coordinates
```

Debug outputs should not become permanent runtime branches unless needed.

If debug visualization requires a dedicated debug Shader Graph/material, prefer that over bloating the production shader.

---

# Implementation Milestones

## Milestone 0 - Repository Inspection

Before modifying the project:

- Identify Unity version
- Identify URP version
- Identify XR provider
- Identify target headset(s)
- Identify target refresh rate
- Identify active renderer
- Inspect package manifest
- Inspect existing FuzzPhyte rendering/shader code
- Inspect naming/folder conventions
- Identify representative character asset
- Identify representative organic asset

Deliver a short implementation plan before substantial changes if repository reality differs from this document.

---

## Milestone 1 - Dither Core

Implement:

```text
FP_Dither.hlsl
FP_DitherCore.shadersubgraph
```

Minimum functionality:

- Bayer 4x4 threshold
- UV coordinate scaling
- Dither strength
- Color steps
- Bias/contrast control if needed
- Mask input
- Original-vs-stylized blend

Validate with a trivial test graph/material.

Success criteria:

- No extra texture sample for dither
- Strength 0 reproduces the undithered color path
- Dither pattern remains deterministic
- No compile warnings/errors on the target graphics API

---

## Milestone 2 - Character Vertical Slice

Implement:

```text
FP_CharacterDitherLit.shadergraph
```

Apply to one representative textured/skinned character.

Validate:

- Skinning
- Lighting
- Shadows
- Animation
- Stereo rendering
- Head movement
- Near/far viewing

Profile against an equivalent URP Lit baseline.

This is the main architectural proof.

Do not proceed by adding many options if this vertical slice is not visually stable in-headset.

---

## Milestone 3 - Organic Variant

Implement:

```text
FP_OrganicDitherLit.shadergraph
```

Start with UV-space dithering.

Apply to representative:

- Rock/wood/static organic asset
- Foliage asset if available

Allow a more aggressive useful parameter range than the character shader.

Profile again.

---

## Milestone 4 - Art Direction Pass

Create a small set of example materials/presets.

Evaluate:

- Dither strength ranges
- Pattern scale
- Color step ranges
- Character readability
- Environmental cohesion

Document recommended starting values.

Do not add new rendering features unless a specific visual problem is identified.

---

## Milestone 5 - Optional Spatial Experiments

Only after the UV implementation passes XR validation:

Evaluate object-space or world-space dithering for static organic objects.

Compare against UV space for:

- Visual cohesion
- Pattern stability
- Surface distortion
- GPU cost
- Ease of material authoring

Do not add screen-space dithering to the production path unless there is a deliberate visual need and it passes headset testing.

---

# Testing

Shader code is primarily validated visually and through GPU profiling, but the package should still include lightweight contract validation where practical.

If the repository has an existing Unity Test Framework setup, add Edit Mode tests that verify:

- Required shader assets can be found
- Materials can be created from the expected shader
- Required exposed shader properties exist
- Default property ranges/values are valid
- Shared resources referenced by the graphs exist

Do not build elaborate tests that merely mirror Shader Graph implementation details.

Use a dedicated XR/rendering validation scene for visual and performance testing.

---

# Documentation Requirements

Create/update the relevant rendering documentation with:

- Purpose
- Supported asset types
- Required Unity/URP/XR versions
- Shader locations
- Material property descriptions
- Character vs organic guidance
- XR-specific restrictions
- Profiling method
- Baseline and measured cost
- Known limitations
- Example materials
- Future extension points

Documentation must describe what was actually implemented rather than copying this design document unchanged.

---

# Explicit Non-Goals for v1

Do not include these in the first implementation:

- Full-screen dithering
- URP Renderer Feature
- Custom post-processing
- Custom lighting model
- Dithered shadows
- Temporal dithering
- Stochastic noise dithering
- Noise texture sampling solely for the dither
- Alpha screen-door transparency
- LOD dithering
- Object/world-space animated foliage dithering
- Large shader keyword matrix
- Procedural material framework
- New external dependencies

These may be evaluated later individually.

---

# Future Extension Path

If the base shaders are successful, the architecture should allow future work such as:

```text
FP_DitherCore
    │
    ├── Character Lit
    ├── Organic Lit
    ├── Stylized Prop Lit
    │
    ├── Optional stylized lighting
    │      ├── Quantized diffuse
    │      ├── Shadow dithering
    │      └── Highlight handling
    │
    └── Optional renderer-level effects
```

A Renderer Feature should only be introduced if there is a separate requirement for scene-wide effects or for rendering objects whose materials cannot use the shared shader logic.

---

# Shape-Based Dithering — Planned Extension

## Status and Scope

Added after the first Bayer material implementation and desktop tree checks. This section is a **design and implementation outline**. The first analytic slice now exists: square marks, dots, lines, plus signs, diamonds and five-point stars, with a Pattern dropdown, rotation, plus arm ratio, and derivative-based filtering. Bayer remains the default. Desktop coverage/repetition/alpha checks pass; custom stamps, alternate color treatments, animated-character shape checks and headset acceptance remain pending. See [README.md](README.md) for precise controls, measured test tolerances and validation results.

Expand the surface pattern vocabulary while retaining the existing character, organic and foliage material workflows:

1. Classic Bayer grid / square marks.
2. Circles and dots for halftone print.
3. Lines and hatching for directional texture.
4. Crosses and plus signs for a crisp digital appearance.
5. Diamonds and five-point stars for geometric and emblem-like patterns.
6. Custom stamps for authored motifs and tiny icons.

Keep the existing Bayer path as the compatibility baseline. New shapes continue to modify albedo before URP lighting. Preserve the existing foliage texture-alpha cutout independently; shape coverage must never feed Alpha or Alpha Clip Threshold. No new scene-wide rendering pass is proposed.

This phase can be prototyped in the Editor, but production acceptance still requires the pending headset tests. It does not unlock the deferred screen-space, world-space, temporal or custom-lighting experiments.

## Separate Pattern Layout, Shape and Tone

These are related controls with different responsibilities:

| Concept | Responsibility | Initial scope |
| --- | --- | --- |
| Layout / lattice | Where repeating cells sit on a surface | Regular square lattice in UV0 |
| Motif | What appears inside each cell | Square, circle, line, plus, diamond, star, authored stamp |
| Tone mapping | How much of the cell becomes the darker or lighter tone | Coverage driven by the color interval or albedo luminance |

"Classic grid" initially means the existing Bayer arrangement of square threshold cells. A visible grid of continuous crossing lines is a separate motif, not another name for Bayer. Square halftone marks may share the lattice while growing within cells; they must not silently replace the existing Bayer algorithm.

Use a common coordinate stage that exposes a cell index and centered coordinates within that cell. Define scale as **cells per UV unit**, retaining the current property meaning; a Bayer tile still spans four cells. Rotation affects the pattern coordinates, not the base texture UVs. A later brick, staggered-dot or hexagonal lattice would be a separate layout experiment rather than a hidden property of the circle mode.

## Pattern Families

| Family | Proposed appearance and tone response | Shape-specific controls | First comparison surface |
| --- | --- | --- | --- |
| Classic grid / squares | Existing ordered Bayer appearance; optional growing square marks for a block-print look | Existing Bayer controls; square fill size only in the new mark mode | Current baseline sphere and tree bark |
| Circles / dots | Repeated circular marks whose area increases with requested ink coverage; newspaper/comic direction | Dot size limit, pattern rotation, ink polarity | Broad bark/rock surfaces and character clothing |
| Lines / hatching | Parallel bands that thicken as ink coverage increases | Angle and line width limit | Fabric panels, wood and metallic props |
| Crosses / plus signs | Centered plus-shaped marks, with arm thickness controlling fill | Arm length and thickness limit; rotation can produce an X-like motif | Painted props and graphic accents |
| Diamonds | Centered diamonds grow and merge as dark coverage increases | Pattern scale and rotation; tone determines mark size | Geometric fabrics and painted surfaces |
| Five-point stars | Star-shaped marks grow, merge and eventually fill the cell | Fixed initial proportions; pattern scale and rotation | Broad graphic surfaces and emblem-like treatments |
| Custom stamps | Authored graphic marks repeated in the same surface-stable cells | Stamp asset, size, rotation and threshold/coverage calibration | Broad surfaces where the icon remains readable |

These appearances are art-direction targets. Dithered albedo on a metallic object is not a model of anisotropic reflection; hatching must not imply that the shader replaces the existing BRDF.

### Circles / Dots

Prototype a procedural circle distance field inside each cell. Darker requested tones grow the inked area; lighter tones shrink it. Define whether the mark represents dark ink or light ink explicitly.

Radius and coverage are not interchangeable: increasing radius linearly does not increase circle area linearly. Calibrate the tone-to-radius mapping against measured cell coverage. An isolated circle confined to a square cell cannot fill the entire cell without changing its silhouette. Specify an endpoint strategy—such as merging neighboring marks or complementing the light holes at high coverage—and verify black and white endpoints without a visible transition jump.

Start with one monochrome coverage decision applied to the textured color. Separate CMYK plates and independent channel rotations are outside this phase.

### Lines / Hatching

Prototype one analytic band per repeating cell or period. Let orientation provide directional flow and requested coverage determine band width. Keep width distinct from cell density; making lines thicker should not change their spacing.

Start with a single hatch direction. Optional cross-hatching is a later substep: introduce a second direction only when a specific darker-tone treatment requires it. It adds another shape evaluation and needs its own coverage calibration. Validate wrapping at cell boundaries and avoid a seam when rotating the pattern.

### Crosses / Plus Signs

Prototype the union of two perpendicular rectangular bars. Keep arm length and bar thickness independently understandable, with restrained ranges. Do not sum the two bar masks in their overlap, which would double-count the center.

Short arms may leave gaps even at maximum thickness. Define how full coverage is reached, or declare that a fixed-size plus stamp is a decorative mark rather than a tone-preserving quantizer. Compare upright plus signs and rotated crosses without creating a separate shader for every angle.

### Diamonds and Five-Point Stars

Both are part of the analytic implementation, not texture-backed custom stamps. Diamonds use a Manhattan-distance contour and its clipped-cell area. Stars use a fixed inner/outer radius ratio of 0.45, reflected edge sectors and a calibrated piecewise-quadratic area function. This permits full tone coverage as marks grow past the cell boundary without sampling a mask.

Rotation applies to both motif and lattice. Shape size remains coverage-driven, with spacing controlled by Pattern Scale. Keep arbitrary point count, star sharpness and independent motif rotation outside the first controls; each would require new coverage calibration. Star coefficients are reproducible with the package's offline `Tools~/Dither/generate_star_coverage.py` helper.

Validate sharp star tips and diamond corners under filtering and distance changes. They remain subject to the same alpha-separation, disabled-effect identity, coverage and headset requirements as the other analytic shapes.

### Custom Stamps

Support simple procedural motifs first when they can be represented with a small analytic distance function. For arbitrary vector artwork or tiny icons, plan an **optional imported mask or signed-distance-field texture**. Vector artwork is an authoring source; this outline does not propose evaluating SVG paths per fragment or adding a runtime vector-rendering package.

The texture-backed route is a deliberate exception to the texture-free Bayer core. Keep it isolated so materials using analytic shapes do not pay for a stamp sample. Do not add an importer, atlas framework or package dependency without a demonstrated need and a separate implementation decision.

Stamp asset requirements:

- A documented ink/background convention and predictable empty/full behavior.
- Linear-data import for masks/SDFs; document the chosen channel and SDF edge value.
- Deliberate filtering, mip behavior, padding and addressing so repeated cells do not bleed or show seams.
- A documented tone strategy: scale the motif, vary its coverage threshold, or vary ink intensity. These produce different appearances and must not be presented as equivalent.
- Coverage measurements across the usable threshold range. A raw distance field or arbitrary mask is not automatically an evenly distributed dither threshold.
- Legibility checks at the smallest intended on-screen size and across mesh LOD changes.

Prefer a single stamp texture for the first custom example. Atlas selection, per-cell random stamps, animated motifs and user-defined runtime shader code remain deferred.

## Two Explicit Color Behaviors

### A. Shape-Driven Quantization

Use a shape-derived threshold field to choose between neighboring quantized color levels. Preserve the current master Strength and material mask blend so Strength 0 or Mask 0 restores the original albedo exactly.

Calibrate the threshold distribution or tone-to-coverage function per shape. Replacing a Bayer rank with an arbitrary distance value can shift average brightness. Use flat tone ramps to measure that shift and distinguish intentional bias from a broken coverage mapping. In particular, test full black, full white, midtones and monotonic coverage through the entire range.

Keep existing RGB quantization available as the compatibility behavior. For print-like motifs, prototype a shared luminance-driven coverage decision and measure hue/chroma changes before choosing it as a default. Do not silently change the current graphs' color response.

### B. Graphic Ink / Stamp Treatment

Blend a chosen ink treatment over the textured albedo through the procedural shape mask. This permits a consistently recognizable plus sign or icon even when a calibrated quantizer would expand or merge the shape.

This is an explicitly artistic color treatment, not necessarily tone-preserving dithering. Ink color, shape size and coverage may alter average brightness. Keep it a later, separate experiment; first establish behavior A with dots and lines. Neither behavior modifies mesh transparency.

## Proposed Controls and Compatibility

Retain existing serialized property names and meanings: `_DitherStrength`, `_DitherScale`, `_DitherColorSteps`, `_DitherAmount`, `_DitherBias` and `_DitherMaskStrength`.

Candidate additions, to be finalized during prototyping:

| Control | Meaning |
| --- | --- |
| Pattern | Implemented: Classic Bayer, square mark, circle, line, plus, diamond, star. Custom stamp remains planned; default remains Classic Bayer |
| Rotation | Pattern angle in UV space; independent of texture rotation |
| Shape size / width | Family-specific size limit or proportion, not a second master strength |
| Ink polarity | Dark marks versus light marks, with explicitly tested endpoints |
| Stamp mask | Optional asset for the texture-backed family only |

Use the same control names and behavior across character, organic and foliage surfaces. Shape controls should not grow into a matrix of coordinate spaces, lighting models and pattern toggles.

The current analytic prototype uses a material-level dropdown and uniform branches, with zero new authored shader keywords and no stamp sample. This is a provisional implementation choice; mobile execution cost has not been measured. Compare it with bounded compile-time variants or small fixed-shape wrappers if profiling identifies a problem. Do not evaluate every shape and every texture sample merely to blend to the selected one. Record generated shader cost and variant count before accepting the production path. Keep the texture-backed stamp path separate if that avoids work in analytic materials.

## XR Stability and Filtering

Retain UV0 as the default coordinate source for static, skinned and foliage materials. Repeating marks can still alias even when their coordinates remain attached to the surface.

Plan derivative-aware edge smoothing for analytic shapes and evaluate a minification strategy when cells or strokes become smaller than a pixel. Edge smoothing alone is not proof that a dense repeating pattern is stable. Compare fading toward the unstylized albedo with a coverage-preserving filtered result; document the chosen color response at distance.

Test thin lines, tiny plus arms and detailed stamps specifically. Compare both eyes at oblique angles, slow/fast head movement, near/far distances and the supplied tree's four LODs. UV changes between LOD meshes can change pattern placement even when each individual mesh is stable. Do not claim seamless LOD transitions without measuring them.

Do not add time-dependent jitter or screen-locked coordinates to conceal aliasing. Headset acceptance remains required for any filtering or distance-fade policy.

## Performance and Validation Contract

- Analytic squares, circles, lines, plus signs, diamonds and stars should require no pattern texture sample, render target, extra effect pass or CPU per-frame work.
- An optional stamp texture must have its sample count and filtering cost recorded separately.
- Compare every family to both equivalent URP Lit and the current Bayer implementation, at matching pixel coverage, texture inputs, lighting, alpha-cutout behavior and LOD.
- Keep the provisional total incremental GPU budget from this document; do not give each shape an additional independent 0.25 ms allowance.
- Record baseline, Bayer, analytic-shape and stamp timings separately on the chosen headset. Editor captures establish appearance only.
- Check required material properties, shared dependencies, shader compilation and unchanged depth/shadow behavior.
- Add GPU checks for disabled-effect identity, deterministic repetition, tone endpoints, monotonic coverage, average tone error and unchanged foliage alpha coverage.
- Define numerical tolerances before accepting a calibrated shape. Treat decorative ink treatment as a separate reference with intentionally different brightness behavior.

## Shape Milestones

1. **S0 — Reference board and semantics:** use the existing tree scene plus broad textured patches to compare baseline Bayer, square marks, dots, lines and plus signs. Decide dark/light polarity, scale units and the intended tone behavior before exposing more properties.
2. **S1 — Dots and lines:** implement the shared UV-cell stage and two analytic patterns. Validate coverage, strength-zero behavior, filtering, skinning and foliage alpha separation. Keep current materials on Bayer unless deliberately changed.
3. **S2 — Geometric motifs and authoring choice:** add calibrated plus signs, diamonds and five-point stars, compare selection/variant approaches, and establish a small set of starting materials. Recheck the four tree LODs. The current desktop slice uses a uniform selector; device profiling remains pending.
4. **S3 — One custom stamp:** implement one authored mask/SDF example only if the analytic results justify the added workflow. Record import rules, sample cost, tone response and minimum readable size.
5. **S4 — Headset acceptance:** test stereo, head motion, animation, viewing distance and GPU cost for each proposed production family. Document accepted patterns and any rejected ranges; do not mark this phase complete from desktop images alone.

## Shape Completion Checklist

- [x] Existing Bayer materials retain their default appearance and serialized settings; GPU legacy comparison passes.
- [x] Dots, lines and plus signs have documented tone behavior and restrained controls.
- [ ] A custom stamp example has explicit import, coverage and filtering rules, if implemented.
- [x] Analytic shapes remain independent of source texture alpha and foliage cutouts; GPU alpha coverage check passes.
- [x] Zero Strength and zero Mask restore the original color path for every implemented analytic family.
- [ ] Coverage calibration, endpoint and minification tests pass within recorded tolerances.
- [ ] Static, skinned and four-LOD tree comparisons are recorded.
- [ ] Shader selection cost, keyword counts and optional stamp samples are recorded.
- [ ] Headset stereo/motion acceptance and per-family timing results are recorded.
- [x] README and example materials describe the analytic patterns actually implemented, with custom stamps explicitly pending.

---

# Lighting and Shadow Extension — 2026-09-26

The user explicitly extended the original albedo-only scope to artistic lighting and shadows on selected objects, while other objects continue using standard URP materials. The original v1 restrictions still govern the three albedo Shader Graphs; this section authorizes a separate opt-in material lighting implementation.

## Ownership and interoperability

- Style belongs to the receiving material, independently for albedo, diffuse lighting and received shadows.
- Standard and stylized objects use the same real-time lights and conventional shadow maps.
- Standard casters may cast patterned shadows on styled receivers. Styled casters may cast conventional shadows on standard receivers.
- Cast geometry and ordinary foliage alpha cutouts remain unchanged by style controls. A caster cannot impose its visual pattern on a standard receiver.
- Zero strengths (or zero common mask) recover the ordinary lighting path for the supported material inputs. Existing materials are not silently migrated.

## Implementation decision

Use a separate `FP_DitherLightingLit.shader` with a small custom direct-light evaluation, rather than replacing the original Shader Graphs or adding a full-screen Renderer Feature. The original Lit graph's Base Color input cannot intercept individual light/shadow attenuation, so this is an intentional, user-requested extension of the original architecture. Reuse installed URP vertex/pass helpers, BRDF, GI, shadow retrieval and clustered light loops. Keep one material buffer layout across all passes. No new package dependency or runtime component is required.

Direct diffuse bands quantize the normal-facing factor for each light. Received-shadow bands quantize that light's sampled visibility. Both use the existing seven pattern families. Keep HDR light color, distance/spot falloff, specular response, ambient/baked illumination, reflections and fog continuous. Shadow stylization also gates direct specular visibility. Per-vertex additional lights retain standard URP behavior; per-pixel lighting is required for their style controls.

Use `UniversalForwardOnly` plus `DepthNormalsOnly` for integration with Forward, Forward+ and Deferred renderers. Retain ordinary ShadowCaster, DepthOnly, Meta and MotionVectors passes. No patterned discard, shadow-atlas modification or artistic keyword variants are introduced. Conventional alpha clipping uses URP's standard keyword. The prototype exposes separate strength/level controls but shares pattern coordinates, scale, bias, amount and filtering.

## Milestones

1. **L1 — Per-material direct lighting and shadows:** implement independent strengths, off/mask paths, shared motifs and ordinary cutouts. Provide a mixed-material comparison with conventional casters and four receiver styles.
2. **L2 — Desktop integration evidence:** render matched Lit/styled materials, directional/point/spot lights, cross-material casting, cutout silhouettes, Forward/Forward+/Deferred and the project's perspective screen-space-shadow path. Preserve the user's scene and pipeline configuration. Tests may create and dispose temporary renderer/pipeline instances.
3. **L3 — Production art and feature coverage:** representative animated characters and foliage, normal/metallic materials, soft-shadow transitions/cascades, multiple colored lights, baked/mixed lighting, cookies/layers, SSAO and reflection probes. Resolve the orthographic screen-space-shadow capture discrepancy before accepting that combination.
4. **L4 — XR acceptance:** both eyes, head motion, animation, distance/LOD changes and measured GPU cost per visible light/material family. The original incremental performance budget remains a target, not an achieved result.

The first desktop implementation and comparison are described in `README.md`, including actual validation results. The active pipeline has additional-light shadows disabled; preserve that setting and document the prerequisite. The sample sun uses partial shadow strength to make intermediate coverage visible. Fully lit and fully shadowed endpoints remain exact; a perfectly hard full-strength shadow has no intermediate visibility for the pattern to distribute.

Out of scope for this slice: material-local shadow fill, independent ink color, patterned shadow silhouettes, full final-color posterization, custom stamps, transparent blending, clear coat, DOTS material overrides, XR space-warp, and a custom global lighting pipeline.

---

# Custom Stamp and Platform Follow-up — 2026-09-26

The user requested Apple Vision Pro / Meta 2027 compatibility research and continued work on missing features. [XR_PLATFORM_SUPPORT.md](XR_PLATFORM_SUPPORT.md) records the sourced platform matrix and remaining queue. Its central distinction is Unity-rendered URP (including Apple's Metal app path) versus PolySpatial/RealityKit MaterialX conversion. The latter is not supported by the current hand-authored lighting shader, and the existing Custom Function graphs are not automatically portable.

**S3 desktop vertical slice now implemented:** `FP_DitherLightingLit` adds CustomStamp (7), a linear red-channel rank texture and the authored lightning-bolt example in `Examples/Stamps/FP_DitherStamps.unity`. A single uniform branch isolates the texture lookup from analytic modes. The same sample supplies albedo, diffuse lighting and received-shadow quantization. No new artist keywords, passes, importer, atlas system or package dependency are introduced. Existing graphs retain their analytic-only interface.

The source polygon is converted offline into a coverage-ranked signed-distance ordering using `Tools~/Dither/generate_stamp_rank.py`. It is not an arbitrary mask/SDF sampler: users must provide a calibrated rank texture. Tone changes threshold coverage rather than simply scaling a fixed icon. Import, padding, repetition, point/LOD-zero sampling, minification fade and legibility limitations are documented in [README.md](README.md). This intentionally uses the already planned texture-backed exception to the original texture-free requirement.

The earlier custom-stamp "pending" references describe the original analytic milestone. They are superseded for this narrow lighting-shader slice; custom stamps in the original three graphs, arbitrary artwork conversion and headset acceptance remain pending. The L1/L2 scope exclusions above likewise describe that original slice.

- [x] One authored stamp example and reproducible source polygon.
- [x] Explicit rank/channel/import/filtering rules and desktop tone/repetition/off tests.
- [x] Platform matrix and point/spot shadow configuration guidance.
- [ ] Headset minimum readable cell size, foveation behavior and measured stamp sample cost.
- [ ] L3 representative production lighting/animation evidence.
- [ ] PolySpatial-compatible material path, if required by the selected Apple app mode.
- [ ] Target build compilation and L4 device acceptance.

Original completion items below are updated for delivered source and desktop evidence only. Surface stability in VR, stereo and measured performance remain open.

# PolySpatial surface-path progress — 2026-09-26

The user approved progressing the separate PolySpatial surface-material approach. `FP_PolySpatialDitherLit` and `FP_PolySpatialFoliageDitherLit` now use a float-only assignment-expression version of the seven procedural patterns, preserving independent texture-alpha cutouts and ordinary Lit shading. Desktop tests pass (176/176 package tests). The original URP graphs and lighting shader remain unchanged in behavior. [POLYSPATIAL_IMPLEMENTATION.md](POLYSPATIAL_IMPLEMENTATION.md) records the source constraints, example, performance caveats and next steps.

This delivers the surface candidate, not a validated RealityKit material: PolySpatial is absent from the current host project, so MaterialX export, native lighting/cross-material shadows, stereo and device profiling remain open. Stamps and combined-lighting stylization are subsequent PolySpatial steps. Mobile planning favors unshadowed point lights; no existing pipeline setting is modified.

# Agent Implementation Rules

When implementing this design:

1. Inspect the repository before creating new architecture.
2. Reuse existing FuzzPhyte shader/rendering conventions.
3. Keep the initial implementation small.
4. Build the character vertical slice before expanding the feature set.
5. Prefer Shader Graph + one concise shared HLSL Custom Function.
6. Do not add a Renderer Feature for v1.
7. Do not add an additional texture sample solely for ordered dithering.
8. Minimize shader keywords and variants.
9. Preserve normal URP lighting unless explicitly extending the project later.
10. Treat headset measurements as authoritative.
11. Record baseline and post-change GPU measurements.
12. Validate stereo stability and head-motion stability in the headset.
13. Document any deviation from this architecture before broadening scope.
14. Do not claim performance success without target-device measurements.

---

# Completion Criteria

The initial feature is complete when all of the following are true:

- [x] Shared ordered-dither primitive exists.
- [x] Bayer 4x4 implementation requires no dither texture.
- [x] Character URP Lit Shader Graph exists.
- [x] Organic URP Lit Shader Graph exists.
- [ ] Character dither is surface-stable in VR.
- [ ] Organic dither is surface-stable in VR.
- [ ] Both shaders render correctly in stereo.
- [x] Strength can cleanly move between normal and stylized appearance (desktop).
- [x] Color quantization is independently controllable.
- [x] No new core render pass was introduced.
- [x] No Renderer Feature is required.
- [x] No unnecessary shader keyword explosion was introduced.
- [ ] Representative headset performance has been measured.
- [ ] Performance results are recorded.
- [x] Example materials demonstrate intended art direction (desktop comparison examples).
- [x] Documentation reflects the implemented shader behavior.

---

# Design Principle

The goal is not to make URP render normally and then place a dither filter over the finished image.

The goal is:

> **Make the surface materials themselves speak the dithered visual language while keeping the rendering path lightweight enough for VR.**

That distinction should govern implementation decisions throughout the work.
