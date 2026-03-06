# FuzzPhyte Unity Tools

## Utility

FP_Utility is designed and built to be a simple set of base classes to be used in almost all future FuzzPhyte packages. There is an element of Scriptable Object and an element of just simple input/output functions as well as some core scripts timed to timers etc. There are a lot of static functions to help with file management and Unity Editor management. Please see the FP_UtilityData class as well as the FP_Utility_Editor class for a lot of these functions/enums/structs etc.

## Internal Utility Tools

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

## Setup & Design

FP_Utility is not much by itself and is designed to allow extensions and/or inheritance for other work. An example of this is the FP_Notification.cs file. This is a super simple class that a lot of other projects will be derived from.

SamplesURP will require additional package imports.

* com.unity.render-pipelines.universal

### Software Architecture

FP_Utility has a core data class for ScriptableObjects called FP_Data. This is heavily used for all generic data classes and in other packages there could be further extension of this for generic ScriptableObjects that need a sort of UniqueID. There are additional sub-folders by domain areas. For example, there is a simple IK manager script located in the FuzzPhyte.Utility.Animation namespace. Some of these sub-folders contain their own domain assembly.

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
