# Shader Update from Original Unity URP Outline

This original work was designed by [Unity-Technologies/Per-Object-Outline RenderGraph RendererFeature Example](https://github.com/Unity-Technologies/Per-Object_Outline_RenderGraph_RendererFeature_Example) and carried the Apache 2.0 license which can be found under the Original License Folder.

## Updates to Original Work

The sections of the URP render feature pass to include additional settings on the outline dilation shader to allow for a thickness and max radius values, the entire 'OutlineDilation.shader' was rewritten around the previous Unity work.

To then accommodate that update we've modified all files under the Runtime folder and adopted namespace conventions for FuzzPhyte packages.
