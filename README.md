# FuzzPhyte Unity Tools

## Utility

FP_Utility is designed and built to be a simple set of base classes to be used in almost all future FuzzPhyte packages. There is an element of Scriptable Object and an element of just simple input/output functions as well as some core scripts timed to timers etc. There are a lot of static functions to help with file management and Unity Editor management. Please see the FP_UtilityData class as well as the FP_Utility_Editor class for a lot of these functions/enums/structs etc.

## Internal Utility Tools

### Simple Convex Generator

Simple Convex Generator is an editor-only mesh collider helper for creating a simplified convex `MeshCollider` asset from an existing mesh or scene object. It is intended for cases where a visual mesh is too detailed for collision, but a box or capsule collider is too rough.

Open the tool from `FuzzPhyte/Utility/Rendering/Simple Convex Generator`.

#### Simple Convex Generator - How To Use It

1. Select or assign a `GameObject`, `MeshFilter`, `SkinnedMeshRenderer`, component, prefab, or raw `Mesh` in the `Object / Mesh` field.
2. Click `Refresh Preview` to build the transparent convex preview around the source mesh.
3. Adjust `Decimated Points`, `Surface Planes`, `Merge Angle`, and `Surface Padding` until the collider shape is as tight and simple as needed.
4. Use the preview window to compare the original rendered mesh against the transparent generated collider mesh.
5. Click `Generate and Save Mesh` to save the collider mesh asset under the configured output folder.
6. If `Create Collider Child` is enabled, the tool creates a child object under the selected parent or source object with only a convex `MeshCollider` assigned.

#### Simple Convex Generator - Preview Controls

* Left click and drag in the preview to freely orbit the view.
* Use the orbit gizmo's `+X`, `-X`, `+Y`, `-Y`, `+Z`, and `-Z` buttons to snap to cardinal views.
* Drag the `X`, `Y`, or `Z` orbit strips to rotate around a single world axis.
* Scroll over the preview to zoom. The zoom readout is shown in the overlay.
* The overlay reports preview vertices, preview triangles, generated-to-source vertex ratio, decimated support points, and `Planes Used` compared to the requested `Surface Planes`.

#### Simple Convex Generator - Settings Notes

* `Decimated Points` controls how many source points are retained as the simplified support set.
* `Surface Planes` controls the maximum number of convex clipping planes. More planes usually gives a tighter shape; fewer planes gives a simpler collider.
* `Merge Angle` merges similar plane directions. A higher value can reduce the actual `Planes Used` below the requested `Surface Planes`.
* `Surface Padding` expands the generated volume. Small values such as `0.001` are usually best for tight collider generation on small assets.
* `Contain Source Mesh` fits surface planes against the original vertices so the generated convex mesh contains the source mesh.
* Generated scene children are collider-only. The geometry asset is saved, but the scene child receives only a convex `MeshCollider`, with no `MeshRenderer`.

### FP Mesh Combiner

FP Mesh Combiner is an editor-only tool for baking multiple source meshes into one combined mesh asset. It is intended for scenes or prefab hierarchies where many separate visual or collider meshes should become a single reusable mesh, especially when generating consolidated `MeshCollider` assets.

Open the tool from `FuzzPhyte/Utility/Rendering/FP Mesh Combiner`.

#### FP Mesh Combiner - How To Use It

1. Assign a `Root Object`, or select a scene object and click `Use Current Selection As Root`.
2. Choose whether to include children and inactive objects.
3. Enable the source types you want to collect: `MeshFilters`, `SkinnedMeshRenderers`, and/or `MeshColliders`.
4. Review `Meshes Found (Preview)` to confirm the tool sees the expected sources.
5. Set the `Combined Mesh Name`.
6. Click `Combine Meshes and Save Asset`, then choose the asset save location in the project.

The generated mesh is baked into the local space of the chosen root object. This means child transforms are applied to the output vertices, so the saved mesh lines up with the root when used as a collider or debug mesh.

#### FP Mesh Combiner - Output Options

* `Add MeshCollider to Root` assigns the saved combined mesh to a `MeshCollider` on the root object.
* `Replace Existing Collider` controls whether an existing root `MeshCollider` is reused. If disabled and a root collider already exists, the tool creates a child object for the new collider.
* `Collider Convex` sets the resulting `MeshCollider.convex` flag.
* `Collider Is Trigger` sets the resulting `MeshCollider.isTrigger` flag.

#### FP Mesh Combiner - Source Notes

* `Skip 'EditorOnly' Tagged Objects` excludes any source object tagged `EditorOnly`.
* Mesh colliders are included only when their `sharedMesh` is assigned.
* If a visual mesh source from the same object has already been included, the matching `MeshCollider` source is skipped to avoid duplicate geometry.
* Large combined meshes automatically use 32-bit indices when the estimated vertex count is greater than 65,535.
* The output keeps source submeshes separate, which can be useful for inspection or later processing.

### FP Mesh Generator and Heightmap Editor

FP Mesh Generator is an editor-only tool for building rectangular grid meshes on the XZ plane. The grid can be saved as a mesh asset, created directly in the scene, or connected to an `FPMeshGridData` asset so it can be regenerated later. The related FP Heightmap Editor can inspect, paint, and save heightmap textures that deform those generated grids.

Open the generator from `FuzzPhyte/Utility/Rendering/FP Mesh Generator`.

Open the heightmap editor from `FuzzPhyte/Utility/Rendering/FP Heightmap Editor`, or from the generator with `Open Heightmap Editor`.

#### FP Mesh Generator - How To Use It

1. Optionally assign an `FPMeshGridData` asset in the `Data Asset` field.
2. Set the grid `Mesh Name`, `Width`, `Length`, `X Segments`, `Y Segments`, and `Center Pivot`.
3. Optionally assign a heightmap texture and choose `Height Scale`, `Height Offset`, channel, inversion, and X/Y flip settings.
4. Adjust height processing options such as remap, edge falloff, and terracing.
5. Set scene output options such as parent, material, `Add MeshCollider`, and `Auto Update Preview`.
6. Click `Create Scene Object` to create a live scene mesh, or `Save Mesh Asset` to save the generated mesh to the project.

The generated grid uses UV0 coordinates for heightmap sampling. If no heightmap is assigned, the tool generates a flat grid. If a heightmap is assigned, vertices are displaced on the Y axis using the selected texture channel and processing settings.

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

1. Add clips with `Add Selected Clip(s)`, `Add Empty Row`, or by dragging AudioClip assets onto the bottom drop area.
2. For each clip, use `Segment (In/Out)` to trim the source clip without changing its base timeline placement.
3. Set `Clip Start` directly, use `Set Start = Playhead`, or use `After Previous + Gap` to place clips in sequence.
4. Use `Nudge This + Later` to shift a clip and following unlocked clips together.
5. Use the top overview to see all clips at once and drag unlocked clip blocks along the timeline.
6. Move the `Playhead` with the slider or by clicking in the overview or waveform tracks, then use `Play From Playhead` or `Play All`.
7. Use `Create In-Memory Combined Clip` or `Save Combined as .wav in Assets` to generate the combined output.

#### FP Audio Combine Tool - Clip Controls

* `Track Color` assigns a visual color per clip; new clips get generated colors automatically.
* `Clip Gain` adjusts per-clip level before mixing and is reflected in the waveform height and gain bar.
* `Locked` prevents editing, dragging, reordering, removing, nudging, and auto-layout movement for that row.
* `Muted` keeps a clip visible in the editor but excludes it from preview and export.
* The overview and row waveforms draw muted clips dimmed and locked clips with a lock-style highlight.
* `Set Export Start = Playhead` drops an export-start bookend. Preview/export trims everything before that bookend until `Remove Export Start` is used.
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

* This software running a dual license
* Most of the work this repository holds is driven by the development process from the team over at Unity3D :heart: to their never ending work on providing fantastic documentation and tutorials that have allowed this to be born into the world.
* I personally feel that software and it's practices should be out in the public domain as often as possible, I also strongly feel that the capitalization of people's free contribution shouldn't be taken advantage of.
  * If you want to use this software to generate a profit for you/business I feel that you should equally 'pay up' and in that theory I support strong copyleft licenses.
  * If you feel that you cannot adhere to the GPLv3 as a business/profit please reach out to me directly as I am willing to listen to your needs and there are other options in how licenses can be drafted for specific use cases, be warned: you probably won't like them :rocket:

### Educational and Research Use MIT Creative Commons

* If you are using this at a Non-Profit and/or are you yourself an educator and want to use this for your classes and for all student use please adhere to the MIT Creative Commons License
* If you are using this back at a research institution for personal research and/or funded research please adhere to the MIT Creative Commons License
  * If the funding line is affiliated with an [SBIR](https://www.sbir.gov) be aware that when/if you transfer this work to a small business that work will have to be moved under the secondary license as mentioned below.

### Commercial and Business Use GPLv3 License

* For commercial/business use please adhere by the GPLv3 License
* Even if you are giving the product away and there is no financial exchange you still must adhere to the GPLv3 License

## Contact

* [John Shull](mailto:JShull@fuzzphyte.com)

### Additional Notes

* Audio Files in the samples came from [FreeSound.org](https://freesound.org/)
