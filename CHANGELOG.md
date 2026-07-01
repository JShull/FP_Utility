# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-07-01

### 1.0.0 Added

- [@JShull](https://github.com/jshull)
  - FP Readme Tool
    - Added inline Markdown link support for readme body text using `[Label](target)` syntax.
    - Added Unity menu link support for readme links, including `menu:` / `unity-menu:` targets and direct `FuzzPhyte/...` menu paths.
    - Added overview-level readme links so the top readme area can include clickable web or tool links below the package overview.
    - Added optional section separators, larger section heading rendering, and builder controls for expanding or collapsing all sections.
    - Added a footer help toggle for inline link syntax so the help text can be shown once at the bottom of the Readme Builder instead of repeated in every section.
  - FP Utility package startup
    - Added an editor session startup check for UPM-loaded FP Utility packages.
    - Added a package startup message toggle under `FuzzPhyte/Utility/Package Messages/Show Startup Messages`.
    - Added automatic selection and pinging of the package-root `Readme` FP Readme Asset after the startup message closes.
  - FP Video Sphere Generator
    - Added a right-side mesh preview panel for generated sphere, ellipsoid, and quad video surfaces.
    - Added preview camera projection, invert orbit, vertex overlay, edge overlay, orbit gizmo, scene orientation gizmo, and scroll-wheel zoom controls.
  - FP Action-Event Scanner documentation
    - Added README documentation for scanning `event`, `delegate`, and `Action` usage across FuzzPhyte package folders.

### 1.0.0 Modified

- [@JShull](https://github.com/jshull)
  - FP Readme Asset inspector
    - Updated readme link rendering so HTTP links keep the normal link color while Unity menu links use `FP_Utility_Editor.WarningColor`.
    - Updated inline link layout to preserve normal word spacing and draw clickable link overlays on the same visual line as surrounding text.
    - Updated the package `Readme.asset` content and visual section layout for the current utility tool set.
  - FP SVG Extruder
    - Updated UI dividers to use the shared warning-color mesh tool separator style.
    - Replaced the vertical preview height slider with scroll-wheel preview scaling in the preview panel.
    - Added a collapsible Regions panel and preview overlay stats for regions, included regions, and preview pixel height.
  - FP Video Sphere Generator
    - Reworked the window into the same left-parameter / right-preview layout used by the mesh tools.
    - Moved generation actions into a stable lower action area and added spacing to prevent the Scene Output controls from clipping.
  - Package update checks
    - Added targeted update checking for `com.fuzzphyte.utility` when the package startup session check runs.
    - Updated package update request monitoring so it waits for all package requests to finish before logging results and detaching from editor updates.
  - Documentation
    - Added README coverage for the FP Video Sphere Generator and FP Action-Event Scanner workflows.
    - Updated package readme links and menu references for the current FuzzPhyte utility navigation.
  - `package.json`
    - Version bumped to `1.0.0`.
    - Added Unity Entities package dependencies used by the current utility package.

### 1.0.0 Removed

- [@JShull](https://github.com/jshull)
  - Removed the bundled `CoordinateSharp` precompiled library assets and cleared the runtime assembly definition's `CoordinateSharp.dll` reference.

## [0.9.7] - 2026-05-27

### 0.9.7 Added

- [@JShull](https://github.com/jshull)
  - FP Audio Combine Tool
    - Added an editor-only multi-clip audio combine workflow under `FuzzPhyte/Utility/Audio/Combine Tool`.
    - Added a split layout with a left parameter stack and right timeline viewer so each clip's settings line up with its visual waveform row.
    - Added `FPAudioCombineData` ScriptableObject support for saving and reloading combine stacks, mix settings, export bookends, colors, gain, fades, lock states, and mute states.
    - Added drag-and-drop AudioClip import into the stack, direct clip dragging in the viewer, stack auto-layout, and protected stack clearing with a confirmation popup.
    - Added per-clip gain, mute, lock, generated or user-assigned clip colors, export start/end bookends, and normalized mix export.
    - Added per-clip fade in/out controls with timeline handles for fade duration and curve handles for `In C` / `Out C` fade shape.

### 0.9.7 Modified

- [@JShull](https://github.com/jshull)
  - FP Audio Combine Tool preview/export
    - Updated combined preview generation to use an imported temporary WAV preview asset path when available, improving Unity editor preview reliability before export.
    - Updated waveform drawing and preview/export paths to reflect per-clip gain, fades, mute state, and export bookends.
    - Added fallback handling for compressed AudioClip waveform reads so compressed clips do not repeatedly spam `AudioClip.GetData()` errors in the editor.
  - FP Audio Segment Tool
    - Updated the playhead visual to match the combine tool and set default waveform thickness to `1`.
    - Added region-aware segment preview so mute/cut regions can be auditioned before export.
  - `README.md`
    - Updated audio tool documentation for the current Segment Tool and Combine Tool workflows.
  - `package.json`
    - Version bumped to `0.9.7`.

## [0.9.6] - 2026-05-22

### 0.9.6 Added

- [@JShull](https://github.com/jshull)
  - Mesh Slicer editor workflow
    - Added `FPMeshSlicerWindow.cs` under `FuzzPhyte/Utility/Mesh/Mesh Slicer`.
    - Added source mesh slicing with adjustable plane position and rotation, positive/negative/both preview visibility, and keep-positive, keep-negative, or keep-both output modes.
    - Added optional slice hole repair for closed cut loops without external library dependencies.
    - Added plane movement and rotation handles, axis-constrained movement, plane front/back colors, plane outline rendering, hover feedback, and Undo support for slicer interactions.
  - Shared mesh preview tooling
    - Added `FPMeshPreviewEditorUtility.cs` for common mesh preview colors, projection settings, Unity-style orbit handling, vertex and edge overlays, section dividers, and scene orientation triad drawing.
    - Added shared `Perspective` / `Orthographic`, `Invert Camera Orbit`, `Show Vertices`, and `Show Edges` preview options across mesh tools.

### 0.9.6 Modified

- [@JShull](https://github.com/jshull)
  - Mesh editor menu organization
    - Moved mesh tools under `FuzzPhyte/Utility/Mesh`.
    - Renamed editor window entries to `Combine Meshes`, `Convex Generator`, `Mesh Slicer`, and `Mesh Generator`.
  - `FPSimpleConvexGeneratorWindow.cs`
    - Added `Include Children` source control with a default of disabled.
    - Updated preview camera controls to use the shared mesh preview utility.
  - `FPMeshCombineEditor.cs`
    - Added a right-side preview panel with shared camera controls, source overlay, vertex overlay, and edge overlay options.
  - `FPMeshGeneratorWindow.cs`
    - Added a right-side mesh preview panel with shared camera controls, vertex overlay, and edge overlay options.
  - `README.md`
    - Updated mesh tool documentation for the new menu paths, current window names, Mesh Slicer workflow, and shared preview controls.
  - `package.json`
    - Version bumped to `0.9.6`.

## [0.9.5] - 2026-05-06

### 0.9.5 Added

- [@JShull](https://github.com/jshull)
  - FP Scene Asset Tool bulk import workflows
    - Added a Bulk Audio Import Settings panel for selected AudioClip assets, including load type, preload audio data, load in background, compression format, and quality.
    - Added active build target audio sample setting overrides so batch audio changes can be applied per build platform.
    - Added in-tool undo support for restoring previous audio import settings and platform override state.
    - Added README documentation for bulk texture and bulk audio import settings.

### 0.9.5 Modified

- [@JShull](https://github.com/jshull)
  - `FPSceneAssetLister.cs`
    - Texture and audio import settings panels now only appear when selected assets include matching importer types.
  - `package.json`
    - Version bumped to `0.9.5`.

## [0.9.4] - 2026-04-29

### 0.9.4 Added

- [@JShull](https://github.com/jshull)
  - FP Scene Asset Tool texture import workflow
    - Added a Texture Import Settings panel to `FPSceneAssetLister.cs` for applying a selected max texture size to selected scene texture assets.
    - Added support for selected assets backed by `TextureImporter`, including Texture2D and Sprite entries.
    - Added an in-tool undo action for restoring the previous max texture size values from the last batch update.

### 0.9.4 Modified

- [@JShull](https://github.com/jshull)
  - `FPSceneAssetLister.cs`
    - Moved the JSON file name input to the bottom export row beside the Save to JSON button.
  - `package.json`
    - Version bumped to `0.9.4`.

## [0.9.3] - 2026-04-23

### 0.9.3 Added

- [@JShull](https://github.com/jshull)
  - Runtime video manifest and cache pipeline
    - Added `FPVideoManifestModels.cs` to define runtime manifest entries, local cache metadata, request results, and related video events.
    - Added `FPVideoRuntimeConfig.cs` as a ScriptableObject for manifest URL, cache folder names, validation policy, and preload options.
    - Added `FPVideoCacheManager.cs` to fetch the remote manifest, validate local cached files, download missing or outdated videos, and resolve local playback paths.
    - Added `FPVideoDownloadUtility.cs` for download-to-disk temp file handling and `FPVideoHashUtility.cs` for SHA256 validation.
    - Added `FPVideoPlaybackResolver.cs` as a small runtime resolver surface for playback consumers.
  - Bootstrap, testing, and playback hookup flow
    - Added `FPVideoCacheBootstrap.cs` as a MonoBehaviour entry point for initialization, requests, preloading, and runtime event relays.
    - Added `FPVideoCacheTester.cs` with startup toggles and `ContextMenu` actions for initialization, preload, request, and cache checks from the inspector.
    - Added `FPVideoDownloadListenerExample.cs` as an example C# subscriber for request and download lifecycle events.
    - Added `FPVideoSimpleEventBridge.cs` to expose simple inspector-friendly events for video id, resolved local path, success state, cache source, download state, and error message.
    - Added `FPVideoPlayerPathReceiver.cs` for direct drag-and-drop wiring from resolved local file paths into a Unity `VideoPlayer`.
  - Editor manifest generation
    - Added `FPVideoHashEditor.cs` under `Editor/Video` to build Azure Blob manifest JSON entries with file name, hash, size, and generated blob URLs.

### 0.9.3 Modified

- [@JShull](https://github.com/jshull)
  - `package.json`
    - Version bumped to `0.9.3`.

## [0.9.2] - 2026-03-24

### 0.9.2 Fixed

- [@JShull](https://github.com/jshull)
  - Mesh grid editor regeneration stability
    - Deferred `FPMeshGridInstance` auto-regeneration out of `OnValidate` to avoid unsafe editor reload timing.
    - Deferred cleanup of transient generated meshes in editor workflows to prevent `DestroyImmediate` errors during validation and reload callbacks.
  - Unity 6 API housekeeping
    - Replaced deprecated `FindObjectsByType` overloads that required `FindObjectsSortMode`.
    - Migrated mesh picker cache tracking to `EntityId`-based dictionaries instead of relying on deprecated `GetInstanceID()` usage.
    - Updated global object lookup helpers to use object-based `GlobalObjectId.GetGlobalObjectIdSlow(...)` calls.
    - Added narrow compatibility guards around legacy editor callback paths that still expose `instanceID`-based APIs, avoiding compiler warnings without changing hierarchy behavior.

## [0.9.1] - 2026-03-16

### 0.9.1 Added

- [@JShull](https://github.com/jshull)
  - Mesh generation and authoring workflow
    - Added `FPMeshGridBuilder.cs` for procedural rectangular grid surface generation.
    - Added `FPMeshGridData.cs` as an `FP_Data` asset for storing grid, heightmap, and processing settings.
    - Added `FPMeshGridInstance.cs` for scene-based regeneration from mesh grid data assets.
    - Added `FPMeshGeneratorWindow.cs` under `FuzzPhyte/Utility/Rendering/FP Mesh Generator`.
    - Added `FPMeshGridInstanceEditor.cs` for inspector and menu-driven regeneration of selected mesh grid instances.
  - Heightmap deformation and processing
    - Added `FPMeshHeightmapUtility.cs` for UV-based heightmap sampling and mesh displacement.
    - Added non-destructive height processing controls including remap, rectangular/radial falloff, and terracing.
  - Heightmap editor and brush presets
    - Added `FPHeightmapEditorWindow.cs` as a dedicated editor workspace for heightmap preview, histogram inspection, and brush-based editing on a working copy.
    - Added `FPHeightBrushData.cs` as an `FP_Data` asset for reusable brush presets, including optional PNG mask support.

### 0.9.1 Modified

- [@JShull](https://github.com/jshull)
  - `package.json`
    - Version bumped to `0.9.1`.
  - `FPMeshGeneratorWindow.cs`
    - Save flow now promotes the live generated mesh and rebinds scene `MeshFilter` and `MeshCollider` references to the saved asset.
    - Added auto-update support for the last generated preview object.
    - Added integration with the heightmap editor and mesh data assets.
  - `FPMeshGridData.cs`
    - Added change notifications so linked mesh instances can auto-regenerate in edit mode when data assets change.
  - `FPHeightmapEditorWindow.cs`
    - Reworked the layout into a 25/75 split with parameter and histogram containers on the left and a larger painting canvas on the right.
    - Added non-destructive working-copy creation, PNG export, brush preview overlay, optional PNG mask brushes, and brush rotation.

## [0.9.0] - 2026-03-06

### 0.9.0 Added

- [@JShull](https://github.com/jshull)
  - FP_HHeaderMeshPickerCache.cs
    - Added a Scene view mesh-picking cache for collapsed headers so hidden hierarchy items can still be selected from the Scene view.
  - FP_HHeaderWindow.cs
    - Added a `FuzzPhyte/Header/Header Options` editor window for assigning and applying `FP_HHeaderData` assets without relying only on the asset context menu.

### 0.9.0 Modified

- [@JShull](https://github.com/jshull)
  - FP_HHeader.cs
    - Improved scene-open restore flow to force a full visual refresh and then reapply cached collapsed states.
    - Expanded collapsed headers automatically when selecting grouped objects.
    - Added support for applying `FP_HHeaderData` assets from shared editor paths.
    - Header background now uses the configured collapsed color while closed.
  - FP_HHeaderData.cs
    - Collapsed color is now reflected in the header row background when a header is closed.
  - FPSceneAssetLister.cs
    - Added a per-asset scene usage count column so deduplicated assets can still show how many scene objects reference them.
    - Refined the asset list header layout to better support quick parsing of scan results.
  - README.md
    - Added documentation for FP Header and FP Scene Asset Tool usage and menu options.
  - package.json
    - Version bumped to `0.9.0`.

## [0.8.5] - 2026-02-26

### 0.8.5 Added

- [@JShull](https://github.com/jshull)
  - Openness Functionality
    - FP_OpennessProviderHinge
    - FP_OpennessProviderSlide
    - FP_OpennessTickDriver
    - FP_OpennessStateTracker
    - FP_OpennessInteractionBridge
  - Openness Samples
    - See Samples folder/Openness

## [0.8.0] - 2026-01-27

### 0.8.0 Added

- [@JShull](https://github.com/jshull)
  - FP_TickSystem.cs
    - Way to centralize management of various uses of our own tick system that's just bootstrapped to Unity
    - still relies on Update/FixedUpdate/LateUpdate and/or your own interval (which uses one of those three as a base)
  - FP_BinderBase.cs
    - Binder Base class: Interface solution for Bind/UnBind/ResetBind/IsBound

### 0.8.0 Modified

- [@JShull](https://github.com/jshull)
  - FP_Timer.cs
    - Works now with updates associated with Priority Queue
  - PriorityQueue.cs
    - Better Error catching for Queue/Dequeue and managing Down/up Heap (better code broken up)
  - FPBootStrapper.cs
    - cleaning up code that was already commented out
  - FP_ScreenRegionGameViewDebug.cs
    - Removing UnityEditor dependency and instead using Unity Code compilation to manage #Unity_Editor needs

## [0.7.0] - 2026-01-07

### 0.7.0 Added

- [@JShull](https://github.com/jshull)
  - FP_ScreenRegionAsset.cs
    - A scriptable object that holds FP_ScreenRegion struct data in an array
  - FP_ScreenRegionGameViewDebug.cs
    - An editor only script that debugs and shows you your screen region you've confined some operation to

### 0.7.0 Modified

- [@JShull](https://github.com/jshull)
  - FP_UtilityData.cs
    - Includes a new struct for 'FP_ScreenRegion'

## [0.6.4] - 2025-12-09

### 0.6.4 Added

- [@JShull](https://github.com/jshull)
  - FPGlobalBootstrap.cs
    - Will update after the FPBootstrapper.cs classes are implemented to go 'find' all of them and run their code for OnSceneLoad
  - FPDebugHelp.cs
    - Editor script for various editor searches, this one currently has a way to look for Runtime Generics that would return InGenericTypeChain = true which can cause issues.
    - Run it from FuzzPhyte/Utility/Editor/Debug

### 0.6.4 Modified

- [@JShull](https://github.com/jshull)
  - FPBootstrapper.cs
    - Removed the '[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]' requirement on InitializeAfterAwake as this won't work on generics
  - FPExecutionOrder.cs
    - Added in the FPGlobalBootstrap.cs

## [0.6.3] - 2025-09-20

### 0.6.3 Removed

- [@JShull](https://github.com/jshull)
  - Anything previously under the Animation sub folder was moved to a new Unity package to clean up dependency issues associated with further development
  - **Follow Spline** system was moved to a new package
    - FPSplineCommandMarker.cs
    - FPSplineFollowBehaviour.cs
    - FPSplineFollowClip.cs
    - FPSplineFollower.cs
    - FPSplineFollowerReceiver.cs
  - **IK** system was moved to a new package
    - FPIKManager.cs
  - **Injection** script was moved to a new package
    - FPAnimationInjector.cs

## [0.6.2] - 2025-08-12

### 0.6.2 Added

- [@JShull](https://github.com/jshull)
  - **Follow Spline** Timeline system that allows us to control a spline following system via timeline markers and parameters.
    - FPSplineCommandMarker.cs
    - FPSplineFollowBehaviour.cs
    - FPSplineFollowClip.cs
    - FPSplineFollower.cs
    - FPSplineFollowerReceiver.cs
  - **Sample Updates**
    - FPSplineFollow under the FollowSpline Folder

## [0.6.1] - 2025-07-11

### 0.6.1 Added

- [@JShull](https://github.com/jshull)
  - **Sample Updates**
    - iOS Plugin to support I/O native iOS for Markdown/Json/Wav

## [0.6.0] - 2025-07-03

### 0.6.0 Added

- [@JShull](https://github.com/jshull)
  - **Skybox Shader Transition Sample**
  - **ShaderGraph Dependency**

### 0.6.0 Modified

- [@JShull](https://github.com/jshull)
  - **FPUnityDefine.cs**
    - Fixed GetDefines and SetDefines function to use updated PlayerSettings requests that rely on UnityEditor.Build.NamedBuildTarget
  
## [0.5.0] - 2025-03-06

### 0.5.0 Added

- [@JShull](https://github.com/jshull)
  - **FPIKManager.cs**
    - Simple Mono class for managing a head and left/right hand IK. Based on simple use cases, visual interior/exterior cone, and distance checks
  - **FPIKManagerEditor.cs**
    - Editor class that works with the FPIKManager
  - **FPGizmoDraw.cs**
    - New static class designed around Gizmo creations for various meshes
    - GenerateConeMesh(segments,height,angle,bothSideUV=false)
  - An IK Sample for demonstrating how to use the FPIKManager.cs class
    - *SamplesURP\IK\testAnim.unity*

### 0.5.0 Changed

- [@JShull](https://github.com/jshull)
  - **FP_Utility_Editor.cs**
    - Modified DrawUIBox to include indent left/right parameters

## [0.4.5] - 2024-9-01 / 2024-11-04

### 0.4.5 Added

- [@JShull](https://github.com/jshull)
  - **FP_HHeader.cs**
    - New static class for managing foldout states and header colors in the Unity Editor
    - Supports editor initialization, hierarchy updates, and selection change monitoring
  - **FPUtilCameraControl.cs**
    - Added new utility script for camera control functionality
  - **LanguageLevel Enum**
    - Added to `FP_UtilityData.cs` for categorizing language levels
  - **FPBuildProcessor.cs**
    - Added build processor for removing tagged components from runtime scenes

### 0.4.5 Changed

- [@JShull](https://github.com/jshull)
  - **Boot Strapper**
    - Refactored to use `FindObjectsOfType` instead of the obsolete `FindObjectOfType`
  - **FPSystemBase**
    - `AfterLastUpdateActive` property set to public
  - **FP_UtilityData.cs**
    - Added a static function to populate `TMP_DropDown` options from any generic enum
  - **FPUnityDefine.cs**
    - Updated to use the latest render pipeline functions, replacing deprecated `renderPipelineAsset` calls
  - **FP Utility Editor**
    - Abstract dropdown class moved to runtime design for improved flexibility
  - **Editor-Only Updates**
    - Adjusted `FP_EditorOnly.cs` for editor-specific functionality
    - Reorganized scripts for better package structure

## [0.4.3] - 2024-8-16 / 2024-8-29

### 0.4.3 Changed

- [@JShull](https://github.com/jshull)
  - FP_Timer.cs
    - Added in multiple ways to StartTimer with different parameters
    - Int
    - String
    - Float
  - FP_FactoryItemEditor.cs
    - Added in Vector3 Support for Default FP_Data related Scriptable Objects 

## [0.4.2] - 2024-6-11

### 0.4.2 Added

- [@JShull](https://github.com/jshull)
  - FP_Data.cs
    - Base derived ScriptableObject class
    - Setting up for the long haul tied to FP_Utility and now FP_Data
  - FP_ItemFactoryEditor.cs
    - Editor tool that lets you generate scriptable objects derived from FP_Data.cs
    - Open it up via FuzzPhyte Window
  - FP_UniqueGenerator.cs
    - Generates an ascii based unique index based on two strings and a Unity Color
    - Encoder and Decoder built into static class
    - used in all FP_Data derived classes
  - FontSettings.cs
    - ScriptableObject derived from FP_Data that holds font related information

### 0.4.2 Changed

- [@JShull](https://github.com/jshull)
  - FP_Theme.cs
    - Reworking information contained within like references to FontSettings.cs
    - Better headers/tooltips
    - Derived from FP_Data.cs
  - FP_Utility_Editor.cs
    - Moved some GUIStyle functions to the FP_UtilityData class
      - Obsolete method tags added - will burn them out over the next update or two
  - FP_UtilityData.cs
    - Added GUIStyles from FP_Utility_Editor.cs
    - new Enums
      - FP_Role
      - MotionState
      - DialogueState
      - FP_Ethnicity
      - FontSettingLabel
      - EmotionalState
    - modified Enums
      - OverlayType
      - NPCHackState
      - NPCHackTalkingState
      - FP_Gender got overhauled

## [0.4.1] - 2024-5-28

### 0.4.1 Added

- [@JShull](https://github.com/jshull)
  - FP_DepthRenderFeature.cs
    - Quick way for a Depth Mask in URP
    - This is a scriptable render pipeline script so you will need the correct package imports
    - Requires the use of the DepthMaskShader.shader/material
  - Updated SamplesURP
    - Includes a DepthMask sub folder with the above scripts/shaders etc

## [0.4.0] - 2024-3-8

### 0.4.0 Added

- [@JShull](https://github.com/jshull)
  - FP_RampAudio.cs
    - Way to fade in and fade out
    - Clamp audio with different start/end values
    - Save time on Audio Editing by using this simple script
  - Updated SamplesURP
    - FP_AudioFadeInOut.unity sample scene for audio
    - FP_Tester.cs: help initialize and start the audio processes using the FP_Timer script

## [0.3.2] - 2024-3-2

### 0.3.2 Added

- [@JShull](https://github.com/jshull)
  - Static Class FPGenerateTag.cs
    - Adds a way to add tags to the current project settings
    - Adds a way to remove tags to the current project settings
  - Interface IFPProductEditorUtility
    - Return ProductName and Product Asset Samples Path
  - Static Class FPUnityDefine.cs
    - Adds a way for me to alter and later on add/remove different #defines
  - Added in IFPWebGL Interface for Javascript WebGL Needs

### 0.3.2 Changed

- [@JShull](https://github.com/jshull)
  - Added a function 'CreateAssetsFolder' in FP_Utility_Editor.cs
    - This will generate an assets folder anywhere under the .Assets folder
    - Added to make it consistent on when a Menu system would generate Scriptable Objects from the editor-menu system

## [0.3.1] - 2024-1-25

### 0.3.1 Added

- [@JShull](https://github.com/jshull).
  - FP_SavWav.cs
    - added a SavWav class from another older project
  - FP_AudioUtils.cs
    - Way to grab audio from various sources and then utilizes the FP_SavWav to save it.

## [0.3.0] - 2023-11-12

### 0.3.0 Changed

- [@JShull](https://github.com/jshull).
  - FP_UtilityData.cs
    - enum SequenceStatus has been moved from the FP_Chain repository to this script

### 0.3.0 Added

- [@JShull](https://github.com/jshull).
  - Moved Chain Editor utility in to this repository
  - FP_Utility_Editor.cs
    - Nice Editor script for building Unity Editor boxes and other visual things
    - Used mainly in the Chain Package but can be used in other editor tool. 
  - Added in FP_UtilityDraw.cs
    - Helps with Debug.Draw tied to MonoBehaviour 

## [0.2.0] - 2023-11-11

### 0.2.0 Changed

- [@JShull](https://github.com/jshull).
  - Moved the EDU core scripts to their own repository/package
  - New base folders for future packages to be removed and made independent
  - Cleaned up references and dependencies
  - Added in other generic Utility classes by folder
    - Meta
    - Game
    - Notification
    - RandomFacts
  - FP_UtilityData.cs
    - Modifications for data and static values tied to:
      - Unit Square
      - Unit Sphere
      - Unit Circle

### 0.2.0 Fixed

- None...

### 0.2.0 Removed

- EDU base is it's own package

## [0.1.0] - 2023-11-01

### 0.1.0 Added

- [@JShull](https://github.com/jshull).
  - Moved all test files to a Unity Package Distribution
  - Setup the ChangeLog.md
  - FP_Utility Asmdef
    - FP_Card.cs
    - FP_Notification.cs
    - FP_PassedEvent.cs
    - FP_Theme.cs
    - FP_Timer.cs
    - FP_Utility.cs
    - PriorityQueue.cs
    - All of these are considered utility base classes. This package is a "core" package for a lot of future and existing FuzzPhyte packages.
  - SamplesURP
    - ScriptableObjects generated as examples for FP_Theme and other script files that utilize FP_Utilty.cs as well as FP_Timer.cs
  - Adjusted and added in more classes and custom UnityEvents for the FP_UtilityData.cs file

### 0.1.0 Changed

- None... yet

### 0.1.0 Fixed

- Setup the contents to align with Unity naming conventions

### 0.1.0 Removed

- None... yet
