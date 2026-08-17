# FuzzPhyte Unity Tools

## Utility

FP_Utility is designed and built to be a simple set of base classes to be used in almost all future FuzzPhyte packages. There is an element of Scriptable Object and an element of just simple input/output functions as well as some core scripts timed to timers etc. There are a lot of static functions to help with file management and Unity Editor management. Please see the FP_UtilityData class as well as the FP_Utility_Editor class for a lot of these functions/enums/structs etc.

Unity editor object identity uses `EntityId` on Unity 6.3 and newer. `FP_Utility_Editor.GetEntityIdFromGUID` and `ReturnGUIDFromEntityId` provide GUID conversion without deprecated instance-ID APIs; the previous integer helpers remain as obsolete compatibility wrappers.

## Runtime Debug Tools

### Runtime Debug Draw

Runtime Debug Draw is an editor-only runtime visualization helper for drawing gizmo-like marks into scene and game cameras while working in the Unity Editor. It is intended for quick gameplay debugging where another system owns the state and only needs a lightweight way to show what is happening in the world.

The runtime API lives in `FuzzPhyte.Utility.DebugTools.FPRuntimeDebugDraw`. Calls are build-safe no-ops outside the Unity Editor, so gameplay scripts can call the debug methods without wrapping every call in `#if UNITY_EDITOR`.

#### Runtime Debug Draw - How To Use It

1. Add calls from the script that already knows the current debug state, such as a selection manager, event manager, character controller, or click handler.
2. Call the draw methods each frame for persistent state such as a selected actor, active target, or navigation link.
3. Pass a `duration` when a mark should remain visible for a short time, such as a click marker or one-shot event.
4. Use `depthTest` to choose whether the visual should obey scene depth or draw over the scene like an always-visible handle.

Example selected-character command debug:

```csharp
using FuzzPhyte.Utility.DebugTools;
using UnityEngine;

public class CharacterOrderDebug : MonoBehaviour
{
    [SerializeField] private Transform selectedCharacter;
    [SerializeField] private Vector3 targetPoint;

    private void LateUpdate()
    {
        if (selectedCharacter == null)
        {
            return;
        }

        FPRuntimeDebugDraw.DrawSelectionMarker(selectedCharacter.position, 0.75f, Color.cyan);
        FPRuntimeDebugDraw.DrawTargetMarker(targetPoint, 0.35f, Color.yellow);
        FPRuntimeDebugDraw.DrawLink(selectedCharacter.position, targetPoint, Color.yellow);
    }
}
```

#### Runtime Debug Draw - Available Visuals

* `DrawLine` and `DrawRay` draw simple world-space links.
* `DrawCameraRelativeRay` draws a ray whose length adapts per rendering camera, similar to handle sizing.
* `DrawPoint` draws a camera-facing point quad and can scale relative to the active camera.
* `DrawWireCircle`, `DrawWireSphere`, `DrawWireBox`, `DrawBounds`, and `DrawPlane` cover common gizmo-style shapes.
* `DrawSelectionMarker` draws a quick selected-object visual.
* `DrawTargetMarker` and `DrawClickMarker` draw destination or click feedback marks.
* `DrawLink` draws a connection line with optional endpoint points.
* `DrawMeshEdges`, `DrawMeshVertices`, and `DrawMeshNormals` provide mesh inspection helpers for runtime debugging.

#### Runtime Debug Draw - Notes

* Runtime Debug Draw does not own selection, click, event, or character state. The calling system decides when a visual should be drawn.
* The renderer batches line primitives and camera-facing point primitives by depth mode to keep a few hundred debug marks lightweight.
* The system uses only `UnityEngine` APIs and does not require URP renderer features, TMP, Input System, or ECS.
* ECS is not required for typical debug overlay use. If debug data already lives in Entities or tens of thousands of markers need to be visualized every frame, an ECS or buffer-backed adapter can be layered on later without changing normal caller code.

### Runtime Mesh Debug Overlay

`FPRuntimeMeshDebugOverlay` is a drop-in component for visualizing a `MeshFilter` or assigned mesh during editor Play Mode. It uses Runtime Debug Draw to show mesh edges, vertices, normals, and bounds in the camera.

#### Runtime Mesh Debug Overlay - How To Use It

1. Add `FPRuntimeMeshDebugOverlay` to a GameObject with a `MeshFilter`, or assign an `Override Mesh`.
2. Enable the overlays you need: `Draw Edges`, `Draw Vertices`, `Draw Normals`, or `Draw Bounds`.
3. Use the camera-relative vertex and normal settings to keep debug marks readable as the camera moves or switches between perspective and orthographic modes.
4. Toggle `Depth Test` depending on whether the debug overlay should sit inside the scene depth or draw over it.

### Runtime URP Mesh View Render Features

The render features under `Runtime/Design/FP_MeshView/URP` are the build-capable URP path for cutaway rendering, mesh inspection, measurement marks, and world grids. They are separate from the editor-only Runtime Debug Draw system above. The current implementations use URP Render Graph APIs and are intended for the Unity and URP versions declared by this package.

Each effect has up to three cooperating parts:

* A `ScriptableRendererFeature` on the URP Renderer Data asset decides when and where to draw.
* A scene component supplies the active runtime state.
* A matching material supplies the shader used by the scene renderer or custom pass.

#### One-Time URP Renderer and Camera Setup

1. In a project that uses URP, select the Universal Renderer Data asset referenced by the active URP Pipeline Asset. This is the Renderer asset, not the Pipeline Asset itself.
2. Use `Add Renderer Feature` to add only the features the application needs. The source renderer features are in `Runtime/Design/FP_MeshView/URP`.
3. Use these default pass events unless the project has a deliberate custom render order:

| Renderer feature | Default pass event | Runtime provider |
| --- | --- | --- |
| `FPRuntimeCutawayGeometryFeature` | `BeforeRenderingOpaques` | `FPRuntimeCutawayVolume` |
| `FPRuntimeCutawayRevealFeature` | `AfterRenderingTransparents` | `FPRuntimeCutawayVolume` |
| `FPRuntimeMeshViewerFeature` | `AfterRenderingOpaques` | `FPRuntimeMeshViewer` |
| `FPRuntimeGridFeature` | `AfterRenderingOpaques` | One or more `FPRuntimeGridPlane` components |
| `FPRuntimeMeasurementFeature` | `AfterRenderingTransparents` | `FPRuntimeMeasurementOverlay` |

4. Confirm that the runtime Camera uses that Renderer Data. Either make its Pipeline Asset renderer the default, or select the matching renderer under the Camera's URP `Rendering > Renderer` setting.
5. For layer-filtered cutaway passes, include the selected target layers in the Camera `Culling Mask` as well as in the renderer feature's layer mask.
6. `FPRuntimeMeshViewerFeature`, `FPRuntimeGridFeature`, and `FPRuntimeMeasurementFeature` expose `Draw In Scene View`. Leave it disabled for Game-camera-only output or enable it for authoring previews. The cutaway features currently run for every camera that uses their Renderer Data and has a matching active volume.

No special projection, field of view, or post-processing setting is required; perspective and orthographic Game cameras use the same feature setup. These features use the active camera depth attachment for normal depth testing but do not sample the URP Opaque Texture or Depth Texture, so those Pipeline Asset copies do not need to be enabled solely for these effects. With camera stacking, add the features only to the renderer or renderers that should draw the effect; using the same configured renderer on multiple cameras can draw it more than once.

Do not edit a Renderer Data asset or material inside an immutable UPM `PackageCache`. Create project-owned Renderer Data and materials under `Assets`, or import one of the package samples through Package Manager and edit the imported copies. The `URP Profiles` sample contains example mesh-viewer, grid, and measurement feature configuration, but material and scene-component assignment is still required.

#### Runtime Cutaway Geometry

`FPRuntimeCutawayGeometryFeature` does not replace an object's material. It sets the global cutaway values and redraws opaque renderers on `Target Layers` using their existing `UniversalForward` or `SRPDefaultUnlit` pass. A target material must therefore read `_VolumeCenter`, `_SphereRadius`, `_BoxExtents`, and `_UseSphere`; an ordinary URP Lit material will be redrawn but will not cut away.

Setup:

1. Create a dedicated layer for the geometry to be clipped, assign the target renderers to it, and select that layer in the feature's `Target Layers` field. Also include it in the Camera `Culling Mask`.
2. Create or copy a material that uses one of the cutaway shaders under `Runtime/Design/FP_MeshView`, then assign it to each target renderer:
   * `CutawayWall_SG_URP.shadergraph` is the full surface option. Enable its `Cutaway Enabled` material property and configure the base texture/color, edge color/thickness, cross-fill color/thickness, smoothness, and related surface properties. Its volume properties are declared globally and are driven by `FPRuntimeCutawayVolume`.
   * `FuzzPhyte/CutawayWallURP` is a minimal textured, unlit cutaway using `Texture` and `Color`.
   * `FuzzPhyte/VolumetricCutawayClipURP` is a minimal flat-color clip shader using `Base Color`.
3. Add one enabled `FPRuntimeCutawayVolume` to the scene. Its Transform position is the world-space cutaway center.
4. For a spherical cutaway, enable `Use Sphere` and set `Sphere Radius`. For a box, disable `Use Sphere` and set `Box Extents`; these are half-extents, so `(1, 2, 3)` produces a box that is `2 x 4 x 6` world units.
5. Move the volume or change its public fields at runtime to animate the cutaway. `Use Gizmo` affects only the editor gizmo.

The current box calculation is world-axis-aligned: the volume Transform's rotation and scale are not read. Change `Box Extents` to resize it. The system also has one static `FPRuntimeCutawayVolume.Active`, so keep only one enabled cutaway volume at a time; the most recently enabled instance becomes active.

`FuzzPhyte/VolumetricCutawayDepthURP` and `CutawayDepthBufURP.mat` are depth-only cutaway assets. The geometry feature does not select or inject that material, so they are not required for the standard setup and only take effect when a project deliberately assigns or draws them in an additional depth workflow.

#### Optional Cutaway Reveal Pass

`FPRuntimeCutawayRevealFeature` redraws every renderer on `Reveal Layer` with one override material, but keeps only the fragments inside the active cutaway volume. This is useful for tinting internal geometry or objects exposed by the hole.

1. Add `FPRuntimeCutawayRevealFeature` to the same Renderer Data.
2. Create a project-owned material using `FuzzPhyte/VolumetricCutawayReveal`, or copy `ClipReveal.mat`, and assign it as `Reveal Material`.
3. Put the geometry to reveal on a dedicated layer and select it as `Reveal Layer`. Include that layer in the Camera `Culling Mask`.
4. Set `Reveal Color` on the material. The pass writes only fragments inside the active sphere or box.

The reveal shader currently uses `ZTest Always`, `Cull Off`, and `ZWrite Off`. It does not declare standard alpha blending, so the `Reveal Color` alpha should not be treated as conventional transparency. The feature is optional and the packaged `FP_Inventory_Utility_URP_Renderer` currently stores its reveal entry inactive; enable and reassign it in a project-owned Renderer Data asset before relying on it.

#### Runtime Mesh Viewer

`FPRuntimeMeshViewerFeature` overlays selected renderers without replacing their normal surface materials.

1. Add the feature to the Renderer Data, normally at `AfterRenderingOpaques`.
2. Create project-owned materials with these package shaders:
   * Wireframe: `FuzzPhyte/WireframeBarycentricURP`
   * Vertices: `FuzzPhyte/VertexDotsURP`
   * Normals: `FuzzPhyte/NormalLineURP`
3. Add one enabled `FPRuntimeMeshViewer` to a scene controller and assign the materials to `Wireframe Mat`, `Vertex Mat`, and `Normals Mat`.
4. Assign initial `Target Renderers` in the Inspector or call `SetMeshModeType` with a renderer collection at runtime. Targets can be `MeshRenderer` components with a `MeshFilter` or `SkinnedMeshRenderer` components.
5. Select `Vertices`, `Wireframe`, `Wireframe And Vertices`, or `Normals`. Use `Default` to stop the overlay and release its generated caches.

Example runtime selection:

```csharp
using FuzzPhyte.Utility;
using UnityEngine;

public sealed class MeshInspectionExample : MonoBehaviour
{
    [SerializeField] private FPRuntimeMeshViewer viewer;
    [SerializeField] private Renderer[] selection;

    public void ShowWireframeAndVertices()
    {
        viewer.SetMeshModeType(MeshViewMode.WireframeAndVertices, selection);
        viewer.UpdateVertexSizing(0.01f);
    }

    public void ClearOverlay()
    {
        viewer.SetMeshModeType(MeshViewMode.Default);
    }
}
```

`Max Triangle Cap` limits the expanded barycentric wireframe mesh; when a source exceeds the cap, its wireframe is skipped and an error is logged. Vertex and normal buffers are built from the source mesh. A `SkinnedMeshRenderer` uses `sharedMesh`, not a per-frame baked deformed pose. The three surface modes (`Surface World Normals`, `Surface UV0`, and `Surface Vertex Color`) all draw pass 0 of the same assigned `Surface Debug Mat`; the feature does not currently change a keyword or property to distinguish those modes, so use a material already configured for the visualization you want. The normals draw also currently selects shader pass 0, so the `Always On Top` material toggle does not switch it to the shader's second pass.

Only one enabled `FPRuntimeMeshViewer` is supported because the feature reads `FPRuntimeMeshViewer.Active`.

#### Runtime World Grid

1. Add `FPRuntimeGridFeature` to the Renderer Data.
2. Create a material using `FuzzPhyte/GridPlaneURP` and assign it to the feature's `Grid Material` field.
3. Add `FPRuntimeGridPlane` to each scene Transform that should host a grid. Position and rotate the Transform to place the plane; `Extents World.x` and `.y` control the generated quad's local X and Z size.
4. Set minor/major colors, opacity, line thickness, units, and spacing. Enable `Use Major Spacing` to specify the major interval directly; otherwise every tenth minor line is major.
5. Toggle `Is Enabled` without disabling the component when a grid should remain registered but temporarily hidden.

The component converts the selected unit to meters for the shader. If code changes `Units`, `Spacing In Units`, `Major Spacing In Units`, `Use Major Spacing`, or `Custom Meters Per Unit` after `Awake`, call `RecalculateWorldSpacing()` afterward. Multiple enabled grid planes are supported. Their appearance fields override the shared grid material immediately before each draw, so use the component fields as the per-grid source of truth.

The grid shader is transparent, writes no depth, and uses `ZTest LEqual`; it is hidden by nearer scene geometry. Change the feature to `AfterRenderingTransparents` only when the desired composition requires the grid to be submitted after other transparent objects.

#### Runtime Measurement Overlay

1. Add `FPRuntimeMeasurementFeature` to the Renderer Data, normally at `AfterRenderingTransparents`.
2. Create one material using `FuzzPhyte/MeasurementDotsURP` and another using `FuzzPhyte/MeasurementLineURP`.
3. Add one enabled `FPRuntimeMeasurementOverlay` to a scene controller and assign those materials as `Point Mat` and `Line Mat`.
4. Set point size/color/opacity and line world width/color/opacity on the component.
5. Call `SetMeasurement` with two world-space points to show the overlay, and call `ClearMeasurement` to hide it.

```csharp
using FuzzPhyte.Utility;
using UnityEngine;

public sealed class MeasurementExample : MonoBehaviour
{
    [SerializeField] private FPRuntimeMeasurementOverlay overlay;

    public void Show(Vector3 start, Vector3 end)
    {
        overlay.SetMeasurement(start, end, true, UnitOfMeasure.Meter);
    }

    public void Clear()
    {
        overlay.ClearMeasurement();
    }
}
```

The measurement shaders use `ZTest Always`, so the points and line remain visible through scene geometry. `Units` is stored as measurement metadata only; this renderer does not convert the endpoints or draw a numeric label. Only one enabled measurement overlay is supported because the feature reads `FPRuntimeMeasurementOverlay.Active`.

#### Runtime URP Troubleshooting

* Nothing draws: confirm the active Camera is using the Renderer Data that contains the feature, the feature is enabled, and its matching scene component is enabled.
* Cutaway volume moves but the wall remains whole: the wall must use a cutaway-aware material; the geometry feature does not override an ordinary Lit material.
* A layer-filtered pass is empty: the object's layer must be selected by both the renderer feature and the Camera `Culling Mask`.
* Mesh-view mode is active but one overlay is missing: confirm the matching material field is assigned and that the target exposes a readable runtime mesh through `MeshFilter.sharedMesh` or `SkinnedMeshRenderer.sharedMesh`.
* Game view works but Scene view does not: enable `Draw In Scene View` for the mesh viewer, grid, or measurement feature. Cutaway features do not expose that filter.
* An effect appears twice: check camera stacking and ensure it is not configured on multiple Renderer Data assets used during the same frame.

## Internal Utility Tools

### ElevenLabs Text to Speech

ElevenLabs Text to Speech is an editor-only translation and audio generation window. It treats every job as one shared request list, whether that list contains one item or fifty. It loads the voices available to an ElevenLabs account, uses the OpenAI Responses API to translate between English, Spanish, and French, and saves the original and translated speech as Unity AudioClip assets. It uses Unity's built-in HTTP APIs and does not require either provider's SDK.

Open the tool from `FuzzPhyte/Utility/Audio/ElevenLabs Text to Speech`.

#### ElevenLabs Text to Speech - How To Use It

1. Open `FuzzPhyte/Utility/Editor/Testing/Keys Manager`. Save the ElevenLabs API key plus the OpenAI API key, organization ID, and project ID.
2. Open the ElevenLabs Text to Speech window. The tool requests all voices available to the ElevenLabs account; use `Refresh Voices` whenever the account's voice list changes.
3. Select a voice and confirm the ElevenLabs model ID. The default is `eleven_v3`.
4. Choose the original and target languages and confirm the OpenAI model ID. The default translation model is `gpt-4o-mini`.
5. Add requests manually or click `Load Markdown`. Markdown imports append to existing rows, apply the file's Person as Voice Name, apply its Translation Model language pair, and create one row for every bullet under `Language` and optional `Color` headings.
6. Click `Translate All Requests` and review each returned translation. The tool fills every Base File Name from the English form: English source text is used directly, an English target uses the returned translation, and a language pair without English makes an additional English translation request for naming. Changing a row's original text or the shared language pair invalidates its translation.
7. Confirm the editable Voice Name suffix and choose an existing folder inside the project's `Assets` folder.
8. Optionally enable `Generate FP_Vocab`. The additional `Level Introduced`, `CEFR Level`, and `Vocab Category` controls appear only while this option is enabled. This optional integration requires FP_Utility EDU to be installed.
9. Click `Generate All Audio Pairs`. Each valid row makes two ElevenLabs requests with the same selected voice and model. Normal assets use `{English Base File Name}_{Original or Translation}_{Language}_{Voice Name}.mp3`; Color items use `Color_{English Base File Name}_{Original or Translation}_{Language}_{Voice Name}.mp3`.
10. Use each row's `Clear` or `Remove` control, or use the confirmed `Clear All` action. `Save Markdown` writes the current Person, Translation Model, Language items, and optional Color items back to the supported format.

When `Generate FP_Vocab` is enabled, the tool creates one source-language and one target-language FP_Vocab asset beside each generated audio pair. It fills Word, Language, Level Introduced, CEFR Level, Vocab Category, and Word Audio. Each UniqueID is the exact imported MP3 filename, and each asset's Translations list references its counterpart. IPA, definitions, semantic maps, and modifier fields remain empty because the request workflow does not provide authoritative values for them.

The markdown loader accepts this structure (the `Color` section is optional):

```markdown
# Eleven Labs

## Person

* Alex

## Translation Model

* Spanish to English

## Language

* La maleta

## Color

* azul
```

The provider credentials remain in local Unity `EditorPrefs`; they are not written into an asset or included in a build. `EditorPrefs` is not encrypted, so use restricted API keys with only the required permissions and appropriate usage quotas.

### Invert Mesh

Invert Mesh is an editor-only authoring tool for creating an inside-out copy of an existing mesh while leaving the source unchanged. It reverses triangle and quad winding, flips vertex normals, corrects tangent handedness, and preserves submeshes, vertex channels, skinning, and blend shapes.

Open the tool from `FuzzPhyte/Utility/Mesh/Invert Mesh`. You can also select a readable Mesh asset in the Project window and use `Assets/FuzzPhyte/Invert Mesh and Save...`.

#### Invert Mesh - How To Use It

1. Select or assign a `Mesh`, `MeshFilter`, `SkinnedMeshRenderer`, component, prefab, or GameObject in the `Object / Mesh` field.
2. Review or change the suggested `{SourceName}_Inverted` output name.
3. Click `Invert and Save Mesh Asset`, then choose an asset location in the project.

The source mesh must be readable. For imported model meshes with Read/Write disabled, enable `Read/Write` in the model importer before using this tool. The saved result is a separate mesh asset; the tool does not replace the source mesh reference on scene or prefab objects.

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

### Remove CS

Remove CS is an editor-only scene cleanup tool that removes attached `MonoBehaviour` scripts while preserving each object's hierarchy, transform, visual components, colliders, lights, cameras, and animation components.

Open the tool from `FuzzPhyte/Utility/Editor/Remove CS`.

1. Add scene objects with the object field, current selection, empty list slots, or drag and drop.
2. Leave `Include Children` enabled to clean each complete hierarchy; inactive children can be included separately.
3. Choose whether to keep `AudioSource` and UI/Text components. Both are kept by default.
4. Review the live removal counts, then click `Clean Listed Objects` and confirm.

UI/Text includes Canvas components, legacy `TextMesh`, Unity UI, and TextMesh Pro. `RectTransform` components always remain so UI layout and orientation are preserved. Missing script entries are also removed. The cleanup is registered as one Unity Undo operation. Project-window prefab assets are skipped; open a prefab in Prefab Mode when its asset hierarchy needs cleanup.

The Target Objects list can be collapsed when working with large batches. Enable `Clean Copies In Another Scene` for a non-destructive workflow: the tool copies only listed objects belonging to the active scene, removes duplicate child selections beneath listed parents, preserves world placement, fully unpacks prefab connections, and cleans only the complete destination hierarchies, including inactive children. The destination can be a newly saved scene with an editable `New Scene Name` and project folder, or an existing scene asset. The complete new-scene asset path is shown before the operation runs. After saving, the tool can either leave the destination open additively and make it active, or close it and highlight the saved scene in the Project window.

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
* `Export Combined OBJ` writes portable OBJ/MTL output. `Preserve Materials And Textures` keeps source material colors and texture files, `Generic White Material` writes geometry with one untextured material, and `Single Albedo Atlas` writes one atlas-backed material for every group.
* `Root Atlas + Group Colors` writes one albedo atlas for the `Root` group while exporting the remaining submesh groups as untextured MTL materials using their source base colors. This is useful for textured terrain with colored canopy, water, or classification groups.

#### Combine Meshes - Source Notes

* `Skip 'EditorOnly' Tagged Objects` excludes any source object tagged `EditorOnly`.
* Mesh colliders are included only when their `sharedMesh` is assigned.
* If a visual mesh source from the same object has already been included, the matching `MeshCollider` source is skipped to avoid duplicate geometry.
* Large combined meshes automatically use 32-bit indices when the estimated vertex count is greater than 65,535.
* The output keeps source submeshes separate, which can be useful for inspection or later processing.

### Runtime OBJ Export

`FPMeshRuntimeObjExporter` provides the player-safe counterpart to the Combine Meshes OBJ workflow. It collects `MeshFilter` hierarchies, optionally bakes `SkinnedMeshRenderer` components, optionally includes non-duplicate `MeshCollider` sources, applies child transforms in root-local space, and keeps supported triangle and quad submeshes separate. No `UnityEditor` API or scene placement is required.

`TryBuildPackage` returns an in-memory ZIP containing the OBJ, an optional MTL, and optional PNG copies of each material's main texture. Materials retain their base color. Texture export uses a GPU readback so non-readable textures can be included, but it consumes additional memory; use `MaximumTextureSize` and leave `ExportTextures` disabled for lightweight WebGL downloads. Imported meshes must still have **Read/Write** enabled because runtime vertex and index access cannot bypass Unity's mesh readability setting. `MaximumVertexCount` can reject oversized exports before the OBJ text and ZIP are allocated.

Pass the result to `FPFileExportUtility.TrySaveOrDownload`. WebGL uses a Blob-backed browser download, iOS presents the system Files export picker, the Unity Editor opens a Save File panel, and Windows standalone opens the native Save As dialog. Cancelling either desktop prompt exits without writing or reporting an export failure. Platforms without a registered prompt implementation write a uniquely named file beneath `Application.persistentDataPath/FP_Exports`. The runtime export contains mesh, submesh, transform, UV, normal, material-color, and optional albedo-texture data; it intentionally does not serialize prefab scripts, audio, animation controllers, colliders as Unity components, or other GameObject behavior.

```csharp
var options = new FPMeshRuntimeObjExportOptions
{
    ExportMaterials = true,
    ExportTextures = false,
    MaximumVertexCount = 500000
};

if (FPMeshRuntimeObjExporter.TryBuildPackage(
        modelRoot,
        options,
        out FPMeshRuntimeObjExportResult package,
        out string buildMessage))
{
    FPFileExportUtility.TrySaveOrDownload(
        package.Data,
        package.FileName,
        package.MimeType,
        out string deliveredLocation,
        out string deliveryMessage);
}
```

### Mesh Generator and FP Heightmap Editor

Mesh Generator is an editor-only tool for building rectangular grid meshes on the XZ plane. The grid can be saved as a mesh asset, created directly in the scene, or connected to an `FPMeshGridData` asset so it can be regenerated later. The related FP Heightmap Editor can inspect, paint, and save heightmap textures that deform those generated grids.

Open the generator from `FuzzPhyte/Utility/Mesh/Mesh Generator`.

Open the heightmap editor from `FuzzPhyte/Utility/Rendering/FP Heightmap Editor`, or from the generator with `Open Heightmap Editor`.

#### Mesh Generator - How To Use It

1. Optionally assign an `FPMeshGridData` asset in the `Data Asset` field.
2. Choose a `Generation Mode`: `Normal Grid`, `GeoTIFF Grid`, or `Sonar Log Grid`.
3. Set the grid `Mesh Name`, `Width`, `Length`, `X Segments`, `Y Segments`, and `Center Pivot`.
4. Optionally assign a heightmap texture and choose `Height Scale`, `Height Offset`, channel, inversion, and X/Y flip settings.
5. Use `Surface Visual` settings to map the heightmap or a separate surface texture onto the generated mesh material while keeping the mesh topology generated from the sampled height data.
6. Adjust height processing options such as remap, edge falloff, and terracing. These processing options apply to standard texture heightmaps; direct GeoTIFF and sonar source modes use their sampled values directly.
7. Set scene output options such as parent, material, `Add MeshCollider`, and preview update behavior.
8. Click `Refresh Preview Mesh` to rebuild the preview, then click `Create Scene Object` to create a live scene mesh, or `Save Mesh Asset` to save the generated mesh to the project.

The generated grid uses UV0 coordinates for heightmap sampling. In `Normal Grid` mode, if no heightmap is assigned, the tool generates a flat grid. If a heightmap is assigned, vertices are displaced on the Y axis using the selected texture channel and processing settings. In GeoTIFF and sonar modes, the selected source data becomes the direct height source while the heightmap editor and surface visual tools remain available as optional grid extensions.

#### Mesh Generator - Generation Modes

The `Generation Mode` stored in `FPMeshGridData.GridSettings` controls which source panels are shown and which direct source flags are active.

* `Normal Grid` shows only the generic mesh grid controls: width, length, segment counts, pivot, optional heightmap deformation, surface visual mapping, and scene output.
* `GeoTIFF Grid` shows GeoTIFF reference, coordinate system, real-scale matching, and GeoTIFF inspection controls.
* `Sonar Log Grid` shows sonar log reference, waterfall/geospatial mosaic options, sonar raster settings, MAVLink placement controls, and sonar inspection controls.
* Switching modes hides unrelated source parameters and synchronizes the stored heightmap source flags so hidden GeoTIFF or sonar values do not affect a normal grid build.

#### Mesh Generator - Preview Controls

* The right-side preview shows the generated grid before it is created in the scene or saved as an asset.
* Use the preview `Display` controls to toggle `Surfaces`, `Edges`, and `Vertices` independently. This makes it possible to inspect the textured surface, mesh wireframe, vertex distribution, or any combination of those views.
* Use `Projection` to switch between `Perspective` and `Orthographic`, and use `Invert Camera Orbit` to flip the camera orbit preference.
* The upper-right orientation triad follows Unity's scene view style and shows the current X/Y/Z view orientation.
* Direct GeoTIFF and sonar source modes are designed for large files, so preview rebuilds are manual. Use `Refresh Preview Mesh` when source settings are ready instead of relying on automatic rebuilds for every field edit.

#### Mesh Generator - Surface Visual Mapping

`Surface Visual` controls how the generated mesh is shaded in the preview and on created scene objects.

* `Map Image To Surface` maps an image onto the generated mesh using the grid's UV0 coordinates.
* `Surface Texture` can override the visual texture without changing the height source. This reference is stored in `FPMeshGridData.HeightmapSettings` when settings are saved to a data asset.
* If `Surface Texture` is empty, the assigned `Heightmap` texture is used as the visual surface texture.
* If a custom material is assigned in `Scene Output`, the generator clones that material for preview/output and assigns the visual texture to common Unity texture properties such as `_BaseMap` and `_MainTex`.
* Surface visual mapping is separate from height sampling. The same image can drive both height and color, or one file can drive height while another is used only for the material.

#### Mesh Generator - GeoTIFF Elevation Mode

`GeoTIFF Grid` mode lets the generator sample a TIFF/GeoTIFF file directly for vertex height instead of using normalized color-channel values from a standard Unity texture.

* If a `Heightmap` texture asset is assigned, its project path is used as the GeoTIFF source path automatically.
* External `.tif` or `.tiff` files can be assigned through the `GeoTIFF File` field when no heightmap asset is assigned.
* The GeoTIFF inspection panel reports raster size, bit depth, compression, layout, NoData value, value range, pixel scale, real-world size, and GDAL scale/offset metadata when available.
* `Coordinate System` supports WGS84 and projected workflows. `Units To Meters` is used for projected coordinate systems when real-world source units need conversion into Unity meters.
* `Match Grid Real Scale` locks the editable grid width and length to the inspected GeoTIFF real-world size so generated mesh dimensions stay in sync with source metadata.
* Direct GeoTIFF height mode applies height as `Y = Height Offset + sample * Height Scale`. If the source is an image/intensity raster instead of a DEM, values may need a much smaller height scale.

#### Mesh Generator - Sonar Log Mode

`Sonar Log Grid` mode lets the generator build a raster from supported sonar log files and use that raster to displace the grid.

* Supported source files include `.svlog` and `.svlz`.
* `Waterfall` mode lays sonar samples out as a forward scan using survey speed, ping rate, range, and ping step settings.
* `Geospatial Mosaic` mode uses MAVLink navigation packets and Omniscan mono profile packets to place samples into a local meter-space mosaic.
* Geospatial controls include `Nav Source`, `Heading Source`, `Overlap Mode`, `Cell Size Meters`, and `Time Offset ms`.
* `LocalPositionNed` is the recommended first navigation source for Unity-local survey meshes. `GlobalPositionInt` is available for GPS-based workflows that will later be aligned with projected raster or GeoTIFF data.
* `Match Grid Log Bounds` locks the grid width and length to the inspected sonar raster bounds.
* The sonar inspection panel reports source data kind, packet/sample counts, MAVLink navigation counts, raster resolution, local bounds, value range, and checksum warnings.
* Omniscan profile logs currently provide acoustic intensity values in dB for the surface height workflow, not guaranteed bathymetric depth. Use `Height Scale` and `Height Offset` to keep intensity surfaces in a useful visual range.

#### FPMeshGridData

`FPMeshGridData` is a ScriptableObject recipe for grid generation. Create one from `Assets/Create/FuzzPhyte/Utility/Design/Mesh Grid Data`.

The asset stores:

* `GridSettings`, including generation mode, mesh name, width, length, segment counts, and pivot mode.
* `HeightmapSettings`, including the heightmap texture, surface texture reference, height scale, offset, channel, inversion, and flips.
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

### FP Video Sphere Generator

FP Video Sphere Generator is an editor-only mesh authoring tool for creating video-ready playback surfaces. It can generate inside-out sphere meshes for 360 equirectangular video, ellipsoid meshes for stretched immersive volumes, and segmented quads for flat video surfaces, kiosks, billboards, or UI-style playback planes.

Open the tool from `FuzzPhyte/Utility/Video/FP Video Sphere Generator`.

The sphere path is backed by `FPVideoSphereBuilder`, which builds a UV sphere with equirectangular-friendly UVs, tangents, normals, and optional inside-out triangle winding. Inside-out output is the default because it is intended for placing the viewer or camera inside the mesh and projecting 360 video onto the interior surface.

#### FP Video Sphere Generator - How To Use It

1. Open the window from `FuzzPhyte/Utility/Video/FP Video Sphere Generator`.
2. Choose a `Shape`: `Sphere`, `Ellipsoid`, or `Quad`.
3. For `Sphere`, set `Mesh Name`, `Radius`, `Longitude Segments`, `Latitude Segments`, and whether the mesh should be `Inside Out`.
4. For `Ellipsoid`, set non-uniform `Radii` plus longitude/latitude segment counts.
5. For `Quad`, set `Width`, `Height`, segment counts, and whether the surface facing should be flipped.
6. Review the generated `Vertices` and `Triangles` counts before creating or saving the mesh.
7. Optionally assign a target parent, material, and `Add MeshCollider` setting under `Scene Output`.
8. Use `Create Scene Object` to create a live scene mesh, or `Save Mesh Asset` to save the generated mesh into the project.

#### FP Video Sphere Generator - Asset Workflow

* `Target Mesh` can reference an existing saved mesh asset when you want to rebuild a video surface in place.
* `Use Selected Mesh Asset` assigns the currently selected persistent `Mesh` asset as the overwrite target.
* `Overwrite Target Mesh` rebuilds the selected target asset while preserving references to that mesh asset.
* `Save Mesh Asset` saves a new mesh asset and updates any live scene object created by the tool to use the saved asset.
* Suggested file names include the shape settings, such as radius and segment counts, so generated assets remain easier to identify.

#### FP Video Sphere Builder Notes

* `FPVideoSphereBuilder.Build` sanitizes settings before mesh creation. Radius is clamped above zero, longitude segments are clamped from `3` to `512`, and latitude segments are clamped from `2` to `256`.
* Generated sphere vertices use `(longitudeSegments + 1) * (latitudeSegments + 1)` so the UV seam can close cleanly.
* UVs are written for equirectangular playback, with horizontal coordinates reversed as `1 - u`.
* When `GenerateInsideOut` is enabled, normals are flipped inward and triangle winding is reversed for interior viewing.
* Meshes with more than 65,535 vertices automatically use 32-bit indices.

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

### FP Action-Event Scanner

FP Action-Event Scanner is an editor-only package audit window for finding C# `event`, `delegate`, and `Action` declarations or usages across FuzzPhyte package folders. It is intended as an internal package-maintenance tool when you need a quick map of where event-style communication is happening across `FP_` packages.

The scanner builds its package filter list from top-level folders under `Assets` whose names start with `FP_`. Enabled packages are scanned recursively for `.cs` files, and matching lines are grouped first by package and then by script path. Packages with scanner hits are highlighted in green in the filter list.

#### Action-Event Scanner - How To Use It

1. Open the window from `FuzzPhyte/Utility/Editor/Action-Event Scanner`.
2. Use `Open File With` to choose the default app, Visual Studio, VS Code, or JetBrains Rider for result links.
3. Toggle package filters to control which `FP_` folders are included in the scan.
4. Use `Select All` or `Deselect All` to quickly change all package filters.
5. Click `Rescan Project` after changing filters or after code changes.
6. Click a script path in the results list to open that file in the selected editor.
7. Use `Export Results to File` to save the grouped results as a Markdown report.

#### Action-Event Scanner - Menu & Window Actions

* `FuzzPhyte/Utility/Editor/Action-Event Scanner`
  * Opens the `FP Action-Event Scanner` editor window.
* `Open File With`
  * Chooses how clicked result files are opened: default app, Visual Studio, VS Code, or JetBrains Rider.
* `Select All` and `Deselect All`
  * Bulk-toggle all discovered `FP_` package filters.
* `Rescan Project`
  * Searches enabled package folders for matching `event`, `delegate`, and `Action` lines.
* `Export Results to File`
  * Writes the grouped scan results to a Markdown file with file links and line numbers.

## Software Architecture

FP_Utility has a core data class for ScriptableObjects called FP_Data. This is heavily used for all generic data classes and in other packages there could be further extension of this for generic ScriptableObjects that need a sort of UniqueID. There are additional sub-folders by domain areas. For example, there is a simple IK manager script located in the FuzzPhyte.Utility.Animation namespace. Some of these sub-folders contain their own domain assembly. There are then sections broken up by Scene asset(s), tools for Audio & Video, and other static/instance utility classes for conversions, enums, states, etc.

### Ways to Extend

Please see the [contributing](./CONTRIBUTING.md) file for more information.

## Dependencies

Please see the [package.json](./package.json) file for more information.

## License Notes

See [LICENSE.md](LICENSE.md) for details

## Contact

* [John Shull](mailto:JShull@fuzzphyte.com)

### Additional Notes

* Audio Files in the samples came from [FreeSound.org](https://freesound.org/)
