# Shader Update from Original Unity URP Outline

This original work was designed by [Unity-Technologies/Per-Object-Outline RenderGraph RendererFeature Example](https://github.com/Unity-Technologies/Per-Object_Outline_RenderGraph_RendererFeature_Example) and carried the Apache 2.0 license which can be found under the Original License Folder.

## Updates to Original Work

The sections of the URP render feature pass to include additional settings on the outline dilation shader to allow for a thickness and max radius values, the entire 'OutlineDilation.shader' was rewritten around the previous Unity work.

To then accommodate that update we've modified all files under the Runtime folder and adopted namespace conventions for FuzzPhyte packages.

## Active Project Setup

1. Add `FPBlurredBufferMultiObjectOutlineRendererFeature` to the active URP renderer.
2. Leave `Dilation Material` and `Outline Material` empty unless you need custom shader/material overrides. The feature creates internal runtime materials from `FuzzPhyte/Dilation` and `FuzzPhyte/Outline Color And Stencil`.
3. Create one or more outline profiles from `Create/FuzzPhyte/Utility/Outline Profile`.
4. Add `FPOutlineTarget` to any object that should be outlined.
5. Assign an `FPOutlineProfile` to the target, or leave it empty to use the render feature defaults.

`FPOutlineTarget` registers itself automatically and can include child renderers or use explicitly assigned renderers. The outline is still a render-feature effect, not a second material slot on the object. A second renderer material would affect normal object rendering and would not drive the screen-space dilation/composite pass.

## Outline Profiles

`FPOutlineProfile` stores the reusable visual settings for a target:

- `Outline Color`: color written into the outline mask.
- `Alpha Mode`: how the target contributes pixels to the mask.
- `Alpha Cutoff`: cutoff used by texture/mask alpha modes.
- `Custom Mask Texture`: optional mask texture for `CustomMaskTexture`.
- `Thickness`: opaque outline width in pixels.
- `Blur`: soft fade width after the thickness band.
- `Max Radius`: maximum search radius for the dilation shader.

Targets with matching `Thickness`, `Blur`, and `Max Radius` are batched together. Different profile sizes are supported, but each unique size group runs another mask/dilation/composite sequence, so keep a small set of shared profiles such as `Thin`, `Default`, and `Hero`.

## Alpha Modes

- `MeshSilhouette`: outline the rendered mesh silhouette. This is the fastest path and works well for opaque textured meshes.
- `MainTextureAlpha`: sample `_BaseMap` or `_MainTex` alpha from each source material. Use this for cutout/transparent textures where the outline should follow visible pixels instead of the whole mesh.
- `CustomMaskTexture`: use a specific mask texture assigned on the target.
