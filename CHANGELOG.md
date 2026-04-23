# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
