# FP SVG Extruder

The FP SVG Extruder is an editor-only tool for converting selectable closed SVG regions into extruded Unity mesh assets.

## Open The Tool

Use the Unity menu:

```text
FuzzPhyte/Utility/Rendering/SVG Extruder
```

## Basic Workflow

1. Drag an SVG asset into the SVG File field or the drop area.
2. Click Parse SVG.
3. Regions with explicit SVG fill colors are selected automatically.
4. Use the right-side SVG Viewer / Selector to click closed outlines and toggle them as included.
5. Leave fully nested interior regions unselected when you want them treated as holes.
6. Adjust Scale, Extrusion Depth, Path Sample Distance, and output settings.
7. Click Generate Mesh.

Generated mesh assets default to:

```text
Assets/_FPUtility/
```

The default mesh name is based on the SVG file name:

```text
SourceFile_GeneratedSVGMesh
```

## Layout

- Left panel: SVG source, mesh settings, scene output, actions, and region toggles.
- Middle vertical slider: preview height.
- Right panel: SVG viewer and selector.
- Bottom panel: warnings, errors, and quick region counts.

## SVG Fill Selection

The parser reads explicit `fill` values from SVG attributes and inline `style` declarations. A region with a usable fill color is initially included, and the preview uses that color softly for selected fill areas.

Supported fill formats include:

- `fill="#ffffff"`
- `fill="#fff"`
- `fill="white"`
- `style="fill: rgb(255,255,255)"`
- `.st0 { fill: #fff; }` with `class="st0"`

Use `fill="none"` or transparent fill opacity when a region should start unselected.

## Notes

- Concave regions are triangulated with an in-repo ear-clipping triangulator.
- Boundary cleanup removes duplicate, nearly collinear, and simplify-tolerance points before triangulation.
- Triangle optimization flips internal diagonals when it improves local triangle quality.
- Unselected regions are only treated as holes when they are fully contained by an included region.
- Overlapping selected regions are generated as separate extrusions; they are not boolean-unioned yet.
- SVG transforms are detected as warnings and are not applied in this first pass.
- SVG arc commands are approximated as straight line segments.

## Mesh Cleanup Controls

- Path Sample Distance controls how densely curves are sampled from SVG paths.
- Simplify Tolerance removes tiny boundary detail after SVG coordinates are scaled into Unity space.
- Collinear Tolerance removes points that are nearly on the same straight edge.
- Optimize Triangles runs a local edge-flip pass to reduce long skinny surface triangles where possible.
