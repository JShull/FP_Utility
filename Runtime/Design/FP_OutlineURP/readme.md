# Shader Update from Original Unity URP Outline

This original work was designed by [Unity-Technologies/Per-Object-Outline RenderGraph RendererFeature Example](https://github.com/Unity-Technologies/Per-Object_Outline_RenderGraph_RendererFeature_Example) and carried the Apache 2.0 license which can be found under the Original License Folder.

## Updates to Original Work

The sections of the URP render feature pass to include additional settings on the outline dilation shader to allow for a thickness and max radius values, the entire 'OutlineDilation.shader' was rewritten around the previous Unity work.

To then accommodate that update we've modified all files under the Runtime folder and adopted namespace conventions for FuzzPhyte packages.

## Usage

Add `FPBlurredBufferMultiObjectOutlineRendererFeature` to the active URP renderer. The feature can use assigned outline/dilation materials, but will also create internal runtime materials from `FuzzPhyte/Outline Color And Stencil` and `FuzzPhyte/Dilation` when those fields are empty.

For object-level usage, add `FPOutlineTarget` to any object that should be outlined. The target registers itself automatically and can include child renderers, use explicitly assigned renderers, choose an outline color, and choose how alpha should affect the mask:

- `MeshSilhouette`: outline the rendered mesh silhouette. This is the fastest path and works well for opaque textured meshes.
- `MainTextureAlpha`: sample `_BaseMap` or `_MainTex` alpha from each source material. Use this for cutout/transparent textures where the outline should follow visible pixels instead of the whole mesh.
- `CustomMaskTexture`: use a specific mask texture assigned on the target.

The outline is still a render-feature effect, not a second material slot on the object. A second renderer material would affect normal object rendering and would not drive the screen-space dilation/composite pass.
