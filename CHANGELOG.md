# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
