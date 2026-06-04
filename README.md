# FuzzPhyte Unity Tools

## Utility

FP_Utility is designed and built to be a simple set of base classes to be used in almost all future FuzzPhyte packages. There is an element of Scriptable Object and an element of just simple input/output functions as well as some core scripts timed to timers etc. There are a lot of static functions to help with file management and Unity Editor management. Please see the FP_UtilityData class as well as the FP_Utility_Editor class for a lot of these functions/enums/structs etc.

## Internal Utility Tools

### Convex Generator

Convex Generator is an editor-only mesh collider helper for creating a simplified convex `MeshCollider` asset from an existing mesh or scene object. It is intended for cases where a visual mesh is too detailed for collision, but a box or capsule collider is too rough.

Open the tool from `FuzzPhyte/Utility/Mesh/Convex Generator`.

#### Convex Generator - How To Use It

1. Select or assign a `GameObject`, `MeshFilter`, `SkinnedMeshRenderer`, component, prefab, or raw `Mesh` in the `Object / Mesh` field.
2. Choose whether `Include Children` should collect meshes below the assigned object. The default is disabled so the selected object is treated directly.
3. Click `Refresh Preview` to build the transparent convex preview around the source mesh.
4. Adjust `Decimated Points`, `Surface Planes`, `Merge Angle`, and `Surface Padding` until the collider shape is as tight and simple as needed.
5. Use the preview window to inspect the transparent generated collider mesh.
6. Click `Generate and Save Mesh` to save the collider mesh asset under the configured output folder.
7. If `Create Collider Child` is enabled, the tool creates a child object under the selected parent or source object with only a convex `MeshCollider` assigned.

#### Convex Generator - Preview Controls

* Left click and drag in the preview to freely orbit the view.
* Use the orbit gizmo's `+X`, `-X`, `+Y`, `-Y`, `+Z`, and `-Z` buttons to snap to cardinal views.
* Drag the `X`, `Y`, or `Z` orbit strips to rotate around a single world axis.
* Scroll over the preview to zoom. The zoom readout is shown in the overlay.
* Use `Projection` to switch between `Perspective` and `Orthographic`.
* Use `Invert Camera Orbit` to flip the preview orbit preference.
* Enable `Show Vertices` and `Show Edges` to inspect the generated mesh topology. Vertices use the shared orange/gold editor color and mesh faces use the shared blue preview color.
* The upper-right orientation triad follows Unity's scene view style and shows the current X/Y/Z view orientation.
* The overlay reports preview vertices, preview triangles, generated-to-source vertex ratio, decimated support points, and `Planes Used` compared to the requested `Surface Planes`.

#### Convex Generator - Settings Notes

* `Decimated Points` controls how many source points are retained as the simplified support set.
* `Surface Planes` controls the maximum number of convex clipping planes. More planes usually gives a tighter shape; fewer planes gives a simpler collider.
* `Merge Angle` merges similar plane directions. A higher value can reduce the actual `Planes Used` below the requested `Surface Planes`.
* `Surface Padding` expands the generated volume. Small values such as `0.001` are usually best for tight collider generation on small assets.
* `Contain Source Mesh` fits surface planes against the original vertices so the generated convex mesh contains the source mesh.
* Generated scene children are collider-only. The geometry asset is saved, but the scene child receives only a convex `MeshCollider`, with no `MeshRenderer`.

### Mesh Slicer

Mesh Slicer is an editor-only tool for cutting a source mesh with an adjustable plane and saving the resulting positive side, negative side, or both pieces. It is intended for authoring custom collision or split mesh assets when the cut needs to be inspected before assets are written.

Open the tool from `FuzzPhyte/Utility/Mesh/Mesh Slicer`.

#### Mesh Slicer - How To Use It

1. Select or assign a `GameObject`, `MeshFilter`, `SkinnedMeshRenderer`, component, prefab, or raw `Mesh` in the `Object / Mesh` field.
2. Choose whether `Include Children` should collect child meshes. The default is disabled.
3. Use `Reference Origin` to frame the preview and plane from either the selected object's pivot or a calculated bounds center.
4. Adjust the source with `Object Adjustment` if the cut should be previewed with an offset, rotation, or scale.
5. Move or rotate the `Slice Plane`, use `XY`, `XZ`, or `YZ` to snap it to a major plane, or click `Frame Plane` to refit it to the current source.
6. Choose `Keep Pieces` to save `Keep Positive`, `Keep Negative`, or `Keep Both`.
7. Click `Refresh Preview` if `Auto Update Preview` is disabled, then click `Generate and Save Slice Meshes` to save the slice result.

#### Mesh Slicer - Preview Controls

* The kept slice is shown in the shared light blue preview color. Removed slice regions are shown in red.
* `Show Source Mesh` can overlay the original source, but is disabled by default to avoid z-fighting with the generated slice preview.
* `Preview Visibility` controls whether the positive side, negative side, or both sides are shown.
* Enable `Show Vertices` and `Show Edges` to inspect slice topology and repaired caps.
* The slice plane draws front and back faces with separate colors and an outline so the plane direction and boundary are visible.
* Drag the plane center to move freely, drag the axis lines to move along an axis, and drag the orbit handles on the plane to rotate it.
* Left click and drag empty preview space to orbit the camera. Scroll over the preview to zoom.
* Use `Projection` to switch between `Perspective` and `Orthographic`, and use `Invert Camera Orbit` to flip the camera orbit preference.
* The upper-right orientation triad follows Unity's scene view style and shows the current X/Y/Z view orientation.

#### Mesh Slicer - Settings Notes

* `Repair Slice Holes` is enabled by default. It attempts to fill closed cut loops so sliced meshes can be saved with capped openings.
* If a cut creates open or ambiguous loops, the preview warning area reports that the holes could not be fully assembled.
* `Keep Pieces` defaults to `Keep Positive`.
* `Auto Update Preview` is enabled by default, but the preview rebuilds only when inputs change or the tool needs a repaint.
* Undo is supported for source changes, camera settings, object adjustment, plane movement, plane rotation, and slice options.

### Combine Meshes

Combine Meshes is an editor-only tool for baking multiple source meshes into one combined mesh asset. It is intended for scenes or prefab hierarchies where many separate visual or collider meshes should become a single reusable mesh, especially when generating consolidated `MeshCollider` assets.

Open the tool from `FuzzPhyte/Utility/Mesh/Combine Meshes`.

#### Combine Meshes - How To Use It

1. Assign a `Root Object`, or select a scene object and click `Use Current Selection As Root`.
2. Choose whether to include children and inactive objects.
3. Enable the source types you want to collect: `MeshFilters`, `SkinnedMeshRenderers`, and/or `MeshColliders`.
4. Review `Meshes Found (Preview)` to confirm the tool sees the expected sources.
5. Set the `Combined Mesh Name`.
6. Click `Combine Meshes and Save Asset`, then choose the asset save location in the project.

The generated mesh is baked into the local space of the chosen root object. This means child transforms are applied to the output vertices, so the saved mesh lines up with the root when used as a collider or debug mesh.

#### Combine Meshes - Preview Controls

* The right-side preview shows the combined mesh before saving.
* `Show Source Mesh` can overlay source geometry for comparison.
* Enable `Show Vertices` and `Show Edges` to inspect the combined topology.
* Use `Projection` to switch between `Perspective` and `Orthographic`, and use `Invert Camera Orbit` to flip the camera orbit preference.
* The upper-right orientation triad follows Unity's scene view style and shows the current X/Y/Z view orientation.

#### Combine Meshes - Output Options

* `Add MeshCollider to Root` assigns the saved combined mesh to a `MeshCollider` on the root object.
* `Replace Existing Collider` controls whether an existing root `MeshCollider` is reused. If disabled and a root collider already exists, the tool creates a child object for the new collider.
* `Collider Convex` sets the resulting `MeshCollider.convex` flag.
* `Collider Is Trigger` sets the resulting `MeshCollider.isTrigger` flag.

#### Combine Meshes - Source Notes

* `Skip 'EditorOnly' Tagged Objects` excludes any source object tagged `EditorOnly`.
* Mesh colliders are included only when their `sharedMesh` is assigned.
* If a visual mesh source from the same object has already been included, the matching `MeshCollider` source is skipped to avoid duplicate geometry.
* Large combined meshes automatically use 32-bit indices when the estimated vertex count is greater than 65,535.
* The output keeps source submeshes separate, which can be useful for inspection or later processing.

### Mesh Generator and FP Heightmap Editor

Mesh Generator is an editor-only tool for building rectangular grid meshes on the XZ plane. The grid can be saved as a mesh asset, created directly in the scene, or connected to an `FPMeshGridData` asset so it can be regenerated later. The related FP Heightmap Editor can inspect, paint, and save heightmap textures that deform those generated grids.

Open the generator from `FuzzPhyte/Utility/Mesh/Mesh Generator`.

Open the heightmap editor from `FuzzPhyte/Utility/Rendering/FP Heightmap Editor`, or from the generator with `Open Heightmap Editor`.

#### Mesh Generator - How To Use It

1. Optionally assign an `FPMeshGridData` asset in the `Data Asset` field.
2. Set the grid `Mesh Name`, `Width`, `Length`, `X Segments`, `Y Segments`, and `Center Pivot`.
3. Optionally assign a heightmap texture and choose `Height Scale`, `Height Offset`, channel, inversion, and X/Y flip settings.
4. Adjust height processing options such as remap, edge falloff, and terracing.
5. Set scene output options such as parent, material, `Add MeshCollider`, and `Auto Update Preview`.
6. Click `Create Scene Object` to create a live scene mesh, or `Save Mesh Asset` to save the generated mesh to the project.

The generated grid uses UV0 coordinates for heightmap sampling. If no heightmap is assigned, the tool generates a flat grid. If a heightmap is assigned, vertices are displaced on the Y axis using the selected texture channel and processing settings.

#### Mesh Generator - Preview Controls

* The right-side preview shows the generated grid before it is created in the scene or saved as an asset.
* Enable `Show Vertices` and `Show Edges` to inspect the grid topology and heightmap deformation.
* Use `Projection` to switch between `Perspective` and `Orthographic`, and use `Invert Camera Orbit` to flip the camera orbit preference.
* The upper-right orientation triad follows Unity's scene view style and shows the current X/Y/Z view orientation.

#### FPMeshGridData

`FPMeshGridData` is a ScriptableObject recipe for grid generation. Create one from `Assets/Create/FuzzPhyte/Utility/Design/Mesh Grid Data`.

The asset stores:

* `GridSettings`, including mesh name, width, length, segment counts, and pivot mode.
* `HeightmapSettings`, including the heightmap texture, height scale, offset, channel, inversion, and flips.
* `HeightProcessSettings`, including remap, edge falloff, and terracing.

Use `Load Settings From Data Asset` to pull a recipe into the generator. Use `Save Current Settings To Data Asset` to write the current generator and heightmap settings back into the asset.

When `Create Scene Object` is used with a data asset assigned, the scene object receives an `FPMeshGridInstance`. That instance references the data asset and can regenerate the mesh in edit mode. It also stores preview material, collider preference, and `AutoRegenerateInEditor`.

#### FPMeshGridInstance

`FPMeshGridInstance` is the scene component that turns an `FPMeshGridData` recipe into a mesh. It requires a `MeshFilter` and `MeshRenderer`, and can optionally keep a `MeshCollider` synced with the generated mesh.

Instances can regenerate in several ways:

* Changes to the assigned `FPMeshGridData` trigger regeneration when `AutoRegenerateInEditor` is enabled.
* The component inspector has `Regenerate Mesh` and `Save Mesh Asset` actions.
* The menu item `GameObject/FuzzPhyte/Rendering/Regenerate Selected Mesh Grid` regenerates selected grid instances.
* The heightmap editor can use a selected grid instance as a live preview target while painting a working heightmap copy.

#### FP Heightmap Editor - How It Connects

The FP Heightmap Editor can work with either an `FPMeshGridData` asset or a direct heightmap texture. When opened from the mesh generator, it receives the current data asset and heightmap reference.

Use the heightmap editor to:

* Preview the source texture, grayscale values, or individual red, green, blue, and alpha channels.
* Inspect texture statistics and a histogram for the selected preview mode.
* Create a non-destructive working copy of a heightmap.
* Paint height values with raise, lower, or set brush modes.
* Use brush size, rotation, softness, strength, set value, and optional brush masks.
* Save the working copy as a PNG and assign it back to the `FPMeshGridData` heightmap settings.
* Use `Live Mesh Preview` with an `FPMeshGridInstance` to update the generated grid as brush edits settle.

#### Heightmap Processing Notes

* Heightmaps are sampled through the generated grid's UV0 coordinates.
* `Use Remap` isolates a useful height range before applying displacement.
* `Edge Falloff` can soften edges or create rectangular/radial island-like surfaces.
* `Use Terracing` quantizes height values into stepped levels.
* `Use GPU Working Copy` enables GPU-backed editing and debug views for source, shader source, mask influence, and final influence.
* Brush edits are made on a working copy. The original source texture is not changed until a new PNG is saved.

### FP Audio Segment Tool

FP Audio Segment Tool is an editor-only AudioClip trimming and cleanup helper. It lets you preview a source clip waveform, choose an in/out segment, add independent mute or cut regions, and export the processed result as a WAV asset.

Open the tool from `FuzzPhyte/Utility/Audio/Segment Tool`.

#### FP Audio Segment Tool - How To Use It

1. Select an AudioClip in the Project window or assign one in the `Source Clip` field.
2. Use `Segment (In/Out)` to choose the main segment window.
3. Move the `Playhead` with the slider or by clicking in the waveform.
4. Use `Set In = Playhead`, `Set Out = Playhead`, `Jump Playhead to In`, and `Jump Playhead to Out` to refine the segment.
5. Use the region picker to add `Mute` or `Cut` regions independent of the main in/out segment.
6. Use `Play Segment (in/out)` for the raw segment preview, or `Play Segment + Regions` to preview the segment after mute/cut regions are applied.
7. Use `Create In-Memory Segment` or `Save Segment as .wav in Assets` to create the output clip.

#### FP Audio Segment Tool - Region Notes

* `Mute` regions preserve timeline length and silence the selected span with edge fades.
* `Cut` regions remove the selected span and compress time, with small crossfades at joins.
* Region overlays are drawn on the waveform so the selected cleanup regions stay visible while adjusting the playhead.
* The processed export path applies the main in/out segment first, then applies region edits within that segment.
* `Waveform Thickness` controls preview amplitude display only; it does not change the exported audio.

### FP Audio Combine Tool

FP Audio Combine Tool is an editor-only multi-clip audio assembly window. It is intended for building a combined WAV from several AudioClips while preserving per-clip trimming, spacing, ordering, gain, and inclusion settings.

Open the tool from `FuzzPhyte/Utility/Audio/Combine Tool`.

#### FP Audio Combine Tool - How To Use It

1. Open the tool and either assign an existing `FPAudioCombineData` asset in `Mix Data`, or enter a `Name` and `Folder` so `Save` can create one for the current stack.
2. Drag AudioClip assets onto `Drop AudioClip(s) Here`, or use `+ Add` in the `Stack` panel to create an empty clip row.
3. Use the left parameter stack to adjust each clip. The matching right-side timeline row updates in the viewer.
4. For each clip, use `Segment` or the `In` and `Out` fields to trim the source audio. Trimming the left edge holds the clip's timeline position and removes audio from the front instead of sliding the clip earlier.
5. Place clips with the `Start` field, the `Move` slider, `Start = Playhead`, `After Prev`, or by dragging the clip block directly in the right-side viewer.
6. Use `+ Fade In` and `+ Fade Out` to add per-clip fades. Drag the fade edge handle to change fade length, or drag the small curve handle on the fade curve to adjust `In C` or `Out C`.
7. Move the `Playhead` with the slider, by clicking the overview, or by grabbing the playhead handle in the top timeline viewer. Use `Play From Playhead`, `Play From Beginning`, and `Stop` to preview the current mix.
8. Use `Set Export Start {` and `Set Export End }` to drop export bookends from the playhead. Export start and end are validated so the start cannot sit after the end.
9. Use `Create In-Memory Combined Clip` or `Save Combined as .wav in Assets` to generate the combined output.
10. Use `Save` to write the current clip stack, mix settings, bookends, colors, gain, fades, lock states, and mute states back to `FPAudioCombineData`.

#### FP Audio Combine Tool - Clip Controls

* `Track Color` assigns a visual color per clip; new clips get generated colors automatically.
* `Clip Gain` adjusts per-clip level before mixing and is reflected in the waveform height and gain bar.
* `+ Fade In` and `+ Fade Out` add non-destructive fades to a clip. Fade duration and fade curve power are shown in the waveform and applied to preview and export.
* `Locked` prevents editing, dragging, reordering, removing, nudging, and auto-layout movement for that row.
* `Muted` keeps a clip visible in the editor but excludes it from preview and export.
* The overview and row waveforms draw muted clips dimmed and locked clips with a lock-style highlight.
* `Auto` lays out unlocked clips in sequence using `Default Gap (sec)`.
* `x Clear` clears the current stack and export bookends after a warning confirmation. It does not delete saved mix data assets.
* `Set Export Start {` and `Set Export End }` drop export bookends. Preview/export trims outside those bookends until they are removed or overwritten.
* `Normalize if mix clips` keeps the final combined output from clipping when overlapping or loud clips exceed full scale.

### FP Header

FP Header is an editor-only hierarchy organization tool for Unity scenes. It lets you create disabled, all-caps GameObjects that act like visual section headers in the standard Unity Hierarchy without forcing the grouped objects into a parent-child transform relationship. This is useful when you want the readability and collapse behavior of folders, but you do not want to change transform inheritance or scene structure.

A header is treated as valid when the GameObject name is all caps, the object is inactive, and it has no children. Objects that appear after that header in the same sibling scope are treated as part of the section until the next valid header is found.

#### Header Tool - How To Use It

1. Create or identify an `FP_HHeaderData` asset.
2. Add the header names you want in the asset's `Headers` list and optionally assign colors and icons.
3. Either right click the `FP_HHeaderData` asset and use `Assets/FuzzPhyte/Header/Create Headers`, or open `FuzzPhyte/Header/Header Options` and press `Create Headers From Data`.
4. In the Hierarchy, use the custom foldout on the header row to expand or collapse the section.
5. If the Scene mesh picker is enabled, clicking an object under a collapsed header in the Scene view will expand the owning header and select that object.

You can also use the `Header Options` window to apply the visual style from an `FP_HHeaderData` asset without creating new header GameObjects.

#### Header Tool - Menu Options

* `FuzzPhyte/Header/Enable FP_HHeader`
  * Enables or disables the header system for the active scene.
* `FuzzPhyte/Header/Enable Scene Mesh Picker`
  * Enables or disables the Scene view mesh-picking cache used to select objects that belong to collapsed headers.
* `FuzzPhyte/Header/Header Options`
  * Opens the editor window for assigning an `FP_HHeaderData` asset, applying its style, or creating headers from it.
* `Assets/FuzzPhyte/Header/Create Headers`
  * Creates header GameObjects from the selected `FP_HHeaderData` asset.
* `Assets/FuzzPhyte/Header/Save Headers`
  * Saves the current header setup and style values into a new `FP_HHeaderData` asset.
* `GameObject/FuzzPhyte/Header/Expand Z Sections`
  * Expands all detected headers in the current scene.
* `GameObject/FuzzPhyte/Header/Collapse Z Sections`
  * Collapses all detected headers in the current scene.

### FP Scene Asset Tool

FP Scene Asset Tool is an editor window that scans the active scene and builds a reference list of the external assets used by that scene. It is intended to help you audit scene dependencies such as materials, meshes, textures, audio clips, prefab assets, animation assets, ScriptableObjects, fonts, and other referenced content so you can spot misplaced project references, redundant assets, or content coming from the wrong part of the project.

The tool deduplicates assets by path, groups them by detected type, tracks which scene GameObjects reference each asset, and marks package or built-in dependencies as non-selectable so you can focus on project content you actually control.

#### Scene Asset Tool - How To Use It

1. Open the window from `FuzzPhyte/Utility/Scene/Asset Tool`.
2. Click `Scan Scene for Assets` to collect all asset dependencies for the active scene.
3. Review the counters at the top of the window for total, selectable, and currently selected assets.
4. Use the type toggles and `Select By Checked Types` to bulk-select categories.
5. Use the `Search` field to filter by asset name or asset path.
6. Use `Object` to select and ping the scene objects that reference the asset.
7. Use `Ping` to ping and select the asset itself in the Project view.
8. Optionally export the current results with `Save to JSON` for manual searching outside the window.

The tool also supports moving selected project assets into a destination folder. This can be useful when organizing content after an audit, but it should be used carefully because it changes asset locations in the project.

#### Bulk Texture Import Settings

When selected scene assets include textures, the tool displays a `Texture Import Settings` panel. Use this panel to set a shared max texture size for the selected Texture2D and Sprite-backed assets.

The change is applied to both the texture importer's default max size and the active build target platform override. The panel displays the active build target so you can confirm which platform override will be changed before applying the batch update. Use `Undo Last Max Size Change` to restore the previous default and platform-specific values from the last texture batch operation.

#### Bulk Audio Import Settings

When selected scene assets include AudioClip assets, the tool displays an `Audio Import Settings` panel. Use this panel to batch set load type, preload audio data, load in background, compression format, and quality for the selected audio importers.

The change is applied to the audio importer's default sample settings and the active build target platform override where supported. Use `Undo Last Audio Change` to restore the previous default settings, load-in-background value, and platform override state from the last audio batch operation.

#### Scene Asset Tool - Menu & Window Actions

* `FuzzPhyte/Utility/Scene/Asset Tool`
  * Opens the `FP Scene Asset Tool` editor window.
* `Scan Scene for Assets`
  * Scans the active scene and builds the asset reference list.
* `Select All` and `Unselect All`
  * Bulk-toggle selectable assets in the results list.
* `Select By Checked Types`
  * Selects assets that match the enabled type filters.
* `Search`
  * Filters the scanned results by asset name or asset path.
* `Object`
  * Selects and pings the scene object or objects that reference the listed asset.
* `Ping`
  * Pings the underlying asset in the Project window and makes it the active selection.
* `Save to JSON`
  * Dumps the scanned asset list to a JSON file under `Assets/_FPUtility` by default.

## Software Architecture

FP_Utility has a core data class for ScriptableObjects called FP_Data. This is heavily used for all generic data classes and in other packages there could be further extension of this for generic ScriptableObjects that need a sort of UniqueID. There are additional sub-folders by domain areas. For example, there is a simple IK manager script located in the FuzzPhyte.Utility.Animation namespace. Some of these sub-folders contain their own domain assembly. There are then sections broken up by Scene asset(s), tools for Audio & Video, and other static/instance utility classes for conversions, enums, states, etc.

### Ways to Extend

## Dependencies

Please see the [package.json](./package.json) file for more information.

## License Notes

See [LICENSE.md](LICENSE.md) for details

## Contact

* [John Shull](mailto:JShull@fuzzphyte.com)

### Additional Notes

* Audio Files in the samples came from [FreeSound.org](https://freesound.org/)
